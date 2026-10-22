#!/usr/bin/env python3
"""macOS PTY smoke checks. Only synthetic keys; never print/capture raw terminal data to disk.

This is automated PTY evidence, not the CLI-AC-033/034/035 human acceptance.
Temporary workspaces are retained; no recursive cleanup is performed.
"""
import argparse
import errno
import os
from pathlib import Path
import select
import signal
import subprocess
import sys
import tempfile
import termios
import time

FAKE_KEY = b"FAKE_KEY_FOR_TERMINAL_TESTS_ONLY"
KEY_PROMPT = "請輸入 API Key（不回顯）：".encode()
CONFIRM = "使用此工作區？[y/N]".encode()
READY = "聊天已就緒".encode()
CHAT_PROMPT = "你> ".encode()


class CaseFailure(Exception):
    pass


def check(condition, case):
    if not condition:
        raise CaseFailure(case)


class TerminalProcess:
    def __init__(self, command, workspace, extra_fds=()):
        self.master, self.slave = os.openpty()
        self.original = termios.tcgetattr(self.slave)
        self.output = bytearray()

        self.process = subprocess.Popen(
            command, cwd=workspace, stdin=self.slave, stdout=self.slave,
            stderr=self.slave, pass_fds=extra_fds,
        )

    def pump(self):
        readable, _, _ = select.select([self.master], [], [], 0.02)
        if readable:
            try:
                self.output.extend(os.read(self.master, 65536))
            except OSError as error:
                if error.errno != errno.EIO:
                    raise

    def wait_for(self, marker, count=1):
        deadline = time.monotonic() + 8
        while self.output.count(marker) < count:
            self.pump()
            if self.output.count(marker) < count:
                check(time.monotonic() < deadline and self.process.poll() is None, "PTY-prompt-" + marker.decode("utf-8", errors="replace") + "-" + str(count) + "-exit-" + str(self.process.poll()))

    def send(self, data):
        os.write(self.master, data)

    def finish(self, expected_exit):
        deadline = time.monotonic() + 8
        while self.process.poll() is None:
            self.pump()
            check(time.monotonic() < deadline, "PTY-exit-timeout")
        for _ in range(3):
            self.pump()
        check(self.process.returncode == expected_exit, "PTY-exit-code")
        check(FAKE_KEY not in self.output and b"*" not in self.output, "PTY-no-echo")
        mask = termios.ECHO | termios.ICANON | termios.ISIG
        restored = termios.tcgetattr(self.slave)
        check((restored[3] & mask) == (self.original[3] & mask), "PTY-restored")

    def close(self):
        if self.process.poll() is None:
            self.process.kill()
            self.process.wait()
        os.close(self.master)
        os.close(self.slave)


def run_case(command, case, actions, expected_exit=0, ready=False):
    workspace = tempfile.mkdtemp(prefix="aiagent-pty-")
    terminal = TerminalProcess(command, workspace)
    try:
        for marker, data, count in actions:
            terminal.wait_for(marker, count)
            terminal.send(data)
        if ready:
            terminal.wait_for(CHAT_PROMPT)
            terminal.send(b"/help\r")
            terminal.wait_for(CHAT_PROMPT, 2)
            terminal.send(b"/clear\r")
            terminal.wait_for(CHAT_PROMPT, 3)
            terminal.send(b"/exit\r")
        terminal.finish(expected_exit)
        check((READY in terminal.output) == ready, case)
        check(os.listdir(workspace) == [], "PTY-no-workspace-writes")
        print(case + ": PASS", flush=True)
    finally:
        terminal.close()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--app", type=Path, default=Path(__file__).resolve().parents[1] / "src/AIAgent.Cli/bin/Debug/net10.0/AIAgent.Cli.dll")
    parser.add_argument("--host", type=Path, default=Path(__file__).resolve().parents[1] / "tests/AIAgent.Cli.TerminalHost/bin/Debug/net10.0/AIAgent.Cli.TerminalHost.dll")
    args = parser.parse_args()
    check(sys.platform == "darwin", "macOS-required")
    command = ["dotnet", str(args.app.resolve())]
    confirm = (CONFIRM, b"yes\r", 1)
    run_case(command, "PTY-ready-edit-paste", [confirm, (KEY_PROMPT, b"  " + FAKE_KEY + b"X\x7fY\x1b[3~  \r", 1)], ready=True)
    run_case(command, "PTY-512", [confirm, (KEY_PROMPT, b"F" * 512 + b"\r", 1)], ready=True)
    run_case(command, "PTY-513-retry", [confirm, (KEY_PROMPT, b"F" * 513 + b"\r", 1), (KEY_PROMPT, FAKE_KEY + b"\r", 2)], ready=True)
    run_case(command, "PTY-non-ascii-retry", [confirm, (KEY_PROMPT, "FAKE測試\r".encode(), 1), (KEY_PROMPT, FAKE_KEY + b"\r", 2)], ready=True)
    run_case(command, "PTY-control-retry", [confirm, (KEY_PROMPT, b"FAKE\tVALUE\r", 1), (KEY_PROMPT, FAKE_KEY + b"\r", 2)], ready=True)
    run_case(command, "PTY-blank-retry", [confirm, (KEY_PROMPT, b"  \r", 1), (KEY_PROMPT, FAKE_KEY + b"\r", 2)], ready=True)
    run_case(command, "PTY-key-control-c", [confirm, (KEY_PROMPT, b"FAKE_PARTIAL_ONLY\x03", 1)], expected_exit=130)
    run_case(command, "PTY-key-control-d", [confirm, (KEY_PROMPT, b"FAKE_PARTIAL_ONLY\x04", 1)])
    run_case(command, "PTY-confirm-control-c", [(CONFIRM, b"\x03", 1)], expected_exit=130)
    run_case(command, "PTY-confirm-control-d", [(CONFIRM, b"\x04", 1)])
    run_case(command, "PTY-decline", [(CONFIRM, b"n\r", 1)])
    # External SIGINT exercises the CancelKeyPress handler rather than a typed control byte.
    workspace = tempfile.mkdtemp(prefix="aiagent-pty-")
    terminal = TerminalProcess(command, workspace)
    try:
        terminal.wait_for(CONFIRM)
        terminal.send(b"y\r")
        terminal.wait_for(KEY_PROMPT)
        os.kill(terminal.process.pid, signal.SIGINT)
        terminal.finish(130)
        print("PTY-external-sigint: PASS", flush=True)
    finally:
        terminal.close()
    # Each redirection is checked independently; --help bypasses all interactive services.
    for stdin_redirected, stdout_redirected in [(True, False), (False, True), (True, True)]:
        master, slave = os.openpty()
        try:
            result = subprocess.run(command, stdin=subprocess.DEVNULL if stdin_redirected else slave,
                                    stdout=subprocess.PIPE if stdout_redirected else slave, stderr=subprocess.PIPE, timeout=8)
            check(result.returncode == 1, "redirect-exit")
            if result.stdout is not None:
                check(KEY_PROMPT not in result.stdout and CONFIRM not in result.stdout, "redirect-no-prompts")
        finally:
            os.close(master)
            os.close(slave)
    help_result = subprocess.run(command + ["--help"], stdin=subprocess.DEVNULL, capture_output=True, timeout=8)
    check(help_result.returncode == 0 and KEY_PROMPT not in help_result.stdout, "redirect-help")
    invalid = subprocess.run(command + ["--key", "FAKE_ARGUMENT_ONLY"], stdin=subprocess.DEVNULL, capture_output=True, timeout=8)
    check(invalid.returncode == 2 and b"FAKE_ARGUMENT_ONLY" not in invalid.stdout + invalid.stderr, "argument-no-echo")
    # The expected output may already exist when a fast child exits.
    completed = TerminalProcess([sys.executable, "-c", "print('PTY_COMPLETE', flush=True)"], tempfile.mkdtemp(prefix="aiagent-pty-completed-"))
    try:
        completed.process.wait(timeout=8)
        completed.wait_for(b"PTY_COMPLETE")
        completed.finish(0)
    finally:
        completed.close()
    print("PTY-redirection-help-arguments: PASS", flush=True)
    chat_cases(["dotnet", str(args.host.resolve())])
    edit_cases(["dotnet", str(args.host.resolve())])


def chat_cases(host):
    def start(scenario, gate_fd=None):
        workspace = tempfile.mkdtemp(prefix="aiagent-chat-pty-")
        command = host + [scenario] + ([] if gate_fd is None else [str(gate_fd)])
        Path(workspace, "notes.md").write_text("Hello\n", encoding="utf-8")
        terminal = TerminalProcess(command, workspace, () if gate_fd is None else (gate_fd,))
        terminal.wait_for(CONFIRM)
        terminal.send(b"y\r")
        terminal.wait_for(KEY_PROMPT)
        terminal.send(FAKE_KEY + b"\r")
        terminal.wait_for(CHAT_PROMPT)
        return terminal

    terminal = start("text")
    try:
        before = len(terminal.output)
        terminal.send(" 安全中文😀X".encode())
        for _ in range(3):
            terminal.pump()
        check("安全中文".encode() not in terminal.output[before:], "CLI-AC-029-hidden-before-enter")
        terminal.send(b"\x7f\x7f" + "字\r\n".encode())
        terminal.wait_for(CHAT_PROMPT, 2)
        check("使用者： 安全中文字".encode() in terminal.output, "CLI-AC-032-unicode-delete")
        terminal.send(FAKE_KEY + b"\r")
        terminal.wait_for(CHAT_PROMPT, 3)
        check("已阻擋本次傳送".encode() in terminal.output, "CLI-AC-029-sensitive-input")
        terminal.send(b"x" * 16385 + b"\x7f\r")
        terminal.wait_for(CHAT_PROMPT, 4)
        check("輸入無效或超過 16384".encode() in terminal.output, "CLI-AC-032-overflow")
        terminal.send(b"control\r")
        terminal.wait_for(CHAT_PROMPT, 5)
        check(b"\\u001B]0;blocked\\u0007" in terminal.output, "CLI-AC-032-control-output")
        check(b"\x1b]0;blocked\x07" not in terminal.output, "CLI-AC-032-no-raw-control-output")
        terminal.send(b"/exit\r")
        terminal.finish(0)
        print("CLI-AC-029/032-text-unicode-controls: PASS", flush=True)
    finally:
        terminal.close()

    terminal = start("tools")
    try:
        terminal.send(b"read\r")
        terminal.wait_for(CHAT_PROMPT, 2)
        check("[本機結果] 已讀取：notes.md，6 bytes".encode() in terminal.output, "LOOP-AC-037-real-read")
        check("生成 2 次；傳輸嘗試 2 次".encode() in terminal.output, "LOOP-AC-037-two-generations")
        terminal.send(b"/clear\r")
        terminal.wait_for(CHAT_PROMPT, 3)
        terminal.send(b"read\r")
        terminal.wait_for(CHAT_PROMPT, 4)
        check(terminal.output.count("[本機結果] 已讀取".encode()) == 2, "LOOP-AC-037-clear-reread")
        terminal.send(b"/exit\r")
        terminal.finish(0)
        print("LOOP-AC-037-read-continue-clear: PASS", flush=True)
    finally:
        terminal.close()

    for external, control, expected in ((False, b"\x03\x03", 0), (True, b"", 0), (False, b"\x04", 0)):
        read_fd, write_fd = os.pipe()
        terminal = start("tool-busy", read_fd)
        os.close(read_fd)
        try:
            terminal.send(b"read\r")
            terminal.wait_for("[工具] read_file".encode())
            terminal.wait_for(b"OFFLINE_TOOL_WAITING")
            terminal.send(b"DISCARDED_TOOL_TEXT/exit")
            if external:
                os.kill(terminal.process.pid, signal.SIGINT)
            else:
                terminal.send(control)
            terminal.wait_for(b"OFFLINE_TOOL_CANCEL_OBSERVED")
            os.write(write_fd, b"1")
            if control == b"\x04":
                terminal.finish(expected)
            else:
                terminal.wait_for(CHAT_PROMPT, 2)
                check(b"DISCARDED_TOOL_TEXT" not in terminal.output, "LOOP-AC-037-tool-no-queue")
                terminal.send(b"next\r")
                terminal.wait_for(CHAT_PROMPT, 3)
                terminal.send(b"/exit\r")
                terminal.finish(expected)
            print("LOOP-AC-037-tool-cancel-" + ("SIGINT" if external else "EOF" if control == b"\x04" else "CtrlC") + ": PASS", flush=True)
        finally:
            os.close(write_fd)
            terminal.close()

    for external in (False, True):
        terminal = start("busy")
        try:
            terminal.send(b"question\r")
            terminal.wait_for("處理中".encode())
            terminal.send(b"DISCARDED_BUSY_TEXT/exit")
            if external:
                os.kill(terminal.process.pid, signal.SIGINT)
            else:
                terminal.send(b"\x03\x03")
            terminal.wait_for(CHAT_PROMPT, 2)
            check(b"DISCARDED_BUSY_TEXT" not in terminal.output, "CLI-AC-030-no-queue")
            terminal.send(b"next\r")
            terminal.wait_for(CHAT_PROMPT, 3)
            if external:
                os.kill(terminal.process.pid, signal.SIGINT)
            else:
                terminal.send(b"\x03")
            terminal.finish(130)
            print("CLI-AC-030-busy-" + ("SIGINT" if external else "CtrlC") + ": PASS", flush=True)
        finally:
            terminal.close()

    for scenario, control, expected in (("busy", b"\x04", 0), ("failure", b"", 1)):
        terminal = start(scenario)
        try:
            terminal.send(b"question\r")
            terminal.wait_for("處理中".encode())
            if control:
                terminal.send(control)
            terminal.finish(expected)
            print("CLI-AC-030-" + scenario + "-exit-" + str(expected) + ": PASS", flush=True)
        finally:
            terminal.close()

    for scenario, control, expected in (("auth", b"\x04", 0), ("auth", b"\x03", 130), ("tool-auth", b"\x04", 0), ("tool-auth", b"\x03", 130)):
        read_fd, write_fd = os.pipe()
        terminal = start(scenario, read_fd)
        os.close(read_fd)
        try:
            terminal.send(b"question\r")
            terminal.wait_for("處理中".encode())
            terminal.send(b"yes\rFAKE_BUSY_KEY_ONLY\r")
            os.write(write_fd, b"1")
            terminal.wait_for("重新輸入 Key？[y/N]".encode())
            terminal.send(b"invalid\ryes\r")
            terminal.wait_for(KEY_PROMPT, 2)
            terminal.send(b"FAKE_NEW_KEY_ONLY\r")
            terminal.wait_for(CHAT_PROMPT, 2)
            check(b"FAKE_NEW_KEY_ONLY" not in terminal.output and b"FAKE_BUSY_KEY_ONLY" not in terminal.output, "CLI-AC-031-no-key-echo")
            check(terminal.output.count("處理中".encode()) == (2 if scenario == "tool-auth" else 1), "CLI-AC-031-no-validation-generation")
            if scenario == "tool-auth":
                check("[本機結果] 已讀取".encode() in terminal.output, "LOOP-AC-037-tool-before-auth")
            terminal.send(control)
            terminal.finish(expected)
            print(("LOOP-AC-037-tool-auth-" if scenario == "tool-auth" else "CLI-AC-031-auth-") + str(expected) + ": PASS", flush=True)
        finally:
            os.close(write_fd)
            terminal.close()

    for key_entry, control, expected in ((False, b"\x04", 0), (False, b"\x03", 130), (True, b"\x04", 0), (True, b"\x03", 130)):
        read_fd, write_fd = os.pipe()
        terminal = start("auth", read_fd)
        os.close(read_fd)
        try:
            terminal.send(b"question\r")
            terminal.wait_for("處理中".encode())
            os.write(write_fd, b"1")
            terminal.wait_for("重新輸入 Key？[y/N]".encode())
            if key_entry:
                terminal.send(b"yes\r\n")
                terminal.wait_for(KEY_PROMPT, 2)
                terminal.send(b"FAKE_PARTIAL_ONLY")
            terminal.send(control)
            terminal.finish(expected)
            check(b"FAKE_PARTIAL_ONLY" not in terminal.output, "CLI-AC-031-partial-discard")
            print("CLI-AC-031-abort-" + ("key" if key_entry else "choice") + "-" + str(expected) + ": PASS", flush=True)
        finally:
            os.close(write_fd)
            terminal.close()


def edit_cases(host):
    ready = b"OFFLINE_APPROVAL_READY"

    def start(scenario="edit", gate_fd=None):
        workspace = tempfile.mkdtemp(prefix="aiagent-edit-pty-")
        terminal = TerminalProcess(host + [scenario] + ([] if gate_fd is None else [str(gate_fd)]), workspace,
                                   () if gate_fd is None else (gate_fd,))
        terminal.wait_for(CONFIRM)
        terminal.send(b"y\r")
        terminal.wait_for(KEY_PROMPT)
        terminal.send(FAKE_KEY + b"\r")
        terminal.wait_for(CHAT_PROMPT)
        return terminal, Path(workspace)

    for choice, applied in ((b"y\r\n", True), (b" YeS \r", True), (b"n\r", False), (b"\r", False)):
        terminal, workspace = start()
        try:
            terminal.send(b"create\r")
            terminal.wait_for(ready)
            terminal.send(choice)
            terminal.wait_for(CHAT_PROMPT, 2)
            target = workspace / "new.md"
            check(target.exists() == applied, "EDIT-AC-042-approval-state")
            if applied:
                check(target.read_bytes() == b"Hello\n", "EDIT-AC-042-exact-bytes")
                check("已寫入並核對".encode() in terminal.output, "EDIT-AC-042-verified")
            terminal.send(b"/clear\r")
            terminal.wait_for(CHAT_PROMPT, 3)
            check(target.exists() == applied, "EDIT-AC-042-clear-no-rollback")
            terminal.send(b"/exit\r")
            terminal.finish(0)
            print("EDIT-AC-042-choice-" + ("yes" if applied else "no") + "-" + str(len(choice)) + ": PASS", flush=True)
        finally:
            terminal.close()

    terminal, workspace = start()
    try:
        terminal.send(b"create\r")
        terminal.wait_for(ready)
        terminal.send(b"/clear\r")
        terminal.wait_for(ready, 2)
        terminal.send(b"yes\r")
        terminal.wait_for(CHAT_PROMPT, 2)
        check((workspace / "new.md").read_bytes() == b"Hello\n", "EDIT-AC-042-invalid-retry")
        terminal.send(b"/exit\r")
        terminal.finish(0)
        print("EDIT-AC-042-invalid-retry: PASS", flush=True)
    finally:
        terminal.close()

    for control in ("CtrlC", "SIGINT", "EOF", "EOF-CtrlC"):
        terminal, workspace = start()
        try:
            terminal.send(b"create\r")
            terminal.wait_for(ready)
            if control == "SIGINT":
                os.kill(terminal.process.pid, signal.SIGINT)
            else:
                terminal.send(b"\x04\x03" if control == "EOF-CtrlC" else b"\x04" if control == "EOF" else b"\x03")
            if control not in ("EOF", "EOF-CtrlC"):
                terminal.wait_for(CHAT_PROMPT, 2)
                terminal.send(b"next\r")
                terminal.wait_for(CHAT_PROMPT, 3)
                terminal.send(b"/exit\r")
            terminal.finish(0)
            check(list(workspace.iterdir()) == [], "EDIT-AC-042-cancel-no-write")
            print("EDIT-AC-042-approval-" + control + ": PASS", flush=True)
        finally:
            terminal.close()

    for scenario, marker in (("edit-busy-gated", b"OFFLINE_EDIT_GENERATING"), ("edit-preview-gated", b"OFFLINE_PREVIEW_PAUSED")):
        read_fd, write_fd = os.pipe()
        terminal, workspace = start(scenario, read_fd)
        os.close(read_fd)
        try:
            terminal.send(b"create\r")
            terminal.wait_for(marker)
            terminal.send(b"yes\r\n")
            os.write(write_fd, b"1")
            terminal.wait_for(ready)
            terminal.send(b"n\r")
            terminal.wait_for(CHAT_PROMPT, 2)
            check(list(workspace.iterdir()) == [], "EDIT-AC-042-discard-early-yes")
            terminal.send(b"/exit\r")
            terminal.finish(0)
            print("EDIT-AC-042-" + scenario + ": PASS", flush=True)
        finally:
            os.close(write_fd)
            terminal.close()

    for scenario, published in (("edit-stage-gated", False), ("edit-publish-gated", True)):
        read_fd, write_fd = os.pipe()
        terminal, workspace = start(scenario, read_fd)
        os.close(read_fd)
        try:
            terminal.send(b"create\r")
            terminal.wait_for(ready)
            terminal.send(b"yes\r")
            terminal.wait_for(b"OFFLINE_EDIT_IO_GATE")
            terminal.send(b"\x03")
            terminal.wait_for(b"OFFLINE_EDIT_CANCEL_OBSERVED")
            os.write(write_fd, b"1")
            terminal.wait_for(CHAT_PROMPT, 2)
            check((workspace / "new.md").exists() == published, "EDIT-AC-042-publication-boundary")
            if published:
                check("已寫入並核對".encode() in terminal.output, "EDIT-AC-042-cancel-keeps-verified")
            terminal.send(b"/exit\r")
            terminal.finish(0)
            print("EDIT-AC-042-" + scenario + ": PASS", flush=True)
        finally:
            os.close(write_fd)
            terminal.close()


if __name__ == "__main__":
    try:
        main()
    except Exception as error:
        # Never dump captured terminal output, key material or raw process exceptions.
        print("PTY checks: FAIL (" + (str(error) if isinstance(error, CaseFailure) else type(error).__name__) + ")", flush=True)
        sys.exit(1)

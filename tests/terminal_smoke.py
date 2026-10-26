#!/usr/bin/env python3
"""macOS PTY smoke checks. Only synthetic keys; never print/capture raw terminal data to disk.

This is automated PTY evidence, not the CLI-AC-033/034/035 human acceptance.
Temporary workspaces are retained; no recursive cleanup is performed.
"""
import argparse
import errno
import os
import re
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
HOST_PROMPT = b"OFFLINE_INPUT_READY"


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
            # Startup cases exercise bootstrap; EOF is preserved across input activation.
            # Local commands are checked below against explicit offline-host activation gates.
            terminal.send(b"\x04")
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
    budget_cases(["dotnet", str(args.host.resolve())])
    discovery_cases(["dotnet", str(args.host.resolve())])
    plan_cases(["dotnet", str(args.host.resolve())])
    controlled_build_cases(["dotnet", str(args.host.resolve())])
    context_cases(["dotnet", str(args.host.resolve())])
    rules_cases(["dotnet", str(args.host.resolve())])
    review_cases(["dotnet", str(args.host.resolve())])
    terminal_editor_cases(["dotnet", str(args.host.resolve())])
    work_cases(["dotnet", str(args.host.resolve())])
    checkpoint_cases(["dotnet", str(args.host.resolve())])


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
        terminal.wait_for(HOST_PROMPT)
        return terminal

    terminal = start("text")
    try:
        before = len(terminal.output)
        terminal.send(" 安全中文😀X".encode())
        for _ in range(3):
            terminal.pump()
        check("安全中文".encode() not in terminal.output[before:], "CLI-AC-029-hidden-before-enter")
        terminal.send(b"\x7f\x7f" + "字\r\n".encode())
        terminal.wait_for(HOST_PROMPT, 2)
        check("使用者： 安全中文字".encode() in terminal.output, "CLI-AC-032-unicode-delete")
        terminal.send(FAKE_KEY + b"\r")
        terminal.wait_for(HOST_PROMPT, 3)
        check("已阻擋本次傳送".encode() in terminal.output, "CLI-AC-029-sensitive-input")
        terminal.send(b"x" * 16385 + b"\x7f\r")
        terminal.wait_for(HOST_PROMPT, 4)
        check("輸入無效或超過 16384".encode() in terminal.output, "CLI-AC-032-overflow")
        terminal.send(b"control\r")
        terminal.wait_for(HOST_PROMPT, 5)
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
        terminal.wait_for(HOST_PROMPT, 2)
        check("[本機結果] 已讀取：notes.md，6 bytes".encode() in terminal.output, "LOOP-AC-037-real-read")
        check("生成 2 次；傳輸嘗試 2 次".encode() in terminal.output, "LOOP-AC-037-two-generations")
        terminal.send(b"/clear\r")
        terminal.wait_for(HOST_PROMPT, 3)
        terminal.send(b"read\r")
        terminal.wait_for(HOST_PROMPT, 4)
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
            terminal.wait_for("[工具開始] read_file".encode())
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
                terminal.wait_for(HOST_PROMPT, 2)
                check(b"DISCARDED_TOOL_TEXT" not in terminal.output, "LOOP-AC-037-tool-no-queue")
                terminal.send(b"next\r")
                terminal.wait_for(HOST_PROMPT, 3)
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
            terminal.wait_for(HOST_PROMPT, 2)
            check(b"DISCARDED_BUSY_TEXT" not in terminal.output, "CLI-AC-030-no-queue")
            terminal.send(b"next\r")
            terminal.wait_for(HOST_PROMPT, 3)
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
            terminal.wait_for(b"OFFLINE_KEY_CHOICE_READY")
            terminal.send(b"invalid\r")
            terminal.wait_for(b"OFFLINE_KEY_CHOICE_READY", 2)
            terminal.send(b"yes\r")
            terminal.wait_for(b"OFFLINE_KEY_READY")
            terminal.send(b"FAKE_NEW_KEY_ONLY\r")
            terminal.wait_for(HOST_PROMPT, 2)
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
            terminal.wait_for(b"OFFLINE_KEY_CHOICE_READY")
            if key_entry:
                terminal.send(b"yes\r\n")
                terminal.wait_for(b"OFFLINE_KEY_READY")
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
        terminal.wait_for(HOST_PROMPT)
        return terminal, Path(workspace)

    for choice, applied in ((b"y\r\n", True), (b" YeS \r", True), (b"n\r", False), (b"\r", False)):
        terminal, workspace = start()
        try:
            terminal.send(b"create\r")
            terminal.wait_for(ready)
            terminal.send(choice)
            terminal.wait_for(HOST_PROMPT, 2)
            target = workspace / "new.md"
            check(target.exists() == applied, "EDIT-AC-042-approval-state")
            if applied:
                check(target.read_bytes() == b"Hello\n", "EDIT-AC-042-exact-bytes")
                check("已寫入並核對".encode() in terminal.output, "EDIT-AC-042-verified")
            terminal.send(b"/clear\r")
            terminal.wait_for(HOST_PROMPT, 3)
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
        terminal.wait_for(HOST_PROMPT, 2)
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
                terminal.wait_for(HOST_PROMPT, 2)
                terminal.send(b"next\r")
                terminal.wait_for(HOST_PROMPT, 3)
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
            terminal.wait_for(HOST_PROMPT, 2)
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
            terminal.wait_for(HOST_PROMPT, 2)
            check((workspace / "new.md").exists() == published, "EDIT-AC-042-publication-boundary")
            if published:
                check("已寫入並核對".encode() in terminal.output, "EDIT-AC-042-cancel-keeps-verified")
            terminal.send(b"/exit\r")
            terminal.finish(0)
            print("EDIT-AC-042-" + scenario + ": PASS", flush=True)
        finally:
            os.close(write_fd)
            terminal.close()


def budget_cases(host):
    """BUD-AC-045: actual PTY handoff, Unicode preview, partial completion."""
    ready = b"OFFLINE_APPROVAL_READY"
    expected = "\ufeff中文\r\n😀\n尾\r終".encode()
    for decision in ("no", "blank", "CtrlC", "EOF"):
        workspace = Path(tempfile.mkdtemp(prefix="aiagent-budget-pty-"))
        terminal = TerminalProcess(host + ["edit-budget"], workspace)
        try:
            terminal.wait_for(CONFIRM)
            terminal.send(b"y\r")
            terminal.wait_for(KEY_PROMPT)
            terminal.send(FAKE_KEY + b"\r")
            terminal.wait_for(HOST_PROMPT)
            terminal.send("建立中文😀\r\n".encode())
            terminal.wait_for(ready)
            check("中文.md".encode() in terminal.output and all(marker in terminal.output for marker in
                  (b"[CRLF]", b"[LF]", b"[CR]", b"[EOF]")), "BUD-AC-045-complete-unicode-preview")
            terminal.send("同意😀\r\n".encode())
            terminal.wait_for(ready, 2)
            check(list(workspace.iterdir()) == [], "BUD-AC-045-invalid-is-not-approval")
            terminal.send(b"yes\r\n")
            terminal.wait_for(ready, 3)
            check((workspace / "中文.md").read_bytes() == expected, "BUD-AC-045-first-verified")
            terminal.send({"no": b"no\r\n", "blank": b"\r\n", "CtrlC": b"\x03", "EOF": b"\x04"}[decision])
            if decision != "EOF":
                terminal.wait_for(HOST_PROMPT, 2)
                terminal.send(b"/clear\r")
                terminal.wait_for(HOST_PROMPT, 3)
                terminal.send("下一回合\r".encode())
                terminal.wait_for(HOST_PROMPT, 4)
                terminal.send(b"/exit\r")
            terminal.finish(0)
            check((workspace / "中文.md").read_bytes() == expected and not (workspace / "second.md").exists(),
                  "BUD-AC-045-retain-partial-completion")
            check("已寫入並核對".encode() in terminal.output and "生成 1 次；傳輸嘗試 1 次".encode() in terminal.output,
                  "BUD-AC-045-local-fact-and-usage")
            print("BUD-AC-045-unicode-partial-" + decision + ": PASS", flush=True)
        finally:
            terminal.close()


def discovery_cases(host):
    """SEARCH-AC-033: offline summaries, cancellation, EOF and discarded busy input."""
    for decision in ("complete", "CtrlC", "EOF"):
        workspace = Path(tempfile.mkdtemp(prefix="aiagent-search-pty-"))
        original = "one\r\n二\nlast".encode()
        (workspace / "notes.md").write_bytes(original)
        read_fd, write_fd = os.pipe()
        scenario = "search" if decision == "complete" else "search-busy"
        terminal = TerminalProcess(host + [scenario, str(read_fd)], workspace, (read_fd,))
        os.close(read_fd)
        try:
            terminal.wait_for(CONFIRM); terminal.send(b"y\r")
            terminal.wait_for(KEY_PROMPT); terminal.send(FAKE_KEY + b"\r")
            terminal.wait_for(HOST_PROMPT); terminal.send(b"/help\r")
            terminal.wait_for(HOST_PROMPT, 2)
            check(all(name in terminal.output for name in (b"search_files", b"search_text", b"read_file_range")), "SEARCH-AC-033-help")
            terminal.send("搜尋中文😀\r".encode())
            if decision == "complete":
                terminal.wait_for(HOST_PROMPT, 3)
                check(all(text.encode() in terminal.output for text in ("找到安全檔案：1 筆", "找到文字命中：1 行", "第 2～3 行；回傳 8 bytes")), "SEARCH-AC-033-summaries")
                terminal.send(b"/exit\r")
            else:
                terminal.wait_for(b"OFFLINE_SEARCH_WAITING")
                terminal.send(b"discarded-busy-input\r")
                terminal.send(b"\x03" if decision == "CtrlC" else b"\x04")
                terminal.wait_for(b"OFFLINE_SEARCH_CANCEL_OBSERVED")
                check(terminal.process.poll() is None, "SEARCH-AC-033-waits-for-cleanup")
                os.write(write_fd, b"x")
                if decision == "CtrlC":
                    terminal.wait_for(HOST_PROMPT, 3)
                    terminal.send("繼續聊天\r".encode())
                    terminal.wait_for(HOST_PROMPT, 4)
                    terminal.send(b"/exit\r")
                check("找到文字命中".encode() not in terminal.output and "已取得行範圍".encode() not in terminal.output,
                      "SEARCH-AC-033-no-late-result")
                check(b"discarded-busy-input" not in terminal.output, "SEARCH-AC-033-no-queued-input")
            terminal.finish(0)
            check((workspace / "notes.md").read_bytes() == original, "SEARCH-AC-033-source-unchanged")
            print("SEARCH-AC-033-" + decision + ": PASS", flush=True)
        finally:
            os.close(write_fd)
            terminal.close()


def plan_cases(host):
    """PLAN-AC-033: local modes, fresh version approval and separate file approval."""
    plan_ready = b"OFFLINE_PLAN_APPROVAL_READY"
    file_ready = b"OFFLINE_APPROVAL_READY"
    for choice in ("yes", "no", "blank", "CtrlC", "EOF", "prepaste", "busy"):
        workspace = Path(tempfile.mkdtemp(prefix="aiagent-plan-pty-"))
        read_fd, write_fd = os.pipe()
        scenario = "plan-preview-gated" if choice == "prepaste" else "plan-busy" if choice == "busy" else "plan-flow"
        terminal = TerminalProcess(host + [scenario, str(read_fd)], workspace, (read_fd,))
        os.close(read_fd)
        try:
            terminal.wait_for(CONFIRM); terminal.send(b"y\r")
            terminal.wait_for(KEY_PROMPT); terminal.send(FAKE_KEY + b"\r")
            terminal.wait_for(HOST_PROMPT); terminal.send(b"/plan\r")
            terminal.wait_for(HOST_PROMPT, 2)
            check(b"[Plan]" in terminal.output and list(workspace.iterdir()) == [], "PLAN-AC-033-plan-local")
            terminal.send("規劃中文😀\r".encode())
            terminal.wait_for(HOST_PROMPT, 3); terminal.send(b"/tasks\r")
            terminal.wait_for(HOST_PROMPT, 4)
            check(all(text.encode() in terminal.output for text in ("中文計畫😀", "建立檔案", "本機寫入核對", "new.md")), "PLAN-AC-033-full-task-list")
            terminal.send(b"/execute\r")
            if choice == "prepaste":
                terminal.wait_for(b"OFFLINE_PLAN_PREVIEW_PAUSED")
                terminal.send(b"yes\r")
                os.write(write_fd, b"x")
            terminal.wait_for(plan_ready)
            check(list(workspace.iterdir()) == [], "PLAN-AC-033-preview-no-write")
            if choice == "prepaste":
                terminal.send(b"\x03")
            elif choice in ("yes", "busy"):
                terminal.send(b"invalid\r")
                terminal.wait_for(plan_ready, 2)
                terminal.send(b" YeS \r\n")
            else:
                terminal.send({"no": b"no\r", "blank": b"\r", "CtrlC": b"\x03", "EOF": b"\x04"}[choice])
            if choice != "EOF":
                terminal.wait_for(HOST_PROMPT, 5)
                check(list(workspace.iterdir()) == [], "PLAN-AC-033-confirm-does-not-execute")
                if choice in ("yes", "busy"):
                    terminal.send("執行下一步\r".encode())
                    if choice == "busy":
                        terminal.wait_for(b"OFFLINE_PLAN_GENERATING")
                        terminal.send(b"discarded-plan-busy\r")
                        os.write(write_fd, b"x")
                    terminal.wait_for(file_ready)
                    check(list(workspace.iterdir()) == [], "PLAN-AC-033-separate-file-approval")
                    terminal.send(b"yes\r")
                    terminal.wait_for(HOST_PROMPT, 6)
                    check((workspace / "new.md").read_bytes() == b"Hello\n", "PLAN-AC-033-verified-bytes")
                    check(b"completed" in terminal.output and b"local_operation" in terminal.output, "PLAN-AC-033-completion-evidence")
                terminal.send(b"/exit\r")
            terminal.finish(0)
            check(b" YeS " not in terminal.output and b"discarded-plan-busy" not in terminal.output, "PLAN-AC-033-hidden-no-queue")
            if choice not in ("yes", "busy"):
                check(list(workspace.iterdir()) == [], "PLAN-AC-033-declined-no-write")
            print("PLAN-AC-033-" + choice + ": PASS", flush=True)
        finally:
            os.close(write_fd)
            terminal.close()


def controlled_build_cases(host):
    """BUILD-AC-035: terminal behavior with an injected simulator, separate from native fixtures."""
    ready = b"OFFLINE_PROCESS_APPROVAL_READY"
    for choice in ("yes", "no", "blank", "CtrlC", "EOF", "prepaste", "running", "unconfirmed", "progress", "plan"):
        workspace = Path(tempfile.mkdtemp(prefix="aiagent-build-pty-"))
        read_fd, write_fd = os.pipe()
        scenario = "build-preview-gated" if choice == "prepaste" else "build-running" if choice == "running" else "build-unconfirmed" if choice == "unconfirmed" else "build-progress" if choice == "progress" else "build-flow"
        terminal = TerminalProcess(host + [scenario, str(read_fd)], workspace, (read_fd,))
        os.close(read_fd)
        try:
            terminal.wait_for(CONFIRM); terminal.send(b"y\r")
            terminal.wait_for(KEY_PROMPT); terminal.send(FAKE_KEY + b"\r")
            terminal.wait_for(HOST_PROMPT)
            prompt_count = 1
            if choice == "plan":
                terminal.send(b"/plan\r"); prompt_count += 1; terminal.wait_for(HOST_PROMPT, prompt_count)
            terminal.send("建置中文😀\r".encode())
            if choice == "plan":
                prompt_count += 1; terminal.wait_for(HOST_PROMPT, prompt_count)
                check(ready not in terminal.output, "BUILD-AC-035-Plan-zero-approval")
                terminal.send(b"/exit\r"); terminal.finish(0)
            else:
                if choice == "prepaste":
                    terminal.wait_for(b"OFFLINE_PROCESS_PREVIEW_PAUSED")
                    terminal.send(b"yes\r"); os.write(write_fd, b"x")
                terminal.wait_for(ready)
                if choice in ("yes", "running", "unconfirmed", "progress"):
                    terminal.send(b"invalid\r"); terminal.wait_for(ready, 2); terminal.send(b" YeS \r\n")
                else:
                    terminal.send({"no": b"no\r", "blank": b"\r", "CtrlC": b"\x03", "EOF": b"\x04", "prepaste": b"\x03"}[choice])
                if choice == "running":
                    terminal.wait_for(b"OFFLINE_PROCESS_RUNNING")
                    terminal.send(b"discarded-process-input\r\x03")
                    terminal.wait_for(b"OFFLINE_PROCESS_CANCEL_OBSERVED")
                    check(terminal.process.poll() is None, "BUILD-AC-035-waits-for-cleanup")
                    os.write(write_fd, b"x")
                if choice == "progress":
                    terminal.wait_for("已經過 5 秒".encode())
                if choice not in ("EOF", "unconfirmed"):
                    prompt_count += 1; terminal.wait_for(HOST_PROMPT, prompt_count); terminal.send(b"/exit\r")
                terminal.finish(1 if choice == "unconfirmed" else 0)
            check(list(workspace.iterdir()) == [], "BUILD-AC-035-simulator-no-source-writes")
            check(b" YeS " not in terminal.output and b"discarded-process-input" not in terminal.output, "BUILD-AC-035-hidden-busy")
            if choice == "yes":
                check(b"build_succeeded" in terminal.output, "BUILD-AC-035-local-summary")
            if choice in ("no", "blank", "CtrlC", "prepaste", "plan"):
                check(b"build_succeeded" not in terminal.output, "BUILD-AC-035-no-false-success")
            print("BUILD-AC-035-" + choice + ": PASS", flush=True)
        finally:
            os.close(write_fd); terminal.close()


def context_cases(host):
    workspace = Path(tempfile.mkdtemp(prefix="aiagent-context-pty-"))
    terminal = TerminalProcess(host + ["context"], workspace)
    try:
        terminal.wait_for(CONFIRM); terminal.send(b"y\r")
        terminal.wait_for(KEY_PROMPT); terminal.send(FAKE_KEY + b"\r")
        terminal.wait_for(HOST_PROMPT)
        prompt = 1
        for text in ["/context pin   中文😀  ", "/context show", "/budget set turn 1",
                     "/budget set program 1", "first", "second", "/budget",
                     "/context unpin n1", "/clear", "/budget reset"]:
            terminal.send((text + "\r").encode())
            prompt += 1
            terminal.wait_for(HOST_PROMPT, prompt)
        terminal.send(b"/exit\r")
        terminal.finish(0)
        check("n1：  中文😀  ".encode() in terminal.output, "CTX-AC-033-pin-exact-text")
        check(terminal.output.count("處理中".encode()) == 1, "CTX-AC-033-program-limit-no-extra-generation")
        check("程序已消耗：1".encode() in terminal.output, "CTX-AC-033-budget-retention")
        print("CTX-AC-033-commands: PASS", flush=True)
    finally:
        terminal.close()
    for automatic in (False, True):
        for control in ("complete", "CtrlC", "EOF"):
            read_fd, write_fd = os.pipe()
            scenario = "context-busy" if control != "complete" else "context"
            terminal = TerminalProcess(host + [scenario, str(read_fd)], workspace, (read_fd,))
            os.close(read_fd)
            try:
                terminal.wait_for(CONFIRM); terminal.send(b"y\r")
                terminal.wait_for(KEY_PROMPT); terminal.send(FAKE_KEY + b"\r")
                terminal.wait_for(HOST_PROMPT)
                prompt = 1
                for i in range(5 if automatic else 3):
                    terminal.send(("question " + str(i) + "\r").encode())
                    prompt += 1
                    terminal.wait_for(HOST_PROMPT, prompt)
                terminal.send(b"current\r" if automatic else b"/compact\r")
                if control != "complete":
                    terminal.wait_for(b"OFFLINE_CONTEXT_COMPACTING")
                    terminal.send(b"discarded-context-input\r" + (b"\x03" if control == "CtrlC" else b"\x04"))
                    terminal.wait_for(b"OFFLINE_CONTEXT_CANCEL_OBSERVED")
                    check(terminal.process.poll() is None, "CTX-AC-033-await-cleanup")
                    os.write(write_fd, b"x")
                if control != "EOF":
                    prompt += 1
                    terminal.wait_for(HOST_PROMPT, prompt)
                    terminal.send(b"/context show\r")
                    prompt += 1
                    terminal.wait_for(HOST_PROMPT, prompt)
                    terminal.send(b"/exit\r")
                terminal.finish(0)
                check(b"discarded-context-input" not in terminal.output, "CTX-AC-033-busy-discard")
                check(terminal.output.count("正在整理對話".encode()) == 1, "CTX-AC-033-one-compaction")
                if control == "complete":
                    check(b"model_summary" in terminal.output, "CTX-AC-033-summary-published")
                print("CTX-AC-033-" + ("auto" if automatic else "manual") + "-" + control + ": PASS", flush=True)
            finally:
                os.close(write_fd)
                terminal.close()


def rules_cases(host):
    workspace = Path(tempfile.mkdtemp(prefix="aiagent-rules-pty-"))
    (workspace / "AGENTS.md").write_text("Root rule.", encoding="utf-8")
    directory = " 中文 空白 "
    (workspace / directory).mkdir()
    leaf = workspace / directory / "AGENTS.md"
    leaf.write_text("Leaf original.", encoding="utf-8")
    terminal = TerminalProcess(host + ["rules"], workspace)
    try:
        terminal.wait_for(CONFIRM); terminal.send(b"y\r")
        terminal.wait_for(KEY_PROMPT); terminal.send(FAKE_KEY + b"\r")
        terminal.wait_for(HOST_PROMPT); prompt = 1
        for text in ["/rules", "/rules show", "/Rules", "/rules scope " + directory, "/rules show"]:
            terminal.send((text + "\r").encode()); prompt += 1; terminal.wait_for(HOST_PROMPT, prompt)
        leaf.write_text("Leaf changed.", encoding="utf-8")
        for text in ["blocked", "/rules", "/rules reload", "/execute", "question", "/rules show", "/rules scope ."]:
            terminal.send((text + "\r").encode()); prompt += 1; terminal.wait_for(HOST_PROMPT, prompt)
        terminal.send(b"/exit\r"); terminal.finish(0)
        check(b"Root rule." in terminal.output and b"Leaf original." in terminal.output and b"Leaf changed." in terminal.output, "RULE-AC-032-complete-sources")
        check(directory.encode() in terminal.output and b"Stale" in terminal.output, "RULE-AC-032-literal-scope-stale")
        check(terminal.output.count("處理中".encode()) == 1, "RULE-AC-032-local-commands-no-generation")
        print("RULE-AC-032-scope-show-reload: PASS", flush=True)
    finally:
        terminal.close()
    bad = Path(tempfile.mkdtemp(prefix="aiagent-rules-unavailable-pty-"))
    (bad / "AGENTS.md").write_bytes(b"\xff")
    terminal = TerminalProcess(host + ["rules"], bad)
    try:
        terminal.wait_for(CONFIRM); terminal.send(b"y\r")
        terminal.wait_for(KEY_PROMPT); terminal.send(FAKE_KEY + b"\r")
        terminal.wait_for(HOST_PROMPT); prompt = 1
        for text in ["/rules", "/compact", "/execute", "/context", "/budget"]:
            terminal.send((text + "\r").encode()); prompt += 1; terminal.wait_for(HOST_PROMPT, prompt)
        (bad / "AGENTS.md").write_text("Fixed.", encoding="utf-8")
        for text in ["/rules reload", "question"]:
            terminal.send((text + "\r").encode()); prompt += 1; terminal.wait_for(HOST_PROMPT, prompt)
        terminal.send(b"/exit\r"); terminal.finish(0)
        check(b"Unavailable" in terminal.output and terminal.output.count("處理中".encode()) == 1, "RULE-AC-032-unavailable-allowed-local")
        print("RULE-AC-032-unavailable-recovery: PASS", flush=True)
    finally:
        terminal.close()
    for control in ("CtrlC", "EOF"):
        read_fd, write_fd = os.pipe()
        terminal = TerminalProcess(host + ["rules-busy", str(read_fd)], workspace, (read_fd,))
        os.close(read_fd)
        try:
            terminal.wait_for(CONFIRM); terminal.send(b"y\r")
            terminal.wait_for(KEY_PROMPT); terminal.send(FAKE_KEY + b"\r")
            terminal.wait_for(HOST_PROMPT); terminal.send(b"/rules reload\r")
            terminal.wait_for(b"OFFLINE_RULES_READING")
            terminal.send(b"discarded-rules-input\r" + (b"\x03" if control == "CtrlC" else b"\x04"))
            terminal.wait_for(b"OFFLINE_RULES_CANCEL_OBSERVED")
            check(terminal.process.poll() is None, "RULE-AC-032-wait-read-cleanup")
            os.write(write_fd, b"x")
            if control == "CtrlC":
                terminal.wait_for(HOST_PROMPT, 2); terminal.send(b"/rules\r")
                terminal.wait_for(HOST_PROMPT, 3); terminal.send(b"/exit\r")
            terminal.finish(0)
            check(b"discarded-rules-input" not in terminal.output and "處理中".encode() not in terminal.output, "RULE-AC-032-busy-no-replay")
            print("RULE-AC-032-busy-" + control + ": PASS", flush=True)
        finally:
            os.close(write_fd); terminal.close()


def review_cases(host):
    workspace = Path(tempfile.mkdtemp(prefix="aiagent-review-pty-"))
    for scenario in ("review", "review-empty"):
        terminal = TerminalProcess(host + [scenario], workspace)
        try:
            terminal.wait_for(CONFIRM); terminal.send(b"y\r")
            terminal.wait_for(KEY_PROMPT); terminal.send(FAKE_KEY + b"\r")
            terminal.wait_for(HOST_PROMPT); terminal.send(b"/review staged\r")
            terminal.wait_for(HOST_PROMPT, 2); terminal.send(b"/review show\r")
            terminal.wait_for(HOST_PROMPT, 3); terminal.send(b"/exit\r"); terminal.finish(0)
            check("本回合未執行建置或測試".encode() in terminal.output, "GIT-AC-020-local-verification-label")
            if scenario == "review":
                check("中文 空白.cs".encode() in terminal.output and b"1/1" in terminal.output, "GIT-AC-020-layer-delivery")
            else:
                check(b"no_changes" in terminal.output and "處理中".encode() not in terminal.output, "GIT-AC-020-zero-generation")
            print("GIT-AC-020-" + scenario + ": PASS", flush=True)
        finally:
            terminal.close()
    for phase in ("capture", "model", "tool"):
        for control in ("CtrlC", "EOF"):
            read_fd, write_fd = os.pipe()
            terminal = TerminalProcess(host + ["review-" + phase + "-busy", str(read_fd)], workspace, (read_fd,))
            os.close(read_fd)
            try:
                terminal.wait_for(CONFIRM); terminal.send(b"y\r")
                terminal.wait_for(KEY_PROMPT); terminal.send(FAKE_KEY + b"\r")
                terminal.wait_for(HOST_PROMPT); terminal.send(b"/review staged\r")
                terminal.wait_for(("OFFLINE_REVIEW_" + phase.upper() + "_PAUSED").encode())
                terminal.send(b"discarded-review-input\r" + (b"\x03" if control == "CtrlC" else b"\x04"))
                terminal.wait_for(b"OFFLINE_REVIEW_CANCEL_OBSERVED")
                check(terminal.process.poll() is None, "GIT-AC-020-wait-cleanup")
                os.write(write_fd, b"x")
                if control == "CtrlC":
                    terminal.wait_for(HOST_PROMPT, 2); terminal.send(b"/exit\r")
                terminal.finish(0)
                check(b"discarded-review-input" not in terminal.output, "GIT-AC-020-discard-busy")
                print("GIT-AC-020-" + phase + "-" + control + ": PASS", flush=True)
            finally:
                os.close(write_fd); terminal.close()


def terminal_editor_cases(host):
    f2, f3, f4 = b"\x1bOQ", b"\x1bOR", b"\x1bOS"

    def start(scenario="terminal", gate_fd=None):
        workspace = tempfile.mkdtemp(prefix="aiagent-terminal-pty-")
        terminal = TerminalProcess(host + [scenario] + ([] if gate_fd is None else [str(gate_fd)]), workspace, () if gate_fd is None else (gate_fd,))
        terminal.wait_for(CONFIRM); terminal.send(b"y\r")
        terminal.wait_for(KEY_PROMPT); terminal.send(FAKE_KEY + b"\r")
        terminal.wait_for(HOST_PROMPT)
        return terminal

    terminal = start()
    try:
        terminal.send("甲é👩🏽‍💻".encode() + b"\x1b[D\x1b[C\x7f" + f4 + b"\t\\t\x1b[A\x1b[B\x1b[H\x1b[F" + f2)
        terminal.wait_for(HOST_PROMPT, 2)
        check("1: 甲é\n2: \\t\\\\t".encode() in terminal.output.replace(b"\r\n", b"\n"), "TERM-AC-029-unicode-preview")
        check("使用者：".encode() not in terminal.output, "TERM-AC-029-preview-no-submit")
        terminal.send(b"\r"); terminal.wait_for(HOST_PROMPT, 3)
        terminal.send(b"\x10" + f2); terminal.wait_for(HOST_PROMPT, 4)
        terminal.send(b"\r"); terminal.wait_for(HOST_PROMPT, 5)
        check(terminal.output.count("使用者：".encode()) == 2, "TERM-AC-029-history-explicit-submit")
        terminal.send(b"/clear\r"); terminal.wait_for(HOST_PROMPT, 6)
        terminal.send(b"\x10" + f2); terminal.wait_for(HOST_PROMPT, 7)
        check("1: \n游標：第 1 行，第 1".encode() in terminal.output.replace(b"\r\n", b"\n"), "TERM-AC-029-clear-history")
        terminal.send(b"/exit\r"); terminal.finish(0)
        print("TERM-AC-029-unicode-preview-history-clear: PASS", flush=True)
    finally:
        terminal.close()

    terminal = start()
    try:
        terminal.send(b"pre" + f3); terminal.wait_for(b"OFFLINE_PASTE_READY")
        terminal.send(b"one\r"); terminal.wait_for(b"OFFLINE_PASTE_CR")
        terminal.send(b"\n\ttwo\r\n" + f3); terminal.wait_for(b"OFFLINE_PASTE_END_READY")
        check("使用者：".encode() not in terminal.output, "TERM-AC-029-paste-not-submitted")
        terminal.send(f2); terminal.wait_for(HOST_PROMPT, 2)
        check(b"1: preone\r\n2: \\ttwo\r\n3: " in terminal.output or b"1: preone\n2: \\ttwo\n3: " in terminal.output, "TERM-AC-029-paste-crlf")
        terminal.send(b"\r"); terminal.wait_for(HOST_PROMPT, 3)
        check(terminal.output.count("使用者：".encode()) == 1, "TERM-AC-029-one-multiline-message")
        terminal.send(b"/exit\r"); terminal.finish(0)
        print("TERM-AC-029-paste-split-crlf-tabs: PASS", flush=True)
    finally:
        terminal.close()

    terminal = start()
    try:
        terminal.send(b"/exit" + f4 + b"/rules reload\r"); terminal.wait_for(HOST_PROMPT, 2)
        check("本機指令只接受單行".encode() in terminal.output, "TERM-AC-029-command-gate")
        terminal.send(FAKE_KEY + f2); terminal.wait_for(HOST_PROMPT, 3)
        terminal.send(b"ok\r"); terminal.wait_for(HOST_PROMPT, 4)
        check("使用者：ok".encode() in terminal.output, "TERM-AC-029-sensitive-preview-restart")
        terminal.send(b"/exit\r"); terminal.finish(0)
        print("TERM-AC-029-command-gate-sensitive-preview: PASS", flush=True)
    finally:
        terminal.close()

    terminal = start()
    try:
        terminal.send(f3); terminal.wait_for(b"OFFLINE_PASTE_READY")
        terminal.send(b"x" * 16385 + b"\rremaining" + f2 + f3)
        terminal.wait_for(b"OFFLINE_PASTE_END_READY")
        terminal.send(b"\r"); terminal.wait_for(HOST_PROMPT, 2)
        check("輸入無效或超過 16384".encode() in terminal.output and "使用者：".encode() not in terminal.output, "TERM-AC-029-invalid-paste-whole")
        terminal.send(b"\x04"); terminal.finish(0)
        print("TERM-AC-029-invalid-paste-drains-whole: PASS", flush=True)
    finally:
        terminal.close()

    for stop, expected in ((b"yes\r", 0), (b"\x03", 130), (b"\x04", 0)):
        read_fd, write_fd = os.pipe(); terminal = start("terminal-preview-gated", read_fd); os.close(read_fd)
        try:
            terminal.send(b"draft" + f2); terminal.wait_for(b"OFFLINE_DRAFT_PREVIEW_PAUSED")
            terminal.send(stop); os.write(write_fd, b"g")
            if stop == b"yes\r":
                terminal.wait_for(HOST_PROMPT, 2); terminal.send(b"\r"); terminal.wait_for(HOST_PROMPT, 3)
                check(terminal.output.count("使用者：draft".encode()) == 1, "TERM-AC-029-preview-drain")
                terminal.send(b"/exit\r")
            terminal.finish(expected)
            print("TERM-AC-029-preview-drain-stop-" + str(expected) + "-" + str(len(stop)) + ": PASS", flush=True)
        finally:
            os.close(write_fd); terminal.close()

    for mode in (b"", f3):
        for external, stop, expected in ((False, b"\x03", 130), (False, b"\x04", 0), (True, b"", 130)):
            terminal = start()
            try:
                if mode:
                    terminal.send(mode); terminal.wait_for(b"OFFLINE_PASTE_READY")
                terminal.send(b"partial")
                if external:
                    os.kill(terminal.process.pid, signal.SIGINT)
                else:
                    terminal.send(stop)
                terminal.finish(expected)
                print("TERM-AC-029-editor-paste-stop-" + str(len(mode)) + "-" + str(external) + "-" + str(expected) + ": PASS", flush=True)
            finally:
                terminal.close()


def work_cases(host):
    """SAVE-AC-029: offline storage and explicit new-reader activation gates."""
    approval = b"OFFLINE_WORK_APPROVAL_READY"

    def start(scenario="work-flow", gate_fd=None):
        workspace = tempfile.mkdtemp(prefix="aiagent-work-pty-")
        Path(workspace, "src").mkdir()
        terminal = TerminalProcess(host + [scenario] + ([] if gate_fd is None else [str(gate_fd)]), workspace,
                                   () if gate_fd is None else (gate_fd,))
        terminal.wait_for(CONFIRM); terminal.send(b"y\r"); terminal.wait_for(KEY_PROMPT)
        check(b"OFFLINE_WORK_STORE_OPEN" not in terminal.output, "SAVE-AC-029-store-after-key")
        terminal.send(FAKE_KEY + b"\r")
        terminal.wait_for(approval if scenario.startswith("work-resume") else HOST_PROMPT)
        return terminal

    def command(terminal, text):
        count = terminal.output.count(HOST_PROMPT)
        terminal.send(text.encode() + b"\r"); terminal.wait_for(HOST_PROMPT, count + 1)

    terminal = start()
    try:
        command(terminal, "/context pin 保留原文")
        terminal.send("/work save   中文工作  \r".encode()); terminal.wait_for(approval)
        match = re.search(rb'"work_id":"([0-9a-f]{32})"', terminal.output)
        check(match is not None, "SAVE-AC-029-save-preview-id")
        work_id = match.group(1).decode()
        check(b"PublishedVerified" not in terminal.output, "SAVE-AC-029-before-confirm-zero-publish")
        terminal.send(b"invalid\r"); terminal.wait_for(approval, 2)
        count = terminal.output.count(HOST_PROMPT); terminal.send(b" YeS \r\n"); terminal.wait_for(HOST_PROMPT, count + 1)
        check(b"PublishedVerified" in terminal.output, "SAVE-AC-029-verified-save")
        command(terminal, "/work list"); command(terminal, "/work show " + work_id)
        check("不載入、不核准、不驗證目前檔案".encode() in terminal.output, "SAVE-AC-029-show-historical")
        terminal.send(("/work delete " + work_id + "\r").encode()); terminal.wait_for(approval, 3)
        count = terminal.output.count(HOST_PROMPT); terminal.send(b"n\r"); terminal.wait_for(HOST_PROMPT, count + 1)
        terminal.send(("/work delete " + work_id + "\r").encode()); terminal.wait_for(approval, 4)
        count = terminal.output.count(HOST_PROMPT); terminal.send(b"y\r"); terminal.wait_for(HOST_PROMPT, count + 1)
        check(b"DeletedVerified" in terminal.output, "SAVE-AC-029-single-delete")
        command(terminal, "/work show " + work_id)
        check(b"work_not_found" in terminal.output, "SAVE-AC-029-deleted-absent")
        terminal.send(b"/exit\r"); terminal.finish(0)
        print("SAVE-AC-029-save-list-show-delete-confirm-retry: PASS", flush=True)
    finally:
        terminal.close()

    read_fd, write_fd = os.pipe(); terminal = start("work-preview-gated", read_fd); os.close(read_fd)
    try:
        command(terminal, "/context pin keep")
        terminal.send(b"/work save title\r"); terminal.wait_for(b"OFFLINE_WORK_PREVIEW_PAUSED")
        terminal.send(b"yes\r"); os.write(write_fd, b"x"); terminal.wait_for(approval)
        check(b"PublishedVerified" not in terminal.output, "SAVE-AC-029-prebuffered-not-approved")
        count = terminal.output.count(HOST_PROMPT); terminal.send(b"n\r"); terminal.wait_for(HOST_PROMPT, count + 1)
        terminal.send(b"/exit\r"); terminal.finish(0)
        check(b"PublishedVerified" not in terminal.output, "SAVE-AC-029-declined-no-publish")
        print("SAVE-AC-029-preview-drains-old-yes: PASS", flush=True)
    finally:
        os.close(write_fd); terminal.close()

    for control in (b"\x03", b"\x04"):
        terminal = start()
        try:
            command(terminal, "/context pin keep")
            terminal.send(b"/work save title\r"); terminal.wait_for(approval)
            count = terminal.output.count(HOST_PROMPT); terminal.send(control)
            if control == b"\x03":
                terminal.wait_for(HOST_PROMPT, count + 1); terminal.send(b"/exit\r")
            terminal.finish(0)
            check(b"PublishedVerified" not in terminal.output, "SAVE-AC-029-local-confirm-cancel")
            print("SAVE-AC-029-local-confirm-stop-" + str(control[0]) + ": PASS", flush=True)
        finally:
            terminal.close()

    for scenario in ("work-resume", "work-resume-scope", "work-resume-completed"):
        terminal = start(scenario)
        try:
            count = 1
            if scenario.endswith("scope"):
                terminal.send(b"yes\r"); terminal.wait_for(approval, 2); count = 2
            terminal.send(b"yes\r"); terminal.wait_for(HOST_PROMPT)
            check(b"[Plan]" in terminal.output, "SAVE-AC-029-resume-plan")
            terminal.send(b"/execute\r")
            if scenario.endswith("completed"):
                terminal.wait_for(approval, count + 1)
            else:
                terminal.wait_for(b"OFFLINE_PLAN_APPROVAL_READY")
            current = terminal.output.count(HOST_PROMPT); terminal.send(b"yes\r"); terminal.wait_for(HOST_PROMPT, current + 1)
            check(b"[Execute]" in terminal.output, "SAVE-AC-029-resume-independent-execute")
            terminal.send(b"/exit\r"); terminal.finish(0)
            print("SAVE-AC-029-" + scenario + ": PASS", flush=True)
        finally:
            terminal.close()

    for control, expected in ((b"n\r", 0), (b"\x04", 0), (b"\x03", 130)):
        terminal = start("work-resume")
        try:
            terminal.send(control); terminal.finish(expected)
            check(READY not in terminal.output, "SAVE-AC-029-resume-no-blank-fallback")
            print("SAVE-AC-029-startup-stop-" + str(control[0]) + ": PASS", flush=True)
        finally:
            terminal.close()

    terminal = start("work-resume-scope")
    try:
        terminal.send(b"n\r"); terminal.finish(0)
        check(READY not in terminal.output, "SAVE-AC-029-scope-decline-exits")
        print("SAVE-AC-029-scope-decline: PASS", flush=True)
    finally:
        terminal.close()

    for control in (b"\x03", b"\x04"):
        read_fd, write_fd = os.pipe(); terminal = start("work-model-busy", read_fd); os.close(read_fd)
        try:
            command(terminal, "question")
            terminal.send(b"/work save summary\r"); terminal.wait_for(b"OFFLINE_WORK_MODEL_PAUSED")
            terminal.send(b"yes\r" + control); terminal.wait_for(b"OFFLINE_WORK_CANCEL_OBSERVED")
            os.write(write_fd, b"x")
            if control == b"\x03":
                terminal.wait_for(HOST_PROMPT, 3); terminal.send(b"/exit\r")
            terminal.finish(0)
            check(b"PublishedVerified" not in terminal.output, "SAVE-AC-029-busy-no-publish")
            print("SAVE-AC-029-busy-cancel-" + str(control[0]) + ": PASS", flush=True)
        finally:
            os.close(write_fd); terminal.close()

def checkpoint_cases(host):
    approval = b"OFFLINE_CHECKPOINT_APPROVAL_READY"

    def start(scenario="checkpoint-flow", gate_fd=None):
        workspace = Path(tempfile.mkdtemp(prefix="aiagent-checkpoint-pty-"))
        (workspace / "notes.md").write_bytes(b"before\r\n")
        terminal = TerminalProcess(host + [scenario] + ([] if gate_fd is None else [str(gate_fd)]), workspace,
                                   () if gate_fd is None else (gate_fd,))
        terminal.wait_for(CONFIRM); terminal.send(b"yes\r")
        terminal.wait_for(KEY_PROMPT); terminal.send(FAKE_KEY + b"\r")
        terminal.wait_for(HOST_PROMPT)
        terminal.send(b"modify\r"); terminal.wait_for(b"OFFLINE_APPROVAL_READY")
        terminal.send(b"yes\r"); terminal.wait_for(HOST_PROMPT, 2)
        check((workspace / "notes.md").read_bytes() == b"after\r\n", "CKPT-AC-029-original-write")
        return terminal, workspace

    def command(terminal, text):
        previous = terminal.output.count(HOST_PROMPT)
        terminal.send(text.encode() + b"\r"); terminal.wait_for(HOST_PROMPT, previous + 1)

    for answer, applied in ((b"yes\r", True), (b"n\r", False), (b" \r", False), (b"\x03", False), (b"\x04", False)):
        terminal, workspace = start()
        try:
            command(terminal, "/checkpoint list"); command(terminal, "/checkpoint show c1")
            terminal.send(b"/checkpoint restore c1\r"); terminal.wait_for(approval)
            check("復原此單一檔案變更？[y/N] ".encode() in terminal.output, "CKPT-AC-029-exact-restore-prompt")
            terminal.send(answer)
            if answer != b"\x04":
                terminal.wait_for(HOST_PROMPT, 5); terminal.send(b"/exit\r")
            terminal.finish(0)
            check((workspace / "notes.md").read_bytes() == (b"before\r\n" if applied else b"after\r\n"), "CKPT-AC-029-restore-publication")
            check(("[單檔復原結果] Verified".encode() in terminal.output) == applied, "CKPT-AC-029-known-result")
            print("CKPT-AC-029-restore-choice-" + str(answer[0]) + ": PASS", flush=True)
        finally:
            terminal.close()

    terminal, workspace = start()
    try:
        command(terminal, "/plan"); command(terminal, "/checkpoint restore c1")
        check(b"mode_restricted" in terminal.output and approval not in terminal.output, "CKPT-AC-029-plan-gate")
        terminal.send(b"/checkpoint forget c1\r"); terminal.wait_for(approval)
        check("忘記此單一檢查點？[y/N] ".encode() in terminal.output, "CKPT-AC-029-exact-forget-prompt")
        terminal.send(b"yes\r"); terminal.wait_for(HOST_PROMPT, 5)
        command(terminal, "/checkpoint show c1")
        check(b"checkpoint_not_found" in terminal.output, "CKPT-AC-029-forget-single-record")
        check((workspace / "notes.md").read_bytes() == b"after\r\n", "CKPT-AC-029-forget-no-file-write")
        terminal.send(b"/exit\r"); terminal.finish(0)
        print("CKPT-AC-029-plan-forget: PASS", flush=True)
    finally:
        terminal.close()

    terminal, workspace = start()
    try:
        command(terminal, "/checkpoint list")
        offset = len(terminal.output); prompts = terminal.output.count(HOST_PROMPT)
        terminal.send(b"\x10\x1bOQ"); terminal.wait_for(HOST_PROMPT, prompts + 1)
        draft = terminal.output[offset:]
        check(b"1: modify" in draft and b"/checkpoint list" not in draft, "CKPT-AC-030-command-excluded-from-history")
        terminal.send(b"\x15/clear\r"); terminal.wait_for(HOST_PROMPT, prompts + 2)
        command(terminal, "/checkpoint show c1")
        check(b"checkpoint_not_found" in terminal.output, "CKPT-AC-030-clear-discards-record")
        check((workspace / "notes.md").read_bytes() == b"after\r\n", "CKPT-AC-030-clear-preserves-file")
        terminal.send(b"/exit\r"); terminal.finish(0)
        print("CKPT-AC-030-history-clear: PASS", flush=True)
    finally:
        terminal.close()

    for control in (b"\x03", b"\x04"):
        read_fd, write_fd = os.pipe(); terminal, workspace = start("checkpoint-busy", read_fd); os.close(read_fd)
        try:
            terminal.send(b"/checkpoint restore c1\r"); terminal.wait_for(b"OFFLINE_CHECKPOINT_WAITING")
            terminal.send(b"/checkpoint forget c1\ryes\r" + control); terminal.wait_for(b"OFFLINE_CHECKPOINT_CANCEL_OBSERVED")
            os.write(write_fd, b"x")
            if control == b"\x03":
                terminal.wait_for(HOST_PROMPT, 3); command(terminal, "/checkpoint show c1"); terminal.send(b"/exit\r")
            terminal.finish(0)
            check(approval not in terminal.output, "CKPT-AC-030-busy-input-no-approval")
            check((workspace / "notes.md").read_bytes() == b"after\r\n", "CKPT-AC-030-busy-no-publication")
            print("CKPT-AC-030-busy-" + str(control[0]) + ": PASS", flush=True)
        finally:
            os.close(write_fd); terminal.close()

    read_fd, write_fd = os.pipe(); terminal, workspace = start("checkpoint-preview-gated", read_fd); os.close(read_fd)
    try:
        terminal.send(b"/checkpoint restore c1\r"); terminal.wait_for(b"OFFLINE_CHECKPOINT_PREVIEW_PAUSED")
        terminal.send(b"yes\r"); os.write(write_fd, b"x"); terminal.wait_for(approval)
        check((workspace / "notes.md").read_bytes() == b"after\r\n", "CKPT-AC-030-preinput-no-approval")
        # Function keys are data in this hidden single-line reader and cannot send a command.
        terminal.send(b"\x1bOQ\x1bOR\x1bOSyes\r")
        terminal.wait_for(approval, 2)
        terminal.send(b"n\r"); terminal.wait_for(HOST_PROMPT, 3)
        check((workspace / "notes.md").read_bytes() == b"after\r\n", "CKPT-AC-030-function-keys-no-approval")
        command(terminal, "/checkpoint show c01")
        check(b"invalid_checkpoint_id" in terminal.output, "CKPT-AC-030-malformed-id")
        terminal.send(b"/exit\r"); terminal.finish(0)
        print("CKPT-AC-030-preinput-function-keys: PASS", flush=True)
    finally:
        os.close(write_fd); terminal.close()

if __name__ == "__main__":
    try:
        main()
    except Exception as error:
        # Never dump captured terminal output, key material or raw process exceptions.
        print("PTY checks: FAIL (" + (str(error) if isinstance(error, CaseFailure) else type(error).__name__) + ")", flush=True)
        sys.exit(1)

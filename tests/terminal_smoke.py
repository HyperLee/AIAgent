#!/usr/bin/env python3
"""macOS PTY smoke checks. Only synthetic keys; never print/capture raw terminal data to disk.

This is automated PTY evidence, not the AC-014/015 human terminal acceptance.
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
READY = "啟動檢查完成；聊天功能尚未接入".encode()


class CaseFailure(Exception):
    pass


def check(condition, case):
    if not condition:
        raise CaseFailure(case)


class TerminalProcess:
    def __init__(self, command, workspace):
        self.master, self.slave = os.openpty()
        self.original = termios.tcgetattr(self.slave)
        self.output = bytearray()

        self.process = subprocess.Popen(
            command, cwd=workspace, stdin=self.slave, stdout=self.slave,
            stderr=self.slave,
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
            check(time.monotonic() < deadline and self.process.poll() is None, "PTY-prompt")

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
        terminal.finish(expected_exit)
        check((READY in terminal.output) == ready, case)
        check(os.listdir(workspace) == [], "PTY-no-workspace-writes")
        print(case + ": PASS", flush=True)
    finally:
        terminal.close()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--app", type=Path, default=Path(__file__).resolve().parents[1] / "src/AIAgent.Cli/bin/Debug/net10.0/AIAgent.Cli.dll")
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
    print("PTY-redirection-help-arguments: PASS", flush=True)


if __name__ == "__main__":
    try:
        main()
    except Exception as error:
        # Never dump captured terminal output, key material or raw process exceptions.
        print("PTY checks: FAIL (" + (str(error) if isinstance(error, CaseFailure) else type(error).__name__) + ")", flush=True)
        sys.exit(1)

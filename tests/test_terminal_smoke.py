"""PTYC regression checks; synthetic markers only, no terminal transcripts in diagnostics."""
import contextlib
import io
from pathlib import Path
import signal
import subprocess
import sys
import tempfile
import unittest
from unittest import mock

import terminal_smoke as smoke


class TerminalProcessTests(unittest.TestCase):
    def start(self, source):
        terminal = smoke.TerminalProcess([sys.executable, "-c", source],
                                         tempfile.mkdtemp(prefix="aiagent-ptyc-test-"))
        self.addCleanup(terminal.close)
        return terminal

    def test_missing_marker_times_out_while_child_is_alive(self):
        terminal = self.start("print('PTY_READY', flush=True); input()")
        terminal.wait_for(b"PTY_READY")
        # Drive the deadline, not the scheduling of the child process.
        with mock.patch.object(smoke.time, "monotonic", side_effect=(0, 9)):
            with self.assertRaises(smoke.CaseFailure) as caught:
                terminal.wait_for(b"PTY_MISSING")
        self.assertIsNone(terminal.process.poll())
        self.assertNotIn("PTY_READY", str(caught.exception))

    def test_exit_without_marker_fails(self):
        terminal = self.start("print('PTY_OTHER', flush=True)")
        terminal.process.wait(timeout=8)
        with self.assertRaises(smoke.CaseFailure) as caught:
            terminal.wait_for(b"PTY_MISSING")
        self.assertNotIn("PTY_OTHER", str(caught.exception))

    def test_fast_exit_preserves_buffered_marker(self):
        terminal = self.start("print('PTY_COMPLETE', flush=True)")
        terminal.process.wait(timeout=8)
        terminal.wait_for(b"PTY_COMPLETE")
        terminal.finish(0)

    def test_marker_does_not_hide_wrong_exit(self):
        terminal = self.start("print('PTY_COMPLETE', flush=True); raise SystemExit(7)")
        terminal.process.wait(timeout=8)
        terminal.wait_for(b"PTY_COMPLETE")
        with self.assertRaisesRegex(smoke.CaseFailure, "^PTY-exit-code$"):
            terminal.finish(0)


class CancellationEntryTests(unittest.TestCase):
    def invoke(self, *args):
        return subprocess.run([sys.executable, "-B", str(Path(smoke.__file__)), *args],
                              stdin=subprocess.DEVNULL, capture_output=True, timeout=8)

    def test_repeat_requires_cancellation_group_and_positive_integer(self):
        for args in (("--repeat", "1"), ("--group", "all", "--repeat", "1"),
                     ("--group", "model", "--repeat", "2"),
                     ("--group", "agents", "--repeat", "2"),
                     ("--group", "cancellation", "--repeat", "0"),
                     ("--group", "cancellation", "--repeat", "-1"),
                     ("--group", "cancellation", "--repeat", "word"),
                     ("--group", "unknown")):
            with self.subTest(args=args):
                self.assertEqual(2, self.invoke(*args).returncode)

    def test_cancellation_defaults_to_one_round(self):
        with mock.patch.object(sys, "argv", ["terminal_smoke.py", "--group", "cancellation"]), \
                mock.patch.object(smoke.sys, "platform", "darwin"), \
                mock.patch.object(smoke, "cancellation_cases", create=True) as cases:
            smoke.main()
        self.assertEqual(1, cases.call_count)
        self.assertEqual(1, cases.call_args.args[1])

    def test_cancellation_requires_macos_before_starting_host(self):
        with mock.patch.object(sys, "argv", ["terminal_smoke.py", "--group", "cancellation"]), \
                mock.patch.object(smoke.sys, "platform", "linux"), \
                mock.patch.object(smoke, "cancellation_cases", create=True) as cases:
            with self.assertRaisesRegex(smoke.CaseFailure, "^macOS-required$"):
                smoke.main()
        cases.assert_not_called()

    def test_round_order_and_first_failure_stop(self):
        calls = []
        def fail(host, control):
            calls.append(control)
            if len(calls) == 5:
                raise smoke.CaseFailure("PTY-prompt-timeout")
        output = io.StringIO()
        with mock.patch.object(smoke, "cancellation_case", side_effect=fail, create=True), \
                contextlib.redirect_stdout(output):
            with self.assertRaises(smoke.CaseFailure):
                smoke.cancellation_cases(["offline-host"], 3)
        self.assertEqual(["CtrlC", "SIGINT", "EOF", "CtrlC", "SIGINT"], calls)
        self.assertIn("round=2/3 case=SIGINT: FAIL", output.getvalue())
        self.assertIn("not-run=4", output.getvalue())
        self.assertEqual(4, output.getvalue().count(": PASS"))

    def test_each_case_starts_and_closes_a_new_host(self):
        terminals = []
        def start(host, scenario):
            terminal = mock.Mock()
            eof = len(terminals) % 3 == 2
            terminal.output = bytearray(b"OFFLINE_BUSY_MODEL_STARTED\r\n" +
                                        smoke.HOST_PROMPT * (1 if eof else 3) +
                                        "處理中".encode() * (1 if eof else 2) +
                                        (b"" if eof else "離線回答".encode()))
            terminals.append(terminal)
            return terminal
        with mock.patch.object(smoke, "start_chat_host", side_effect=start, create=True) as started, \
                mock.patch.object(smoke.os, "kill"), contextlib.redirect_stdout(io.StringIO()):
            smoke.cancellation_cases(["offline-host"], 2)
        self.assertEqual(6, len(terminals))
        self.assertEqual(6, started.call_count)
        for terminal in terminals:
            terminal.close.assert_called_once()
            terminal.finish.assert_called_once()

    def test_no_busy_input_or_control_before_model_marker(self):
        terminal = mock.Mock()
        terminal.wait_for.side_effect = [None, smoke.CaseFailure("PTY-prompt-timeout")]
        with mock.patch.object(smoke, "start_chat_host", return_value=terminal, create=True), \
                mock.patch.object(smoke.os, "kill") as kill:
            with self.assertRaises(smoke.CaseFailure):
                smoke.cancellation_case(["offline-host"], "SIGINT")
        self.assertEqual([mock.call(b"question\r")], terminal.send.call_args_list)
        kill.assert_not_called()
        terminal.close.assert_called_once()

    def test_control_paths_keep_typed_and_external_interrupts_distinct(self):
        for control in ("CtrlC", "SIGINT", "EOF"):
            with self.subTest(control=control):
                terminal = mock.Mock()
                eof = control == "EOF"
                terminal.output = bytearray(b"OFFLINE_BUSY_MODEL_STARTED\n" +
                                            smoke.HOST_PROMPT * (1 if eof else 3) +
                                            "處理中".encode() * (1 if eof else 2) +
                                            (b"" if eof else "離線回答".encode()))
                with mock.patch.object(smoke, "start_chat_host", return_value=terminal, create=True), \
                        mock.patch.object(smoke.os, "kill") as kill:
                    smoke.cancellation_case(["offline-host"], control)
                waits = terminal.wait_for.call_args_list
                self.assertEqual(mock.call("處理中".encode()), waits[0])
                self.assertEqual(mock.call(b"OFFLINE_BUSY_MODEL_STARTED"), waits[1])
                terminal.finish.assert_called_once_with(0 if eof else 130)
                if control == "SIGINT":
                    self.assertEqual([mock.call(terminal.process.pid, signal.SIGINT)] * 2,
                                     kill.call_args_list)
                    self.assertFalse(any(b"\x03" in call.args[0] for call in terminal.send.call_args_list))
                else:
                    kill.assert_not_called()
                    self.assertIn(mock.call(b"\x04" if eof else b"\x03\x03"), terminal.send.call_args_list)

    def test_duplicate_marker_extra_turn_or_busy_echo_is_rejected(self):
        base = (b"OFFLINE_BUSY_MODEL_STARTED\n" + smoke.HOST_PROMPT * 3 +
                "處理中".encode() * 2 + "離線回答".encode())
        for extra in (b"OFFLINE_BUSY_MODEL_STARTED", smoke.HOST_PROMPT,
                      "離線回答".encode(), b"DISCARDED_BUSY_TEXT"):
            with self.subTest(extra=extra):
                terminal = mock.Mock()
                terminal.output = bytearray(base + extra)
                with mock.patch.object(smoke, "start_chat_host", return_value=terminal):
                    with self.assertRaises(smoke.CaseFailure):
                        smoke.cancellation_case(["offline-host"], "CtrlC")
                terminal.close.assert_called_once()

    def test_failure_summary_does_not_print_exception_body(self):
        output = io.StringIO()
        with mock.patch.object(smoke, "cancellation_case", side_effect=RuntimeError("PTY_PRIVATE_BODY")), \
                contextlib.redirect_stdout(output):
            with self.assertRaises(RuntimeError):
                smoke.cancellation_cases(["offline-host"], 30)
        self.assertNotIn("PTY_PRIVATE_BODY", output.getvalue())
        self.assertIn("round=1/30 case=CtrlC: FAIL category=RuntimeError not-run=89", output.getvalue())

    def test_bootstrap_failure_still_closes_host(self):
        terminal = mock.Mock()
        terminal.wait_for.side_effect = smoke.CaseFailure("PTY-prompt-timeout")
        with mock.patch.object(smoke, "TerminalProcess", return_value=terminal):
            with self.assertRaises(smoke.CaseFailure):
                smoke.start_chat_host(["offline-host"], "busy")
        terminal.close.assert_called_once()


if __name__ == "__main__":
    unittest.main()

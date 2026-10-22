"""Offline BUD acceptance. Synthetic fixtures are retained; no recursive cleanup."""
import contextlib
import importlib
import io
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest
from unittest import mock

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[1]
SCRIPT = ROOT / "scripts/check_sensitive_content.py"
HOOK = ROOT / ".githooks/pre-commit"


def synthetic_key():
    return b"AI" + b"za" + b"X" * 35


def private_marker(kind=b""):
    return b"-" * 5 + b"BEGIN " + kind + b"PRIVATE " + b"KEY" + b"-" * 5


def new_directory():
    return Path(tempfile.mkdtemp(prefix="aiagent-bud-", dir="/private/tmp"))


def git_environment():
    env = {k: v for k, v in os.environ.items()
           if not k.startswith(("GIT_", "SSH_"))}
    env.update(GIT_CONFIG_NOSYSTEM="1", GIT_CONFIG_GLOBAL=os.devnull,
               GIT_AUTHOR_NAME="Offline Test", GIT_AUTHOR_EMAIL="test@example.invalid",
               GIT_COMMITTER_NAME="Offline Test", GIT_COMMITTER_EMAIL="test@example.invalid",
               GIT_TERMINAL_PROMPT="0", PYTHONDONTWRITEBYTECODE="1")
    return env


class ScannerTests(unittest.TestCase):
    def git(self, root, *args, env=None, data=None, ok=True):
        result = subprocess.run(["git", "-c", "commit.gpgsign=false", *args],
                                cwd=root, env=env or git_environment(), input=data,
                                stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        if ok:
            self.assertTrue(result.returncode == 0, "BUD-git-fixture")
        return result

    def repository(self):
        root = new_directory()
        self.git(root, "init", "-q")
        self.git(root, "config", "core.hooksPath", str(root / "unused-hooks"))
        return root

    def scan(self, root, *args, env=None):
        return subprocess.run([sys.executable, "-B", str(SCRIPT), *args], cwd=root,
                              env=env or git_environment(), stdout=subprocess.PIPE,
                              stderr=subprocess.PIPE)

    def outcome(self, result, code, status, rule=None):
        self.assertTrue(result.returncode == code, "BUD-exit")
        self.assertTrue(result.stderr == b"", "BUD-no-stderr")
        self.assertTrue(status.encode() in result.stdout, "BUD-status")
        if rule:
            self.assertTrue(rule.encode() in result.stdout, "BUD-rule")
        self.assertTrue(synthetic_key() not in result.stdout, "BUD-no-content")
        for line in result.stdout.splitlines():
            self.assertTrue(all(c < 128 for c in line), "BUD-ascii")
            self.assertTrue(line.startswith((b"[scan] ", b"[error] ", b"[finding] ")),
                            "BUD-controlled-output")

    def publish_tree(self):
        root = new_directory()
        for name in ("AIAgent.Cli.dll", "AIAgent.Cli.deps.json",
                     "AIAgent.Cli.runtimeconfig.json", "appsettings.json"):
            (root / name).write_bytes(b"ordinary bytes")
        return root

    def engine(self):
        self.assertTrue(SCRIPT.exists(), "BUD-scanner-required")
        sys.path.insert(0, str(ROOT / "scripts"))
        return importlib.import_module("_sensitive_scan")

    def test_BUDAC021_help_and_strict_arguments_do_not_read_sources(self):
        root = new_directory()
        help_result = self.scan(root, "--help")
        self.assertTrue(help_result.returncode == 0 and help_result.stderr == b"", "BUD-help")
        for args in ((), ("unknown",), ("staged", "--help"), ("staged", "extra"),
                     ("artifacts",), ("artifacts", "--path"),
                     ("artifacts", "--path", "a", "--path", "b"), ("--help", "--help")):
            self.outcome(self.scan(root, *args), 2, "invalid_arguments")
        self.outcome(self.scan(root, "staged"), 2, "incomplete")

    def test_BUDAC022_empty_index_and_all_tracked_files(self):
        root = self.repository()
        self.outcome(self.scan(root, "staged"), 0, "scanned_files=0")
        (root / "a").write_bytes(b"safe")
        self.git(root, "add", "--", "a")
        self.git(root, "commit", "-qm", "fixture")
        (root / "a").write_bytes(synthetic_key())
        self.git(root, "add", "--", "a")
        self.git(root, "commit", "-qm", "synthetic fixture")
        self.outcome(self.scan(root, "staged"), 1, "blocked", "BUD-S002")

    def test_BUDAC023_index_blob_not_worktree_in_both_directions(self):
        root = self.repository()
        path = root / "a"
        path.write_bytes(synthetic_key())
        self.git(root, "add", "--", "a")
        path.write_bytes(b"safe")
        self.outcome(self.scan(root, "staged"), 1, "blocked", "BUD-S002")
        self.git(root, "add", "--", "a")
        path.write_bytes(synthetic_key())
        index_before = (root / ".git/index").read_bytes()
        self.outcome(self.scan(root, "staged"), 0, "passed")
        self.assertTrue((root / ".git/index").read_bytes() == index_before, "BUD-scan-index-readonly")

    def test_BUDAC024_delete_rename_same_oid_and_executable(self):
        root = self.repository()
        (root / "a").write_bytes(b"safe")
        (root / "b").write_bytes(b"safe")
        self.git(root, "add", ".")
        self.git(root, "update-index", "--chmod=+x", "a")
        self.outcome(self.scan(root, "staged"), 0, "scanned_files=2")
        self.git(root, "mv", "b", "api-key.txt")
        self.outcome(self.scan(root, "staged"), 1, "blocked", "BUD-S001")
        self.git(root, "update-index", "--force-remove", "api-key.txt")
        (root / "api-key.txt").write_bytes(synthetic_key())
        self.outcome(self.scan(root, "staged"), 0, "scanned_files=1")

    def test_BUDAC025_raw_names_are_data_and_never_diagnostics(self):
        root = self.repository()
        names = [b"space name", b"tab\tname", b"line\nname", b"-dash", b":colon",
                 b"invalid-\xff", b"$(touch INJECTED)", b"`touch INJECTED2`"]
        # APFS rejects non-UTF-8 worktree names; Git index still accepts raw bytes.
        oid = self.git(root, "hash-object", "-w", "--stdin", data=synthetic_key()).stdout.strip()
        self.git(root, "update-index", "-z", "--index-info",
                 data=b"".join(b"100644 " + oid + b"\t" + name + b"\0" for name in names))
        result = self.scan(root, "staged")
        self.outcome(result, 1, "scanned_files=8", "BUD-S002")
        self.assertTrue(not (root / "INJECTED").exists() and not (root / "INJECTED2").exists(),
                        "BUD-no-shell")
        self.assertTrue(all(name not in result.stdout for name in names), "BUD-no-names")

    def test_BUDAC026_symlink_unmerged_and_gitlink_are_incomplete(self):
        root = self.repository()
        (root / "outside").write_bytes(b"safe")
        (root / "link").symlink_to("outside")
        self.git(root, "add", "--", "link")
        self.outcome(self.scan(root, "staged"), 2, "incomplete")
        self.git(root, "update-index", "--force-remove", "link")
        oid = self.git(root, "hash-object", "-w", "--stdin", data=b"safe").stdout.strip()
        self.git(root, "update-index", "--index-info", data=b"100644 " + oid + b" 1\tconflict\n")
        self.outcome(self.scan(root, "staged"), 2, "index_invalid")
        self.git(root, "update-index", "--force-remove", "conflict")
        self.git(root, "update-index", "--add", "--cacheinfo", "160000", oid.decode(), "module")
        self.outcome(self.scan(root, "staged"), 2, "unsupported_entry")

    def test_BUDAC027_nested_cwd_and_relative_alternate_index(self):
        root = self.repository()
        (root / "nested").mkdir()
        (root / "a").write_bytes(synthetic_key())
        env = git_environment()
        env["GIT_INDEX_FILE"] = "alternate-index"
        self.git(root, "add", "a", env=env)
        # Git interprets this environment path relative to its worktree root;
        # rev-parse --git-path then expresses it relative to the original cwd.
        self.outcome(self.scan(root / "nested", "staged", env=env), 1, "blocked", "BUD-S002")
        absolute_env = dict(env, GIT_INDEX_FILE=str(root / "alternate-index"))
        self.outcome(self.scan(root / "nested", "staged", env=absolute_env), 1, "blocked", "BUD-S002")
        self.outcome(self.scan(root, "staged"), 0, "scanned_files=0")

    def test_BUDAC028_force_added_ignored_sensitive_paths(self):
        root = self.repository()
        (root / ".gitignore").write_text("*.key\n.env*\n")
        (root / "nested").mkdir()
        (root / "nested/a.key").write_bytes(b"")
        self.git(root, "add", "-f", "--", ".gitignore", "nested/a.key")
        self.outcome(self.scan(root, "staged"), 1, "blocked", "BUD-S001")

    def test_BUDAC029_binary_encodings_offsets_and_cross_chunk(self):
        root = self.repository()
        payloads = [synthetic_key()] + [private_marker(kind) for kind in
                    (b"", b"RSA ", b"DSA ", b"EC ", b"ENCRYPTED ", b"OPENSSH ")]
        for index, payload in enumerate(payloads):
            for encoding in ("ascii", "utf-16le", "utf-16be"):
                encoded = payload.decode("ascii").encode(encoding)
                for offset in (0, 1):
                    name = "f-%d-%s-%d" % (index, encoding, offset)
                    (root / name).write_bytes(b"\xff" + b"x" * (65530 + offset) + encoded + b"\x00")
        self.git(root, "add", ".")
        result = self.scan(root, "staged")
        self.outcome(result, 1, "scanned_files=42", "BUD-S003")
        self.assertTrue(result.stdout.count(b"[finding]") == 42, "BUD-one-rule-per-item")

    def test_BUDAC030_env_example_grammar_and_case(self):
        root = self.repository()
        accepted = [b"", b"\xef\xbb\xbf# comment\r\nA=YOUR_API_KEY\nB = ''\rC=\"[REDACTED]\"",
                    b"A=YOUR_GEMINI_API_KEY\nB=YOUR_ACCESS_TOKEN\nC=YOUR_CLIENT_SECRET\nD=EXAMPLE_ONLY_NOT_A_SECRET"]
        rejected = [b"A=123", b"A=your_api_key", b"export A=", b"A=\nA=", b"A=\xff", b"#\x00",
                    b"A=YOUR_API_KEY # comment", b"A='x\ny'", b"#\x1b", "#\u2028".encode()]
        path = root / ".env.example"
        for payload in accepted + rejected:
            path.write_bytes(payload)
            self.git(root, "add", "-f", "--", ".env.example")
            self.outcome(self.scan(root, "staged"), 0 if payload in accepted else 1,
                         "passed" if payload in accepted else "blocked")
        self.git(root, "mv", ".env.example", ".ENV.EXAMPLE")
        self.outcome(self.scan(root, "staged"), 1, "blocked", "BUD-S001")

    def test_BUDAC031_temp_names_block_without_deleting(self):
        root = self.repository()
        name = ".aiagent-edit-" + "a" * 32 + ".tmp"
        (root / name.upper()).write_bytes(b"")
        (root / (name + "x")).write_bytes(b"safe")
        self.git(root, "add", ".")
        self.outcome(self.scan(root, "staged"), 1, "findings=1", "BUD-S004")
        self.assertTrue(len(list(root.iterdir())) == 3, "BUD-readonly")

    def test_BUDAC034_minimal_publish_and_all_binary_files(self):
        root = self.publish_tree()
        (root / ".hidden.pdb").write_bytes(b"\x00\xffordinary")
        self.outcome(self.scan(root, "artifacts", "--path", str(root)), 0, "scanned_files=5")
        self.outcome(self.scan(root, "artifacts", "--path", str(new_directory())), 2, "artifact_invalid")
        (root / ".hidden.pdb").write_bytes(b"\x00" + synthetic_key().decode().encode("utf-16le"))
        self.outcome(self.scan(root, "artifacts", "--path", str(root)), 1, "blocked", "BUD-S002")

    def test_BUDAC035_sensitive_file_directory_links_and_archives(self):
        for kind in ("file", "directory", "link", "archive", "fifo"):
            root = self.publish_tree()
            if kind == "file":
                (root / "secret.key").write_bytes(b"ordinary")
            elif kind == "directory":
                (root / "logs").mkdir()
                (root / "logs/hidden").write_bytes(synthetic_key())
            elif kind == "link":
                (root / "link").symlink_to(root / "AIAgent.Cli.dll")
            elif kind == "fifo":
                os.mkfifo(root / "pipe")
            else:
                (root / "data.zip").write_bytes(b"ordinary")
            result = self.scan(root, "artifacts", "--path", str(root))
            self.outcome(result, 1 if kind == "file" else 2, "blocked" if kind == "file" else "incomplete")
            if kind == "directory":
                self.assertTrue(b"BUD-S001" in result.stdout and b"BUD-S002" not in result.stdout,
                                "BUD-no-sensitive-directory-read")

    def test_BUDAC036_root_and_ancestor_symlinks_rejected(self):
        root = self.publish_tree()
        parent = new_directory()
        (parent / "link").symlink_to(root, target_is_directory=True)
        self.outcome(self.scan(parent, "artifacts", "--path", str(parent / "link")), 2, "incomplete")
        (root / "nested").mkdir()
        self.outcome(self.scan(parent, "artifacts", "--path", str(parent / "link/nested")), 2, "incomplete")

    def test_BUDAC037_artifact_changes_invalidate_old_result(self):
        root = self.publish_tree()
        self.outcome(self.scan(root, "artifacts", "--path", str(root)), 0, "passed")
        (root / "appsettings.json").write_bytes(synthetic_key())
        self.outcome(self.scan(root, "artifacts", "--path", str(root)), 1, "blocked")

    def test_BUDAC027_linked_worktree_trace_and_replace_refs(self):
        root = self.repository()
        (root / "a").write_bytes(synthetic_key())
        self.git(root, "add", "a")
        self.git(root, "commit", "-qm", "fixture")
        source = self.git(root, "rev-parse", ":a").stdout.strip()
        safe = self.git(root, "hash-object", "-w", "--stdin", data=b"safe").stdout.strip()
        self.git(root, "replace", source.decode(), safe.decode())
        env = git_environment()
        env.update(GIT_TRACE=str(root / "trace-output"), GIT_TRACE2_EVENT=str(root / "trace-event"))
        self.outcome(self.scan(root, "staged", env=env), 1, "blocked", "BUD-S002")
        self.assertTrue(not (root / "trace-output").exists() and not (root / "trace-event").exists(), "BUD-trace-disabled")
        worktree = new_directory() / "linked"
        self.git(root, "worktree", "add", "-q", "--detach", str(worktree))
        (worktree / "nested").mkdir()
        self.outcome(self.scan(worktree / "nested", "staged"), 1, "blocked", "BUD-S002")

    def test_BUDAC027_configured_trace_targets_do_not_create_logs(self):
        root = self.repository()
        target = root / "trace-output"
        configuration = root / "global-config"
        self.git(root, "config", "--file", str(configuration), "trace2.eventTarget", str(target))
        env = dict(git_environment(), GIT_CONFIG_GLOBAL=str(configuration))
        self.outcome(self.scan(root, "staged", env=env), 0, "passed")
        self.assertFalse(target.exists(), "BUD-configured-trace-disabled")

    def test_BUDAC026_missing_blob_and_invalid_index_metadata(self):
        root = self.repository()
        oid = self.git(root, "hash-object", "-w", "--stdin", data=b"safe").stdout.strip().decode()
        self.git(root, "update-index", "--add", "--cacheinfo", "100644", oid, "missing")
        (root / ".git/objects" / oid[:2] / oid[2:]).unlink()  # One explicitly owned fixture file.
        self.outcome(self.scan(root, "staged"), 2, "incomplete")
        self.engine()
        from _sensitive_scan.git_index import parse_record
        from _sensitive_scan.common import ScanError
        for data in (b"", b"broken", b"100644 " + b"a" * 40 + b" 0\t", b"100644 " + b"a" * 40 + b" 4\ta"):
            with self.assertRaises(ScanError, msg="BUD-malformed-record"):
                parse_record(data)

    def test_BUDAC027_index_change_during_scan_never_retries(self):
        self.engine()
        from _sensitive_scan import git_index
        from _sensitive_scan.common import Report, ScanError
        root = self.repository()
        (root / "a").write_bytes(b"safe")
        self.git(root, "add", "a")
        original = git_index.index_entries
        calls = []

        def changed(*args):
            calls.append(1)
            if len(calls) == 2:
                self.git(root, "update-index", "--chmod=+x", "a")
            return original(*args)

        with mock.patch.object(git_index.os, "getcwd", return_value=str(root)), \
                mock.patch.object(git_index, "index_entries", side_effect=changed):
            with self.assertRaises(ScanError) as caught:
                git_index.scan_staged(Report("staged"))
        self.assertTrue(caught.exception.code == "scan_changed" and len(calls) == 2, "BUD-no-rescan")

    def test_BUDAC032_file_total_path_and_depth_boundaries(self):
        self.engine()
        from _sensitive_scan.common import Limits, Report, ScanError, validate_path
        for size in (67108863, 67108864, 67108865):
            report = Report("staged")
            if size <= 67108864:
                report.accept_size(size)
            else:
                with self.assertRaises(ScanError, msg="BUD-file-limit"):
                    report.accept_size(size)
        for total in (1073741823, 1073741824, 1073741825):
            report = Report("staged", total_bytes=total - 1)
            if total <= 1073741824:
                report.accept_size(1)
            else:
                with self.assertRaises(ScanError, msg="BUD-total-limit"):
                    report.accept_size(1)
        for length in (4095, 4096, 4097):
            if length <= 4096:
                validate_path(b"a" * length)
            else:
                with self.assertRaises(ScanError, msg="BUD-path-limit"):
                    validate_path(b"a" * length)
        for depth in (63, 64, 65):
            if depth <= 64:
                validate_path(b"/".join([b"a"] * depth))
            else:
                with self.assertRaises(ScanError, msg="BUD-depth-limit"):
                    validate_path(b"/".join([b"a"] * depth))
        # Same-content paths must count separately, not by OID or inode.
        root = self.repository()
        for name in ("a", "b", "c", "d"):
            (root / name).write_bytes(b"1234")
            self.git(root, "add", name)
        from _sensitive_scan.git_index import scan_staged
        with mock.patch("_sensitive_scan.git_index.os.getcwd", return_value=str(root)):
            with mock.patch.object(Limits, "MaxScanEntries", 3):
                with self.assertRaises(ScanError, msg="BUD-entry-limit"):
                    scan_staged(Report("staged"))
            with mock.patch.object(Limits, "MaxScanTotalBytes", 15):
                with self.assertRaises(ScanError, msg="BUD-per-path-accounting"):
                    scan_staged(Report("staged"))

    def test_BUDAC032_example_file_line_and_assignment_boundaries(self):
        self.engine()
        from _sensitive_scan.rules import ExampleParser
        from _sensitive_scan.common import ScanError
        for length in (4095, 4096, 4097):
            parser = ExampleParser()
            if length <= 4096:
                parser.update(b"#" + b"x" * (length - 3) + b"\r\n")
                self.assertFalse(parser.finish(), "BUD-line-boundary")
            else:
                with self.assertRaises(ScanError, msg="BUD-line-limit"):
                    parser.update(b"#" + b"x" * (length - 3) + b"\r\n")
        for length in (131071, 131072, 131073):
            parser = ExampleParser()
            data = (b"#\n" * 65537)[:length]
            if length <= 131072:
                for start in range(0, length, 65536):
                    parser.update(data[start:start + 65536])
                self.assertFalse(parser.finish(), "BUD-example-size")
            else:
                with self.assertRaises(ScanError, msg="BUD-example-limit"):
                    parser.update(data)
        for count in (199, 200, 201):
            parser = ExampleParser()
            data = b"".join(("A%d=\n" % i).encode() for i in range(count))
            if count <= 200:
                parser.update(data)
                self.assertFalse(parser.finish(), "BUD-assignment-boundary")
            else:
                with self.assertRaises(ScanError, msg="BUD-assignment-limit"):
                    parser.update(data)

    def test_BUDAC032_invalid_utf8_assignments_still_count_toward_limit(self):
        self.engine()
        from _sensitive_scan.rules import ExampleParser
        from _sensitive_scan.common import ScanError
        with self.assertRaises(ScanError, msg="BUD-invalid-assignment-count"):
            ExampleParser().update(b"A=\xff\n" * 201)

    def test_BUDAC033_errors_cancellation_and_output_failure_are_controlled(self):
        self.engine()
        from _sensitive_scan import cli
        for failure, code in ((KeyboardInterrupt(), "scan_cancelled"), (OSError("unsafe"), "scan_io"),
                              (RuntimeError("unsafe"), "internal_failure")):
            output = io.StringIO()
            with mock.patch.object(cli, "scan_staged", side_effect=failure), contextlib.redirect_stdout(output):
                result = cli.main(["staged"])
            self.assertTrue(result == 2 and ("code=" + code) in output.getvalue() and
                            "unsafe" not in output.getvalue(), "BUD-safe-error")
        for failure in (BrokenPipeError(), KeyboardInterrupt(), RuntimeError("unsafe")):
            output = mock.Mock()
            output.write.side_effect = failure
            output.fileno.side_effect = OSError()
            with mock.patch.object(cli, "scan_staged"), mock.patch.object(sys, "stdout", output):
                try:
                    result = cli.main(["staged"])
                except BaseException:
                    self.fail("BUD-output-failure-escaped")
                self.assertTrue(result == 2, "BUD-output-failure")

    def test_BUDAC033_real_broken_pipe_has_no_interpreter_traceback(self):
        root = self.repository()
        process = subprocess.Popen([sys.executable, "-B", str(SCRIPT), "staged"], cwd=root,
                                   env=git_environment(), stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        process.stdout.close()
        errors = process.stderr.read()
        result = process.wait(timeout=5)
        process.stderr.close()
        self.assertTrue(result != 0 and errors == b"", "BUD-broken-pipe")

    def test_BUDAC033_short_write_and_unavailable_fileno_are_nonzero(self):
        self.engine()
        from _sensitive_scan import cli
        for short_write in (True, False):
            output = mock.Mock()
            if short_write:
                output.write.return_value = 0
            else:
                output.write.side_effect = OSError()
            output.fileno.side_effect = RuntimeError("unsafe")
            with mock.patch.object(cli, "scan_staged"), mock.patch.object(sys, "stdout", output):
                try:
                    result = cli.main(["staged"])
                except BaseException:
                    self.fail("BUD-output-failure-escaped")
                self.assertTrue(result == 2, "BUD-short-write")

    def test_BUDAC032_index_entry_limit_with_bounded_synthetic_pipe(self):
        self.engine()
        from _sensitive_scan import git_index
        from _sensitive_scan.common import Limits, ScanError

        class Process:
            def __enter__(self):
                return self

            def __exit__(self, *args):
                pass

            def chunks(self):
                for i in range(count):
                    yield b"100644 " + b"a" * 40 + b" 0\t" + str(i).encode() + b"\0"

        for count in (9999, 10000, 10001):
            with mock.patch.object(git_index, "GitProcess", return_value=Process()):
                if count <= 10000:
                    self.assertTrue(len(git_index.index_entries("unused", {})) == count, "BUD-index-count")
                else:
                    with self.assertRaises(ScanError, msg="BUD-index-count-limit"):
                        git_index.index_entries("unused", {})
        from _sensitive_scan.artifacts import scan_artifacts
        from _sensitive_scan.common import Report
        root = self.publish_tree()
        with mock.patch.object(Limits, "MaxScanEntries", 4):
            report = Report("artifacts")
            scan_artifacts(str(root), report)
            self.assertTrue(report.scanned_files == 4, "BUD-artifact-entry-boundary")
            (root / "extra").write_bytes(b"")
            with self.assertRaises(ScanError, msg="BUD-artifact-entry-limit"):
                scan_artifacts(str(root), Report("artifacts"))

    def test_BUDAC036_mutations_after_read_are_scan_changed(self):
        self.engine()
        from _sensitive_scan import artifacts
        from _sensitive_scan.common import Report, ScanError
        for action in ("grow", "identity", "root", "member"):
            root = self.publish_tree()
            original = artifacts.ContentMatcher.update
            changed = []

            def mutate(matcher, data):
                original(matcher, data)
                if changed:
                    return
                changed.append(True)
                if action == "grow":
                    (root / "AIAgent.Cli.dll").write_bytes(b"longer bytes than before")
                elif action == "identity":
                    replacement = root / "replacement"
                    replacement.write_bytes(b"ordinary bytes")
                    os.replace(replacement, root / "AIAgent.Cli.dll")
                elif action == "member":
                    (root / "new").write_bytes(b"safe")
                else:
                    root.rename(root.with_name(root.name + "-moved"))
                    root.mkdir()

            with mock.patch.object(artifacts.ContentMatcher, "update", mutate):
                with self.assertRaises(ScanError) as caught:
                    artifacts.scan_artifacts(str(root), Report("artifacts"))
            self.assertTrue(caught.exception.code == "scan_changed", "BUD-artifact-mutation")

    def test_BUDAC036_link_swap_at_open_is_scan_changed(self):
        self.engine()
        from _sensitive_scan import artifacts
        from _sensitive_scan.common import Report, ScanError
        root = self.publish_tree()
        original = os.open

        def swap(path, flags, *args, **kwargs):
            if path == b"AIAgent.Cli.dll" and not flags & os.O_DIRECTORY:
                (root / "AIAgent.Cli.dll").unlink()
                (root / "AIAgent.Cli.dll").symlink_to(root / "appsettings.json")
            return original(path, flags, *args, **kwargs)

        with mock.patch.object(artifacts.os, "supports_dir_fd", os.supports_dir_fd | {swap}), \
                mock.patch.object(artifacts.os, "open", swap):
            with self.assertRaises(ScanError) as caught:
                artifacts.scan_artifacts(str(root), Report("artifacts"))
        self.assertTrue(caught.exception.code == "scan_changed", "BUD-link-race")

    def test_BUDAC038_hook_is_opt_in_and_executable(self):
        root = self.repository()
        before = self.git(root, "config", "--get", "core.hooksPath").stdout
        self.scan(root, "staged")
        after = self.git(root, "config", "--get", "core.hooksPath").stdout
        self.assertTrue(before == after, "BUD-no-hook-install")
        self.assertTrue(HOOK.exists() and os.access(HOOK, os.X_OK), "BUD-executable-hook")

    def test_BUDAC039_hook_uses_git_commit_temporary_index(self):
        root = self.hook_repository()
        (root / "a").write_bytes(b"initial")
        self.git(root, "add", "a")
        self.git(root, "commit", "-qm", "fixture")
        (root / "a").write_bytes(synthetic_key())
        self.git(root, "add", "a")
        (root / "a").write_bytes(b"safe partial commit")
        # --only makes Git pass a temporary index to its hook. The real index
        # contains a synthetic finding; only the actual commit snapshot is safe.
        self.git(root, "commit", "--only", "-qm", "partial fixture", "--", "a")
        blob = self.git(root, "show", "HEAD:a").stdout
        self.assertTrue(blob == b"safe partial commit", "BUD-hook-effective-index")

    def test_BUDAC039_hook_preflight_cannot_write_inherited_trace(self):
        root = self.hook_repository()
        target = root / "trace-output"
        env = dict(git_environment(), GIT_TRACE2_EVENT=str(target))
        result = subprocess.run([str(root / ".githooks/pre-commit")], cwd=root, env=env,
                                stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        self.outcome(result, 0, "passed")
        self.assertFalse(target.exists(), "BUD-hook-preflight-readonly")

    def hook_repository(self):
        root = self.repository()
        self.assertTrue(SCRIPT.exists() and HOOK.exists(), "BUD-hook-required")
        shutil.copytree(ROOT / "scripts", root / "scripts")
        (root / ".githooks").mkdir()
        shutil.copy2(HOOK, root / ".githooks/pre-commit")
        self.git(root, "config", "core.hooksPath", ".githooks")
        return root

    def test_BUDAC039_real_commit_passes_or_blocks_and_preserves_index(self):
        root = self.hook_repository()
        (root / "a").write_bytes(b"safe")
        self.git(root, "add", "a")
        self.git(root, "commit", "-qm", "fixture")
        head = self.git(root, "rev-parse", "HEAD").stdout
        for kind in ("finding", "incomplete", "script", "python"):
            (root / "a").write_bytes(synthetic_key() if kind == "finding" else b"safe")
            self.git(root, "add", "a")
            env = git_environment()
            if kind == "incomplete":
                (root / "link").symlink_to("a")
                self.git(root, "add", "link")
            elif kind == "script":
                (root / "scripts/check_sensitive_content.py").rename(root / "scripts/disabled.py")
            elif kind == "python":
                (root / "scripts/disabled.py").rename(root / "scripts/check_sensitive_content.py")
                command_dir = root / "commands"
                command_dir.mkdir()
                (command_dir / "git").symlink_to(shutil.which("git"))
                env["PATH"] = str(command_dir)
            # git commit itself may refresh stat/cache-tree bytes before its hook.
            index_before = self.git(root, "ls-files", "--stage", "-z").stdout
            result = self.git(root, "commit", "--allow-empty", "-qm", "blocked", env=env, ok=False)
            self.assertTrue(result.returncode != 0 and synthetic_key() not in result.stderr, "BUD-hook-blocks")
            self.assertTrue(self.git(root, "rev-parse", "HEAD").stdout == head and
                            self.git(root, "ls-files", "--stage", "-z").stdout == index_before, "BUD-no-commit-or-index-change")
            if kind == "incomplete":
                self.git(root, "update-index", "--force-remove", "link")

    def test_BUDAC026_process_timeout_and_diagnostic_cap_are_nonzero(self):
        self.engine()
        from _sensitive_scan.git_index import GitProcess
        from _sensitive_scan.common import Limits, ScanError
        root = new_directory()
        fake = root / "git"
        env = dict(git_environment(), PATH=str(root))
        for body in ("import time; time.sleep(3)", "import sys; sys.stderr.write('x' * 65537)"):
            fake.write_text("#!" + sys.executable + "\n" + body + "\n")
            fake.chmod(0o755)
            with mock.patch.object(Limits, "GitCommandTimeoutSeconds", 0.1):
                with self.assertRaises(ScanError, msg="BUD-process-bounds"):
                    with GitProcess(str(root), env, ["ignored"]) as process:
                        list(process.chunks())
            self.assertTrue(process.process.poll() is not None, "BUD-process-reaped")


if __name__ == "__main__":
    unittest.main()

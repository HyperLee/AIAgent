"""RVA release regressions; SDK calls are replaced, filesystem and scanner are real."""
import os
import errno
from pathlib import Path
import subprocess
import sys
import unittest
from unittest import mock

import test_release_publish as release


class ReleaseAcceptanceTests(unittest.TestCase):
    def artifact(self):
        return release.ReleaseBoundaryTests().artifact_fixture()

    def test_RVAAC026_sdk_children_disable_background_workload_network_updates(self):
        with mock.patch.dict(os.environ, {"DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE": "0",
                                          "DOTNET_CLI_TELEMETRY_OPTOUT": "0"}):
            fixture = release.SdkFixture()
        self.assertEqual("1", fixture.env.get("DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE"))
        self.assertEqual("1", fixture.env.get("DOTNET_CLI_TELEMETRY_OPTOUT"))

    def test_RVAAC005_inconsistent_scan_never_authorizes_help(self):
        cases = (
            (0, b"[error] code=scan_changed\n[scan] mode=artifacts status=passed scanned_files=8 findings=0\n"),
            (1, b"[scan] mode=artifacts status=blocked scanned_files=8 findings=0\n"),
            (1, b"[finding] item=000001 rule=BUD-S999\n[scan] mode=artifacts status=blocked scanned_files=8 findings=1\n"),
            (1, b"[finding] item=000001 rule=BUD-S003\n[finding] item=000001 rule=BUD-S003\n[scan] mode=artifacts status=blocked scanned_files=8 findings=2\n"),
            (2, b"[scan] mode=artifacts status=incomplete scanned_files=0 findings=0\n"),
            (0, b"[scan] mode=artifacts status=passed scanned_files=0 findings=0\n"),
            (0, b"[scan] mode=artifacts status=passed scanned_files=8 findings=0\n[scan] mode=artifacts status=passed scanned_files=8 findings=0\n"),
            (0, b"[scan] mode=artifacts status=blocked scanned_files=8 findings=0\n"),
            (0, b"[scan] mode=artifacts status=passed scanned_files=10001 findings=0\n"),
            (1, b"[finding] item=999999 rule=BUD-S003\n[scan] mode=artifacts status=blocked scanned_files=8 findings=1\n"),
        )
        for code, output in cases:
            with self.subTest(code=code, payload_index=cases.index((code, output))):
                fixture = release.ReleaseBoundaryTests().fixture()
                fixture.scan.return_value = subprocess.CompletedProcess([], code, output, b"")
                with self.assertRaises(release.PublishFailure):
                    release.release_steps(fixture, Path("unused"))
                fixture.help.assert_not_called()

    def test_RVAAC005_valid_incomplete_and_all_six_rules_remain_distinct(self):
        for number in range(1, 7):
            result = subprocess.CompletedProcess([], 1,
                ("[finding] item=000001 rule=BUD-S%03d\n" % number).encode() +
                b"[scan] mode=artifacts status=blocked scanned_files=8 findings=1\n", b"")
            self.assertEqual("blocked", release.scan_summary(result).status)
        result = subprocess.CompletedProcess([], 2, b"[error] code=scan_changed\n"
            b"[scan] mode=artifacts status=incomplete scanned_files=0 findings=0\n", b"")
        self.assertEqual("incomplete", release.scan_summary(result).status)

    def test_RVAAC004_manifest_binds_all_files_names_and_modes(self):
        for change in ("dependency", "mode", "rename", "addition", "directory_mode", "root_exchange"):
            with self.subTest(change=change):
                output, _ = self.artifact()
                extra = output / "ordinary dependency.dll"
                extra.write_bytes(b"original dependency")
                before = release.artifact_identity(output)
                if change == "dependency":
                    extra.write_bytes(b"different dependency")
                elif change == "mode":
                    extra.chmod(0o600 if extra.stat().st_mode & 0o077 else 0o644)
                elif change == "rename":
                    extra.rename(output / "renamed dependency.dll")
                elif change == "addition":
                    (output / ".hidden ordinary").write_bytes(b"additional file")
                elif change == "directory_mode":
                    (output / "ProcessHost").chmod(0o700)
                else:
                    output.rename(output.with_name("previous-publish"))
                    output.mkdir()
                self.assertNotEqual(before, release.artifact_identity(output))

    def test_RVAAC004_manifest_is_stable_and_handles_raw_names(self):
        output, _ = self.artifact()
        raw = os.fsencode(output) + b"/non-utf8-\xff.dll"
        try:
            descriptor = os.open(raw, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
        except OSError as failure:
            if failure.errno != errno.EILSEQ:
                raise
            # APFS rejects ill-formed names. Still exercise byte sorting with
            # Unicode; this does not claim arbitrary-byte filename support.
            print("[rva] filesystem_non_utf8_names=unsupported", flush=True)
            raw = os.fsencode(output / "中文 e\u0301.dll")
            descriptor = os.open(raw, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
        os.write(descriptor, b"ordinary bytes")
        os.close(descriptor)
        first = release.artifact_identity(output)
        self.assertEqual(first, release.artifact_identity(output))
        self.assertEqual(9, first.file_count)
        self.assertEqual(64, len(first.manifest_hash))

    def test_RVAAC005_manifest_rejects_links_fifo_and_ancestor_links(self):
        for kind in ("link", "fifo", "ancestor"):
            with self.subTest(kind=kind):
                output, _ = self.artifact()
                if kind == "link":
                    (output / "linked.dll").symlink_to(output / release.CLI_FILES[0])
                elif kind == "fifo":
                    os.mkfifo(output / "pipe")
                else:
                    alias = output.parent / "alias"
                    alias.symlink_to(output, target_is_directory=True)
                    output = alias / "ProcessHost"
                with self.assertRaises(release.PublishFailure):
                    release.artifact_identity(output)

    def test_RVAAC005_manifest_rejects_changes_during_read(self):
        output, _ = self.artifact()
        target = output / release.CLI_FILES[0]
        read = os.read
        changed = False

        def mutate(descriptor, count):
            nonlocal changed
            block = read(descriptor, count)
            if not changed and os.fstat(descriptor).st_ino == target.stat().st_ino:
                changed = True
                target.write_bytes(b"changed during manifest observation")
            return block

        with mock.patch.object(release.os, "read", side_effect=mutate):
            with self.assertRaises(release.PublishFailure):
                release.artifact_identity(output)

    def test_RVAAC005_changes_at_each_gate_invalidate_release(self):
        for gate in ("scan", "before_help", "help"):
            with self.subTest(gate=gate):
                output, built = self.artifact()
                fixture = LocalReleaseFixture(output, built, gate)
                with self.assertRaisesRegex(release.PublishFailure, "artifact_changed"):
                    release.release_steps(fixture, output)
                self.assertEqual(gate == "help", "help" in fixture.calls)

    def test_RVAAC004_real_scan_and_same_artifact_help_finish_with_identity(self):
        output, built = self.artifact()
        fixture = LocalReleaseFixture(output, built)
        result = release.release_steps(fixture, output)
        self.assertEqual("passed", result.scan.status)
        self.assertEqual(0, result.help_exit)
        self.assertEqual(release.artifact_identity(output), result.identity)
        self.assertEqual(["publish", "validate", "scan", "help"], fixture.calls)

    def test_RVAAC005_scan_count_must_cover_the_entire_bound_manifest(self):
        output, built = self.artifact()
        fixture = LocalReleaseFixture(output, built)
        with mock.patch.object(fixture, "scan", return_value=subprocess.CompletedProcess([], 0,
                b"[scan] mode=artifacts status=passed scanned_files=9 findings=0\n", b"")):
            with self.assertRaises(release.PublishFailure):
                release.release_steps(fixture, output)
        self.assertNotIn("help", fixture.calls)


class LocalReleaseFixture:
    def __init__(self, output, built, mutate=None):
        self.output, self.built, self.mutate = output, built, mutate
        self.calls = []
        self.identities = 0

    def publish(self, output):
        if output != self.output:
            raise AssertionError("wrong output")
        self.calls.append("publish")
        return subprocess.CompletedProcess([], 0, b"", b"")

    def validate(self, output):
        self.calls.append("validate")
        release.validate_publish(output, self.built)

    def identity(self, output):
        self.identities += 1
        if self.mutate == "before_help" and self.identities == 3:
            (output / "new dependency.dll").write_bytes(b"changed")
        return release.artifact_identity(output)

    def scan(self, output):
        self.calls.append("scan")
        result = subprocess.run([sys.executable, "-B", str(release.SCANNER), "artifacts",
                                 "--path", str(output)], capture_output=True, check=False)
        if self.mutate == "scan":
            (output / "new dependency.dll").write_bytes(b"changed")
        return result

    def help(self, output):
        self.calls.append("help")
        if self.mutate == "help":
            (output / "new dependency.dll").write_bytes(b"changed")
        return subprocess.CompletedProcess([], 0, b"", b"")


if __name__ == "__main__":
    unittest.main()

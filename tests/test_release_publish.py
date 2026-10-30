"""Offline PUB acceptance with the real SDK. Sources and artifacts are retained.

A passing stop-boundary test is not Release approval. Actual scan outcomes are
reported separately, and blocked artifacts are never started or modified.
"""
from dataclasses import dataclass
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import stat
import subprocess
import sys
import tempfile
from typing import Optional
import unittest
from unittest import mock

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[1]
SCANNER = ROOT / "scripts/check_sensitive_content.py"
CLI = "src/AIAgent.Cli/AIAgent.Cli.csproj"
HOST = "src/AIAgent.ProcessHost/AIAgent.ProcessHost.csproj"
HOST_FILES = tuple("AIAgent.ProcessHost" + suffix for suffix in
                   ("", ".dll", ".deps.json", ".runtimeconfig.json"))
CLI_FILES = ("AIAgent.Cli.dll", "AIAgent.Cli.deps.json",
             "AIAgent.Cli.runtimeconfig.json", "appsettings.json")
EXCLUDED = frozenset(("bin", "obj", "secrets", "logs"))


class PublishFailure(AssertionError):
    """Only controlled classifications enter unittest diagnostics."""


def require(condition, code):
    if not condition:
        raise PublishFailure("PUB-" + code)


def new_directory():
    return Path(tempfile.mkdtemp(prefix="aiagent-pub-", dir="/private/tmp"))


def regular(path):
    try:
        info = path.lstat()
    except OSError:
        raise PublishFailure("PUB-required_file_missing") from None
    require(stat.S_ISREG(info.st_mode) and info.st_size > 0, "required_file_invalid")
    return info


def directory(path):
    try:
        info = path.lstat()
    except OSError:
        raise PublishFailure("PUB-required_directory_missing") from None
    require(stat.S_ISDIR(info.st_mode), "required_directory_invalid")


def copy_sources(original, destination):
    """Copy compile inputs only; never traverse hidden, output or linked trees."""
    directory(original)
    destination.mkdir()

    def copy_file(source):
        regular(source)
        target = destination / source.relative_to(original)
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, target, follow_symlinks=False)

    copy_file(original / "global.json")
    source_root = original / "src"
    directory(source_root)
    for project in ("AIAgent.Cli", "AIAgent.ProcessHost"):
        project_root = source_root / project
        directory(project_root)
        copy_file(project_root / (project + ".csproj"))
        for current, directories, files in os.walk(project_root, followlinks=False):
            directories[:] = sorted(name for name in directories
                                    if not name.startswith(".")
                                    and name.lower() not in EXCLUDED
                                    and not (Path(current) / name).is_symlink())
            for name in sorted(files):
                path = Path(current) / name
                if (name.endswith(".cs") and not name.startswith(".")
                        and not path.is_symlink()):
                    copy_file(path)
    copy_file(original / "src/AIAgent.Cli/appsettings.json")


def execute(args, cwd, env):
    try:
        return subprocess.run(args, cwd=cwd, env=env, stdin=subprocess.DEVNULL,
                              stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                              timeout=180, check=False)
    except subprocess.TimeoutExpired:
        raise PublishFailure("PUB-command_timeout") from None
    except OSError:
        raise PublishFailure("PUB-command_unavailable") from None


def succeeded(result, stage):
    # Do not forward raw SDK diagnostics, paths, request data or source text.
    codes = sorted(set(re.findall(rb"\b(?:MSB|NU|NETSDK|CS)[0-9]{4}\b",
                                  result.stdout + result.stderr)))
    classification = ",".join(code.decode("ascii") for code in codes) or "none"
    require(result.returncode == 0,
            stage + "_exit=" + str(result.returncode) + "_codes=" + classification)


def validate_host(host):
    directory(host)
    for name in HOST_FILES:
        info = regular(host / name)
        if name == HOST_FILES[0]:
            require(bool(info.st_mode & 0o111) and os.access(host / name, os.X_OK),
                    "apphost_not_executable")


def digest(path):
    regular(path)
    value = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(65536), b""):
            value.update(block)
    return value.digest()


def validate_publish(output, built_host):
    directory(output)
    for name in CLI_FILES:
        regular(output / name)
    require(not os.path.lexists(output / "AIAgent.Cli"), "unexpected_cli_apphost")
    published_host = output / "ProcessHost"
    validate_host(published_host)
    validate_host(built_host)
    for name in HOST_FILES:
        require(digest(published_host / name) == digest(built_host / name),
                "processhost_hash_mismatch")


@dataclass(frozen=True)
class ScanSummary:
    exit_code: int
    status: str
    scanned_files: int
    findings: int
    rules: tuple


def scan_summary(result):
    require(result.stderr == b"", "scan_diagnostics_invalid")
    lines = result.stdout.splitlines()
    require(bool(lines), "scan_summary_missing")
    summary = re.fullmatch(rb"\[scan\] mode=artifacts status=(passed|blocked|incomplete) "
                           rb"scanned_files=([0-9]+) findings=([0-9]+)", lines[-1])
    require(summary is not None, "scan_summary_invalid")
    rules = []
    for line in lines[:-1]:
        finding = re.fullmatch(rb"\[finding\] item=[0-9]+ rule=(BUD-S[0-9]{3})", line)
        if finding:
            rules.append(finding[1].decode("ascii"))
        else:
            require(re.fullmatch(rb"\[error\] code=[a-z_]+", line) is not None,
                    "scan_output_invalid")
    status = summary[1].decode("ascii")
    findings = int(summary[3])
    require(findings == len(rules), "scan_count_invalid")
    require((result.returncode, status) in ((0, "passed"), (1, "blocked"),
                                          (2, "incomplete")), "scan_exit_invalid")
    require(status != "passed" or findings == 0, "scan_pass_invalid")
    return ScanSummary(result.returncode, status, int(summary[2]), findings,
                       tuple(sorted(set(rules))))


@dataclass(frozen=True)
class ReleaseOutcome:
    scan: ScanSummary
    help_exit: Optional[int] = None


def release_steps(fixture, output):
    succeeded(fixture.publish(output), "publish")
    fixture.validate(output)
    scan = scan_summary(fixture.scan(output))
    if scan.exit_code != 0:
        return ReleaseOutcome(scan)
    help_result = fixture.help(output)
    succeeded(help_result, "help")
    return ReleaseOutcome(scan, help_result.returncode)


class SdkFixture:
    def __init__(self):
        self.root = new_directory()
        self.source = self.root / "source"
        copy_sources(ROOT, self.source)
        self.feed = self.root / "empty-local-source"
        self.feed.mkdir()
        self.env = {name: os.environ[name] for name in
                    ("PATH", "DOTNET_ROOT", "TMPDIR", "LANG", "LC_ALL")
                    if name in os.environ}
        self.env.update(DOTNET_CLI_TELEMETRY_OPTOUT="1", DOTNET_NOLOGO="1",
                        DOTNET_SKIP_FIRST_TIME_EXPERIENCE="1",
                        DOTNET_EnableDiagnostics="0",
                        DOTNET_CLI_HOME=str(self.root / "sdk-home"),
                        NUGET_PACKAGES=str(Path.home() / ".nuget/packages"))
        print("[pub] retained=" + str(self.root), flush=True)
        require(not any(path.name in ("bin", "obj") for path in self.source.rglob("*")),
                "source_not_clean")

    def run(self, *args):
        return execute(list(args), self.source, self.env)

    def restore(self, project=CLI):
        succeeded(self.run("dotnet", "restore", project, "--source", str(self.feed),
                           "-p:NuGetAudit=false"), "offline_restore_prerequisite")

    def query(self, project, configuration, value):
        args = ["dotnet", "msbuild", project,
                "-getProperty:UseAppHost,SelfContained,RuntimeIdentifier",
                "-p:Configuration=" + configuration]
        if value is not None:
            args.append("-p:UseAppHost=" + value)
        result = self.run(*args)
        succeeded(result, "property_query")
        try:
            return json.loads(result.stdout)["Properties"]
        except (ValueError, KeyError, TypeError):
            raise PublishFailure("PUB-property_result_invalid") from None

    def built_host(self, configuration="Release"):
        return self.source / "src/AIAgent.ProcessHost/bin" / configuration / "net10.0"

    def publish(self, output):
        require(not os.path.lexists(output), "publish_directory_reused")
        return self.run("dotnet", "publish", CLI, "-c", "Release", "--no-restore",
                        "--no-self-contained", "-p:UseAppHost=false", "-o", str(output))

    def validate(self, output):
        validate_publish(output, self.built_host())

    def scan(self, output):
        return self.run(sys.executable, "-B", str(SCANNER), "artifacts", "--path", str(output))

    def help(self, output):
        return self.run("dotnet", str(output / "AIAgent.Cli.dll"), "--help")


class ReleaseSdkTests(unittest.TestCase):
    def test_PUBAC001_property_matrix(self):
        fixture = SdkFixture()
        for configuration in ("Debug", "Release"):
            for value in (None, "true", "false"):
                with self.subTest(configuration=configuration, value=value):
                    properties = fixture.query(HOST, configuration, value)
                    require(properties["UseAppHost"] == "true", "host_property_not_true")
                    require(properties["SelfContained"] == "false"
                            and properties["RuntimeIdentifier"] == "", "deployment_changed")
            require(fixture.query(CLI, configuration, "false")["UseAppHost"] == "false",
                    "cli_property_not_false")

    def test_PUBAC001_direct_and_reference_build_matrix(self):
        for project in (HOST, CLI):
            for configuration in ("Debug", "Release"):
                for value in (None, "true", "false"):
                    with self.subTest(project=project, configuration=configuration, value=value):
                        fixture = SdkFixture()
                        fixture.restore(project)
                        args = ["dotnet", "build", project, "-c", configuration, "--no-restore"]
                        if value is not None:
                            args.append("-p:UseAppHost=" + value)
                        succeeded(fixture.run(*args), "build")
                        validate_host(fixture.built_host(configuration))
                        if project == CLI:
                            validate_host(fixture.source / "src/AIAgent.Cli/bin" /
                                          configuration / "net10.0/ProcessHost")

    def test_PUBAC002_003_004_006_clean_and_incremental_publish(self):
        fixture = SdkFixture()
        fixture.restore()
        require(not os.path.lexists(fixture.built_host() / HOST_FILES[0]),
                "release_host_prebuilt")
        for name in ("first publish with spaces", "second-publish"):
            output = fixture.root / name
            outcome = release_steps(fixture, output)
            scan = outcome.scan
            print("[pub] output=" + str(output) + " publish=0 structure=passed scan_exit="
                  + str(scan.exit_code) + " scan_status=" + scan.status + " scanned_files="
                  + str(scan.scanned_files) + " findings=" + str(scan.findings) + " rules="
                  + (",".join(scan.rules) or "none") + " help="
                  + ("not_run release=blocked" if outcome.help_exit is None else "0 release=passed"),
                  flush=True)
            if scan.exit_code != 0:
                require(scan.exit_code == 1 and scan.status == "blocked" and scan.findings > 0
                        and scan.rules == ("BUD-S003",), "release_scan_unexpected_blocker")
                require(outcome.help_exit is None, "blocked_release_started")
            else:
                require(outcome.help_exit == 0, "release_help_not_verified")


class ReleaseBoundaryTests(unittest.TestCase):
    def completed(self, code=0, output=b""):
        return subprocess.CompletedProcess([], code, output, b"")

    def fixture(self, scan_code=0):
        status = {0: b"passed", 1: b"blocked", 2: b"incomplete"}[scan_code]
        output = b"[scan] mode=artifacts status=" + status + b" scanned_files=8 findings=0\n"
        fixture = mock.Mock()
        fixture.publish.return_value = self.completed()
        fixture.scan.return_value = self.completed(scan_code, output)
        fixture.help.return_value = self.completed()
        return fixture

    def test_PUBAC006_publish_failure_stops_before_validation_scan_and_help(self):
        fixture = self.fixture()
        fixture.publish.return_value = self.completed(1, b"MSB3030")
        with self.assertRaisesRegex(PublishFailure, "publish_exit=1_codes=MSB3030"):
            release_steps(fixture, Path("unused"))
        fixture.validate.assert_not_called()
        fixture.scan.assert_not_called()
        fixture.help.assert_not_called()

    def test_PUBAC006_structure_failure_stops_before_scan_and_help(self):
        fixture = self.fixture()
        fixture.validate.side_effect = PublishFailure("PUB-required_file_missing")
        with self.assertRaises(PublishFailure):
            release_steps(fixture, Path("unused"))
        fixture.scan.assert_not_called()
        fixture.help.assert_not_called()

    def test_PUBAC006_scan_nonzero_never_starts_artifact(self):
        for code in (1, 2):
            with self.subTest(code=code):
                fixture = self.fixture(code)
                outcome = release_steps(fixture, Path("unused"))
                require(outcome.scan.exit_code == code and outcome.help_exit is None,
                        "nonzero_scan_lost")
                fixture.help.assert_not_called()

    def test_PUBAC006_success_uses_same_output_in_order(self):
        fixture = self.fixture()
        output = Path("same output with spaces")
        outcome = release_steps(fixture, output)
        require(outcome.help_exit == 0, "help_missing")
        self.assertTrue(fixture.mock_calls == [mock.call.publish(output), mock.call.validate(output),
                                              mock.call.scan(output), mock.call.help(output)],
                        "PUB-release_order")

    def test_PUBAC006_help_failure_is_not_success(self):
        fixture = self.fixture()
        fixture.help.return_value = self.completed(1)
        with self.assertRaisesRegex(PublishFailure, "help_exit=1"):
            release_steps(fixture, Path("unused"))

    def test_PUBAC006_raw_diagnostics_are_not_forwarded(self):
        result = self.completed(1, b"untrusted diagnostic payload MSB3030")
        with self.assertRaises(PublishFailure) as caught:
            succeeded(result, "publish")
        require(str(caught.exception) == "PUB-publish_exit=1_codes=MSB3030", "diagnostic_leak")
        fixture = self.fixture()
        fixture.scan.return_value = self.completed(0, b"untrusted diagnostic payload")
        with self.assertRaises(PublishFailure):
            release_steps(fixture, Path("unused"))
        fixture.help.assert_not_called()

    def artifact_fixture(self, missing=None):
        root = new_directory()
        output, built = root / "publish", root / "built"
        output.mkdir()
        built.mkdir()
        (output / "ProcessHost").mkdir()
        for name in CLI_FILES:
            if name != missing:
                (output / name).write_bytes(b"ordinary fixture")
        for parent in (output / "ProcessHost", built):
            for name in HOST_FILES:
                if parent == built or "ProcessHost/" + name != missing:
                    path = parent / name
                    path.write_bytes(b"ordinary fixture")
                    if name == HOST_FILES[0]:
                        path.chmod(0o700)
        return output, built

    def test_PUBAC003_every_required_file_is_mandatory(self):
        for missing in CLI_FILES + tuple("ProcessHost/" + name for name in HOST_FILES):
            with self.subTest(missing=missing):
                output, built = self.artifact_fixture(missing)
                with self.assertRaises(PublishFailure):
                    validate_publish(output, built)

    def test_PUBAC003_invalid_type_empty_permissions_hash_and_cli_apphost(self):
        for kind in ("symlink", "directory", "empty", "permissions", "hash", "cli_apphost"):
            with self.subTest(kind=kind):
                omitted = "ProcessHost/AIAgent.ProcessHost.dll" if kind in ("symlink", "directory") else None
                output, built = self.artifact_fixture(omitted)
                target = output / "ProcessHost/AIAgent.ProcessHost.dll"
                if kind == "symlink":
                    target.symlink_to(built / target.name)
                elif kind == "directory":
                    target.mkdir()
                elif kind == "empty":
                    target.write_bytes(b"")
                elif kind == "permissions":
                    (output / "ProcessHost/AIAgent.ProcessHost").chmod(0o600)
                elif kind == "hash":
                    target.write_bytes(b"different fixture")
                else:
                    (output / "AIAgent.Cli").write_bytes(b"unexpected apphost")
                with self.assertRaises(PublishFailure):
                    validate_publish(output, built)

    def test_PUBAC002_source_copy_excludes_outputs_sensitive_and_links(self):
        root = new_directory()
        original = root / "original"
        original.mkdir()
        (original / "global.json").write_bytes(b"ordinary fixture")
        for project in ("AIAgent.Cli", "AIAgent.ProcessHost"):
            parent = original / "src" / project
            parent.mkdir(parents=True)
            (parent / (project + ".csproj")).write_bytes(b"ordinary fixture")
            (parent / "Program.cs").write_bytes(b"ordinary fixture")
            for name in ("bin", "obj", ".git", ".aiagent", ".agents", ".codex", ".aws", "secrets", "logs"):
                excluded = parent / name
                excluded.mkdir()
                (excluded / "Excluded.cs").write_bytes(b"excluded fixture")
            (parent / "credentials.json").write_bytes(b"excluded fixture")
            (parent / "Linked.cs").symlink_to(root / "unreadable-target")
            (parent / "linked-directory").symlink_to(root)
        (original / "src/AIAgent.Cli/appsettings.json").write_bytes(b"ordinary fixture")
        copied = root / "copy"
        copy_sources(original, copied)
        paths = {str(path.relative_to(copied)) for path in copied.rglob("*") if path.is_file()}
        expected = {"global.json", "src/AIAgent.Cli/appsettings.json"}
        for project in ("AIAgent.Cli", "AIAgent.ProcessHost"):
            expected.update(("src/" + project + "/" + project + ".csproj",
                             "src/" + project + "/Program.cs"))
        require(paths == expected, "source_copy_boundary")


if __name__ == "__main__":
    unittest.main()

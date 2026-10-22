"""Strict arguments and safe diagnostics; no raw data crosses stdout/stderr."""
import os
import sys

from .artifacts import scan_artifacts
from .common import Report, ScanError
from .git_index import scan_staged

HELP = ("Usage: python3 scripts/check_sensitive_content.py staged\n"
        "       python3 scripts/check_sensitive_content.py artifacts --path DIRECTORY\n"
        "       python3 scripts/check_sensitive_content.py --help\n"
        "Read-only offline scan. Exit: 0 passed, 1 blocked, 2 incomplete/invalid.\n"
        "Covers only the observed index or publish directory; manual review is required.\n")


def emit(text):
    try:
        if sys.stdout.write(text) != len(text):
            raise OSError()
        sys.stdout.flush()
        return True
    except (Exception, KeyboardInterrupt):
        # Avoid an unsafe interpreter-finalization BrokenPipe diagnostic.
        try:
            descriptor = os.open(os.devnull, os.O_WRONLY)
            try:
                os.dup2(descriptor, sys.stdout.fileno())
            finally:
                os.close(descriptor)
        except (Exception, KeyboardInterrupt):
            sys.stdout = None
        return False


def main(args):
    mode = args[0] if args and args[0] in ("staged", "artifacts") else None
    if args == ["--help"]:
        return 0 if emit(HELP) else 2
    if not (args == ["staged"] or len(args) == 3 and args[:2] == ["artifacts", "--path"] and args[2]):
        emit("[error] code=invalid_arguments\n")
        return 2
    report = Report(mode)
    error = None
    try:
        if mode == "staged":
            scan_staged(report)
        else:
            scan_artifacts(args[2], report)
    except KeyboardInterrupt:
        error = "scan_cancelled"
    except ScanError as failure:
        error = failure.code
    except OSError:
        error = "scan_io"
    except Exception:
        error = "internal_failure"
    lines = ["[finding] item=%06d rule=%s\n" % finding for finding in sorted(report.findings)]
    if error:
        lines.append("[error] code=%s\n" % error)
    status = "incomplete" if error else "blocked" if report.findings else "passed"
    lines.append("[scan] mode=%s status=%s scanned_files=%d findings=%d\n" %
                 (mode, status, report.scanned_files, len(report.findings)))
    if not emit("".join(lines)):
        return 2
    return 2 if error else 1 if report.findings else 0

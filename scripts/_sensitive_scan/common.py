"""Fixed limits, bounded accounting and controlled classifications."""
from dataclasses import dataclass, field


class Limits:
    MaxScanEntries = 10000
    MaxScanFileBytes = 67108864
    MaxScanTotalBytes = 1073741824
    MaxScanPathBytes = 4096
    MaxScanDepth = 64
    MaxExampleFileBytes = 131072
    MaxExampleLineBytes = 4096
    MaxExampleAssignments = 200
    ScanChunkBytes = 65536
    ScanOverlapBytes = 256
    GitCommandTimeoutSeconds = 30
    MaxGitDiagnosticBytes = 65536


class ScanError(Exception):
    def __init__(self, code):
        super().__init__(code)
        self.code = code


@dataclass
class Report:
    mode: str
    scanned_files: int = 0
    findings: set = field(default_factory=set)
    total_bytes: int = 0

    def add(self, item, rule):
        self.findings.add((item, rule))

    def accept_size(self, size, example=False):
        if size < 0:
            raise ScanError("scan_io")
        if (size > Limits.MaxScanFileBytes or
                self.total_bytes + size > Limits.MaxScanTotalBytes or
                example and size > Limits.MaxExampleFileBytes):
            raise ScanError("scan_limit")
        self.total_bytes += size


def validate_path(path):
    if len(path) > Limits.MaxScanPathBytes or len(path.split(b"/")) > Limits.MaxScanDepth:
        raise ScanError("scan_limit")
    if not path or any(part in (b"", b".", b"..") for part in path.split(b"/")):
        raise ScanError("unsupported_entry")

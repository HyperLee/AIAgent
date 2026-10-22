"""Read the effective index and its immutable raw blobs, with bounded Git pipes."""
import os
import re
import selectors
import shutil
import subprocess
import time
from dataclasses import dataclass

from .common import Limits, ScanError, validate_path
from .rules import ContentMatcher, path_rules


def git_environment():
    env = {k: v for k, v in os.environ.items() if not k.startswith("GIT_TRACE")}
    env.update(GIT_NO_LAZY_FETCH="1", GIT_NO_REPLACE_OBJECTS="1", GIT_OPTIONAL_LOCKS="0",
               GIT_TERMINAL_PROMPT="0", GIT_PAGER="cat", GIT_TRACE="0",
               GIT_TRACE2="0", GIT_TRACE2_EVENT="0", GIT_TRACE2_PERF="0")
    return env


class GitProcess:
    def __init__(self, cwd, env, args, data=None):
        self.cwd, self.env, self.args, self.data = cwd, env, args, data
        self.process = None

    def __enter__(self):
        executable = shutil.which("git", path=self.env.get("PATH"))
        if not executable:
            raise ScanError("git_unavailable")
        command = [executable, "--no-pager", "--no-replace-objects", "--no-lazy-fetch",
                   "--no-optional-locks", "-c", "core.fsmonitor=false", *self.args]
        self.deadline = time.monotonic() + Limits.GitCommandTimeoutSeconds
        self.process = subprocess.Popen(command, cwd=self.cwd, env=self.env, shell=False,
                                        stdin=subprocess.PIPE if self.data is not None else subprocess.DEVNULL,
                                        stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        try:
            if self.data is not None:
                self.process.stdin.write(self.data)
                self.process.stdin.close()
        except BaseException:
            self.__exit__(None, None, None)
            raise
        return self

    def chunks(self):
        diagnostics = 0
        with selectors.DefaultSelector() as selector:
            for stream in (self.process.stdout, self.process.stderr):
                os.set_blocking(stream.fileno(), False)
                selector.register(stream, selectors.EVENT_READ)
            while selector.get_map():
                remaining = self.deadline - time.monotonic()
                if remaining <= 0:
                    raise ScanError("scan_io")
                for key, _ in selector.select(min(remaining, 0.1)):
                    block = os.read(key.fd, Limits.ScanChunkBytes)
                    if not block:
                        selector.unregister(key.fileobj)
                    elif key.fileobj is self.process.stderr:
                        diagnostics += len(block)
                        if diagnostics > Limits.MaxGitDiagnosticBytes:
                            raise ScanError("scan_limit")
                    else:
                        yield block
            try:
                result = self.process.wait(timeout=max(0.001, self.deadline - time.monotonic()))
            except subprocess.TimeoutExpired:
                raise ScanError("scan_io") from None
            if result != 0:
                raise ScanError("git_unavailable")

    def __exit__(self, kind, value, traceback):
        if self.process is not None:
            if self.process.poll() is None:
                self.process.kill()
            self.process.wait()
            for stream in (self.process.stdin, self.process.stdout, self.process.stderr):
                if stream is not None:
                    stream.close()


def scalar(cwd, env, args):
    value = bytearray()
    with GitProcess(cwd, env, args) as process:
        for chunk in process.chunks():
            value.extend(chunk)
            if len(value) > Limits.MaxGitDiagnosticBytes:
                raise ScanError("scan_limit")
    if not value.endswith(b"\n") or b"\0" in value:
        raise ScanError("index_invalid")
    return os.fsdecode(bytes(value[:-1]))


@dataclass(frozen=True)
class IndexEntry:
    mode: bytes
    oid: bytes
    stage: int
    path: bytes


def parse_record(record):
    match = re.fullmatch(br"([0-7]{6}) ([0-9a-f]{40}|[0-9a-f]{64}) ([0-3])\t(.+)", record, re.DOTALL)
    if not match:
        raise ScanError("index_invalid")
    mode, oid, stage, path = match.groups()
    validate_path(path)
    if stage != b"0":
        raise ScanError("index_invalid")
    if mode not in (b"100644", b"100755"):
        raise ScanError("unsupported_entry")
    return IndexEntry(mode, oid, int(stage), path)


def index_entries(root, env):
    entries, names, pending = [], set(), bytearray()
    with GitProcess(root, env, ["ls-files", "--full-name", "--stage", "-z"]) as process:
        for chunk in process.chunks():
            pending.extend(chunk)
            while b"\0" in pending:
                end = pending.index(0)
                record = bytes(pending[:end])
                del pending[:end + 1]
                if len(entries) >= Limits.MaxScanEntries:
                    raise ScanError("scan_limit")
                entry = parse_record(record)
                if entry.path in names:
                    raise ScanError("index_invalid")
                names.add(entry.path)
                entries.append(entry)
            if len(pending) > Limits.MaxScanPathBytes + 160:
                raise ScanError("scan_limit")
    if pending:
        raise ScanError("index_invalid")
    return sorted(entries, key=lambda entry: entry.path)


class PipeReader:
    def __init__(self, chunks):
        self.chunks = iter(chunks)
        self.pending = b""

    def fill(self):
        if not self.pending:
            self.pending = next(self.chunks, b"")
        return bool(self.pending)

    def line(self, limit):
        header = bytearray()
        while self.fill():
            end = self.pending.find(b"\n")
            size = end + 1 if end >= 0 else len(self.pending)
            if len(header) + size > limit:
                raise ScanError("index_invalid")
            header.extend(self.pending[:size])
            self.pending = self.pending[size:]
            if end >= 0:
                return bytes(header)
        raise ScanError("index_invalid")

    def take(self, length):
        while length:
            if not self.fill():
                raise ScanError("scan_io")
            count = min(length, len(self.pending))
            yield self.pending[:count]
            self.pending = self.pending[count:]
            length -= count

    def finish(self):
        if self.fill():
            raise ScanError("scan_io")


def scan_blob(root, env, entry, report, item):
    example = entry.path.split(b"/")[-1] == b".env.example"
    with GitProcess(root, env, ["cat-file", "--batch"], entry.oid + b"\n") as process:
        reader = PipeReader(process.chunks())
        header = reader.line(256)
        match = re.fullmatch(br"([0-9a-f]{40}|[0-9a-f]{64}) blob ([0-9]{1,20})\n", header)
        if not match or match[1] != entry.oid:
            raise ScanError("index_invalid")
        size = int(match[2])
        report.accept_size(size, example)
        matcher = ContentMatcher(report, item, example)
        for block in reader.take(size):
            matcher.update(block)
        if b"".join(reader.take(1)) != b"\n":
            raise ScanError("scan_io")
        reader.finish()
        matcher.finish()


def scan_staged(report):
    cwd, env = os.getcwd(), git_environment()
    root = scalar(cwd, env, ["rev-parse", "--show-toplevel"])
    effective_index = scalar(cwd, env, ["rev-parse", "--git-path", "index"])
    env["GIT_INDEX_FILE"] = os.path.abspath(os.path.join(cwd, effective_index))
    entries = index_entries(root, env)
    for item, entry in enumerate(entries, 1):
        for rule in path_rules(entry.path, "staged"):
            report.add(item, rule)
        scan_blob(root, env, entry, report, item)
    if index_entries(root, env) != entries:
        raise ScanError("scan_changed")
    report.scanned_files = len(entries)

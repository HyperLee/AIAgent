"""Descriptor-based publish-tree inspection; never follow links or unpack content."""
import os
import stat
import errno
from dataclasses import dataclass

from .common import Limits, ScanError, validate_path
from .rules import ARCHIVES, ContentMatcher, path_rules

REQUIRED = frozenset((b"AIAgent.Cli.dll", b"AIAgent.Cli.deps.json",
                       b"AIAgent.Cli.runtimeconfig.json", b"appsettings.json"))


def signature(value):
    return (value.st_dev, value.st_ino, stat.S_IFMT(value.st_mode), value.st_size,
            value.st_mtime_ns, value.st_ctime_ns)


def identity(value):
    return value.st_dev, value.st_ino, stat.S_IFMT(value.st_mode)


def open_root(path):
    # Do not normpath/realpath: link/../root must still reject that link.
    raw = os.fsencode(path)
    if not raw or b"\0" in raw:
        raise ScanError("artifact_invalid")
    if not raw.startswith(b"/"):
        raw = os.getcwdb() + b"/" + raw
    descriptor = os.open(b"/", os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW)
    chain = [identity(os.fstat(descriptor))]
    try:
        for part in raw.split(b"/"):
            if part in (b"", b"."):
                continue
            before = os.stat(part, dir_fd=descriptor, follow_symlinks=False)
            if not stat.S_ISDIR(before.st_mode):
                raise ScanError("artifact_invalid")
            child = os.open(part, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW, dir_fd=descriptor)
            try:
                if identity(os.fstat(child)) != identity(before):
                    raise ScanError("scan_changed")
            except BaseException:
                os.close(child)
                raise
            os.close(descriptor)
            descriptor = child
            chain.append(identity(before))
        return descriptor, tuple(chain)
    except BaseException:
        os.close(descriptor)
        raise


@dataclass(frozen=True)
class ArtifactEntry:
    path: bytes
    metadata: tuple


class ArtifactTree:
    def __init__(self, path, report):
        self.path, self.report = path, report
        self.root = None
        self.entries = []
        self.directories = {}
        self.by_path = {}
        self.incomplete = False

    def __enter__(self):
        if not hasattr(os, "O_NOFOLLOW") or os.open not in os.supports_dir_fd:
            raise ScanError("dependency_unavailable")
        try:
            self.root, self.chain = open_root(self.path)
        except OSError:
            raise ScanError("artifact_invalid") from None
        return self

    def __exit__(self, kind, value, traceback):
        if self.root is not None:
            os.close(self.root)

    def enumerate(self, descriptor, relative=b""):
        before = signature(os.fstat(descriptor))
        children = []
        with os.scandir(descriptor) as iterator:
            for entry in iterator:
                if len(self.entries) >= Limits.MaxScanEntries:
                    raise ScanError("scan_limit")
                name = os.fsencode(entry.name)
                path = relative + b"/" + name if relative else name
                validate_path(path)
                value = os.stat(name, dir_fd=descriptor, follow_symlinks=False)
                metadata = signature(value)
                children.append((name, metadata))
                record = ArtifactEntry(path, metadata)
                self.entries.append(record)
                self.by_path[path] = record
        for name, metadata in children:
            path = relative + b"/" + name if relative else name
            if stat.S_ISDIR(metadata[2]) and not path_rules(path, "artifacts"):
                child = os.open(name, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW, dir_fd=descriptor)
                try:
                    if signature(os.fstat(child)) != metadata:
                        raise ScanError("scan_changed")
                    self.enumerate(child, path)
                finally:
                    os.close(child)
        if signature(os.fstat(descriptor)) != before:
            raise ScanError("scan_changed")
        self.directories[relative] = before, tuple(sorted(children))

    def parent(self, relative):
        descriptor = os.dup(self.root)
        current = b""
        try:
            for part in relative.split(b"/") if relative else ():
                current = current + b"/" + part if current else part
                child = os.open(part, os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW, dir_fd=descriptor)
                try:
                    if signature(os.fstat(child)) != self.by_path[current].metadata:
                        raise ScanError("scan_changed")
                except BaseException:
                    os.close(child)
                    raise
                os.close(descriptor)
                descriptor = child
            return descriptor
        except BaseException:
            os.close(descriptor)
            raise

    def scan_file(self, entry, item):
        relative, _, name = entry.path.rpartition(b"/")
        parent = self.parent(relative)
        descriptor = None
        try:
            if signature(os.stat(name, dir_fd=parent, follow_symlinks=False)) != entry.metadata:
                raise ScanError("scan_changed")
            descriptor = os.open(name, os.O_RDONLY | os.O_NOFOLLOW | os.O_NONBLOCK, dir_fd=parent)
            if signature(os.fstat(descriptor)) != entry.metadata:
                raise ScanError("scan_changed")
            matcher = ContentMatcher(self.report, item)
            count, size = 0, entry.metadata[3]
            while True:
                block = os.read(descriptor, min(Limits.ScanChunkBytes, size + 1 - count))
                if not block:
                    break
                count += len(block)
                if count > size:
                    raise ScanError("scan_changed")
                matcher.update(block)
            if (count != size or signature(os.fstat(descriptor)) != entry.metadata or
                    signature(os.stat(name, dir_fd=parent, follow_symlinks=False)) != entry.metadata):
                raise ScanError("scan_changed")
            matcher.finish()
        except (FileNotFoundError, NotADirectoryError, IsADirectoryError):
            raise ScanError("scan_changed") from None
        finally:
            if descriptor is not None:
                os.close(descriptor)
            os.close(parent)

    def verify(self):
        try:
            fresh, chain = open_root(self.path)
            try:
                if chain != self.chain or identity(os.fstat(fresh)) != identity(os.fstat(self.root)):
                    raise ScanError("scan_changed")
            finally:
                os.close(fresh)
            for relative, (metadata, expected) in self.directories.items():
                descriptor = self.parent(relative)
                try:
                    if signature(os.fstat(descriptor)) != metadata:
                        raise ScanError("scan_changed")
                    observed = []
                    with os.scandir(descriptor) as iterator:
                        for entry in iterator:
                            if len(observed) >= len(expected):
                                raise ScanError("scan_changed")
                            name = os.fsencode(entry.name)
                            observed.append((name, signature(os.stat(name, dir_fd=descriptor, follow_symlinks=False))))
                    if tuple(sorted(observed)) != expected or signature(os.fstat(descriptor)) != metadata:
                        raise ScanError("scan_changed")
                finally:
                    os.close(descriptor)
        except (OSError, ScanError):
            raise ScanError("scan_changed") from None

    def scan(self):
        self.enumerate(self.root)
        entries = sorted(self.entries, key=lambda entry: entry.path)
        present = {entry.path for entry in entries if stat.S_ISREG(entry.metadata[2])}
        if not REQUIRED.issubset(present):
            raise ScanError("artifact_invalid")
        for item, entry in enumerate(entries, 1):
            rules = path_rules(entry.path, "artifacts")
            for rule in rules:
                self.report.add(item, rule)
            kind = entry.metadata[2]
            if (stat.S_ISDIR(kind) and rules or not stat.S_ISDIR(kind) and not stat.S_ISREG(kind) or
                    stat.S_ISREG(kind) and entry.path.lower().endswith(ARCHIVES)):
                self.incomplete = True
            elif stat.S_ISREG(kind):
                self.report.accept_size(entry.metadata[3])
        count = 0
        for item, entry in enumerate(entries, 1):
            if stat.S_ISREG(entry.metadata[2]) and not entry.path.lower().endswith(ARCHIVES):
                self.scan_file(entry, item)
                count += 1
        self.verify()
        if self.incomplete:
            raise ScanError("unsupported_entry")
        self.report.scanned_files = count


def scan_artifacts(path, report):
    with ArtifactTree(path, report) as tree:
        try:
            tree.scan()
        except OSError as failure:
            if failure.errno in (errno.ENOENT, errno.ENOTDIR, errno.EISDIR, errno.ELOOP):
                raise ScanError("scan_changed") from None
            raise

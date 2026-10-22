"""Byte rules and a bounded physical-line parser for the sole example exception."""
import re
import unicodedata

from .common import Limits, ScanError

DIRECTORIES = frozenset((b".git", b".agents", b".codex", b".aws", b".ssh",
                         b".secrets", b"secrets", b"logs"))
NAMES = frozenset((b"gemini-api-key.txt", b"api-key.txt", b"appsettings.local.json",
                   b"credentials", b"credentials.json", b"id_rsa", b"id_dsa",
                   b"id_ecdsa", b"id_ed25519", b"identity"))
SUFFIXES = (b".secrets.json", b".pem", b".key", b".p12", b".pfx", b".ppk",
            b".keystore", b".jks", b".log", b".dmp", b".dump")
ARCHIVES = (b".zip", b".tar", b".gz", b".tgz", b".7z", b".rar", b".dmg", b".pkg")
TEMP = re.compile(br"\.aiagent-edit-[0-9a-f]{32}\.tmp\Z")
KEY = re.compile(br"AIza[A-Za-z0-9_-]{35}")
PRIVATE = re.compile(br"-{5}BEGIN (?:RSA |DSA |EC |ENCRYPTED |OPENSSH )?PRIVATE KEY-{5}")
PLACEHOLDERS = frozenset(("", "YOUR_GEMINI_API_KEY", "YOUR_API_KEY", "YOUR_ACCESS_TOKEN",
                          "YOUR_CLIENT_SECRET", "EXAMPLE_ONLY_NOT_A_SECRET", "[REDACTED]"))


def path_rules(path, mode):
    parts = path.lower().split(b"/")  # bytes.lower changes ASCII only.
    name = parts[-1]
    example = mode == "staged" and path.split(b"/")[-1] == b".env.example"
    forbidden = DIRECTORIES | (frozenset((b"bin", b"obj")) if mode == "staged" else frozenset())
    rules = set()
    if (any(part in forbidden for part in parts) or name in NAMES or name.endswith(SUFFIXES) or
            (name == b".env" or name.startswith(b".env.")) and not example):
        rules.add("BUD-S001")
    if TEMP.fullmatch(name):
        rules.add("BUD-S004")
    return rules


def encoded_patterns(encoding):
    def literal(value):
        return re.escape(value.encode(encoding))
    zero_before = encoding == "utf-16be"
    character = br"\x00[A-Za-z0-9_-]" if zero_before else br"[A-Za-z0-9_-]\x00"
    key = literal("AI" + "za") + b"(?:" + character + b"){35}"
    kinds = b"|".join(literal(value) for value in ("RSA ", "DSA ", "EC ", "ENCRYPTED ", "OPENSSH "))
    private = literal("-" * 5 + "BEGIN ") + b"(?:" + kinds + b")?" + literal("PRIVATE " + "KEY" + "-" * 5)
    return re.compile(key), re.compile(private)


ENCODED = (encoded_patterns("utf-16le"), encoded_patterns("utf-16be"))


class ExampleParser:
    def __init__(self):
        self.line = bytearray()
        self.total = 0
        self.first = True
        self.names = set()
        self.assignments = 0
        self.invalid = False

    def update(self, data):
        self.total += len(data)
        if self.total > Limits.MaxExampleFileBytes:
            raise ScanError("scan_limit")
        for value in data:
            if self.line and self.line[-1] == 13:
                if value == 10:
                    self._append(value)
                    self._finish_line()
                    continue
                self._finish_line()
            self._append(value)
            if value == 10:
                self._finish_line()

    def _append(self, value):
        self.line.append(value)
        if len(self.line) > Limits.MaxExampleLineBytes:
            raise ScanError("scan_limit")

    def _finish_line(self):
        raw = bytes(self.line)
        self.line.clear()
        if self.first and raw.startswith(b"\xef\xbb\xbf"):
            raw = raw[3:]
        self.first = False
        # Count assignment-shaped physical lines before decoding/validation.
        stripped = raw.strip(b" \t\r\n")
        if b"=" in stripped and not stripped.startswith(b"#"):
            self.assignments += 1
            if self.assignments > Limits.MaxExampleAssignments:
                raise ScanError("scan_limit")
        try:
            text = raw.decode("utf-8", "strict")
        except UnicodeError:
            self.invalid = True
            return
        if any((unicodedata.category(c) == "Cc" and c not in "\t\r\n") or
               c in "\u2028\u2029\ufeff" for c in text):
            self.invalid = True
        text = text.rstrip("\r\n").strip(" \t")
        if not text or text.startswith("#"):
            return
        if "=" not in text:
            self.invalid = True
            return
        name, value = (part.strip(" \t") for part in text.split("=", 1))
        if not re.fullmatch(r"[A-Za-z_][A-Za-z0-9_]*", name) or name in self.names:
            self.invalid = True
        self.names.add(name)
        if len(value) >= 2 and value[0] in "\"'" and value[-1] == value[0]:
            value = value[1:-1]
        if value not in PLACEHOLDERS:
            self.invalid = True

    def finish(self):
        if self.line:
            self._finish_line()
        return self.invalid


class ContentMatcher:
    def __init__(self, report, item, example=False):
        self.report = report
        self.item = item
        self.tail = b""
        self.example = ExampleParser() if example else None

    def update(self, data):
        block = self.tail + data
        for key, private in ((KEY, PRIVATE),) + ENCODED:
            if key.search(block):
                self.report.add(self.item, "BUD-S002")
            if private.search(block):
                self.report.add(self.item, "BUD-S003")
        self.tail = block[-Limits.ScanOverlapBytes:]
        if self.example:
            self.example.update(data)

    def finish(self):
        if self.example and self.example.finish():
            self.report.add(self.item, "BUD-S005")

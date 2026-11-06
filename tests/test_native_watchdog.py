"""Controlled guard failures, separately from real product-native acceptance."""
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import time
import unittest


@unittest.skipUnless(sys.platform == "darwin", "RVA guard requires macOS; not native acceptance")
class NativeWatchdogTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.root = Path(tempfile.mkdtemp(prefix="aia-guard-qa-", dir="/private/tmp"))
        cls.helper, cls.fault = cls.root / "helper", cls.root / "observation-fault.dylib"
        sources = Path(__file__).resolve().parent / "native"
        for args in (("-O2", str(sources / "rva_fixture.c"), "-o", str(cls.helper)),
                     ("-dynamiclib", str(sources / "rva_observation_fault.c"), "-o", str(cls.fault))):
            result = subprocess.run(["/usr/bin/xcrun", "clang", "-std=c11", "-Wall", "-Wextra",
                                     "-Werror", *args], capture_output=True, timeout=30, check=False)
            if result.returncode:
                raise AssertionError("RVA guard prerequisite compilation failed")

    def directory(self):
        root = Path(tempfile.mkdtemp(prefix="case-", dir=self.root))
        (root / "obj/rva").mkdir(parents=True)
        return root

    def gate(self, root, name):
        (root / "obj/rva" / name).write_text("1\n")

    def wait(self, root, name):
        until = time.monotonic() + 5
        while not (root / "obj/rva" / name).is_file():
            if time.monotonic() >= until:
                self.fail("RVA guard gate unavailable: " + name)
            time.sleep(0.01)

    def guard(self, root, fault=False):
        env = {"PATH": "/usr/bin:/bin"}
        if fault:
            env["DYLD_INSERT_LIBRARIES"] = str(self.fault)
        process = subprocess.Popen([str(self.helper), "watchdog", str(root), "30000"],
                                   stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL,
                                   stderr=subprocess.DEVNULL, env=env)
        self.wait(root, "watchdog-ready")
        self.gate(root, "start")
        self.wait(root, "armed")
        return process

    def owned_waiter(self, root):
        (root / "obj/rva/scenario").write_text("wait")
        process = subprocess.Popen([str(self.helper), "build"], cwd=root,
                                   stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL,
                                   stderr=subprocess.DEVNULL, env={"PATH": "/usr/bin:/bin"})
        self.wait(root, "ready")
        registration = root / "obj/rva" / ("pid-" + str(process.pid))
        return process, registration.read_bytes()

    def test_RVAAC009_observation_loss_never_confirms_guard_cleanup(self):
        primary = self.directory()
        guard = self.guard(primary)
        child, record = self.owned_waiter(primary)
        fault_root = self.directory()
        (fault_root / "obj/rva" / ("pid-" + str(child.pid))).write_bytes(record)
        fault_guard = self.guard(fault_root, fault=True)
        try:
            self.gate(fault_root, "finish")
            self.assertEqual(2, fault_guard.wait(timeout=8))
            self.assertTrue((fault_root / "obj/rva/guard-unconfirmed").is_file())
        finally:
            self.gate(primary, "finish")
            self.assertIn(guard.wait(timeout=8), (0, 1))
            child.wait(timeout=2)

    def test_RVAAC009_malformed_record_does_not_skip_verified_owned_cleanup(self):
        primary = self.directory()
        guard = self.guard(primary)
        child, record = self.owned_waiter(primary)
        fault_root = self.directory()
        (fault_root / "obj/rva/pid-999999").write_bytes(b"incomplete")
        (fault_root / "obj/rva" / ("pid-" + str(child.pid))).write_bytes(record)
        fault_guard = self.guard(fault_root)
        try:
            self.gate(fault_root, "finish")
            self.assertEqual(2, fault_guard.wait(timeout=8))
            child.wait(timeout=1)
        finally:
            self.gate(primary, "finish")
            self.assertIn(guard.wait(timeout=8), (0, 1))
            child.wait(timeout=2)

    def test_RVAAC009_unpublished_temporary_is_outside_registration_namespace(self):
        root = self.directory()
        (root / "obj/rva/pid-123.partial").write_bytes(b"")
        guard = self.guard(root)
        self.gate(root, "finish")
        self.assertEqual(0, guard.wait(timeout=8))


if __name__ == "__main__":
    unittest.main()

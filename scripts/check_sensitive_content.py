#!/usr/bin/env python3
"""Read-only developer scan; never a product tool or credential source."""
import sys

sys.dont_write_bytecode = True

if __name__ == "__main__":
    if sys.version_info < (3, 9):
        print("[error] code=dependency_unavailable")
        sys.exit(2)
    try:
        from _sensitive_scan.cli import main
    except Exception:
        print("[error] code=internal_failure")
        sys.exit(2)
    sys.exit(main(sys.argv[1:]))

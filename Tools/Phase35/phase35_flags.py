"""Canonical Phase 35 managed artifact proof flags."""

PHASE35_FLAGS = {
    "success": 0xFE000000,
    "failfast": 0xDE000000,
    "stale-owner": 0xEE000000,
    "cross-scope": 0xF6000000,
    "malformed": 0xFA000000,
    "no-read": 0xFC000000,
    "one-read": 0xF4000000,
    "two-read": 0xEC000000,
    "not-found": 0xE4000000,
}

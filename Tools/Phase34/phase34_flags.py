"""Canonical Phase 34 managed artifact proof flags."""

PHASE34_FLAGS = {
    "success": (1 << 25) | (1 << 26) | (1 << 30) | (1 << 31),
    "failfast": (1 << 25) | (1 << 27) | (1 << 30) | (1 << 31),
    "stale-owner": (1 << 25) | (1 << 28) | (1 << 30) | (1 << 31),
    "cross-scope": (1 << 25) | (1 << 29) | (1 << 30) | (1 << 31),
    "malformed": (1 << 26) | (1 << 27) | (1 << 30) | (1 << 31),
}

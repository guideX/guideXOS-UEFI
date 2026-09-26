#!/usr/bin/env python3
from pathlib import Path
import hashlib
import struct
import sys

MAGIC = 0x49425847
HEADER = struct.Struct('<IIIIIIQIIIIIII32s')

image_path = Path(sys.argv[1])
descriptor_path = Path(sys.argv[2])
image = image_path.read_bytes()
image_size = (len(image) + 0xFFF) & ~0xFFF
descriptor = HEADER.pack(
    MAGIC, 1, 0x8664, 0x1F, len(image), image_size,
    0x0000401200000000, 0, 0, len(image), 1, 1, 0, 0,
    hashlib.sha256(image).digest())
descriptor_path.parent.mkdir(parents=True, exist_ok=True)
descriptor_path.write_bytes(descriptor)

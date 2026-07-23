using System;
using System.Drawing;
using System.Runtime.CompilerServices;

namespace guideXOS;

internal enum UefiCursorPngProbeStage {
    None,
    S0,
    S1,
    S2,
    S3,
    S4,
    S4A,
    S4B0,
    S4B1,
    S4B2,
    S4B3,
    S4B4,
    S4B5,
    S4B6,
    S4B7R,
    S4B7S,
    S4B7T,
    S4B7T2,
    S4B7U,
    S4B7,
    S4B8,
    S4B,
    S4C,
    S4C0,
    S4C1,
    S4C2,
    S4C3,
    S4C4,
    S4D0,
    S4D1,
    S4D2,
    S4D3,
    S4D,
    S4E,
    S4F,
    S4G,
    S4H
}

internal static class UefiCursorPngProbe {
    private static byte[] s_standaloneProbeBytes;

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static Image Probe(byte[] bytes, UefiCursorPngProbeStage stage) {
        switch (stage) {
            case UefiCursorPngProbeStage.S0:
                ProbeS0(bytes);
                break;
            case UefiCursorPngProbeStage.S1:
                ProbeS1(bytes);
                break;
            case UefiCursorPngProbeStage.S2:
                ProbeS2(bytes);
                break;
            case UefiCursorPngProbeStage.S3:
                ProbeS3(bytes);
                break;
            case UefiCursorPngProbeStage.S4:
                ProbeS4(bytes);
                break;
            case UefiCursorPngProbeStage.S4A:
                ProbeS4A(bytes);
                break;
            case UefiCursorPngProbeStage.S4B0:
                Breadcrumb("UEFI_PNG_PROBE_S4B0_SELECTED");
                Breadcrumb("UEFI_PNG_PROBE_S4B0_RETURN");
                break;
            case UefiCursorPngProbeStage.S4B1:
                Breadcrumb("UEFI_PNG_PROBE_S4B1_BEFORE_CALL");
                ProbeS4B1Body();
                Breadcrumb("UEFI_PNG_PROBE_S4B1_AFTER_CALL");
                break;
            case UefiCursorPngProbeStage.S4B2:
                Breadcrumb("UEFI_PNG_PROBE_S4B2_BEFORE_CALL");
                ProbeS4B2Body(1070, 28, 28, 555, 3164);
                Breadcrumb("UEFI_PNG_PROBE_S4B2_AFTER_CALL");
                break;
            case UefiCursorPngProbeStage.S4B3:
                Breadcrumb("UEFI_PNG_PROBE_S4B3_BEFORE_CALL");
                ProbeS4B3Body(bytes);
                Breadcrumb("UEFI_PNG_PROBE_S4B3_AFTER_CALL");
                break;
            case UefiCursorPngProbeStage.S4B4:
                Breadcrumb("UEFI_PNG_PROBE_S4B4_BEFORE_CALL");
                ProbeS4B4Body(bytes);
                Breadcrumb("UEFI_PNG_PROBE_S4B4_AFTER_CALL");
                break;
            case UefiCursorPngProbeStage.S4B5:
                Breadcrumb("UEFI_PNG_PROBE_S4B5_BEFORE_CALL");
                ProbeS4B5Body(bytes);
                Breadcrumb("UEFI_PNG_PROBE_S4B5_AFTER_CALL");
                break;
            case UefiCursorPngProbeStage.S4B6:
                s_standaloneProbeBytes = bytes;
                try {
                    ProbeS4B6Body();
                } finally {
                    s_standaloneProbeBytes = null;
                }
                break;
            case UefiCursorPngProbeStage.S4B7R:
                Breadcrumb("UEFI_PNG_PROBE_S4B7_ROUTE_ENTER");
                ProbeS4B7RBody();
                break;
            case UefiCursorPngProbeStage.S4B7S:
                Breadcrumb("UEFI_PNG_PROBE_S4B7_ROUTE_ENTER");
                s_standaloneProbeBytes = bytes;
                try {
                    ProbeS4B7SBody();
                } finally {
                    s_standaloneProbeBytes = null;
                }
                break;
            case UefiCursorPngProbeStage.S4B7T:
                Breadcrumb("UEFI_PNG_PROBE_S4B7_ROUTE_ENTER");
                s_standaloneProbeBytes = bytes;
                try {
                    ProbeS4B7TBody();
                } finally {
                    s_standaloneProbeBytes = null;
                }
                break;
            case UefiCursorPngProbeStage.S4B7T2:
            case UefiCursorPngProbeStage.S4C0:
                Breadcrumb("UEFI_PNG_PROBE_S4B7_ROUTE_ENTER");
                s_standaloneProbeBytes = bytes;
                try {
                    bool isS4C0 = stage == UefiCursorPngProbeStage.S4C0;
                    string probePrefix = isS4C0 ? "UEFI_PNG_PROBE_S4C0" : "UEFI_PNG_PROBE_S4B7T2";
                    Breadcrumb(probePrefix + "_ENTER");
                    try {
                        if (!TryPrepareStandaloneS4ProbeBody(probePrefix, out byte[] compressedData, out int idatTotalBytes, out _)) {
                            break;
                        }

                        byte rawCompressedByte = compressedData[2];
                        Value(probePrefix + "_RAW_BYTE", (ulong)rawCompressedByte);

                        int bfinal = rawCompressedByte & 1;
                        int btype = (rawCompressedByte >> 1) & 3;
                        Value(probePrefix + "_BFINAL", (ulong)(uint)bfinal);
                        Value(probePrefix + "_BTYPE", (ulong)(uint)btype);
                        if (isS4C0) {
                            if (btype == 2) {
                                Breadcrumb("UEFI_PNG_PROBE_S4C0_AFTER_BLOCK_HEADER");
                            }
                        } else if (btype == 2) {
                            Breadcrumb("UEFI_PNG_PROBE_S4B7T2_DYNAMIC");
                        }

                        _ = idatTotalBytes;
                    } finally {
                        Breadcrumb(probePrefix + "_EXIT");
                    }
                } finally {
                    s_standaloneProbeBytes = null;
                }
                break;
            case UefiCursorPngProbeStage.S4C1:
                Breadcrumb("UEFI_PNG_PROBE_S4B7_ROUTE_ENTER");
                s_standaloneProbeBytes = bytes;
                try {
                    ProbeS4C1StandaloneBody();
                } finally {
                    s_standaloneProbeBytes = null;
                }
                break;
            case UefiCursorPngProbeStage.S4C2:
                Breadcrumb("UEFI_PNG_PROBE_S4B7_ROUTE_ENTER");
                s_standaloneProbeBytes = bytes;
                try {
                    ProbeS4C2StandaloneBody();
                } finally {
                    s_standaloneProbeBytes = null;
                }
                break;
            case UefiCursorPngProbeStage.S4C3:
                Breadcrumb("UEFI_PNG_PROBE_S4B7_ROUTE_ENTER");
                s_standaloneProbeBytes = bytes;
                try {
                    ProbeS4C3StandaloneBody();
                } finally {
                    s_standaloneProbeBytes = null;
                }
                break;
            case UefiCursorPngProbeStage.S4C4:
                Breadcrumb("UEFI_PNG_PROBE_S4B7_ROUTE_ENTER");
                s_standaloneProbeBytes = bytes;
                try {
                    ProbeS4C4StandaloneBody();
                } finally {
                    s_standaloneProbeBytes = null;
                }
                break;
            case UefiCursorPngProbeStage.S4D0:
                Breadcrumb("UEFI_PNG_PROBE_S4B7_ROUTE_ENTER");
                s_standaloneProbeBytes = bytes;
                try {
                    ProbeS4D0StandaloneBody();
                } finally {
                    s_standaloneProbeBytes = null;
                }
                break;
            case UefiCursorPngProbeStage.S4D1:
                Breadcrumb("UEFI_PNG_PROBE_S4B7_ROUTE_ENTER");
                s_standaloneProbeBytes = bytes;
                try {
                    ProbeS4D1StandaloneBody();
                } finally {
                    s_standaloneProbeBytes = null;
                }
                break;
            case UefiCursorPngProbeStage.S4D2:
                Breadcrumb("UEFI_PNG_PROBE_S4B7_ROUTE_ENTER");
                s_standaloneProbeBytes = bytes;
                try {
                    ProbeS4D2StandaloneBody();
                } finally {
                    s_standaloneProbeBytes = null;
                }
                break;
            case UefiCursorPngProbeStage.S4D3:
                Breadcrumb("UEFI_PNG_PROBE_S4B7_ROUTE_ENTER");
                s_standaloneProbeBytes = bytes;
                try {
                    ProbeS4D3StandaloneBody();
                } finally {
                    s_standaloneProbeBytes = null;
                }
                break;
            case UefiCursorPngProbeStage.S4B7U:
                Breadcrumb("UEFI_PNG_PROBE_S4B7_ROUTE_ENTER");
                s_standaloneProbeBytes = bytes;
                try {
                    ProbeS4B7UBody();
                } finally {
                    s_standaloneProbeBytes = null;
                }
                break;
            case UefiCursorPngProbeStage.S4B7:
                Breadcrumb("UEFI_PNG_PROBE_S4B7_ROUTE_ENTER");
                s_standaloneProbeBytes = bytes;
                try {
                    ProbeS4B7Body();
                } finally {
                    s_standaloneProbeBytes = null;
                }
                break;
            case UefiCursorPngProbeStage.S4B8:
                s_standaloneProbeBytes = bytes;
                try {
                    ProbeS4B8Body();
                } finally {
                    s_standaloneProbeBytes = null;
                }
                break;
            case UefiCursorPngProbeStage.S4B:
                ProbeS4B(bytes);
                break;
            case UefiCursorPngProbeStage.S4C:
                ProbeS4C(bytes);
                break;
            case UefiCursorPngProbeStage.S4D:
                ProbeS4D(bytes);
                break;
            case UefiCursorPngProbeStage.S4E:
                ProbeS4E(bytes);
                break;
            case UefiCursorPngProbeStage.S4F:
                ProbeS4F(bytes);
                break;
            case UefiCursorPngProbeStage.S4G:
                ProbeS4G(bytes);
                break;
            case UefiCursorPngProbeStage.S4H:
                ProbeS4H(bytes);
                break;
            default:
                Breadcrumb("UEFI_PNG_PROBE_VARIANT_UNSET");
                break;
        }

        return null;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ProbeStandaloneS4B(byte[] bytes) {
        ProbeS4B(bytes);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void ProbeStandaloneS4C(byte[] bytes) {
        ProbeS4C(bytes);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ProbeS0(byte[] bytes) {
        Breadcrumb("UEFI_PNG_PROBE_S0_ENTER");
        try {
            if (bytes == null) {
                return;
            }

            Breadcrumb("UEFI_PNG_PROBE_S0_BYTES_OK");
            Value("UEFI_PNG_PROBE_S0_LEN", (ulong)bytes.Length);

            if (!TryReadHeader(bytes)) {
                return;
            }

            Breadcrumb("UEFI_PNG_PROBE_S0_HEADER_OK");

            int width = (int)ReadU32BE(bytes, 16);
            int height = (int)ReadU32BE(bytes, 20);
            byte bitDepth = bytes[24];
            byte colorType = bytes[25];
            Value("UEFI_PNG_PROBE_S0_IHDR_WIDTH", (ulong)(uint)width);
            Value("UEFI_PNG_PROBE_S0_IHDR_HEIGHT", (ulong)(uint)height);
            Value("UEFI_PNG_PROBE_S0_IHDR_BIT_DEPTH", (ulong)bitDepth);
            Value("UEFI_PNG_PROBE_S0_IHDR_COLOR_TYPE", (ulong)colorType);

            if (width <= 0 || height <= 0 || bitDepth != 8 || colorType != 6) {
                return;
            }

            Breadcrumb("UEFI_PNG_PROBE_S0_IHDR_OK");
        } finally {
            Breadcrumb("UEFI_PNG_PROBE_S0_EXIT");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ProbeS1(byte[] bytes) {
        Breadcrumb("UEFI_PNG_PROBE_S1_ENTER");
        try {
            ProbeS1Body(bytes);
        } finally {
            Breadcrumb("UEFI_PNG_PROBE_S1_EXIT");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ProbeS1Body(byte[] bytes) {
        if (!TryReadHeader(bytes)) {
            return;
        }

        int width = (int)ReadU32BE(bytes, 16);
        int height = (int)ReadU32BE(bytes, 20);
        byte bitDepth = bytes[24];
        byte colorType = bytes[25];
        if (width <= 0 || height <= 0 || bitDepth != 8 || colorType != 6) {
            return;
        }

        Value("UEFI_PNG_PROBE_S1_LEN", (ulong)bytes.Length);
        Value("UEFI_PNG_PROBE_S1_IHDR_WIDTH", (ulong)(uint)width);
        Value("UEFI_PNG_PROBE_S1_IHDR_HEIGHT", (ulong)(uint)height);
        Value("UEFI_PNG_PROBE_S1_IHDR_BIT_DEPTH", (ulong)bitDepth);
        Value("UEFI_PNG_PROBE_S1_IHDR_COLOR_TYPE", (ulong)colorType);

        Breadcrumb("UEFI_PNG_PROBE_S1_CHUNK_SCAN_ENTER");
        int pos = 33;
        int idatChunkCount = 0;
        int idatTotalBytes = 0;
        while (pos + 12 <= bytes.Length) {
            uint chunkLen = ReadU32BE(bytes, pos);
            if (chunkLen > (uint)(bytes.Length - pos - 12)) {
                Breadcrumb("UEFI_PNG_PROBE_S1_CHUNK_SCAN_EXIT");
                return;
            }

            uint chunkType = ReadU32BE(bytes, pos + 4);
            if (chunkType == 0x49444154u) {
                idatChunkCount++;
                idatTotalBytes += (int)chunkLen;
                Breadcrumb("UEFI_PNG_PROBE_S1_IDAT_SEEN");
            } else if (chunkType == 0x49454E44u) {
                Breadcrumb("UEFI_PNG_PROBE_S1_IEND_SEEN");
                Value("UEFI_PNG_PROBE_S1_IDAT_CHUNK_COUNT", (ulong)(uint)idatChunkCount);
                Value("UEFI_PNG_PROBE_S1_IDAT_COMPRESSED_BYTES", (ulong)(uint)idatTotalBytes);
                Breadcrumb("UEFI_PNG_PROBE_S1_CHUNK_SCAN_EXIT");
                return;
            }

            pos += 12 + (int)chunkLen;
        }

        Breadcrumb("UEFI_PNG_PROBE_S1_CHUNK_SCAN_EXIT");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ProbeS2(byte[] bytes) {
        Breadcrumb("UEFI_PNG_PROBE_S2_ENTER");
        try {
            ProbeS2Body(bytes);
        } finally {
            Breadcrumb("UEFI_PNG_PROBE_S2_EXIT");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ProbeS2Body(byte[] bytes) {
        if (!TryReadHeader(bytes)) {
            return;
        }

        int width = (int)ReadU32BE(bytes, 16);
        int height = (int)ReadU32BE(bytes, 20);
        byte bitDepth = bytes[24];
        byte colorType = bytes[25];
        if (width <= 0 || height <= 0 || bitDepth != 8 || colorType != 6) {
            return;
        }

        Value("UEFI_PNG_PROBE_S2_LEN", (ulong)bytes.Length);
        Value("UEFI_PNG_PROBE_S2_IHDR_WIDTH", (ulong)(uint)width);
        Value("UEFI_PNG_PROBE_S2_IHDR_HEIGHT", (ulong)(uint)height);
        Value("UEFI_PNG_PROBE_S2_IHDR_BIT_DEPTH", (ulong)bitDepth);
        Value("UEFI_PNG_PROBE_S2_IHDR_COLOR_TYPE", (ulong)colorType);

        Breadcrumb("UEFI_PNG_PROBE_S2_IDAT_AGG_ENTER");
        byte[] compressedData = new byte[555];
        if (compressedData == null) {
            Breadcrumb("UEFI_PNG_PROBE_S2_IDAT_AGG_EXIT");
            return;
        }

        if (!TryAggregateIdat(bytes, 33, compressedData, out int idatChunkCount, out int idatTotalBytes) ||
            idatChunkCount != 1 ||
            idatTotalBytes != 555) {
            Breadcrumb("UEFI_PNG_PROBE_S2_IDAT_AGG_EXIT");
            return;
        }

        Value("UEFI_PNG_PROBE_S2_IDAT_CHUNK_COUNT", (ulong)(uint)idatChunkCount);
        Value("UEFI_PNG_PROBE_S2_IDAT_BYTES", (ulong)(uint)idatTotalBytes);
        Breadcrumb("UEFI_PNG_PROBE_S2_IDAT_AGG_EXIT");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ProbeS3(byte[] bytes) {
        Breadcrumb("UEFI_PNG_PROBE_S3_ENTER");
        try {
            ProbeS3Body(bytes);
        } finally {
            Breadcrumb("UEFI_PNG_PROBE_S3_EXIT");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ProbeS3Body(byte[] bytes) {
        if (!TryReadHeader(bytes)) {
            return;
        }

        int width = (int)ReadU32BE(bytes, 16);
        int height = (int)ReadU32BE(bytes, 20);
        byte bitDepth = bytes[24];
        byte colorType = bytes[25];
        if (width <= 0 || height <= 0 || bitDepth != 8 || colorType != 6) {
            return;
        }

        Value("UEFI_PNG_PROBE_S3_LEN", (ulong)bytes.Length);
        Value("UEFI_PNG_PROBE_S3_IHDR_WIDTH", (ulong)(uint)width);
        Value("UEFI_PNG_PROBE_S3_IHDR_HEIGHT", (ulong)(uint)height);
        Value("UEFI_PNG_PROBE_S3_IHDR_BIT_DEPTH", (ulong)bitDepth);
        Value("UEFI_PNG_PROBE_S3_IHDR_COLOR_TYPE", (ulong)colorType);

        Breadcrumb("UEFI_PNG_PROBE_S3_IDAT_AGG_ENTER");
        byte[] compressedData = new byte[555];
        if (compressedData == null) {
            Breadcrumb("UEFI_PNG_PROBE_S3_IDAT_AGG_EXIT");
            return;
        }

        if (!TryAggregateIdat(bytes, 33, compressedData, out int idatChunkCount, out int idatTotalBytes) ||
            idatChunkCount != 1 ||
            idatTotalBytes != 555) {
            Breadcrumb("UEFI_PNG_PROBE_S3_IDAT_AGG_EXIT");
            return;
        }

        Value("UEFI_PNG_PROBE_S3_IDAT_CHUNK_COUNT", (ulong)(uint)idatChunkCount);
        Value("UEFI_PNG_PROBE_S3_IDAT_BYTES", (ulong)(uint)idatTotalBytes);
        Breadcrumb("UEFI_PNG_PROBE_S3_IDAT_AGG_EXIT");

        byte cmf = compressedData[0];
        byte flg = compressedData[1];
        if ((cmf & 0x0F) != 8 ||
            (flg & 0x20) != 0 ||
            ((cmf * 256 + flg) % 31) != 0) {
            return;
        }

        Value("UEFI_PNG_PROBE_S3_ZLIB_CMF", (ulong)cmf);
        Value("UEFI_PNG_PROBE_S3_ZLIB_FLG", (ulong)flg);
        Breadcrumb("UEFI_PNG_PROBE_S3_ZLIB_HEADER_OK");

        Breadcrumb("UEFI_PNG_PROBE_S3_OUTPUT_ALLOC_ENTER");
        byte[] output = new byte[3164];
        if (output == null) {
            Breadcrumb("UEFI_PNG_PROBE_S3_OUTPUT_ALLOC_EXIT");
            return;
        }

        Value("UEFI_PNG_PROBE_S3_OUTPUT_ALLOC_SIZE", (ulong)(uint)output.Length);
        Breadcrumb("UEFI_PNG_PROBE_S3_OUTPUT_ALLOC_OK");
        Breadcrumb("UEFI_PNG_PROBE_S3_OUTPUT_ALLOC_EXIT");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ProbeS4(byte[] bytes) {
        Breadcrumb("UEFI_PNG_PROBE_S4_ENTER");
        try {
            ProbeS4Body(bytes);
        } finally {
            Breadcrumb("UEFI_PNG_PROBE_S4_EXIT");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ProbeS4Body(byte[] bytes) {
        if (!TryReadHeader(bytes)) {
            return;
        }

        int width = (int)ReadU32BE(bytes, 16);
        int height = (int)ReadU32BE(bytes, 20);
        byte bitDepth = bytes[24];
        byte colorType = bytes[25];
        if (width <= 0 || height <= 0 || bitDepth != 8 || colorType != 6) {
            return;
        }

        Value("UEFI_PNG_PROBE_S4_LEN", (ulong)bytes.Length);
        Value("UEFI_PNG_PROBE_S4_IHDR_WIDTH", (ulong)(uint)width);
        Value("UEFI_PNG_PROBE_S4_IHDR_HEIGHT", (ulong)(uint)height);
        Value("UEFI_PNG_PROBE_S4_IHDR_BIT_DEPTH", (ulong)bitDepth);
        Value("UEFI_PNG_PROBE_S4_IHDR_COLOR_TYPE", (ulong)colorType);

        Breadcrumb("UEFI_PNG_PROBE_S4_IDAT_AGG_ENTER");
        byte[] compressedData = new byte[555];
        if (compressedData == null) {
            Breadcrumb("UEFI_PNG_PROBE_S4_IDAT_AGG_EXIT");
            return;
        }

        if (!TryAggregateIdat(bytes, 33, compressedData, out int idatChunkCount, out int idatTotalBytes) ||
            idatChunkCount != 1 ||
            idatTotalBytes != 555) {
            Breadcrumb("UEFI_PNG_PROBE_S4_INFLATE_BOUNDS_ABORT");
            Breadcrumb("UEFI_PNG_PROBE_S4_IDAT_AGG_EXIT");
            return;
        }

        Value("UEFI_PNG_PROBE_S4_IDAT_CHUNK_COUNT", (ulong)(uint)idatChunkCount);
        Value("UEFI_PNG_PROBE_S4_IDAT_BYTES", (ulong)(uint)idatTotalBytes);
        Breadcrumb("UEFI_PNG_PROBE_S4_IDAT_AGG_EXIT");

        byte cmf = compressedData[0];
        byte flg = compressedData[1];
        if ((cmf & 0x0F) != 8 ||
            (flg & 0x20) != 0 ||
            ((cmf * 256 + flg) % 31) != 0) {
            return;
        }

        Value("UEFI_PNG_PROBE_S4_ZLIB_CMF", (ulong)cmf);
        Value("UEFI_PNG_PROBE_S4_ZLIB_FLG", (ulong)flg);
        Breadcrumb("UEFI_PNG_PROBE_S4_ZLIB_HEADER_OK");

        Breadcrumb("UEFI_PNG_PROBE_S4_OUTPUT_ALLOC_ENTER");
        byte[] output = new byte[3164];
        if (output == null) {
            Breadcrumb("UEFI_PNG_PROBE_S4_OUTPUT_ALLOC_EXIT");
            return;
        }

        Value("UEFI_PNG_PROBE_S4_OUTPUT_ALLOC_SIZE", (ulong)(uint)output.Length);
        Breadcrumb("UEFI_PNG_PROBE_S4_OUTPUT_ALLOC_OK");
        Breadcrumb("UEFI_PNG_PROBE_S4_OUTPUT_ALLOC_EXIT");

        Breadcrumb("UEFI_PNG_PROBE_S4_INFLATE_ENTER");
        try {
            Breadcrumb("UEFI_PNG_PROBE_S4_INFLATE_PROGRESS");
            if (TryInflateZlibSmoke(compressedData, idatTotalBytes, output, 3164)) {
                Breadcrumb("UEFI_PNG_PROBE_S4_INFLATE_OK");
            } else {
                Breadcrumb("UEFI_PNG_PROBE_S4_INFLATE_BOUNDS_ABORT");
            }
        } catch {
            Breadcrumb("UEFI_PNG_PROBE_S4_INFLATE_FAULT");
        } finally {
            Breadcrumb("UEFI_PNG_PROBE_S4_INFLATE_EXIT");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ProbeS4A(byte[] bytes) {
        Breadcrumb("UEFI_PNG_PROBE_S4A_ENTER");
        try {
            if (!TryPrepareStandaloneS4Metadata(bytes, "UEFI_PNG_PROBE_S4A", out byte[] compressedData, out int idatTotalBytes)) {
                return;
            }

            _ = compressedData;

            if (idatTotalBytes != 555) {
                return;
            }

            Breadcrumb("UEFI_PNG_PROBE_S4A_IDAT_LEN_OK");

            const int expectedOutputBytes = 3164;
            byte[] output = new byte[expectedOutputBytes];
            if (output == null || output.Length != expectedOutputBytes) {
                return;
            }

            Value("UEFI_PNG_PROBE_S4A_EXPECTED_OUT_LEN", (ulong)(uint)output.Length);
            Breadcrumb("UEFI_PNG_PROBE_S4A_EXPECTED_OUT_OK");
            Breadcrumb("UEFI_PNG_PROBE_S4A_AFTER_ZLIB_HEADER");
        } finally {
            Breadcrumb("UEFI_PNG_PROBE_S4A_EXIT");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ProbeS4C1StandaloneBody() {
        Breadcrumb("UEFI_PNG_PROBE_S4C1_ENTER");
        try {
            if (!TryPrepareStandaloneS4ProbeBody("UEFI_PNG_PROBE_S4C1", out byte[] compressedData, out int idatTotalBytes, out _)) {
                return;
            }

            byte rawCompressedByte = compressedData[2];
            Value("UEFI_PNG_PROBE_S4C1_RAW_BYTE", (ulong)rawCompressedByte);

            int bfinal = rawCompressedByte & 1;
            int btype = (rawCompressedByte >> 1) & 3;
            Value("UEFI_PNG_PROBE_S4C1_BFINAL", (ulong)(uint)bfinal);
            Value("UEFI_PNG_PROBE_S4C1_BTYPE", (ulong)(uint)btype);
            if (bfinal != 1 || btype != 2) {
                return;
            }

            Breadcrumb("UEFI_PNG_PROBE_S4C1_AFTER_BLOCK_HEADER");
            Breadcrumb("UEFI_PNG_PROBE_S4C1_BEFORE_COUNTS");

            _ = idatTotalBytes;
        } finally {
            Breadcrumb("UEFI_PNG_PROBE_S4C1_EXIT");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ProbeS4C2StandaloneBody() {
        Breadcrumb("UEFI_PNG_PROBE_S4C2_ENTER");
        try {
            if (!TryPrepareStandaloneS4ProbeBody("UEFI_PNG_PROBE_S4C2", out byte[] compressedData, out int idatTotalBytes, out _)) {
                return;
            }

            byte rawCompressedByte = compressedData[2];
            Value("UEFI_PNG_PROBE_S4C2_RAW_BYTE", (ulong)rawCompressedByte);

            int bfinal = rawCompressedByte & 1;
            int btype = (rawCompressedByte >> 1) & 3;
            Value("UEFI_PNG_PROBE_S4C2_BFINAL", (ulong)(uint)bfinal);
            Value("UEFI_PNG_PROBE_S4C2_BTYPE", (ulong)(uint)btype);
            if (bfinal != 1 || btype != 2) {
                return;
            }

            Breadcrumb("UEFI_PNG_PROBE_S4C2_AFTER_BLOCK_HEADER");
            Breadcrumb("UEFI_PNG_PROBE_S4C2_BEFORE_COUNTS");

            int hlitBits = (compressedData[2] >> 3) & 0x1F;
            Value("UEFI_PNG_PROBE_S4C2_HLIT_BITS", (ulong)(uint)hlitBits);
            int hlit = hlitBits + 257;
            Value("UEFI_PNG_PROBE_S4C2_HLIT", (ulong)(uint)hlit);

            _ = idatTotalBytes;
        } finally {
            Breadcrumb("UEFI_PNG_PROBE_S4C2_EXIT");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ProbeS4C3StandaloneBody() {
        Breadcrumb("UEFI_PNG_PROBE_S4C3_ENTER");
        try {
            if (!TryPrepareStandaloneS4ProbeBody("UEFI_PNG_PROBE_S4C3", out byte[] compressedData, out int idatTotalBytes, out _)) {
                return;
            }

            byte rawCompressedByte = compressedData[2];
            Value("UEFI_PNG_PROBE_S4C3_RAW_BYTE", (ulong)rawCompressedByte);

            int bfinal = rawCompressedByte & 1;
            int btype = (rawCompressedByte >> 1) & 3;
            Value("UEFI_PNG_PROBE_S4C3_BFINAL", (ulong)(uint)bfinal);
            Value("UEFI_PNG_PROBE_S4C3_BTYPE", (ulong)(uint)btype);
            if (bfinal != 1 || btype != 2) {
                return;
            }

            Breadcrumb("UEFI_PNG_PROBE_S4C3_AFTER_BLOCK_HEADER");
            Breadcrumb("UEFI_PNG_PROBE_S4C3_BEFORE_COUNTS");

            int hlitBits = (compressedData[2] >> 3) & 0x1F;
            Value("UEFI_PNG_PROBE_S4C3_HLIT_BITS", (ulong)(uint)hlitBits);
            int hlit = hlitBits + 257;
            Value("UEFI_PNG_PROBE_S4C3_HLIT", (ulong)(uint)hlit);

            int hdistBits = compressedData[3] & 0x1F;
            Value("UEFI_PNG_PROBE_S4C3_HDIST_BITS", (ulong)(uint)hdistBits);
            int hdist = hdistBits + 1;
            Value("UEFI_PNG_PROBE_S4C3_HDIST", (ulong)(uint)hdist);

            _ = idatTotalBytes;
        } finally {
            Breadcrumb("UEFI_PNG_PROBE_S4C3_EXIT");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ProbeS4C4StandaloneBody() {
        Breadcrumb("UEFI_PNG_PROBE_S4C4_ENTER");
        try {
            if (!TryPrepareStandaloneS4ProbeBody("UEFI_PNG_PROBE_S4C4", out byte[] compressedData, out int idatTotalBytes, out _)) {
                return;
            }

            byte rawCompressedByte = compressedData[2];
            Value("UEFI_PNG_PROBE_S4C4_RAW_BYTE", (ulong)rawCompressedByte);

            int bfinal = rawCompressedByte & 1;
            int btype = (rawCompressedByte >> 1) & 3;
            Value("UEFI_PNG_PROBE_S4C4_BFINAL", (ulong)(uint)bfinal);
            Value("UEFI_PNG_PROBE_S4C4_BTYPE", (ulong)(uint)btype);
            if (bfinal != 1 || btype != 2) {
                return;
            }

            Breadcrumb("UEFI_PNG_PROBE_S4C4_AFTER_BLOCK_HEADER");
            Breadcrumb("UEFI_PNG_PROBE_S4C4_BEFORE_COUNTS");

            int hlitBits = (compressedData[2] >> 3) & 0x1F;
            Value("UEFI_PNG_PROBE_S4C4_HLIT_BITS", (ulong)(uint)hlitBits);
            int hlit = hlitBits + 257;
            Value("UEFI_PNG_PROBE_S4C4_HLIT", (ulong)(uint)hlit);

            int hdistBits = compressedData[3] & 0x1F;
            Value("UEFI_PNG_PROBE_S4C4_HDIST_BITS", (ulong)(uint)hdistBits);
            int hdist = hdistBits + 1;
            Value("UEFI_PNG_PROBE_S4C4_HDIST", (ulong)(uint)hdist);

            int hclenBits = ((compressedData[3] >> 5) & 0x07) | ((compressedData[4] & 0x01) << 3);
            Value("UEFI_PNG_PROBE_S4C4_HCLEN_BITS", (ulong)(uint)hclenBits);
            int hclen = hclenBits + 4;
            Value("UEFI_PNG_PROBE_S4C4_HCLEN", (ulong)(uint)hclen);

            bool countsOk = hlit >= 257 && hlit <= 286 && hdist >= 1 && hdist <= 32 && hclen >= 4 && hclen <= 19;
            Breadcrumb(countsOk ? "UEFI_PNG_PROBE_S4C4_COUNTS_OK" : "UEFI_PNG_PROBE_S4C4_COUNTS_BAD");

            _ = idatTotalBytes;
        } finally {
            Breadcrumb("UEFI_PNG_PROBE_S4C4_EXIT");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ProbeS4D0StandaloneBody() {
        Breadcrumb("UEFI_PNG_PROBE_S4D0_ENTER");
        try {
            if (!TryPrepareStandaloneS4ProbeBody("UEFI_PNG_PROBE_S4D0", out byte[] compressedData, out int idatTotalBytes, out _)) {
                return;
            }

            byte rawCompressedByte = compressedData[2];
            Value("UEFI_PNG_PROBE_S4D0_RAW_BYTE", (ulong)rawCompressedByte);

            int bfinal = rawCompressedByte & 1;
            int btype = (rawCompressedByte >> 1) & 3;
            Value("UEFI_PNG_PROBE_S4D0_BFINAL", (ulong)(uint)bfinal);
            Value("UEFI_PNG_PROBE_S4D0_BTYPE", (ulong)(uint)btype);
            if (bfinal != 1 || btype != 2) {
                return;
            }

            Breadcrumb("UEFI_PNG_PROBE_S4D0_AFTER_BLOCK_HEADER");
            Breadcrumb("UEFI_PNG_PROBE_S4D0_BEFORE_COUNTS");

            int hlitBits = (compressedData[2] >> 3) & 0x1F;
            Value("UEFI_PNG_PROBE_S4D0_HLIT_BITS", (ulong)(uint)hlitBits);
            int hlit = hlitBits + 257;
            Value("UEFI_PNG_PROBE_S4D0_HLIT", (ulong)(uint)hlit);

            int hdistBits = compressedData[3] & 0x1F;
            Value("UEFI_PNG_PROBE_S4D0_HDIST_BITS", (ulong)(uint)hdistBits);
            int hdist = hdistBits + 1;
            Value("UEFI_PNG_PROBE_S4D0_HDIST", (ulong)(uint)hdist);

            int hclenBits = ((compressedData[3] >> 5) & 0x07) | ((compressedData[4] & 0x01) << 3);
            Value("UEFI_PNG_PROBE_S4D0_HCLEN_BITS", (ulong)(uint)hclenBits);
            int hclen = hclenBits + 4;
            Value("UEFI_PNG_PROBE_S4D0_HCLEN", (ulong)(uint)hclen);

            if (hlit != 283 || hdist != 23 || hclen != 18) {
                return;
            }

            Breadcrumb("UEFI_PNG_PROBE_S4D0_AFTER_COUNTS");
            Breadcrumb("UEFI_PNG_PROBE_S4D0_BEFORE_CODELEN_ALPHABET");

            _ = idatTotalBytes;
        } finally {
            Breadcrumb("UEFI_PNG_PROBE_S4D0_EXIT");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ProbeS4D1StandaloneBody() {
        Breadcrumb("UEFI_PNG_PROBE_S4D1_ENTER");
        try {
            if (!TryPrepareStandaloneS4ProbeBody("UEFI_PNG_PROBE_S4D1", out byte[] compressedData, out int idatTotalBytes, out _)) {
                return;
            }

            byte rawCompressedByte = compressedData[2];
            Value("UEFI_PNG_PROBE_S4D1_RAW_BYTE", (ulong)rawCompressedByte);

            int bfinal = rawCompressedByte & 1;
            int btype = (rawCompressedByte >> 1) & 3;
            Value("UEFI_PNG_PROBE_S4D1_BFINAL", (ulong)(uint)bfinal);
            Value("UEFI_PNG_PROBE_S4D1_BTYPE", (ulong)(uint)btype);
            if (bfinal != 1 || btype != 2) {
                return;
            }

            Breadcrumb("UEFI_PNG_PROBE_S4D1_AFTER_BLOCK_HEADER");
            Breadcrumb("UEFI_PNG_PROBE_S4D1_BEFORE_COUNTS");

            int hlitBits = (compressedData[2] >> 3) & 0x1F;
            Value("UEFI_PNG_PROBE_S4D1_HLIT_BITS", (ulong)(uint)hlitBits);
            int hlit = hlitBits + 257;
            Value("UEFI_PNG_PROBE_S4D1_HLIT", (ulong)(uint)hlit);

            int hdistBits = compressedData[3] & 0x1F;
            Value("UEFI_PNG_PROBE_S4D1_HDIST_BITS", (ulong)(uint)hdistBits);
            int hdist = hdistBits + 1;
            Value("UEFI_PNG_PROBE_S4D1_HDIST", (ulong)(uint)hdist);

            int hclenBits = ((compressedData[3] >> 5) & 0x07) | ((compressedData[4] & 0x01) << 3);
            Value("UEFI_PNG_PROBE_S4D1_HCLEN_BITS", (ulong)(uint)hclenBits);
            int hclen = hclenBits + 4;
            Value("UEFI_PNG_PROBE_S4D1_HCLEN", (ulong)(uint)hclen);

            if (hlit != 283 || hdist != 23 || hclen != 18) {
                return;
            }

            Breadcrumb("UEFI_PNG_PROBE_S4D1_AFTER_COUNTS");
            Breadcrumb("UEFI_PNG_PROBE_S4D1_BEFORE_CODELEN_ALPHABET");
            Value("UEFI_PNG_PROBE_S4D1_SYMBOL", 16UL);

            int bitOffset = 17;
            int byteIndex = 2 + (bitOffset >> 3);
            int bitShift = bitOffset & 7;
            int codeLengthValue = compressedData[byteIndex] >> bitShift;
            if (bitShift > 5) {
                codeLengthValue |= compressedData[byteIndex + 1] << (8 - bitShift);
            }
            codeLengthValue &= 0x07;
            Value("UEFI_PNG_PROBE_S4D1_VALUE", (ulong)(uint)codeLengthValue);
        } finally {
            Breadcrumb("UEFI_PNG_PROBE_S4D1_EXIT");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ProbeS4D2StandaloneBody() {
        Breadcrumb("UEFI_PNG_PROBE_S4D2_ENTER");
        try {
            if (!TryPrepareStandaloneS4ProbeBody("UEFI_PNG_PROBE_S4D2", out byte[] compressedData, out int idatTotalBytes, out _)) {
                return;
            }

            byte rawCompressedByte = compressedData[2];
            Value("UEFI_PNG_PROBE_S4D2_RAW_BYTE", (ulong)rawCompressedByte);

            int bfinal = rawCompressedByte & 1;
            int btype = (rawCompressedByte >> 1) & 3;
            Value("UEFI_PNG_PROBE_S4D2_BFINAL", (ulong)(uint)bfinal);
            Value("UEFI_PNG_PROBE_S4D2_BTYPE", (ulong)(uint)btype);
            if (bfinal != 1 || btype != 2) {
                return;
            }

            Breadcrumb("UEFI_PNG_PROBE_S4D2_AFTER_BLOCK_HEADER");
            Breadcrumb("UEFI_PNG_PROBE_S4D2_BEFORE_COUNTS");

            int hlitBits = (compressedData[2] >> 3) & 0x1F;
            Value("UEFI_PNG_PROBE_S4D2_HLIT_BITS", (ulong)(uint)hlitBits);
            int hlit = hlitBits + 257;
            Value("UEFI_PNG_PROBE_S4D2_HLIT", (ulong)(uint)hlit);

            int hdistBits = compressedData[3] & 0x1F;
            Value("UEFI_PNG_PROBE_S4D2_HDIST_BITS", (ulong)(uint)hdistBits);
            int hdist = hdistBits + 1;
            Value("UEFI_PNG_PROBE_S4D2_HDIST", (ulong)(uint)hdist);

            int hclenBits = ((compressedData[3] >> 5) & 0x07) | ((compressedData[4] & 0x01) << 3);
            Value("UEFI_PNG_PROBE_S4D2_HCLEN_BITS", (ulong)(uint)hclenBits);
            int hclen = hclenBits + 4;
            Value("UEFI_PNG_PROBE_S4D2_HCLEN", (ulong)(uint)hclen);

            if (hlit != 283 || hdist != 23 || hclen != 18) {
                return;
            }

            Breadcrumb("UEFI_PNG_PROBE_S4D2_AFTER_COUNTS");
            Breadcrumb("UEFI_PNG_PROBE_S4D2_BEFORE_CODELEN_ALPHABET");

            int countRead = 0;
            int nonzeroCount = 0;
            int bitOffset = 17;
            for (int i = 0; i < 4; i++) {
                int byteIndex = 2 + (bitOffset >> 3);
                int bitShift = bitOffset & 7;
                int codeLengthValue = compressedData[byteIndex] >> bitShift;
                if (bitShift > 5) {
                    codeLengthValue |= compressedData[byteIndex + 1] << (8 - bitShift);
                }

                codeLengthValue &= 0x07;
                if (codeLengthValue != 0) {
                    nonzeroCount++;
                }

                countRead++;
                bitOffset += 3;
            }

            Value("UEFI_PNG_PROBE_S4D2_COUNT_READ", (ulong)(uint)countRead);
            Value("UEFI_PNG_PROBE_S4D2_NONZERO_COUNT", (ulong)(uint)nonzeroCount);
            if (countRead != 4) {
                return;
            }

            _ = idatTotalBytes;
        } finally {
            Breadcrumb("UEFI_PNG_PROBE_S4D2_EXIT");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ProbeS4D3StandaloneBody() {
        Breadcrumb("UEFI_PNG_PROBE_S4D3_ENTER");
        try {
            if (!TryPrepareStandaloneS4ProbeBody("UEFI_PNG_PROBE_S4D3", out byte[] compressedData, out int idatTotalBytes, out _)) {
                return;
            }

            byte rawCompressedByte = compressedData[2];
            Value("UEFI_PNG_PROBE_S4D3_RAW_BYTE", (ulong)rawCompressedByte);

            int bfinal = rawCompressedByte & 1;
            int btype = (rawCompressedByte >> 1) & 3;
            Value("UEFI_PNG_PROBE_S4D3_BFINAL", (ulong)(uint)bfinal);
            Value("UEFI_PNG_PROBE_S4D3_BTYPE", (ulong)(uint)btype);
            if (bfinal != 1 || btype != 2) {
                return;
            }

            Breadcrumb("UEFI_PNG_PROBE_S4D3_AFTER_BLOCK_HEADER");
            Breadcrumb("UEFI_PNG_PROBE_S4D3_BEFORE_COUNTS");

            int hlitBits = (compressedData[2] >> 3) & 0x1F;
            Value("UEFI_PNG_PROBE_S4D3_HLIT_BITS", (ulong)(uint)hlitBits);
            int hlit = hlitBits + 257;
            Value("UEFI_PNG_PROBE_S4D3_HLIT", (ulong)(uint)hlit);

            int hdistBits = compressedData[3] & 0x1F;
            Value("UEFI_PNG_PROBE_S4D3_HDIST_BITS", (ulong)(uint)hdistBits);
            int hdist = hdistBits + 1;
            Value("UEFI_PNG_PROBE_S4D3_HDIST", (ulong)(uint)hdist);

            int hclenBits = ((compressedData[3] >> 5) & 0x07) | ((compressedData[4] & 0x01) << 3);
            Value("UEFI_PNG_PROBE_S4D3_HCLEN_BITS", (ulong)(uint)hclenBits);
            int hclen = hclenBits + 4;
            Value("UEFI_PNG_PROBE_S4D3_HCLEN", (ulong)(uint)hclen);

            if (hlit != 283 || hdist != 23 || hclen != 18) {
                return;
            }

            Breadcrumb("UEFI_PNG_PROBE_S4D3_AFTER_COUNTS");
            Breadcrumb("UEFI_PNG_PROBE_S4D3_BEFORE_CODELEN_ALPHABET");

            int countRead = 0;
            int nonzeroCount = 0;
            int maxValue = 0;
            int bitOffset = 17;
            for (int i = 0; i < 18; i++) {
                int byteIndex = 2 + (bitOffset >> 3);
                int bitShift = bitOffset & 7;
                int codeLengthValue = compressedData[byteIndex] >> bitShift;
                if (bitShift > 5) {
                    codeLengthValue |= compressedData[byteIndex + 1] << (8 - bitShift);
                }

                codeLengthValue &= 0x07;
                if (codeLengthValue != 0) {
                    nonzeroCount++;
                }

                if (codeLengthValue > maxValue) {
                    maxValue = codeLengthValue;
                }

                countRead++;
                bitOffset += 3;
            }

            Value("UEFI_PNG_PROBE_S4D3_COUNT_READ", (ulong)(uint)countRead);
            Value("UEFI_PNG_PROBE_S4D3_NONZERO_COUNT", (ulong)(uint)nonzeroCount);
            Value("UEFI_PNG_PROBE_S4D3_MAX_VALUE", (ulong)(uint)maxValue);

            bool valuesOk = countRead == 18 && nonzeroCount > 0 && maxValue <= 7;
            Breadcrumb(valuesOk ? "UEFI_PNG_PROBE_S4D3_VALUES_OK" : "UEFI_PNG_PROBE_S4D3_VALUES_BAD");
            if (!valuesOk) {
                return;
            }

            _ = idatTotalBytes;
        } finally {
            Breadcrumb("UEFI_PNG_PROBE_S4D3_EXIT");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ProbeS4B1Body() {
        Breadcrumb("UEFI_PNG_PROBE_S4B1_ENTER");
        try {
        } finally {
            Breadcrumb("UEFI_PNG_PROBE_S4B1_EXIT");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ProbeS4B2Body(int pngLen, int width, int height, int idatLen, int expectedOut) {
        Breadcrumb("UEFI_PNG_PROBE_S4B2_ENTER");
        try {
            if (pngLen != 1070 ||
                width != 28 ||
                height != 28 ||
                idatLen != 555 ||
                expectedOut != 3164) {
                return;
            }

            Breadcrumb("UEFI_PNG_PROBE_S4B2_VALUES_OK");
        } finally {
            Breadcrumb("UEFI_PNG_PROBE_S4B2_EXIT");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ProbeS4B3Body(byte[] bytes) {
        Breadcrumb("UEFI_PNG_PROBE_S4B3_ENTER");
        try {
            if (bytes == null || bytes.Length != 1070) {
                return;
            }

            Breadcrumb("UEFI_PNG_PROBE_S4B3_BYTES_OK");
            Value("UEFI_PNG_PROBE_S4B3_LEN", (ulong)(uint)bytes.Length);
        } finally {
            Breadcrumb("UEFI_PNG_PROBE_S4B3_EXIT");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ProbeS4B4Body(byte[] bytes) {
        Breadcrumb("UEFI_PNG_PROBE_S4B4_ENTER");
        try {
            if (!TryPrepareStandaloneS4Setup(bytes, "UEFI_PNG_PROBE_S4B4", out byte[] compressedData, out int idatTotalBytes, out byte[] output)) {
                return;
            }

            _ = compressedData;
            _ = idatTotalBytes;
            _ = output;
            Breadcrumb("UEFI_PNG_PROBE_S4B4_AFTER_S4A_SETUP");
            Breadcrumb("UEFI_PNG_PROBE_S4B4_BEFORE_BLOCK_HEADER");
        } finally {
            Breadcrumb("UEFI_PNG_PROBE_S4B4_EXIT");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ProbeS4B5Body(byte[] bytes) {
        Breadcrumb("UEFI_PNG_PROBE_S4B5_ENTER");
        try {
            if (!TryPrepareStandaloneS4Setup(bytes, "UEFI_PNG_PROBE_S4B5", out byte[] compressedData, out int idatTotalBytes, out byte[] output)) {
                return;
            }

            _ = output;
            if (!TryReadStandaloneS4BlockHeader(compressedData, idatTotalBytes, "UEFI_PNG_PROBE_S4B5", out int bfinal, out int btype)) {
                return;
            }

            if (bfinal != 1 || btype != 2) {
                return;
            }

            Breadcrumb("UEFI_PNG_PROBE_S4B5_DYNAMIC");
        } finally {
            Breadcrumb("UEFI_PNG_PROBE_S4B5_EXIT");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool TryPrepareStandaloneS4ProbeBody(string prefix, out byte[] compressedData, out int idatTotalBytes, out byte[] output) {
        byte[] bytes = s_standaloneProbeBytes;
        compressedData = null;
        idatTotalBytes = 0;
        output = null;

        if (!TryReadHeader(bytes)) {
            return false;
        }

        int width = (int)ReadU32BE(bytes, 16);
        int height = (int)ReadU32BE(bytes, 20);
        byte bitDepth = bytes[24];
        byte colorType = bytes[25];
        Value(prefix + "_LEN", (ulong)bytes.Length);
        Value(prefix + "_IHDR_WIDTH", (ulong)(uint)width);
        Value(prefix + "_IHDR_HEIGHT", (ulong)(uint)height);
        Value(prefix + "_IHDR_BIT_DEPTH", (ulong)bitDepth);
        Value(prefix + "_IHDR_COLOR_TYPE", (ulong)colorType);

        if (width <= 0 || height <= 0 || bitDepth != 8 || colorType != 6) {
            return false;
        }

        Breadcrumb(prefix + "_IDAT_AGG_ENTER");
        compressedData = new byte[555];
        if (compressedData == null) {
            Breadcrumb(prefix + "_IDAT_AGG_EXIT");
            return false;
        }

        if (!TryAggregateIdat(bytes, 33, compressedData, out int idatChunkCount, out idatTotalBytes) ||
            idatChunkCount != 1 ||
            idatTotalBytes != 555) {
            Breadcrumb(prefix + "_IDAT_AGG_EXIT");
            return false;
        }

        Value(prefix + "_IDAT_CHUNK_COUNT", (ulong)(uint)idatChunkCount);
        Value(prefix + "_IDAT_BYTES", (ulong)(uint)idatTotalBytes);
        Breadcrumb(prefix + "_IDAT_AGG_EXIT");

        byte cmf = compressedData[0];
        byte flg = compressedData[1];
        if ((cmf & 0x0F) != 8 ||
            (flg & 0x20) != 0 ||
            ((cmf * 256 + flg) % 31) != 0) {
            return false;
        }

        Value(prefix + "_ZLIB_CMF", (ulong)cmf);
        Value(prefix + "_ZLIB_FLG", (ulong)flg);
        Breadcrumb(prefix + "_ZLIB_HEADER_OK");

        const int expectedOutputBytes = 3164;
        output = new byte[expectedOutputBytes];
        if (output == null || output.Length != expectedOutputBytes) {
            return false;
        }

        Value(prefix + "_EXPECTED_OUT_LEN", (ulong)(uint)output.Length);
        Breadcrumb(prefix + "_EXPECTED_OUT_OK");
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ProbeS4B6Body() {
        Breadcrumb("UEFI_PNG_PROBE_S4B6_ENTER");
        try {
            if (!TryPrepareStandaloneS4ProbeBody("UEFI_PNG_PROBE_S4B6", out _, out _, out _)) {
                return;
            }

            Breadcrumb("UEFI_PNG_PROBE_S4B6_AFTER_S4A_SETUP");
            Breadcrumb("UEFI_PNG_PROBE_S4B6_BEFORE_BLOCK_HEADER");
        } finally {
            Breadcrumb("UEFI_PNG_PROBE_S4B6_EXIT");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ProbeS4B7RBody() {
        Breadcrumb("UEFI_PNG_PROBE_S4B7R_ENTER");
        try {
        } finally {
            Breadcrumb("UEFI_PNG_PROBE_S4B7R_EXIT");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ProbeS4B7SBody() {
        Breadcrumb("UEFI_PNG_PROBE_S4B7S_ENTER");
        try {
            if (!TryPrepareStandaloneS4ProbeBody("UEFI_PNG_PROBE_S4B7S", out _, out _, out _)) {
                return;
            }

            Breadcrumb("UEFI_PNG_PROBE_S4B7S_AFTER_S4A_SETUP");
            Breadcrumb("UEFI_PNG_PROBE_S4B7S_BEFORE_BLOCK_HEADER");
        } finally {
            Breadcrumb("UEFI_PNG_PROBE_S4B7S_EXIT");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ProbeS4B7TBody() {
        Breadcrumb("UEFI_PNG_PROBE_S4B7T_ENTER");
        try {
            if (!TryPrepareStandaloneS4ProbeBody("UEFI_PNG_PROBE_S4B7T", out byte[] compressedData, out int idatTotalBytes, out _)) {
                return;
            }

            byte rawCompressedByte = compressedData[2];
            Value("UEFI_PNG_PROBE_S4B7T_RAW_BYTE", (ulong)rawCompressedByte);
            _ = idatTotalBytes;
        } finally {
            Breadcrumb("UEFI_PNG_PROBE_S4B7T_EXIT");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ProbeS4B7UBody() {
        Breadcrumb("UEFI_PNG_PROBE_S4B7U_ENTER");
        try {
            if (!TryPrepareStandaloneS4ProbeBody("UEFI_PNG_PROBE_S4B7U", out byte[] compressedData, out int idatTotalBytes, out _)) {
                return;
            }

            byte rawCompressedByte = compressedData[2];
            int bfinal = rawCompressedByte & 1;
            int btype = (rawCompressedByte >> 1) & 3;
            Value("UEFI_PNG_PROBE_S4B7U_BFINAL", (ulong)(uint)bfinal);
            Value("UEFI_PNG_PROBE_S4B7U_BTYPE", (ulong)(uint)btype);
            Breadcrumb("UEFI_PNG_PROBE_S4B7U_DYNAMIC");
            _ = idatTotalBytes;
        } finally {
            Breadcrumb("UEFI_PNG_PROBE_S4B7U_EXIT");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ProbeS4B7Body() {
        Breadcrumb("UEFI_PNG_PROBE_S4B7_ENTER");
        try {
            if (!TryPrepareStandaloneS4ProbeBody("UEFI_PNG_PROBE_S4B7", out byte[] compressedData, out int idatTotalBytes, out _)) {
                return;
            }

            Breadcrumb("UEFI_PNG_PROBE_S4B7_AFTER_S4A_SETUP");
            Breadcrumb("UEFI_PNG_PROBE_S4B7_BEFORE_BLOCK_HEADER");

            byte deflate0 = compressedData[2];
            int bfinal = deflate0 & 1;
            int btype = (deflate0 >> 1) & 3;
            Value("UEFI_PNG_PROBE_S4B7_BFINAL", (ulong)(uint)bfinal);
            Value("UEFI_PNG_PROBE_S4B7_BTYPE", (ulong)(uint)btype);
            Breadcrumb("UEFI_PNG_PROBE_S4B7_DYNAMIC");
            _ = idatTotalBytes;
        } finally {
            Breadcrumb("UEFI_PNG_PROBE_S4B7_EXIT");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ProbeS4B8Body() {
        if (!TryPrepareStandaloneS4ProbeBody("UEFI_PNG_PROBE_S4B8", out byte[] compressedData, out int idatTotalBytes, out _)) {
            return;
        }

        Breadcrumb("UEFI_PNG_PROBE_S4B8_AFTER_S4A_SETUP");

        byte deflate0 = compressedData[2];
        ProbeS4B8PrimitiveBitReader(0, 0, deflate0, compressedData[3], compressedData[4], compressedData[5]);
        _ = idatTotalBytes;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ProbeS4B8PrimitiveBitReader(int bytePosition, int bitCount, byte compressed0, byte compressed1, byte compressed2, byte compressed3) {
        Breadcrumb("UEFI_PNG_PROBE_S4B8_ENTER");
        try {
            _ = bytePosition;
            _ = bitCount;
            _ = compressed1;
            _ = compressed2;
            _ = compressed3;

            int bfinal = compressed0 & 1;
            int btype = (compressed0 >> 1) & 3;
            Value("UEFI_PNG_PROBE_S4B8_BFINAL", (ulong)(uint)bfinal);
            Value("UEFI_PNG_PROBE_S4B8_BTYPE", (ulong)(uint)btype);
        } finally {
            Breadcrumb("UEFI_PNG_PROBE_S4B8_EXIT");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ProbeS4B(byte[] bytes) {
        Breadcrumb("UEFI_PNG_PROBE_S4B_ENTER");
        try {
            if (!TryPrepareStandaloneS4Metadata(bytes, "UEFI_PNG_PROBE_S4B", out byte[] compressedData, out int idatTotalBytes)) {
                return;
            }

            const int expectedOutputBytes = 3164;
            byte[] output = new byte[expectedOutputBytes];
            if (output == null || output.Length != expectedOutputBytes) {
                return;
            }

            Value("UEFI_PNG_PROBE_S4B_EXPECTED_OUT_LEN", (ulong)(uint)output.Length);
            Breadcrumb("UEFI_PNG_PROBE_S4B_EXPECTED_OUT_OK");
            Breadcrumb("UEFI_PNG_PROBE_S4B_AFTER_ZLIB_HEADER");

            if (!ProbeStandaloneS4BBlockHeader(compressedData, idatTotalBytes, out int bfinal, out int btype)) {
                return;
            }

            if (bfinal != 1 || btype != 2) {
                return;
            }

            Breadcrumb("UEFI_PNG_PROBE_S4B_DYNAMIC");
        } finally {
            Breadcrumb("UEFI_PNG_PROBE_S4B_EXIT");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ProbeS4C(byte[] bytes) {
        Breadcrumb("UEFI_PNG_PROBE_S4C_ENTER");
        try {
            if (!TryPrepareStandaloneS4Metadata(bytes, "UEFI_PNG_PROBE_S4C", out byte[] compressedData, out int idatTotalBytes)) {
                return;
            }

            const int expectedOutputBytes = 3164;
            byte[] output = new byte[expectedOutputBytes];
            if (output == null || output.Length != expectedOutputBytes) {
                return;
            }

            Value("UEFI_PNG_PROBE_S4C_EXPECTED_OUT_LEN", (ulong)(uint)output.Length);
            Breadcrumb("UEFI_PNG_PROBE_S4C_EXPECTED_OUT_OK");
            Breadcrumb("UEFI_PNG_PROBE_S4C_AFTER_ZLIB_HEADER");

            if (!ProbeStandaloneS4CBlockHeader(compressedData, idatTotalBytes, out int bfinal, out int btype)) {
                return;
            }

            if (btype != 2) {
                Breadcrumb("UEFI_PNG_PROBE_S4C_BTYPE_BAD");
                return;
            }

            _ = bfinal;

            if (!ProbeStandaloneS4CDynamicCounts(compressedData, idatTotalBytes, out int hlit, out int hdist, out int hclen)) {
                return;
            }

            _ = hlit;
            _ = hdist;
            _ = hclen;

            Breadcrumb("UEFI_PNG_PROBE_S4C_COUNTS_OK");
        } finally {
            Breadcrumb("UEFI_PNG_PROBE_S4C_EXIT");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool ProbeStandaloneS4BBlockHeader(byte[] compressedData, int idatTotalBytes, out int bfinal, out int btype) {
        return TryReadStandaloneS4BlockHeader(compressedData, idatTotalBytes, "UEFI_PNG_PROBE_S4B", out bfinal, out btype);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool ProbeStandaloneS4CBlockHeader(byte[] compressedData, int idatTotalBytes, out int bfinal, out int btype) {
        return TryReadStandaloneS4BlockHeader(compressedData, idatTotalBytes, "UEFI_PNG_PROBE_S4C", out bfinal, out btype);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool ProbeStandaloneS4CDynamicCounts(byte[] compressedData, int idatTotalBytes, out int hlit, out int hdist, out int hclen) {
        return TryReadStandaloneS4DynamicCounts(compressedData, idatTotalBytes, "UEFI_PNG_PROBE_S4C", out hlit, out hdist, out hclen);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool TryReadStandaloneS4BlockHeader(byte[] compressedData, int idatTotalBytes, string prefix, out int bfinal, out int btype) {
        bfinal = 0;
        btype = 0;
        if (compressedData == null || idatTotalBytes < 6) {
            return false;
        }

        unsafe {
            fixed (byte* compressedPtr = compressedData) {
                ZlibBitReader reader = new ZlibBitReader(compressedPtr + 2, idatTotalBytes - 6);
                return TryReadStandaloneBlockHeader(ref reader, prefix, out bfinal, out btype);
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool TryReadStandaloneS4DynamicCounts(byte[] compressedData, int idatTotalBytes, string prefix, out int hlit, out int hdist, out int hclen) {
        hlit = 0;
        hdist = 0;
        hclen = 0;
        if (compressedData == null || idatTotalBytes < 6) {
            return false;
        }

        unsafe {
            fixed (byte* compressedPtr = compressedData) {
                ZlibBitReader reader = new ZlibBitReader(compressedPtr + 2, idatTotalBytes - 6);
                if (!TryReadStandaloneBlockHeader(ref reader, prefix, out _, out int btype)) {
                    return false;
                }

                if (btype != 2) {
                    return false;
                }

                return TryReadStandaloneDynamicCounts(ref reader, prefix, out hlit, out hdist, out hclen);
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool TryPrepareStandaloneS4Metadata(byte[] bytes, string prefix, out byte[] compressedData, out int idatTotalBytes) {
        compressedData = null;
        idatTotalBytes = 0;

        if (!TryReadHeader(bytes)) {
            return false;
        }

        int width = (int)ReadU32BE(bytes, 16);
        int height = (int)ReadU32BE(bytes, 20);
        byte bitDepth = bytes[24];
        byte colorType = bytes[25];
        Value(prefix + "_LEN", (ulong)bytes.Length);
        Value(prefix + "_IHDR_WIDTH", (ulong)(uint)width);
        Value(prefix + "_IHDR_HEIGHT", (ulong)(uint)height);
        Value(prefix + "_IHDR_BIT_DEPTH", (ulong)bitDepth);
        Value(prefix + "_IHDR_COLOR_TYPE", (ulong)colorType);

        if (width <= 0 || height <= 0 || bitDepth != 8 || colorType != 6) {
            return false;
        }

        Breadcrumb(prefix + "_IDAT_AGG_ENTER");
        compressedData = new byte[555];
        if (compressedData == null) {
            Breadcrumb(prefix + "_IDAT_AGG_EXIT");
            return false;
        }

        if (!TryAggregateIdat(bytes, 33, compressedData, out int idatChunkCount, out idatTotalBytes) ||
            idatChunkCount != 1 ||
            idatTotalBytes != 555) {
            Breadcrumb(prefix + "_IDAT_AGG_EXIT");
            return false;
        }

        Value(prefix + "_IDAT_CHUNK_COUNT", (ulong)(uint)idatChunkCount);
        Value(prefix + "_IDAT_BYTES", (ulong)(uint)idatTotalBytes);
        Breadcrumb(prefix + "_IDAT_AGG_EXIT");

        byte cmf = compressedData[0];
        byte flg = compressedData[1];
        if ((cmf & 0x0F) != 8 ||
            (flg & 0x20) != 0 ||
            ((cmf * 256 + flg) % 31) != 0) {
            return false;
        }

        Value(prefix + "_ZLIB_CMF", (ulong)cmf);
        Value(prefix + "_ZLIB_FLG", (ulong)flg);
        Breadcrumb(prefix + "_ZLIB_HEADER_OK");
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool TryPrepareStandaloneS4Setup(byte[] bytes, string prefix, out byte[] compressedData, out int idatTotalBytes, out byte[] output) {
        compressedData = null;
        idatTotalBytes = 0;
        output = null;

        if (!TryPrepareStandaloneS4Metadata(bytes, prefix, out compressedData, out idatTotalBytes)) {
            return false;
        }

        if (idatTotalBytes != 555) {
            return false;
        }

        Breadcrumb(prefix + "_IDAT_LEN_OK");

        const int expectedOutputBytes = 3164;
        output = new byte[expectedOutputBytes];
        if (output == null || output.Length != expectedOutputBytes) {
            return false;
        }

        Value(prefix + "_EXPECTED_OUT_LEN", (ulong)(uint)output.Length);
        Breadcrumb(prefix + "_EXPECTED_OUT_OK");
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ProbeS4D(byte[] bytes) {
        Breadcrumb("UEFI_PNG_PROBE_S4D_ENTER");
        try {
            if (!TryPrepareStandaloneInflate(bytes, "UEFI_PNG_PROBE_S4D", out byte[] compressedData, out int idatTotalBytes, out _, out _, out _, out _)) {
                return;
            }

            unsafe {
                fixed (byte* compressedPtr = compressedData) {
                    ZlibBitReader reader = new ZlibBitReader(compressedPtr + 2, idatTotalBytes - 6);
                    if (!TryReadStandaloneBlockHeader(ref reader, "UEFI_PNG_PROBE_S4D", out _, out int btype)) {
                        return;
                    }

                    if (btype != 2) {
                        return;
                    }

                    if (!TryReadStandaloneDynamicCounts(ref reader, "UEFI_PNG_PROBE_S4D", out int hlit, out int hdist, out int hclen)) {
                        return;
                    }

                    if (!TryReadStandaloneCodeLengthAlphabet(ref reader, "UEFI_PNG_PROBE_S4D", hclen, out _)) {
                        return;
                    }
                }
            }
        } finally {
            Breadcrumb("UEFI_PNG_PROBE_S4D_EXIT");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ProbeS4E(byte[] bytes) {
        Breadcrumb("UEFI_PNG_PROBE_S4E_ENTER");
        try {
            if (!TryPrepareStandaloneInflate(bytes, "UEFI_PNG_PROBE_S4E", out byte[] compressedData, out int idatTotalBytes, out _, out _, out _, out _)) {
                Breadcrumb("UEFI_PNG_PROBE_S4E_TABLES_BAD");
                return;
            }

            unsafe {
                fixed (byte* compressedPtr = compressedData) {
                    ZlibBitReader reader = new ZlibBitReader(compressedPtr + 2, idatTotalBytes - 6);
                    if (!TryBuildStandaloneDynamicTrees(ref reader, "UEFI_PNG_PROBE_S4E", out _, out _)) {
                        Breadcrumb("UEFI_PNG_PROBE_S4E_TABLES_BAD");
                        return;
                    }
                }
            }

            Breadcrumb("UEFI_PNG_PROBE_S4E_TABLES_OK");
        } finally {
            Breadcrumb("UEFI_PNG_PROBE_S4E_EXIT");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ProbeS4F(byte[] bytes) {
        Breadcrumb("UEFI_PNG_PROBE_S4F_ENTER");
        try {
            if (!TryPrepareStandaloneInflate(bytes, "UEFI_PNG_PROBE_S4F", out byte[] compressedData, out int idatTotalBytes, out _, out _, out _, out _)) {
                return;
            }

            unsafe {
                fixed (byte* compressedPtr = compressedData) {
                    ZlibBitReader reader = new ZlibBitReader(compressedPtr + 2, idatTotalBytes - 6);
                    if (!TryBuildStandaloneDynamicTrees(ref reader, "UEFI_PNG_PROBE_S4F", out HuffmanTree litTree, out _)) {
                        return;
                    }

                    if (!TryDecodeSymbol(ref reader, litTree, out int symbol)) {
                        return;
                    }

                    Value("UEFI_PNG_PROBE_S4F_SYMBOL_VALUE", (ulong)(uint)symbol);
                    if (symbol < 256) {
                        Breadcrumb("UEFI_PNG_PROBE_S4F_LITERAL");
                    } else if (symbol == 256) {
                        Breadcrumb("UEFI_PNG_PROBE_S4F_END");
                    } else if (symbol >= 257 && symbol <= 285) {
                        Breadcrumb("UEFI_PNG_PROBE_S4F_LENGTH");
                    }
                }
            }
        } finally {
            Breadcrumb("UEFI_PNG_PROBE_S4F_EXIT");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ProbeS4G(byte[] bytes) {
        Breadcrumb("UEFI_PNG_PROBE_S4G_ENTER");
        try {
            if (!TryPrepareStandaloneInflate(bytes, "UEFI_PNG_PROBE_S4G", out byte[] compressedData, out int idatTotalBytes, out _, out _, out _, out _)) {
                return;
            }

            unsafe {
                fixed (byte* compressedPtr = compressedData) {
                    ZlibBitReader reader = new ZlibBitReader(compressedPtr + 2, idatTotalBytes - 6);
                    if (!TryBuildStandaloneDynamicTrees(ref reader, "UEFI_PNG_PROBE_S4G", out HuffmanTree litTree, out HuffmanTree distTree)) {
                        return;
                    }

                    byte[] output = new byte[3164];
                    if (output == null) {
                        return;
                    }

                    if (!TryDecodeSymbol(ref reader, litTree, out int symbol)) {
                        return;
                    }

                    if (symbol < 256) {
                        output[0] = (byte)symbol;
                        Breadcrumb("UEFI_PNG_PROBE_S4G_LITERAL_WRITE");
                        Breadcrumb("UEFI_PNG_PROBE_S4G_OK");
                        return;
                    }

                    if (symbol == 256) {
                        Breadcrumb("UEFI_PNG_PROBE_S4G_OK");
                        return;
                    }

                    if (symbol >= 257 && symbol <= 285) {
                        int lengthIndex = symbol - 257;
                        int length = GetLengthBase(lengthIndex);
                        int lengthExtra = GetLengthExtra(lengthIndex);
                        if (lengthExtra > 0) {
                            if (!reader.ReadBits(lengthExtra, out int extra)) {
                                return;
                            }

                            length += extra;
                        }

                        if (!TryDecodeSymbol(ref reader, distTree, out int distSymbol) || distSymbol < 0 || distSymbol >= 30) {
                            return;
                        }

                        int distance = GetDistanceBase(distSymbol);
                        int distanceExtra = GetDistanceExtra(distSymbol);
                        if (distanceExtra > 0) {
                            if (!reader.ReadBits(distanceExtra, out int extra)) {
                                return;
                            }

                            distance += extra;
                        }

                        Breadcrumb("UEFI_PNG_PROBE_S4G_LEN_DIST");
                        if (distance <= 0 || distance > 0 || length > output.Length) {
                            Breadcrumb("UEFI_PNG_PROBE_S4G_BOUNDS_ABORT");
                            return;
                        }

                        Breadcrumb("UEFI_PNG_PROBE_S4G_OK");
                        return;
                    }
                }
            }
        } finally {
            Breadcrumb("UEFI_PNG_PROBE_S4G_EXIT");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ProbeS4H(byte[] bytes) {
        Breadcrumb("UEFI_PNG_PROBE_S4H_ENTER");
        try {
            if (!TryPrepareStandaloneInflate(bytes, "UEFI_PNG_PROBE_S4H", out byte[] compressedData, out int idatTotalBytes, out _, out _, out _, out _)) {
                return;
            }

            unsafe {
                fixed (byte* compressedPtr = compressedData) {
                    ZlibBitReader reader = new ZlibBitReader(compressedPtr + 2, idatTotalBytes - 6);
                    if (TryReadStandaloneBlockHeader(ref reader, "UEFI_PNG_PROBE_S4H", out _, out int btype) &&
                        btype == 2 &&
                        TryReadStandaloneDynamicCounts(ref reader, "UEFI_PNG_PROBE_S4H", out _, out _, out int hclen)) {
                        _ = TryReadStandaloneCodeLengthAlphabet(ref reader, "UEFI_PNG_PROBE_S4H", hclen, out _);
                    }
                }
            }

            Breadcrumb("UEFI_PNG_PROBE_S4H_PROGRESS");
            byte[] output = new byte[3164];
            if (output == null) {
                Breadcrumb("UEFI_PNG_PROBE_S4H_BOUNDS_ABORT");
                return;
            }

            if (TryInflateZlibSmoke(compressedData, idatTotalBytes, output, 3164)) {
                Breadcrumb("UEFI_PNG_PROBE_S4H_OK");
            } else {
                Breadcrumb("UEFI_PNG_PROBE_S4H_BOUNDS_ABORT");
            }
        } finally {
            Breadcrumb("UEFI_PNG_PROBE_S4H_EXIT");
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool TryPrepareStandaloneInflate(byte[] bytes, string prefix, out byte[] compressedData, out int idatTotalBytes, out int width, out int height, out byte bitDepth, out byte colorType) {
        compressedData = null;
        idatTotalBytes = 0;
        width = 0;
        height = 0;
        bitDepth = 0;
        colorType = 0;

        if (!TryReadHeader(bytes)) {
            return false;
        }

        width = (int)ReadU32BE(bytes, 16);
        height = (int)ReadU32BE(bytes, 20);
        bitDepth = bytes[24];
        colorType = bytes[25];
        Value(prefix + "_LEN", (ulong)bytes.Length);
        Value(prefix + "_IHDR_WIDTH", (ulong)(uint)width);
        Value(prefix + "_IHDR_HEIGHT", (ulong)(uint)height);
        Value(prefix + "_IHDR_BIT_DEPTH", (ulong)bitDepth);
        Value(prefix + "_IHDR_COLOR_TYPE", (ulong)colorType);

        if (width <= 0 || height <= 0 || bitDepth != 8 || colorType != 6) {
            return false;
        }

        Breadcrumb(prefix + "_IDAT_AGG_ENTER");
        compressedData = new byte[555];
        if (compressedData == null) {
            Breadcrumb(prefix + "_IDAT_AGG_EXIT");
            return false;
        }

        if (!TryAggregateIdat(bytes, 33, compressedData, out int idatChunkCount, out idatTotalBytes) ||
            idatChunkCount != 1 ||
            idatTotalBytes != 555) {
            Breadcrumb(prefix + "_IDAT_AGG_EXIT");
            return false;
        }

        Value(prefix + "_IDAT_CHUNK_COUNT", (ulong)(uint)idatChunkCount);
        Value(prefix + "_IDAT_BYTES", (ulong)(uint)idatTotalBytes);
        Breadcrumb(prefix + "_IDAT_AGG_EXIT");

        byte cmf = compressedData[0];
        byte flg = compressedData[1];
        if ((cmf & 0x0F) != 8 ||
            (flg & 0x20) != 0 ||
            ((cmf * 256 + flg) % 31) != 0) {
            return false;
        }

        Value(prefix + "_ZLIB_CMF", (ulong)cmf);
        Value(prefix + "_ZLIB_FLG", (ulong)flg);
        Breadcrumb(prefix + "_ZLIB_HEADER_OK");
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool TryReadStandaloneBlockHeader(ref ZlibBitReader reader, string prefix, out int bfinal, out int btype) {
        bfinal = 0;
        btype = 0;
        if (!reader.ReadBits(1, out bfinal) ||
            !reader.ReadBits(2, out btype)) {
            return false;
        }

        Value(prefix + "_BFINAL", (ulong)(uint)bfinal);
        Value(prefix + "_BTYPE", (ulong)(uint)btype);
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool TryReadStandaloneDynamicCounts(ref ZlibBitReader reader, string prefix, out int hlit, out int hdist, out int hclen) {
        hlit = 0;
        hdist = 0;
        hclen = 0;

        if (!reader.ReadBits(5, out int hlitBits) ||
            !reader.ReadBits(5, out int hdistBits) ||
            !reader.ReadBits(4, out int hclenBits)) {
            return false;
        }

        hlit = hlitBits + 257;
        hdist = hdistBits + 1;
        hclen = hclenBits + 4;
        Value(prefix + "_HLIT", (ulong)(uint)hlit);
        Value(prefix + "_HDIST", (ulong)(uint)hdist);
        Value(prefix + "_HCLEN", (ulong)(uint)hclen);
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool TryReadStandaloneCodeLengthAlphabet(ref ZlibBitReader reader, string prefix, int hclen, out int[] codeLengthLengths) {
        codeLengthLengths = new int[19];
        for (int i = 0; i < hclen; i++) {
            if (!reader.ReadBits(3, out int length)) {
                codeLengthLengths = null;
                return false;
            }

            codeLengthLengths[GetCLCLOrder(i)] = length;
        }

        Breadcrumb(prefix + "_CODELEN_ALPHABET_OK");
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool TryBuildStandaloneDynamicTrees(ref ZlibBitReader reader, string prefix, out HuffmanTree litTree, out HuffmanTree distTree) {
        litTree = null;
        distTree = null;

        if (!TryReadStandaloneBlockHeader(ref reader, prefix, out _, out int btype)) {
            return false;
        }

        if (btype != 2) {
            return false;
        }

        if (!TryReadStandaloneDynamicCounts(ref reader, prefix, out int hlit, out int hdist, out int hclen)) {
            return false;
        }

        int[] codeLengthLengths = null;
        Breadcrumb(prefix + "_CL_BUILD_ENTER");
        if (!TryReadStandaloneCodeLengthAlphabet(ref reader, prefix, hclen, out codeLengthLengths) ||
            !BuildHuffmanTree(codeLengthLengths, 19, 7, out HuffmanTree codeLengthTree)) {
            return false;
        }

        Breadcrumb(prefix + "_CL_TREE_OK");

        int totalLengths = hlit + hdist;
        int[] lengths = new int[totalLengths];
        int index = 0;
        while (index < totalLengths) {
            if (!TryDecodeSymbol(ref reader, codeLengthTree, out int code)) {
                litTree = null;
                distTree = null;
                return false;
            }

            if (code <= 15) {
                lengths[index++] = code;
            } else if (code == 16) {
                if (index == 0) {
                    litTree = null;
                    distTree = null;
                    return false;
                }

                if (!reader.ReadBits(2, out int repeatBits)) {
                    litTree = null;
                    distTree = null;
                    return false;
                }

                int repeat = repeatBits + 3;
                if (index + repeat > totalLengths) {
                    litTree = null;
                    distTree = null;
                    return false;
                }

                int value = lengths[index - 1];
                for (int i = 0; i < repeat && index < totalLengths; i++) {
                    lengths[index++] = value;
                }
            } else if (code == 17) {
                if (!reader.ReadBits(3, out int repeatBits)) {
                    litTree = null;
                    distTree = null;
                    return false;
                }

                int repeat = repeatBits + 3;
                if (index + repeat > totalLengths) {
                    litTree = null;
                    distTree = null;
                    return false;
                }

                for (int i = 0; i < repeat && index < totalLengths; i++) {
                    lengths[index++] = 0;
                }
            } else if (code == 18) {
                if (!reader.ReadBits(7, out int repeatBits)) {
                    litTree = null;
                    distTree = null;
                    return false;
                }

                int repeat = repeatBits + 11;
                if (index + repeat > totalLengths) {
                    litTree = null;
                    distTree = null;
                    return false;
                }

                for (int i = 0; i < repeat && index < totalLengths; i++) {
                    lengths[index++] = 0;
                }
            } else {
                litTree = null;
                distTree = null;
                return false;
            }
        }

        Breadcrumb(prefix + "_LENGTHS_OK");

        int[] decodedLitLengths = new int[hlit];
        int[] decodedDistLengths = new int[hdist];
        for (int i = 0; i < hlit; i++) {
            decodedLitLengths[i] = lengths[i];
        }
        for (int i = 0; i < hdist; i++) {
            decodedDistLengths[i] = lengths[hlit + i];
        }

        codeLengthTree = null;
        if (!BuildHuffmanTree(decodedLitLengths, hlit, 15, out litTree)) {
            litTree = null;
            distTree = null;
            return false;
        }

        Breadcrumb(prefix + "_LIT_TREE_OK");

        if (!BuildHuffmanTree(decodedDistLengths, hdist, 15, out distTree)) {
            litTree = null;
            distTree = null;
            return false;
        }

        Breadcrumb(prefix + "_DIST_TREE_OK");
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool TryReadHeader(byte[] bytes) {
        return bytes != null &&
               bytes.Length >= 33 &&
               bytes[0] == 0x89 &&
               bytes[1] == 0x50 &&
               bytes[2] == 0x4E &&
               bytes[3] == 0x47 &&
               bytes[4] == 0x0D &&
               bytes[5] == 0x0A &&
               bytes[6] == 0x1A &&
               bytes[7] == 0x0A &&
               ReadU32BE(bytes, 8) == 13 &&
               ReadU32BE(bytes, 12) == 0x49484452;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool TryAggregateIdat(byte[] bytes, int pos, byte[] idatData, out int idatChunkCount, out int idatTotalBytes) {
        idatChunkCount = 0;
        idatTotalBytes = 0;

        if (bytes == null) {
            return false;
        }

        int copyPos = 0;
        while (pos + 12 <= bytes.Length) {
            uint chunkLen = ReadU32BE(bytes, pos);
            if (chunkLen > (uint)(bytes.Length - pos - 12)) {
                return false;
            }

            uint chunkType = ReadU32BE(bytes, pos + 4);
            if (chunkType == 0x49444154u) {
                int copyLen = (int)chunkLen;
                if (idatData != null && copyPos + copyLen > idatData.Length) {
                    return false;
                }

                for (int i = 0; i < copyLen; i++) {
                    if (idatData != null) {
                        idatData[copyPos] = bytes[pos + 8 + i];
                    }
                    copyPos++;
                }

                idatChunkCount++;
                idatTotalBytes += copyLen;
            } else if (chunkType == 0x49454E44u) {
                return idatChunkCount > 0 && (idatData == null || copyPos == idatTotalBytes);
            }

            pos += 12 + (int)chunkLen;
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static uint ReadU32BE(byte[] bytes, int offset) {
        return ((uint)bytes[offset] << 24) |
               ((uint)bytes[offset + 1] << 16) |
               ((uint)bytes[offset + 2] << 8) |
               bytes[offset + 3];
    }

    private static void Breadcrumb(string breadcrumb) {
        Program.CursorImageProbeBreadcrumb(breadcrumb);
    }

    private static void Value(string marker, ulong value) {
        Program.CursorImageProbeValue(marker, value);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool TryInflateZlibSmoke(byte[] data, int compressedLength, byte[] output, int expectedSize) {
        Breadcrumb("UEFI_PNG_PROBE_S4_INFLATE_HELPER_ENTER");
        if (data == null || output == null) {
            return false;
        }

        if (compressedLength < 6 || compressedLength > data.Length) {
            return false;
        }

        if (expectedSize <= 0 || output.Length < expectedSize) {
            return false;
        }

        int cmf = data[0];
        int flg = data[1];
        if ((cmf & 0x0F) != 8 ||
            (flg & 0x20) != 0 ||
            (((cmf << 8) | flg) % 31) != 0) {
            return false;
        }

        Breadcrumb("UEFI_PNG_PROBE_S4_INFLATE_HELPER_HEADER_OK");

        unsafe {
            fixed (byte* compressedPtr = data) {
                ZlibBitReader reader = new ZlibBitReader(compressedPtr + 2, compressedLength - 6);
                int outPos = 0;
                bool lastBlock = false;
                int blockCount = 0;

                while (!lastBlock) {
                    blockCount++;
                    if (blockCount > 64) {
                        return false;
                    }

                    if (!reader.ReadBits(1, out int bfinal) ||
                        !reader.ReadBits(2, out int btype)) {
                        return false;
                    }

                    Breadcrumb("UEFI_PNG_PROBE_S4_INFLATE_HELPER_BLOCK_HEADER_OK");
                    lastBlock = bfinal != 0;

                    if (btype == 0) {
                        if (!TryInflateStoredBlock(ref reader, output, ref outPos, expectedSize)) {
                            return false;
                        }
                    } else if (btype == 1) {
                        if (!TryInflateHuffmanBlock(ref reader, output, ref outPos, expectedSize, true)) {
                            return false;
                        }
                    } else if (btype == 2) {
                        if (!TryInflateHuffmanBlock(ref reader, output, ref outPos, expectedSize, false)) {
                            return false;
                        }
                    } else {
                        return false;
                    }

                    if (outPos > expectedSize) {
                        return false;
                    }
                }

                return outPos == expectedSize;
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool TryInflateStoredBlock(ref ZlibBitReader reader, byte[] output, ref int outPos, int expectedSize) {
        reader.AlignToByte();

        if (!reader.ReadByte(out int lenLow) ||
            !reader.ReadByte(out int lenHigh) ||
            !reader.ReadByte(out int nlenLow) ||
            !reader.ReadByte(out int nlenHigh)) {
            return false;
        }

        int len = lenLow | (lenHigh << 8);
        int nlen = nlenLow | (nlenHigh << 8);
        if ((len ^ nlen) != 0xFFFF || len < 0 || outPos + len > expectedSize) {
            return false;
        }

        for (int i = 0; i < len; i++) {
            if (!reader.ReadByte(out int value)) {
                return false;
            }

            output[outPos++] = (byte)value;
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool TryInflateHuffmanBlock(ref ZlibBitReader reader, byte[] output, ref int outPos, int expectedSize, bool fixedTrees) {
        return fixedTrees
            ? TryInflateFixedHuffmanBlock(ref reader, output, ref outPos, expectedSize)
            : TryInflateDynamicHuffmanBlock(ref reader, output, ref outPos, expectedSize);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool TryInflateFixedHuffmanBlock(ref ZlibBitReader reader, byte[] output, ref int outPos, int expectedSize) {
        Breadcrumb("UEFI_PNG_PROBE_S4_INFLATE_HELPER_FIXED_ENTER");
        if (!BuildInflateTrees(ref reader, true, out HuffmanTree litTree, out HuffmanTree distTree)) {
            return false;
        }

        Breadcrumb("UEFI_PNG_PROBE_S4_INFLATE_HELPER_TREES_OK");
        return TryInflateDecodedBlock(ref reader, output, ref outPos, expectedSize, litTree, distTree);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool TryInflateDynamicHuffmanBlock(ref ZlibBitReader reader, byte[] output, ref int outPos, int expectedSize) {
        Breadcrumb("UEFI_PNG_PROBE_S4_INFLATE_HELPER_DYNAMIC_ENTER");
        if (!TryBuildDynamicInflateTrees(ref reader, out HuffmanTree litTree, out HuffmanTree distTree)) {
            return false;
        }

        Breadcrumb("UEFI_PNG_PROBE_S4_INFLATE_HELPER_TREES_OK");
        return TryInflateDecodedBlock(ref reader, output, ref outPos, expectedSize, litTree, distTree);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool TryInflateDecodedBlock(ref ZlibBitReader reader, byte[] output, ref int outPos, int expectedSize, HuffmanTree litTree, HuffmanTree distTree) {
        int symbolCount = 0;
        while (true) {
            symbolCount++;
            if (symbolCount > 8192) {
                return false;
            }

            if (!TryDecodeSymbol(ref reader, litTree, out int symbol)) {
                return false;
            }

            if (symbolCount == 1) {
                Breadcrumb("UEFI_PNG_PROBE_S4_INFLATE_HELPER_SYMBOL_OK");
            }

            if (symbol < 256) {
                if (outPos >= expectedSize) {
                    return false;
                }

                output[outPos++] = (byte)symbol;
            } else if (symbol == 256) {
                break;
            } else if (symbol >= 257 && symbol <= 285) {
                int lengthIndex = symbol - 257;
                int length = GetLengthBase(lengthIndex);
                int lengthExtra = GetLengthExtra(lengthIndex);
                if (lengthExtra > 0) {
                if (!reader.ReadBits(lengthExtra, out int extra)) {
                    return false;
                }

                    length += extra;
                }

                if (!TryDecodeSymbol(ref reader, distTree, out int distSymbol) || distSymbol < 0 || distSymbol >= 30) {
                    return false;
                }

                int distance = GetDistanceBase(distSymbol);
                int distanceExtra = GetDistanceExtra(distSymbol);
                if (distanceExtra > 0) {
                    if (!reader.ReadBits(distanceExtra, out int extra)) {
                        return false;
                    }

                    distance += extra;
                }

                if (distance <= 0 || distance > outPos || outPos + length > expectedSize) {
                    return false;
                }

                int copyPos = outPos - distance;
                for (int i = 0; i < length; i++) {
                    output[outPos++] = output[copyPos++];
                }
            } else {
                return false;
            }
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool BuildInflateTrees(ref ZlibBitReader reader, bool fixedTrees, out HuffmanTree litTree, out HuffmanTree distTree) {
        litTree = null;
        distTree = null;

        if (fixedTrees) {
            int[] fixedLitLengths = new int[288];
            for (int i = 0; i <= 143; i++) {
                fixedLitLengths[i] = 8;
            }
            for (int i = 144; i <= 255; i++) {
                fixedLitLengths[i] = 9;
            }
            for (int i = 256; i <= 279; i++) {
                fixedLitLengths[i] = 7;
            }
            for (int i = 280; i <= 287; i++) {
                fixedLitLengths[i] = 8;
            }

            int[] fixedDistLengths = new int[32];
            for (int i = 0; i < 32; i++) {
                fixedDistLengths[i] = 5;
            }

            if (!BuildHuffmanTree(fixedLitLengths, 288, 15, out litTree)) {
                return false;
            }

            if (!BuildHuffmanTree(fixedDistLengths, 32, 15, out distTree)) {
                return false;
            }

            return true;
        }

        return TryBuildDynamicInflateTrees(ref reader, out litTree, out distTree);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool TryBuildDynamicInflateTrees(ref ZlibBitReader reader, out HuffmanTree litTree, out HuffmanTree distTree) {
        litTree = null;
        distTree = null;

        int hlitBits = 0;
        if (!TryReadDynamicHeaderBits(ref reader, 5, out hlitBits)) {
            return false;
        }

        Breadcrumb("UEFI_PNG_PROBE_S4_INFLATE_HELPER_HLIT_OK");

        int hdistBits = 0;
        if (!TryReadDynamicHeaderBits(ref reader, 5, out hdistBits)) {
            return false;
        }

        Breadcrumb("UEFI_PNG_PROBE_S4_INFLATE_HELPER_HDIST_OK");

        int hclenBits = 0;
        if (!TryReadDynamicHeaderBits(ref reader, 4, out hclenBits)) {
            return false;
        }

        Breadcrumb("UEFI_PNG_PROBE_S4_INFLATE_HELPER_HCLEN_OK");

        int hlit = hlitBits + 257;
        int hdist = hdistBits + 1;
        int hclen = hclenBits + 4;
        Breadcrumb("UEFI_PNG_PROBE_S4_INFLATE_HELPER_DYN_COUNTS_OK");

        int[] codeLengthLengths = new int[19];
        for (int i = 0; i < hclen; i++) {
            if (!reader.ReadBits(3, out int length)) {
                litTree = null;
                distTree = null;
                return false;
            }

            codeLengthLengths[GetCLCLOrder(i)] = length;
        }

        Breadcrumb("UEFI_PNG_PROBE_S4_INFLATE_HELPER_CL_LENGTHS_OK");

        Breadcrumb("UEFI_PNG_PROBE_S4_INFLATE_HELPER_CL_BUILD_ENTER");
        if (!BuildHuffmanTree(codeLengthLengths, 19, 7, out HuffmanTree codeLengthTree)) {
            litTree = null;
            distTree = null;
            return false;
        }

        Breadcrumb("UEFI_PNG_PROBE_S4_INFLATE_HELPER_CL_TREE_OK");

        int totalLengths = hlit + hdist;
        int[] lengths = new int[totalLengths];
        int index = 0;
        while (index < totalLengths) {
            if (!TryDecodeSymbol(ref reader, codeLengthTree, out int code)) {
                litTree = null;
                distTree = null;
                return false;
            }

            if (code <= 15) {
                lengths[index++] = code;
            } else if (code == 16) {
                if (index == 0) {
                    litTree = null;
                    distTree = null;
                    return false;
                }

                if (!reader.ReadBits(2, out int repeatBits)) {
                    litTree = null;
                    distTree = null;
                    return false;
                }

                int repeat = repeatBits + 3;
                if (index + repeat > totalLengths) {
                    litTree = null;
                    distTree = null;
                    return false;
                }

                int value = lengths[index - 1];
                for (int i = 0; i < repeat && index < totalLengths; i++) {
                    lengths[index++] = value;
                }
            } else if (code == 17) {
                if (!reader.ReadBits(3, out int repeatBits)) {
                    litTree = null;
                    distTree = null;
                    return false;
                }

                int repeat = repeatBits + 3;
                if (index + repeat > totalLengths) {
                    litTree = null;
                    distTree = null;
                    return false;
                }

                for (int i = 0; i < repeat && index < totalLengths; i++) {
                    lengths[index++] = 0;
                }
            } else if (code == 18) {
                if (!reader.ReadBits(7, out int repeatBits)) {
                    litTree = null;
                    distTree = null;
                    return false;
                }

                int repeat = repeatBits + 11;
                if (index + repeat > totalLengths) {
                    litTree = null;
                    distTree = null;
                    return false;
                }

                for (int i = 0; i < repeat && index < totalLengths; i++) {
                    lengths[index++] = 0;
                }
            } else {
                litTree = null;
                distTree = null;
                return false;
            }
        }

        Breadcrumb("UEFI_PNG_PROBE_S4_INFLATE_HELPER_LENGTHS_OK");

        int[] decodedLitLengths = new int[hlit];
        int[] decodedDistLengths = new int[hdist];
        for (int i = 0; i < hlit; i++) {
            decodedLitLengths[i] = lengths[i];
        }
        for (int i = 0; i < hdist; i++) {
            decodedDistLengths[i] = lengths[hlit + i];
        }

        codeLengthTree = null;
        if (!BuildHuffmanTree(decodedLitLengths, hlit, 15, out litTree)) {
            return false;
        }

        Breadcrumb("UEFI_PNG_PROBE_S4_INFLATE_HELPER_LIT_TREE_OK");

        if (!BuildHuffmanTree(decodedDistLengths, hdist, 15, out distTree)) {
            return false;
        }

        Breadcrumb("UEFI_PNG_PROBE_S4_INFLATE_HELPER_DIST_TREE_OK");

        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool TryReadDynamicHeaderBits(ref ZlibBitReader reader, int count, out int value) {
        return reader.ReadBits(count, out value);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool BuildHuffmanTree(int[] lengths, int count, int maxBitLen, out HuffmanTree tree) {
        tree = new HuffmanTree();
        tree.Zero = new int[8192];
        tree.One = new int[8192];
        tree.Symbol = new int[8192];

        for (int i = 0; i < tree.Zero.Length; i++) {
            tree.Zero[i] = -1;
            tree.One[i] = -1;
            tree.Symbol[i] = -1;
        }

        int[] blCount = new int[maxBitLen + 1];
        bool hasSymbol = false;
        for (int i = 0; i < count; i++) {
            int length = lengths[i];
            if (length < 0 || length > maxBitLen) {
                return false;
            }

            if (length != 0) {
                blCount[length]++;
                hasSymbol = true;
            }
        }

        if (!hasSymbol) {
            return false;
        }

        int code = 0;
        int[] nextCode = new int[maxBitLen + 1];
        for (int bits = 1; bits <= maxBitLen; bits++) {
            code = (code + blCount[bits - 1]) << 1;
            nextCode[bits] = code;
        }

        int nodeCount = 1;
        for (int symbol = 0; symbol < count; symbol++) {
            int length = lengths[symbol];
            if (length == 0) {
                continue;
            }

            int currentCode = nextCode[length]++;
            int node = 0;
            for (int bitIndex = 0; bitIndex < length; bitIndex++) {
                int bit = (currentCode >> bitIndex) & 1;
                int next = bit == 0 ? tree.Zero[node] : tree.One[node];
                bool isLeaf = bitIndex == length - 1;

                if (next < 0) {
                    if (nodeCount >= tree.Zero.Length) {
                        return false;
                    }

                    next = nodeCount++;
                    tree.Zero[next] = -1;
                    tree.One[next] = -1;
                    tree.Symbol[next] = -1;
                    if (bit == 0) {
                        tree.Zero[node] = next;
                    } else {
                        tree.One[node] = next;
                    }
                } else if (!isLeaf && tree.Symbol[next] >= 0) {
                    return false;
                }

                if (isLeaf) {
                    if (tree.Symbol[next] >= 0) {
                        return false;
                    }

                    tree.Symbol[next] = symbol;
                }

                node = next;
            }
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool TryDecodeSymbol(ref ZlibBitReader reader, HuffmanTree tree, out int symbol) {
        int node = 0;
        for (int depth = 0; depth < 16; depth++) {
            if (!reader.ReadBits(1, out int bit)) {
                symbol = 0;
                return false;
            }

            node = bit == 0 ? tree.Zero[node] : tree.One[node];
            if (node < 0) {
                symbol = 0;
                return false;
            }

            if (tree.Symbol[node] >= 0) {
                symbol = tree.Symbol[node];
                return true;
            }
        }

        symbol = 0;
        return false;
    }

    private sealed class HuffmanTree {
        internal int[] Zero;
        internal int[] One;
        internal int[] Symbol;
    }

    private unsafe struct ZlibBitReader {
        private readonly byte* data;
        private readonly int limit;
        private int position;
        private uint buffer;
        private int bitsInBuffer;

        internal ZlibBitReader(byte* data, int size) {
            this.data = data;
            position = 0;
            limit = size;
            buffer = 0;
            bitsInBuffer = 0;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal bool ReadBits(int count, out int value) {
            if (count == 0) {
                value = 0;
                return true;
            }

            while (bitsInBuffer < count) {
                if (position >= limit) {
                    value = 0;
                    return false;
                }

                buffer |= (uint)data[position++] << bitsInBuffer;
                bitsInBuffer += 8;
            }

            value = (int)(buffer & (uint)((1 << count) - 1));
            buffer >>= count;
            bitsInBuffer -= count;
            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal bool ReadByte(out int value) {
            return ReadBits(8, out value);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal void AlignToByte() {
            buffer = 0;
            bitsInBuffer = 0;
        }
    }

    private static int GetLengthBase(int idx) {
        switch (idx) {
            case 0: return 3;
            case 1: return 4;
            case 2: return 5;
            case 3: return 6;
            case 4: return 7;
            case 5: return 8;
            case 6: return 9;
            case 7: return 10;
            case 8: return 11;
            case 9: return 13;
            case 10: return 15;
            case 11: return 17;
            case 12: return 19;
            case 13: return 23;
            case 14: return 27;
            case 15: return 31;
            case 16: return 35;
            case 17: return 43;
            case 18: return 51;
            case 19: return 59;
            case 20: return 67;
            case 21: return 83;
            case 22: return 99;
            case 23: return 115;
            case 24: return 131;
            case 25: return 163;
            case 26: return 195;
            case 27: return 227;
            case 28: return 258;
            default: return 3;
        }
    }

    private static int GetLengthExtra(int idx) {
        switch (idx) {
            case 0:
            case 1:
            case 2:
            case 3:
            case 4:
            case 5:
            case 6:
            case 7:
                return 0;
            case 8:
            case 9:
            case 10:
            case 11:
                return 1;
            case 12:
            case 13:
            case 14:
            case 15:
                return 2;
            case 16:
            case 17:
            case 18:
            case 19:
                return 3;
            case 20:
            case 21:
            case 22:
            case 23:
                return 4;
            case 24:
            case 25:
            case 26:
            case 27:
                return 5;
            case 28:
                return 0;
            default:
                return 0;
        }
    }

    private static int GetDistanceBase(int idx) {
        switch (idx) {
            case 0: return 1;
            case 1: return 2;
            case 2: return 3;
            case 3: return 4;
            case 4: return 5;
            case 5: return 7;
            case 6: return 9;
            case 7: return 13;
            case 8: return 17;
            case 9: return 25;
            case 10: return 33;
            case 11: return 49;
            case 12: return 65;
            case 13: return 97;
            case 14: return 129;
            case 15: return 193;
            case 16: return 257;
            case 17: return 385;
            case 18: return 513;
            case 19: return 769;
            case 20: return 1025;
            case 21: return 1537;
            case 22: return 2049;
            case 23: return 3073;
            case 24: return 4097;
            case 25: return 6145;
            case 26: return 8193;
            case 27: return 12289;
            case 28: return 16385;
            case 29: return 24577;
            default: return 1;
        }
    }

    private static int GetDistanceExtra(int idx) {
        switch (idx) {
            case 0:
            case 1:
            case 2:
            case 3:
                return 0;
            case 4:
            case 5:
                return 1;
            case 6:
            case 7:
                return 2;
            case 8:
            case 9:
                return 3;
            case 10:
            case 11:
                return 4;
            case 12:
            case 13:
                return 5;
            case 14:
            case 15:
                return 6;
            case 16:
            case 17:
                return 7;
            case 18:
            case 19:
                return 8;
            case 20:
            case 21:
                return 9;
            case 22:
            case 23:
                return 10;
            case 24:
            case 25:
                return 11;
            case 26:
            case 27:
                return 12;
            case 28:
            case 29:
                return 13;
            default:
                return 0;
        }
    }

    private static int GetCLCLOrder(int idx) {
        switch (idx) {
            case 0: return 16;
            case 1: return 17;
            case 2: return 18;
            case 3: return 0;
            case 4: return 8;
            case 5: return 7;
            case 6: return 9;
            case 7: return 6;
            case 8: return 10;
            case 9: return 5;
            case 10: return 11;
            case 11: return 4;
            case 12: return 12;
            case 13: return 3;
            case 14: return 13;
            case 15: return 2;
            case 16: return 14;
            case 17: return 1;
            case 18: return 15;
            default: return 0;
        }
    }
}

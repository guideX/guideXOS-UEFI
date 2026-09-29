using System;
using System.Runtime.InteropServices;

namespace guideXOS.Misc {
    // Minimal SHA-256 implementation (public domain style) suitable for kernel use.
    // Works incrementally via Update and Final.
    public unsafe struct SHA256Ctx {
        public fixed uint State[8];
        public fixed uint W[64];
        public ulong TotalLen; // bytes processed
        public fixed byte Buffer[64];
        public int BufferLen;
    }

    public static unsafe class SHA256 {
        // Keep the round constants in code so kernel hashing does not trigger
        // a managed array allocation or a type-initializer syscall.
        static uint RoundConstant(int i) {
            switch (i) {
                case 0: return 0x428A2F98; case 1: return 0x71374491;
                case 2: return 0xB5C0FBCF; case 3: return 0xE9B5DBA5;
                case 4: return 0x3956C25B; case 5: return 0x59F111F1;
                case 6: return 0x923F82A4; case 7: return 0xAB1C5ED5;
                case 8: return 0xD807AA98; case 9: return 0x12835B01;
                case 10: return 0x243185BE; case 11: return 0x550C7DC3;
                case 12: return 0x72BE5D74; case 13: return 0x80DEB1FE;
                case 14: return 0x9BDC06A7; case 15: return 0xC19BF174;
                case 16: return 0xE49B69C1; case 17: return 0xEFBE4786;
                case 18: return 0x0FC19DC6; case 19: return 0x240CA1CC;
                case 20: return 0x2DE92C6F; case 21: return 0x4A7484AA;
                case 22: return 0x5CB0A9DC; case 23: return 0x76F988DA;
                case 24: return 0x983E5152; case 25: return 0xA831C66D;
                case 26: return 0xB00327C8; case 27: return 0xBF597FC7;
                case 28: return 0xC6E00BF3; case 29: return 0xD5A79147;
                case 30: return 0x06CA6351; case 31: return 0x14292967;
                case 32: return 0x27B70A85; case 33: return 0x2E1B2138;
                case 34: return 0x4D2C6DFC; case 35: return 0x53380D13;
                case 36: return 0x650A7354; case 37: return 0x766A0ABB;
                case 38: return 0x81C2C92E; case 39: return 0x92722C85;
                case 40: return 0xA2BFE8A1; case 41: return 0xA81A664B;
                case 42: return 0xC24B8B70; case 43: return 0xC76C51A3;
                case 44: return 0xD192E819; case 45: return 0xD6990624;
                case 46: return 0xF40E3585; case 47: return 0x106AA070;
                case 48: return 0x19A4C116; case 49: return 0x1E376C08;
                case 50: return 0x2748774C; case 51: return 0x34B0BCB5;
                case 52: return 0x391C0CB3; case 53: return 0x4ED8AA4A;
                case 54: return 0x5B9CCA4F; case 55: return 0x682E6FF3;
                case 56: return 0x748F82EE; case 57: return 0x78A5636F;
                case 58: return 0x84C87814; case 59: return 0x8CC70208;
                case 60: return 0x90BEFFFA; case 61: return 0xA4506CEB;
                case 62: return 0xBEF9A3F7; default: return 0xC67178F2;
            }
        }

        static uint ROTR(uint x, int n) => (x >> n) | (x << (32 - n));
        static uint Ch(uint x, uint y, uint z) => (x & y) ^ (~x & z);
        static uint Maj(uint x, uint y, uint z) => (x & y) ^ (x & z) ^ (y & z);
        static uint BigSig0(uint x) => ROTR(x, 2) ^ ROTR(x, 13) ^ ROTR(x, 22);
        static uint BigSig1(uint x) => ROTR(x, 6) ^ ROTR(x, 11) ^ ROTR(x, 25);
        static uint SmallSig0(uint x) => ROTR(x, 7) ^ ROTR(x, 18) ^ (x >> 3);
        static uint SmallSig1(uint x) => ROTR(x, 17) ^ ROTR(x, 19) ^ (x >> 10);

        public static void Init(SHA256Ctx* c) {
            c->State[0] = 0x6A09E667; c->State[1] = 0xBB67AE85; c->State[2] = 0x3C6EF372; c->State[3] = 0xA54FF53A;
            c->State[4] = 0x510E527F; c->State[5] = 0x9B05688C; c->State[6] = 0x1F83D9AB; c->State[7] = 0x5BE0CD19;
            c->TotalLen = 0; c->BufferLen = 0;
        }

        static void Transform(SHA256Ctx* c, byte* block) {
            // Load message schedule
            for (int i = 0; i < 16; i++) {
                int off = i * 4;
                c->W[i] = ((uint)block[off] << 24) | ((uint)block[off + 1] << 16) | ((uint)block[off + 2] << 8) | block[off + 3];
            }
            for (int i = 16; i < 64; i++) c->W[i] = SmallSig1(c->W[i - 2]) + c->W[i - 7] + SmallSig0(c->W[i - 15]) + c->W[i - 16];

            uint a = c->State[0], b = c->State[1], cv = c->State[2], d = c->State[3], e = c->State[4], f = c->State[5], g = c->State[6], h = c->State[7];
            for (int i = 0; i < 64; i++) {
                uint T1 = h + BigSig1(e) + Ch(e, f, g) + RoundConstant(i) + c->W[i];
                uint T2 = BigSig0(a) + Maj(a, b, cv);
                h = g; g = f; f = e; e = d + T1; d = cv; cv = b; b = a; a = T1 + T2;
            }
            c->State[0] += a; c->State[1] += b; c->State[2] += cv; c->State[3] += d;
            c->State[4] += e; c->State[5] += f; c->State[6] += g; c->State[7] += h;
        }

        public static void Update(SHA256Ctx* c, byte* data, int len) {
            if (len <= 0) return;
            c->TotalLen += (ulong)len;
            int blen = c->BufferLen;
            if (blen > 0) {
                int need = 64 - blen;
                if (len < need) { for (int i = 0; i < len; i++) c->Buffer[blen + i] = data[i]; c->BufferLen += len; return; }
                for (int i = 0; i < need; i++) c->Buffer[blen + i] = data[i];
                Transform(c, (byte*)c->Buffer);
                data += need; len -= need; c->BufferLen = 0; blen = 0;
            }
            while (len >= 64) { Transform(c, data); data += 64; len -= 64; }
            for (int i = 0; i < len; i++) c->Buffer[i] = data[i];
            c->BufferLen = len;
        }

        public static void Final(SHA256Ctx* c, byte* out32) {
            ulong bitLen = c->TotalLen * 8UL;
            int blen = c->BufferLen;
            c->Buffer[blen++] = 0x80;
            if (blen > 56) {
                for (int i = blen; i < 64; i++) c->Buffer[i] = 0;
                Transform(c, (byte*)c->Buffer);
                blen = 0;
            }
            for (int i = blen; i < 56; i++) c->Buffer[i] = 0;
            // Append big-endian length
            for (int i = 0; i < 8; i++) c->Buffer[63 - i] = (byte)(bitLen >> (8 * i));
            Transform(c, (byte*)c->Buffer);
            // Output big-endian digest
            for (int i = 0; i < 8; i++) {
                uint v = c->State[i];
                out32[i * 4 + 0] = (byte)(v >> 24);
                out32[i * 4 + 1] = (byte)(v >> 16);
                out32[i * 4 + 2] = (byte)(v >> 8);
                out32[i * 4 + 3] = (byte)(v);
            }
        }

        public static void Compute(byte* data, int len, byte* out32) {
            SHA256Ctx ctx; Init(&ctx); Update(&ctx, data, len); Final(&ctx, out32);
        }

        public static string ToHex(byte* digest32) {
            char[] hex = new char[64];
            for (int i = 0; i < 32; i++) {
                byte b = digest32[i];
                int hi = (b >> 4) & 0xF; int lo = b & 0xF;
                hex[i * 2] = (char)(hi < 10 ? ('0' + hi) : ('a' + (hi - 10)));
                hex[i * 2 + 1] = (char)(lo < 10 ? ('0' + lo) : ('a' + (lo - 10)));
            }
            return new string(hex);
        }
    }
}

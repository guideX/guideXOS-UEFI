using System.Windows.Forms;
namespace System.Drawing {
    public class Image {
        public int[] RawData;
        public int Bpp;
        public int Width;
        public int Height;

        public Image(int width, int height) {
            Bpp = 4;
            if (width <= 0 || height <= 0) {
                Width = 0;
                Height = 0;
                RawData = new int[0];
                return;
            }

            long pixelCount = (long)width * height;
            if (pixelCount > int.MaxValue) {
                Width = 0;
                Height = 0;
                RawData = new int[0];
                return;
            }

            Width = width;
            Height = height;
            RawData = new int[(int)pixelCount];
        }

        public Image() {

        }
        /// <summary>
        /// Is Under Mouse
        /// </summary>
        /// <returns></returns>
        public bool IsUnderMouse(int X, int Y) {
            if (Control.MousePosition.X > X &&
                Control.MousePosition.X < X + Width &&
                Control.MousePosition.Y > Y &&
                Control.MousePosition.Y < Y + Height) return true;
            return false;
        }
        public uint GetPixel(int X, int Y) {
            return (uint)RawData[Y * Width + X];
        }

        static unsafe void Resample(void* input, void* output, int oldw, int oldh, int neww, int newh) {
            for (int i = 0; i < newh; i++) {
                for (int j = 0; j < neww; j++) {

                    // Clamp both neighbors. This preserves the existing
                    // bilinear scaler while making one-pixel source/target
                    // dimensions and the final destination row safe.
                    float y = newh == 1 ? 0 : (float)i / (float)(newh - 1) * (oldh - 1);
                    int l = (int)MathF.Floor(y);
                    if (l < 0) l = 0;
                    if (l >= oldh) l = oldh - 1;
                    int l2 = l + 1 < oldh ? l + 1 : l;
                    float u = l2 == l ? 0 : y - l;

                    float x = neww == 1 ? 0 : (float)j / (float)(neww - 1) * (oldw - 1);
                    int c = (int)MathF.Floor(x);
                    if (c < 0) c = 0;
                    if (c >= oldw) c = oldw - 1;
                    int c2 = c + 1 < oldw ? c + 1 : c;
                    float t = c2 == c ? 0 : x - c;

                    float d1 = (1 - t) * (1 - u);
                    float d2 = t * (1 - u);
                    float d3 = t * u;
                    float d4 = (1 - t) * u;

                    uint p1 = *((uint*)input + (l * oldw) + c);
                    uint p2 = *((uint*)input + (l * oldw) + c2);
                    uint p3 = *((uint*)input + (l2 * oldw) + c2);
                    uint p4 = *((uint*)input + (l2 * oldw) + c);

                    byte blue = (byte)((byte)p1 * d1 + (byte)p2 * d2 + (byte)p3 * d3 + (byte)p4 * d4);
                    byte green = (byte)((byte)(p1 >> 8) * d1 + (byte)(p2 >> 8) * d2 + (byte)(p3 >> 8) * d3 + (byte)(p4 >> 8) * d4);
                    byte red = (byte)((byte)(p1 >> 16) * d1 + (byte)(p2 >> 16) * d2 + (byte)(p3 >> 16) * d3 + (byte)(p4 >> 16) * d4);
                    byte alpha = (byte)((byte)(p1 >> 24) * d1 + (byte)(p2 >> 24) * d2 + (byte)(p3 >> 24) * d3 + (byte)(p4 >> 24) * d4);

                    *((uint*)output + (i * neww) + j) = (uint)((alpha << 24) | (red << 16) | (green << 8) | (blue));
                }
            }
        }

        public unsafe Image ResizeImage(int NewWidth, int NewHeight) {
            if (NewWidth <= 0 || NewHeight <= 0 || Width <= 0 || Height <= 0 ||
                RawData == null) {
                return new Image();
            }

            long sourcePixels = (long)Width * Height;
            long targetPixels = (long)NewWidth * NewHeight;
            if (sourcePixels <= 0 || sourcePixels > RawData.Length ||
                targetPixels <= 0 || targetPixels > int.MaxValue) {
                return new Image();
            }

            int[] temp = new int[(int)targetPixels];

            fixed (int* output = temp) {
                fixed (int* input = this.RawData) {
                    lock (null) {
                        Resample(input, output, Width, Height, NewWidth, NewHeight);
                    }
                }
            }

            Image image = new Image() {
                Width = NewWidth,
                Height = NewHeight,
                Bpp = Bpp,
                RawData = temp
            };
            return image;
        }

        public override void Dispose() {
            RawData.Dispose();
            base.Dispose();
        }
    }
}

using System.Drawing;
namespace guideXOS.Graph {
    public unsafe class Graphics {
        public uint* VideoMemory;
        public int Width;
        public int Height;
        private FramebufferSurfaceLayout _physicalLayout;
        private bool _physicalSurfaceActive;
        // Reusable buffers for blur to avoid per-frame allocations
        int[] _blurSrc;
        int[] _blurTmp;
        int[] _blurDst;
        int _blurCapW;
        int _blurCapH;
        public Graphics(int width, int height, void* vm) {
            this.Width = width;
            this.Height = height;
            this.VideoMemory = (uint*)vm;
        }

        public bool ConfigurePhysicalSurface(FramebufferSurfaceLayout layout) {
            if (!FramebufferSurface.IsLayoutValid(layout) ||
                layout.Width != (uint)Width || layout.Height != (uint)Height)
                return false;
            _physicalLayout = layout;
            _physicalSurfaceActive = (ulong)VideoMemory == layout.BaseAddress;
            return true;
        }

        public void SetPhysicalSurfaceActive(bool active) {
            _physicalSurfaceActive = active &&
                FramebufferSurface.IsLayoutValid(_physicalLayout) &&
                (ulong)VideoMemory == _physicalLayout.BaseAddress;
        }

        public bool HasPhysicalSurface =>
            _physicalSurfaceActive &&
            FramebufferSurface.IsLayoutValid(_physicalLayout) &&
            (ulong)VideoMemory == _physicalLayout.BaseAddress;

        public bool ClearPhysical(uint color) =>
            HasPhysicalSurface && FramebufferSurface.Clear(
                VideoMemory, _physicalLayout, color);

        public bool PresentPhysicalChanges(uint* source, uint* previous) =>
            FramebufferSurface.IsLayoutValid(_physicalLayout) &&
            (ulong)VideoMemory == _physicalLayout.BaseAddress &&
            FramebufferSurface.PresentChanged(
                VideoMemory, _physicalLayout, source, previous);
        public static Graphics FromImage(Image img) {
            fixed (int* ptr = img.RawData)
                return new Graphics(img.Width, img.Height, ptr);
        }
        public virtual void Update() { }
        public virtual void Clear(uint Color) {
            if (VideoMemory == null || Width <= 0 || Height <= 0) return;
            if (HasPhysicalSurface) {
                ClearPhysical(Color);
                return;
            }
            Native.Stosd(VideoMemory, Color,
                (ulong)(uint)Width * (uint)Height);
        }
        public virtual void Copy(int dX, int dY, int sX, int sY, int Width, int Height) {
            if (Width <= 0 || Height <= 0 || this.Width <= 0 ||
                this.Height <= 0 || VideoMemory == null) return;

            long startX = 0;
            long startY = 0;
            long endX = Width;
            long endY = Height;
            if (-((long)sX) > startX) startX = -((long)sX);
            if (-((long)dX) > startX) startX = -((long)dX);
            if (-((long)sY) > startY) startY = -((long)sY);
            if (-((long)dY) > startY) startY = -((long)dY);
            long sourceMaxX = this.Width - (long)sX;
            long destinationMaxX = this.Width - (long)dX;
            long sourceMaxY = this.Height - (long)sY;
            long destinationMaxY = this.Height - (long)dY;
            if (sourceMaxX < endX) endX = sourceMaxX;
            if (destinationMaxX < endX) endX = destinationMaxX;
            if (sourceMaxY < endY) endY = sourceMaxY;
            if (destinationMaxY < endY) endY = destinationMaxY;
            if (endX <= startX || endY <= startY) return;

            bool reverseY = dY > sY && (long)dY < (long)sY + Height &&
                (long)sY < (long)dY + Height;
            bool reverseX = dY == sY && dX > sX &&
                (long)dX < (long)sX + Width &&
                (long)sX < (long)dX + Width;
            long h = reverseY ? endY - 1 : startY;
            long hEnd = reverseY ? startY - 1 : endY;
            long hStep = reverseY ? -1 : 1;
            for (; h != hEnd; h += hStep) {
                long w = reverseX ? endX - 1 : startX;
                long wEnd = reverseX ? startX - 1 : endX;
                long wStep = reverseX ? -1 : 1;
                for (; w != wEnd; w += wStep) {
                    int sourceX = (int)((long)sX + w);
                    int sourceY = (int)((long)sY + h);
                    int destinationX = (int)((long)dX + w);
                    int destinationY = (int)((long)dY + h);
                    DrawPoint(destinationX, destinationY,
                        GetPoint(sourceX, sourceY));
                }
            }
        }
        public virtual void FillRectangle(int X, int Y, int Width, int Height, uint Color) {
            if (VideoMemory == null || this.Width <= 0 || this.Height <= 0 ||
                Width <= 0 || Height <= 0) return;

            if (HasPhysicalSurface) {
                FramebufferSurface.FillRectangle(VideoMemory, _physicalLayout,
                    X, Y, Width, Height, Color);
                return;
            }

            if (!TryClipRectangle(X, Y, Width, Height,
                    this.Width, this.Height, out int x0, out int y0,
                    out int x1, out int y1)) return;

            ulong rowPixels = (ulong)(x1 - x0);
            for (int y = y0; y < y1; y++) {
                ulong offset = (ulong)(uint)y * (uint)this.Width + (uint)x0;
                Native.Stosd(VideoMemory + (long)offset, Color, rowPixels);
            }
        }
        public virtual void AFillRectangle(int X, int Y, int Width, int Height, uint Color) {
            // Optimized alpha fill to reduce per-pixel function overhead
            if (VideoMemory == null || this.Width <= 0 || this.Height <= 0 || Width <= 0 || Height <= 0) return;
            if (!TryClipRectangle(X, Y, Width, Height,
                    this.Width, this.Height, out int x0, out int y0,
                    out int x1, out int y1)) return;
            int fA = (byte)((Color >> 24) & 0xFF);
            int fR = (byte)((Color >> 16) & 0xFF);
            int fG = (byte)((Color >> 8) & 0xFF);
            int fB = (byte)(Color & 0xFF);
            int invA = 255 - fA;
            for (int yy = y0; yy < y1; yy++) {
                for (int xx = x0; xx < x1; xx++) {
                    uint bg = GetPoint(xx, yy);
                    int bR = (byte)((bg >> 16) & 0xFF);
                    int bG = (byte)((bg >> 8) & 0xFF);
                    int bB = (byte)(bg & 0xFF);
                    int r = (fR * fA + bR * invA) >> 8;
                    int g = (fG * fA + bG * invA) >> 8;
                    int b = (fB * fA + bB * invA) >> 8;
                    DrawPoint(xx, yy, System.Drawing.Color.ToArgb(
                        (byte)r, (byte)g, (byte)b));
                }
            }
        }

        public virtual uint GetPoint(int X, int Y) {
            if (VideoMemory != null && X >= 0 && Y >= 0 && X < Width && Y < Height) {
                if (HasPhysicalSurface && FramebufferSurface.TryReadPixel(
                        VideoMemory, _physicalLayout, X, Y, out uint argb))
                    return argb;
                if (HasPhysicalSurface) return 0;
                ulong offset = (ulong)(uint)Y * (uint)Width + (uint)X;
                return VideoMemory[(long)offset];
            }
            return 0;
        }

        public virtual void DrawPoint(int X, int Y, uint color, bool alphaBlending = false) {
            if (alphaBlending) {
                uint foreground = color;
                int fA = (byte)((foreground >> 24) & 0xFF);
                int fR = (byte)((foreground >> 16) & 0xFF);
                int fG = (byte)((foreground >> 8) & 0xFF);
                int fB = (byte)((foreground) & 0xFF);

                uint background = GetPoint(X, Y);
                int bA = (byte)((background >> 24) & 0xFF);
                int bR = (byte)((background >> 16) & 0xFF);
                int bG = (byte)((background >> 8) & 0xFF);
                int bB = (byte)((background) & 0xFF);

                int alpha = fA;
                int inv_alpha = 255 - alpha;

                int newR = (fR * alpha + inv_alpha * bR) >> 8;
                int newG = (fG * alpha + inv_alpha * bG) >> 8;
                int newB = (fB * alpha + inv_alpha * bB) >> 8;

                color = Color.ToArgb((byte)newR, (byte)newG, (byte)newB);
            }

            if (VideoMemory != null && X >= 0 && Y >= 0 && X < Width && Y < Height) {
                if (HasPhysicalSurface) {
                    FramebufferSurface.TryWritePixel(VideoMemory,
                        _physicalLayout, X, Y, color);
                    return;
                }
                ulong offset = (ulong)(uint)Y * (uint)Width + (uint)X;
                VideoMemory[(long)offset] = color;
            }
        }

        public virtual void DrawRectangle(int X, int Y, int Width, int Height, uint Color, int Weight = 1) {
            if (Width <= 0 || Height <= 0 || Weight <= 0) return;
            int border = Weight;
            if (border > Width) border = Width;
            if (border > Height) border = Height;
            FillRectangle(X, Y, Width, border, Color);
            FillRectangle(X, Y, border, Height, Color);

            long right = (long)X + Width - border;
            long bottom = (long)Y + Height - border;
            if (right >= int.MinValue && right <= int.MaxValue)
                FillRectangle((int)right, Y, border, Height, Color);
            if (bottom >= int.MinValue && bottom <= int.MaxValue)
                FillRectangle(X, (int)bottom, Width, border, Color);
        }

        public virtual Image Save() {
            Image image = new Image(Width, Height);
            fixed (int* ptr = image.RawData) {
                if (HasPhysicalSurface) {
                    for (int y = 0; y < Height; y++) {
                        ulong row = (ulong)(uint)y * (uint)Width;
                        for (int x = 0; x < Width; x++)
                            ptr[(long)(row + (uint)x)] = (int)GetPoint(x, y);
                    }
                } else {
                    Native.Movsd((uint*)ptr, VideoMemory,
                        (ulong)(uint)Width * (uint)Height);
                }
            }
            return image;
        }

        public virtual void ADrawImage(int X, int Y, Image image, byte alpha) {
            if (image == null || image.RawData == null ||
                VideoMemory == null ||
                !TryClipRectangle(X, Y, image.Width, image.Height,
                    Width, Height, out int x0, out int y0,
                    out int x1, out int y1)) return;
            int sourceX = x0 - X;
            int sourceY = y0 - Y;
            for (int h = 0; h < y1 - y0; h++)
                for (int w = 0; w < x1 - x0; w++) {
                    ulong sourceOffset = (ulong)(uint)(sourceY + h) *
                        (uint)image.Width + (uint)(sourceX + w);
                    uint foreground = (uint)image.RawData[(long)sourceOffset];
                    foreground &= ~0xFF000000;
                    foreground |= (uint)alpha << 24;
                    int fA = (byte)((foreground >> 24) & 0xFF);

                    if (fA != 0) {
                        DrawPoint(x0 + w, y0 + h, foreground, true);
                    }
                }
        }

        public virtual void DrawImage(int X, int Y, Image image, bool AlphaBlending = true) {
            if (image == null || image.RawData == null) return;
            if (VideoMemory == null || Width <= 0 || Height <= 0) return;

            if (AlphaBlending) {
                if (!TryClipRectangle(X, Y, image.Width, image.Height,
                        Width, Height, out int x0, out int y0,
                        out int x1, out int y1)) return;
                int sourceX = x0 - X;
                int sourceY = y0 - Y;
                for (int h = 0; h < y1 - y0; h++) {
                    for (int w = 0; w < x1 - x0; w++) {
                        ulong sourceOffset = (ulong)(uint)(sourceY + h) *
                            (uint)image.Width + (uint)(sourceX + w);
                        uint foreground = (uint)image.RawData[(long)sourceOffset];
                        int fA = (byte)((foreground >> 24) & 0xFF);
                        if (fA != 0)
                            DrawPoint(x0 + w, y0 + h, foreground, true);
                    }
                }
            } else {
                if (image.Width <= 0 || image.Height <= 0 ||
                    !TryClipRectangle(X, Y, image.Width, image.Height,
                        Width, Height, out int destinationX,
                        out int destinationY, out int destinationXEnd,
                        out int destinationYEnd)) return;

                int sourceX = destinationX - X;
                int sourceY = destinationY - Y;
                int copyWidth = destinationXEnd - destinationX;
                int copyHeight = destinationYEnd - destinationY;
                fixed (int* ptr = image.RawData) {
                    if (HasPhysicalSurface) {
                        for (int row = 0; row < copyHeight; row++) {
                            ulong sourceRow = (ulong)(uint)(sourceY + row) *
                                (uint)image.Width + (uint)sourceX;
                            for (int column = 0; column < copyWidth; column++) {
                                uint pixel = (uint)ptr[(long)(sourceRow +
                                    (uint)column)];
                                DrawPoint(destinationX + column,
                                    destinationY + row, pixel);
                            }
                        }
                    } else {
                        for (int row = 0; row < copyHeight; row++) {
                            ulong destinationOffset = (ulong)(uint)(destinationY + row) *
                                (uint)Width + (uint)destinationX;
                            ulong sourceOffset = (ulong)(uint)(sourceY + row) *
                                (uint)image.Width + (uint)sourceX;
                            Native.Movsd(VideoMemory + (long)destinationOffset,
                                (uint*)(ptr + (long)sourceOffset),
                                (ulong)(uint)copyWidth);
                        }
                    }
                }
            }
        }

        private static bool TryClipRectangle(int x, int y, int width,
            int height, int surfaceWidth, int surfaceHeight,
            out int x0, out int y0, out int x1, out int y1) {
            x0 = y0 = x1 = y1 = 0;
            if (width <= 0 || height <= 0 || surfaceWidth <= 0 ||
                surfaceHeight <= 0) return false;
            long left = x;
            long top = y;
            long right = (long)x + width;
            long bottom = (long)y + height;
            if (left < 0) left = 0;
            if (top < 0) top = 0;
            if (right > surfaceWidth) right = surfaceWidth;
            if (bottom > surfaceHeight) bottom = surfaceHeight;
            if (right <= left || bottom <= top) return false;
            x0 = (int)left;
            y0 = (int)top;
            x1 = (int)right;
            y1 = (int)bottom;
            return true;
        }

        // swaps two numbers
        void Swap(int* a, int* b) {
            int temp = *a;
            *a = *b;
            *b = temp;
        }

        // returns absolute value of number
        float Absolute(float x) {
            if (x < 0) return -x;
            else return x;
        }

        //returns integer part of a floating point number
        int IPartOfNumber(float x) {
            return (int)x;
        }

        //rounds off a number
        int RoundNumber(float x) {
            return IPartOfNumber(x + 0.5f);
        }

        //returns fractional part of a number
        float FPartOfNumber(float x) {
            if (x > 0) return x - IPartOfNumber(x);
            else return x - (IPartOfNumber(x) + 1);

        }

        //returns 1 - fractional part of number
        float RFPartOfNumber(float x) {
            return 1 - FPartOfNumber(x);
        }

        // draws a pixel on screen of given brightness
        // 0<=brightness<=1. We can use your own library
        // to draw on screen
        public virtual void DrawPoint(int X, int Y, uint Color, float Brightness) {
            byte A = (byte)((Color >> 24) & 0xFF);
            byte R = (byte)((Color >> 16) & 0xFF);
            byte G = (byte)((Color >> 8) & 0xFF);
            byte B = (byte)((Color) & 0xFF);
            A = ((byte)(A * (1f - Brightness)));
            DrawPoint(X, Y, System.Drawing.Color.ToArgb(A, R, G, B), true);
        }

        public virtual void DrawLine(int x0, int y0, int x1, int y1, uint color) {
            bool steep = Absolute(y1 - y0) > Absolute(x1 - x0);

            // swap the co-ordinates if slope > 1 or we
            // draw backwards
            if (steep) {
                Swap(&x0, &y0);
                Swap(&x1, &y1);
            }
            if (x0 > x1) {
                Swap(&x0, &x1);
                Swap(&y0, &y1);
            }

            //compute the slope
            float dx = x1 - x0;
            float dy = y1 - y0;
            float gradient = dy / dx;
            if (dx == 0.0)
                gradient = 1;

            int xpxl1 = x0;
            int xpxl2 = x1;
            float intersectY = y0;

            // main loop
            if (steep) {
                int x;
                for (x = xpxl1; x <= xpxl2; x++) {
                    // pixel coverage is determined by fractional
                    // part of y co-ordinate
                    DrawPoint(IPartOfNumber(intersectY), x, color,
                                RFPartOfNumber(intersectY));
                    DrawPoint(IPartOfNumber(intersectY) - 1, x, color,
                                FPartOfNumber(intersectY));
                    intersectY += gradient;
                }
            } else {
                int x;
                for (x = xpxl1; x <= xpxl2; x++) {
                    // pixel coverage is determined by fractional
                    // part of y co-ordinate
                    DrawPoint(x, IPartOfNumber(intersectY), color,
                                RFPartOfNumber(intersectY));
                    DrawPoint(x, IPartOfNumber(intersectY) - 1, color,
                                  FPartOfNumber(intersectY));
                    intersectY += gradient;
                }
            }

        }


        // Applies a simple box blur to the specified rectangle and writes the result back to the framebuffer.
        // Uses reusable buffers and disposes previous ones if growing to prevent leaks in environments without GC.
        public virtual void BlurRectangle(int X, int Y, int W, int H, int radius) {
            if (VideoMemory == null || W <= 0 || H <= 0 || radius <= 0 ||
                !TryClipRectangle(X, Y, W, H, Width, Height,
                    out int x0, out int y0, out int x1, out int y1)) return;
            int rw = x1 - x0; int rh = y1 - y0;

            // Ensure buffers - only grow, never shrink to reduce churn, but limit max size
            const int MaxBlurW = 2048;
            const int MaxBlurH = 2048;
            if (rw > MaxBlurW || rh > MaxBlurH) {
                // Request too large - reject to prevent massive allocations
                return;
            }
            
            if (_blurSrc == null || rw > _blurCapW || rh > _blurCapH) {
                // Dispose old buffers explicitly to avoid leaks on custom runtimes
                if (_blurSrc != null) _blurSrc.Dispose();
                if (_blurTmp != null) _blurTmp.Dispose();
                if (_blurDst != null) _blurDst.Dispose();
                // Allocate slightly larger to reduce future reallocations
                _blurCapW = (rw + 63) & ~63; // round up to multiple of 64
                _blurCapH = (rh + 63) & ~63;
                if (_blurCapW > MaxBlurW) _blurCapW = MaxBlurW;
                if (_blurCapH > MaxBlurH) _blurCapH = MaxBlurH;
                _blurSrc = new int[_blurCapW * _blurCapH];
                _blurTmp = new int[_blurCapW * _blurCapH];
                _blurDst = new int[_blurCapW * _blurCapH];
            }

            // Snapshot region
            for (int yy = 0; yy < rh; yy++) {
                int rowOff = yy * rw;
                for (int xx = 0; xx < rw; xx++) {
                    _blurSrc[rowOff + xx] = (int)GetPoint(x0 + xx, y0 + yy);
                }
            }

            // Horizontal pass (simple box blur with edge clamping)
            for (int yy = 0; yy < rh; yy++) {
                int row = yy * rw;
                for (int xx = 0; xx < rw; xx++) {
                    int r = 0, g = 0, b = 0, a = 0, count = 0;
                    int xmin = radius > xx ? 0 : xx - radius;
                    long requestedXMax = (long)xx + radius;
                    int xmax = requestedXMax >= rw ? rw - 1 : (int)requestedXMax;
                    for (int k = xmin; k <= xmax; k++) {
                        int c = _blurSrc[row + k];
                        a += (byte)(c >> 24);
                        r += (byte)(c >> 16);
                        g += (byte)(c >> 8);
                        b += (byte)(c);
                        count++;
                    }
                    a /= count; r /= count; g /= count; b /= count;
                    _blurTmp[row + xx] = (int)((a << 24) | (r << 16) | (g << 8) | b);
                }
            }

            // Vertical pass
            for (int xx = 0; xx < rw; xx++) {
                for (int yy = 0; yy < rh; yy++) {
                    int r = 0, g = 0, b = 0, a = 0, count = 0;
                    int ymin = radius > yy ? 0 : yy - radius;
                    long requestedYMax = (long)yy + radius;
                    int ymax = requestedYMax >= rh ? rh - 1 : (int)requestedYMax;
                    for (int k = ymin; k <= ymax; k++) {
                        int c = _blurTmp[k * rw + xx];
                        a += (byte)(c >> 24);
                        r += (byte)(c >> 16);
                        g += (byte)(c >> 8);
                        b += (byte)(c);
                        count++;
                    }
                    a /= count; r /= count; g /= count; b /= count;
                    _blurDst[yy * rw + xx] = (int)((a << 24) | (r << 16) | (g << 8) | b);
                }
            }

            // Write back
            for (int yy = 0; yy < rh; yy++) {
                int rowOff = yy * rw;
                for (int xx = 0; xx < rw; xx++) {
                    DrawPoint(x0 + xx, y0 + yy, (uint)_blurDst[rowOff + xx]);
                }
            }
        }

        // Explicitly release blur buffers to free memory on custom runtimes without GC.
        public void ResetBlurBuffers() {
            if (_blurSrc != null) { _blurSrc.Dispose(); _blurSrc = null; }
            if (_blurTmp != null) { _blurTmp.Dispose(); _blurTmp = null; }
            if (_blurDst != null) { _blurDst.Dispose(); _blurDst = null; }
            _blurCapW = 0; _blurCapH = 0;
        }
    }
}

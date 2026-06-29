using System.Drawing;
namespace guideXOS.Graph {
    public unsafe class Graphics {
        public uint* VideoMemory;
        public int Width;
        public int Height;
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
        public static Graphics FromImage(Image img) {
            fixed (int* ptr = img.RawData)
                return new Graphics(img.Width, img.Height, ptr);
        }
        public virtual void Update() { }
        public virtual void Clear(uint Color) {
            Native.Stosd(VideoMemory, Color, (ulong)(Width * Height));
        }
        public virtual void Copy(int dX, int dY, int sX, int sY, int Width, int Height) {
            for (int w = 0; w < Width; w++) {
                for (int h = 0; h < Height; h++) {
                    DrawPoint(dX + w, dY + h, GetPoint(sX + w, sY + h));
                }
            }
        }
        public virtual void FillRectangle(int X, int Y, int Width, int Height, uint Color) {
            if (VideoMemory == null || this.Width <= 0 || this.Height <= 0 || Width <= 0 || Height <= 0) return;
            int x0 = X;
            int y0 = Y;
            int x1 = X + Width;
            int y1 = Y + Height;

            if (x0 < 0) x0 = 0;
            if (y0 < 0) y0 = 0;
            if (x1 > this.Width) x1 = this.Width;
            if (y1 > this.Height) y1 = this.Height;
            if (x1 <= x0 || y1 <= y0 || VideoMemory == null) return;

            ulong rowPixels = (ulong)(x1 - x0);
            for (int y = y0; y < y1; y++) {
                Native.Stosd(VideoMemory + (this.Width * y) + x0, Color, rowPixels);
            }
        }
        public virtual void AFillRectangle(int X, int Y, int Width, int Height, uint Color) {
            // Optimized alpha fill to reduce per-pixel function overhead
            if (VideoMemory == null || this.Width <= 0 || this.Height <= 0 || Width <= 0 || Height <= 0) return;
            int x0 = X; int y0 = Y; int x1 = X + Width; int y1 = Y + Height;
            if (x0 < 0) x0 = 0; if (y0 < 0) y0 = 0; if (x1 > this.Width) x1 = this.Width; if (y1 > this.Height) y1 = this.Height;
            if (x1 <= x0 || y1 <= y0) return;
            int fA = (byte)((Color >> 24) & 0xFF);
            int fR = (byte)((Color >> 16) & 0xFF);
            int fG = (byte)((Color >> 8) & 0xFF);
            int fB = (byte)(Color & 0xFF);
            int invA = 255 - fA;
            for (int yy = y0; yy < y1; yy++) {
                int row = yy * this.Width;
                for (int xx = x0; xx < x1; xx++) {
                    uint bg = VideoMemory[row + xx];
                    int bR = (byte)((bg >> 16) & 0xFF);
                    int bG = (byte)((bg >> 8) & 0xFF);
                    int bB = (byte)(bg & 0xFF);
                    int r = (fR * fA + bR * invA) >> 8;
                    int g = (fG * fA + bG * invA) >> 8;
                    int b = (fB * fA + bB * invA) >> 8;
                    VideoMemory[row + xx] = System.Drawing.Color.ToArgb((byte)r, (byte)g, (byte)b);
                }
            }
        }

        public virtual uint GetPoint(int X, int Y) {
            if (VideoMemory != null && X >= 0 && Y >= 0 && X < Width && Y < Height) {
                return VideoMemory[Width * Y + X];
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
                VideoMemory[Width * Y + X] = color;
            }
        }

        public virtual void DrawRectangle(int X, int Y, int Width, int Height, uint Color, int Weight = 1) {
            FillRectangle(X, Y, Width, Weight, Color);

            FillRectangle(X, Y, Weight, Height, Color);
            FillRectangle(X + (Width - Weight), Y, Weight, Height, Color);

            FillRectangle(X, Y + (Height - Weight), Width, Weight, Color);
        }

        public virtual Image Save() {
            Image image = new Image(Width, Height);
            fixed (int* ptr = image.RawData) {
                Native.Movsd((uint*)ptr, VideoMemory, (ulong)(Width * Height));
            }
            return image;
        }

        public virtual void ADrawImage(int X, int Y, Image image, byte alpha) {
            for (int h = 0; h < image.Height; h++)
                for (int w = 0; w < image.Width; w++) {
                    uint foreground = (uint)image.RawData[image.Width * h + w];
                    foreground &= ~0xFF000000;
                    foreground |= (uint)alpha << 24;
                    int fA = (byte)((foreground >> 24) & 0xFF);

                    if (fA != 0) {
                        DrawPoint(X + w, Y + h, foreground, true);
                    }
                }
        }

        public virtual void DrawImage(int X, int Y, Image image, bool AlphaBlending = true) {
            bool cursorProbe = global::Program.IsCursorImageDrawProbeActive();
            if (cursorProbe) {
                global::Program.CursorImageProbeBreadcrumb("CURSOR_IMG_DRAWIMAGE_METHOD_ENTER");
            }

            try {
                if (cursorProbe) {
                    global::Program.CursorImageProbeBreadcrumb("CURSOR_IMG_DRAWIMAGE_SOURCE_IMAGE_NULL_CHECK_ENTER");
                }

                if (image == null || image.RawData == null) {
                    if (cursorProbe) {
                        global::Program.CursorImageProbeBreadcrumb(image == null ? "CURSOR_IMG_DRAWIMAGE_SOURCE_IMAGE=NULL" : "CURSOR_IMG_DRAWIMAGE_RAWDATA=NULL");
                        global::Program.CursorImageProbeBreadcrumb("CURSOR_IMG_DRAWIMAGE_SOURCE_IMAGE_NULL_CHECK_EXIT");
                    }
                    return;
                }

                if (cursorProbe) {
                    global::Program.CursorImageProbeBreadcrumb("CURSOR_IMG_DRAWIMAGE_SOURCE_IMAGE_NULL_CHECK_EXIT");
                    global::Program.CursorImageProbeBreadcrumb("CURSOR_IMG_DRAWIMAGE_BOUNDS_CLIPPING_ENTER");
                    bool clipped = X < 0 || Y < 0 || X + image.Width > Width || Y + image.Height > Height;
                    global::Program.CursorImageProbeBreadcrumb(clipped ? "CURSOR_IMG_DRAWIMAGE_CLIP=OUT" : "CURSOR_IMG_DRAWIMAGE_CLIP=IN");
                    global::Program.CursorImageProbeBreadcrumb("CURSOR_IMG_DRAWIMAGE_BOUNDS_CLIPPING_EXIT");
                    global::Program.CursorImageProbeBreadcrumb(AlphaBlending ? "CURSOR_IMG_DRAWIMAGE_ALPHA_BLEND_BRANCH=1" : "CURSOR_IMG_DRAWIMAGE_ALPHA_BLEND_BRANCH=0");
                }

                if (VideoMemory == null || Width <= 0 || Height <= 0) {
                    return;
                }

                if (AlphaBlending) {
                    bool firstSourcePixelLogged = false;
                    bool firstDestPixelLogged = false;
                    for (int h = 0; h < image.Height; h++)
                        for (int w = 0; w < image.Width; w++) {
                            if (cursorProbe && !firstSourcePixelLogged && h == 0 && w == 0) {
                                global::Program.CursorImageProbeBreadcrumb("CURSOR_IMG_DRAWIMAGE_FIRST_SOURCE_PIXEL_READ_ENTER");
                            }

                            uint foreground = (uint)image.RawData[image.Width * h + w];

                            if (cursorProbe && !firstSourcePixelLogged && h == 0 && w == 0) {
                                global::Program.CursorImageProbeBreadcrumb("CURSOR_IMG_DRAWIMAGE_FIRST_SOURCE_PIXEL_READ_EXIT");
                                firstSourcePixelLogged = true;
                            }

                            int fA = (byte)((foreground >> 24) & 0xFF);

                            if (fA != 0) {
                                if (cursorProbe && !firstDestPixelLogged) {
                                    global::Program.CursorImageProbeBreadcrumb("CURSOR_IMG_DRAWIMAGE_FIRST_DEST_PIXEL_WRITE_ENTER");
                                }

                                DrawPoint(X + w, Y + h, foreground, true);

                                if (cursorProbe && !firstDestPixelLogged) {
                                    global::Program.CursorImageProbeBreadcrumb("CURSOR_IMG_DRAWIMAGE_FIRST_DEST_PIXEL_WRITE_EXIT");
                                    firstDestPixelLogged = true;
                                }
                            }
                        }
                } else {
                    int _x = 0;
                    int _y = 0;
                    int clip_x = 0;
                    int clip_y = 0;

                    if (X < 0) _x = X;
                    if (Y < 0) _y = Y;
                    if (X + image.Width >= Width) clip_x = X - (Width - image.Width - 1);
                    if (Y + image.Height >= Height) clip_y = Y - (Height - image.Height - 1);
                    if (
                        _x! >= -image.Width &&
                        _y! >= -image.Height &&

                        clip_x < image.Width &&
                        clip_y < image.Height
                        )
                        fixed (int* ptr = image.RawData)
                            for (int h = 1; h < image.Height + _y - clip_y + 1; h++) {
                                Native.Movsd(VideoMemory + (Width * ((Y - _y) + h) + (X - _x)) + 1, (uint*)(ptr + ((h - _y) * image.Width) + 1 - _x), (ulong)(image.Width + _x - clip_x));
                            }
                }
            } finally {
                if (cursorProbe) {
                    global::Program.CursorImageProbeBreadcrumb("CURSOR_IMG_DRAWIMAGE_METHOD_EXIT");
                }
            }
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
            if (W <= 0 || H <= 0 || radius <= 0) return;

            // Clamp region to screen bounds
            int x0 = X < 0 ? 0 : X;
            int y0 = Y < 0 ? 0 : Y;
            int x1 = X + W; if (x1 > Width) x1 = Width;
            int y1 = Y + H; if (y1 > Height) y1 = Height;
            int rw = x1 - x0; int rh = y1 - y0;
            if (rw <= 0 || rh <= 0) return;

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
                int sy = y0 + yy; int rowOff = yy * rw;
                int fbRow = sy * Width;
                for (int xx = 0; xx < rw; xx++) {
                    int sx = x0 + xx;
                    _blurSrc[rowOff + xx] = (int)VideoMemory[fbRow + sx];
                }
            }

            // Horizontal pass (simple box blur with edge clamping)
            for (int yy = 0; yy < rh; yy++) {
                int row = yy * rw;
                for (int xx = 0; xx < rw; xx++) {
                    int r = 0, g = 0, b = 0, a = 0, count = 0;
                    int xmin = xx - radius; if (xmin < 0) xmin = 0;
                    int xmax = xx + radius; if (xmax >= rw) xmax = rw - 1;
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
                    int ymin = yy - radius; if (ymin < 0) ymin = 0;
                    int ymax = yy + radius; if (ymax >= rh) ymax = rh - 1;
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
                int sy = y0 + yy; int fbRow = sy * Width;
                int rowOff = yy * rw;
                for (int xx = 0; xx < rw; xx++) {
                    int sx = x0 + xx;
                    VideoMemory[fbRow + sx] = (uint)_blurDst[rowOff + xx];
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

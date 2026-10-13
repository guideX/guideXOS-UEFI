using System;

namespace guideXOS.GUI {
    /// <summary>
    /// Resolves whether the desktop may begin a pointer gesture after higher
    /// priority GUI surfaces have had their normal input pass.
    /// </summary>
    public static class UefiDesktopInputArbitration {
        public static bool CanBeginPointerCapture(int x, int y,
                int screenWidth, int workAreaBottom, bool alreadyHandled,
                bool windowMoving, bool overVisibleWindow) {
            return x >= 0 && x < screenWidth && y >= 0 &&
                   y < workAreaBottom && !alreadyHandled &&
                   !windowMoving && !overVisibleWindow;
        }
    }

    /// <summary>
    /// Draw and input bounds for one UEFI desktop tile. The bounds include
    /// its square tile and the label drawn below it.
    /// </summary>
    public struct UefiDesktopIconGeometry {
        public int IconId;
        public int IconX;
        public int IconY;
        public int TileSize;
        public int LabelX;
        public int LabelY;
        public int LabelWidth;
        public int LabelHeight;
        public int HitX;
        public int HitY;
        public int HitWidth;
        public int HitHeight;
        public int HitOffsetX => HitX - IconX;
        public int HitOffsetY => HitY - IconY;

        public bool Contains(int x, int y) {
            return x >= HitX && y >= HitY &&
                   x < HitX + HitWidth && y < HitY + HitHeight;
        }

        public static UefiDesktopIconGeometry Create(int iconId,
                int iconX, int iconY, int tileSize,
                int labelWidth, int labelHeight, int labelGap) {
            if (tileSize < 0) tileSize = 0;
            if (labelWidth < 0) labelWidth = 0;
            if (labelHeight < 0) labelHeight = 0;
            if (labelGap < 0) labelGap = 0;

            int labelX = iconX + ((tileSize - labelWidth) / 2);
            int labelY = iconY + tileSize + labelGap;
            int hitX = Math.Min(iconX, labelX);
            int hitRight = Math.Max(iconX + tileSize, labelX + labelWidth);
            int hitBottom = labelHeight > 0
                ? labelY + labelHeight : iconY + tileSize;

            return new UefiDesktopIconGeometry {
                IconId = iconId,
                IconX = iconX,
                IconY = iconY,
                TileSize = tileSize,
                LabelX = labelX,
                LabelY = labelY,
                LabelWidth = labelWidth,
                LabelHeight = labelHeight,
                HitX = hitX,
                HitY = iconY,
                HitWidth = hitRight - hitX,
                HitHeight = hitBottom - iconY
            };
        }

        public static int HitTest(UefiDesktopIconGeometry[] icons,
                                  int x, int y) {
            if (icons == null) return -1;
            for (int i = icons.Length - 1; i >= 0; i--) {
                if (icons[i].Contains(x, y)) return icons[i].IconId;
            }
            return -1;
        }

        public static void ClampPosition(int x, int y, int hitOffsetX,
                int hitOffsetY, int hitWidth, int hitHeight,
                int workAreaWidth, int workAreaBottom,
                out int clampedX, out int clampedY) {
            int minX = -hitOffsetX;
            int minY = -hitOffsetY;
            int maxX = workAreaWidth - hitOffsetX - hitWidth;
            int maxY = workAreaBottom - hitOffsetY - hitHeight;
            if (maxX < minX) maxX = minX;
            if (maxY < minY) maxY = minY;
            clampedX = Math.Clamp(x, minX, maxX);
            clampedY = Math.Clamp(y, minY, maxY);
        }
    }

    /// <summary>
    /// State machine for the UEFI desktop's single-pointer icon gestures.
    /// It owns a press until release, distinguishes clicks from drags, and
    /// recognizes a bounded double-click for activation.
    /// </summary>
    public sealed class UefiDesktopInteraction {
        public const int NoIcon = -1;
        public const int DragThresholdPixels = 5;
        public const ulong DoubleClickIntervalMilliseconds = 500;
        public const int DoubleClickDistancePixels = 8;

        private bool _hasPointerCapture;
        private int _capturedIconId = NoIcon;
        private int _selectedIconId = NoIcon;
        private int _pressX;
        private int _pressY;
        private ulong _pressTicks;
        private int _iconOffsetX;
        private int _iconOffsetY;
        private bool _dragStarted;
        private int _dragX;
        private int _dragY;
        private int _lastClickIconId = NoIcon;
        private int _lastClickX;
        private int _lastClickY;
        private ulong _lastClickTicks;

        public bool HasPointerCapture => _hasPointerCapture;
        public int CapturedIconId => _capturedIconId;
        public int SelectedIconId => _selectedIconId;
        public bool IsDragging => _hasPointerCapture && _dragStarted;
        public int DraggedIconId => IsDragging ? _capturedIconId : NoIcon;
        public int DragX => _dragX;
        public int DragY => _dragY;

        public void Reset() {
            _hasPointerCapture = false;
            _capturedIconId = NoIcon;
            _selectedIconId = NoIcon;
            _pressX = _pressY = 0;
            _pressTicks = 0;
            _iconOffsetX = _iconOffsetY = 0;
            _dragStarted = false;
            _dragX = _dragY = 0;
            ClearLastClick();
        }

        public bool BeginPress(int hitIconId, int pointerX, int pointerY,
                int iconX, int iconY, ulong nowTicks, bool inputAvailable) {
            if (!inputAvailable || _hasPointerCapture) return false;

            _hasPointerCapture = true;
            _capturedIconId = hitIconId;
            _selectedIconId = hitIconId;
            _pressX = pointerX;
            _pressY = pointerY;
            _pressTicks = nowTicks;
            _iconOffsetX = pointerX - iconX;
            _iconOffsetY = pointerY - iconY;
            _dragStarted = false;
            _dragX = iconX;
            _dragY = iconY;
            return true;
        }

        public bool UpdatePointer(int pointerX, int pointerY,
                int hitOffsetX, int hitOffsetY, int hitWidth,
                int hitHeight, int workAreaWidth, int workAreaBottom) {
            if (!_hasPointerCapture || _capturedIconId == NoIcon) return false;

            int dx = pointerX - _pressX;
            int dy = pointerY - _pressY;
            if (!_dragStarted &&
                dx * dx + dy * dy > DragThresholdPixels * DragThresholdPixels) {
                _dragStarted = true;
                ClearLastClick();
            }
            if (!_dragStarted) return false;

            UefiDesktopIconGeometry.ClampPosition(
                pointerX - _iconOffsetX, pointerY - _iconOffsetY,
                hitOffsetX, hitOffsetY, hitWidth, hitHeight,
                workAreaWidth, workAreaBottom,
                out _dragX, out _dragY);
            return true;
        }

        public bool EndPress(int releaseHitIconId, int pointerX, int pointerY,
                ulong nowTicks, out int activatedIconId,
                out int movedIconId, out int movedX, out int movedY) {
            activatedIconId = NoIcon;
            movedIconId = NoIcon;
            movedX = movedY = 0;
            if (!_hasPointerCapture) return false;

            if (_capturedIconId != NoIcon) {
                if (_dragStarted) {
                    movedIconId = _capturedIconId;
                    movedX = _dragX;
                    movedY = _dragY;
                    ClearLastClick();
                } else if (releaseHitIconId == _capturedIconId) {
                    bool isDoubleClick =
                        _lastClickIconId == _capturedIconId &&
                        _pressTicks >= _lastClickTicks &&
                        _pressTicks - _lastClickTicks <=
                            DoubleClickIntervalMilliseconds &&
                        DistanceSquared(_lastClickX, _lastClickY,
                                        _pressX, _pressY) <=
                            DoubleClickDistancePixels * DoubleClickDistancePixels;
                    if (isDoubleClick) {
                        activatedIconId = _capturedIconId;
                        ClearLastClick();
                    } else {
                        _lastClickIconId = _capturedIconId;
                        _lastClickX = pointerX;
                        _lastClickY = pointerY;
                        _lastClickTicks = nowTicks;
                    }
                } else {
                    ClearLastClick();
                }
            } else {
                ClearLastClick();
            }

            _hasPointerCapture = false;
            _capturedIconId = NoIcon;
            _pressX = _pressY = 0;
            _pressTicks = 0;
            _iconOffsetX = _iconOffsetY = 0;
            _dragStarted = false;
            return true;
        }

        public void CancelPointerCapture() {
            _hasPointerCapture = false;
            _capturedIconId = NoIcon;
            _pressX = _pressY = 0;
            _pressTicks = 0;
            _iconOffsetX = _iconOffsetY = 0;
            _dragStarted = false;
            ClearLastClick();
        }

        private static int DistanceSquared(int x1, int y1, int x2, int y2) {
            int dx = x1 - x2;
            int dy = y1 - y2;
            return dx * dx + dy * dy;
        }

        private void ClearLastClick() {
            _lastClickIconId = NoIcon;
            _lastClickX = _lastClickY = 0;
            _lastClickTicks = 0;
        }
    }

    /// <summary>
    /// Applies an image hotspot so the mouse event point stays at the
    /// cursor's actual pointing pixel.
    /// </summary>
    public static class UefiCursorGeometry {
        public static void GetTopLeft(int pointerX, int pointerY,
                int hotspotX, int hotspotY, out int left, out int top) {
            left = pointerX - hotspotX;
            top = pointerY - hotspotY;
        }
    }
}

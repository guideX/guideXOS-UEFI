using System;
using guideXOS.GUI;

internal static class Program {
    private static int _assertions;

    private static void Check(bool condition, string name) {
        _assertions++;
        if (!condition) throw new InvalidOperationException("FAIL: " + name);
    }

    private static void Main() {
        HitTestingUsesTileAndLabelBounds();
        SelectionAndDoubleClickAreDistinct();
        DragThresholdAndOffsetAreStable();
        DragClampsAtWorkAreaEdges();
        ArbitrationHonorsHigherPrioritySurfaces();
        CursorHotspotMatchesPointerCoordinates();
        Console.WriteLine("UEFI desktop interaction tests: PASS (" +
                          _assertions + " assertions)");
    }

    private static void HitTestingUsesTileAndLabelBounds() {
        UefiDesktopIconGeometry icon =
            UefiDesktopIconGeometry.Create(0, 40, 60, 32, 40, 16, 4);
        UefiDesktopIconGeometry[] icons = { icon };
        Check(UefiDesktopIconGeometry.HitTest(icons, 50, 70) == 0,
              "hit inside graphic");
        Check(UefiDesktopIconGeometry.HitTest(icons, 38, 98) == 0,
              "hit inside label");
        Check(UefiDesktopIconGeometry.HitTest(icons, 80, 70) == -1,
              "outside graphic and label is empty desktop");
        Check(icon.HitX == icon.LabelX && icon.HitWidth == 40,
              "selection and input share the exact label-inclusive bounds");
    }

    private static void SelectionAndDoubleClickAreDistinct() {
        UefiDesktopInteraction state = new UefiDesktopInteraction();
        Check(state.BeginPress(0, 50, 70, 40, 60, 1000, true),
              "first press captured");
        Check(state.SelectedIconId == 0, "single press selects immediately");
        Check(state.EndPress(0, 50, 70, 1100, out int activated,
              out int moved, out _, out _), "first release consumed");
        Check(activated == -1 && moved == -1,
              "single click does not activate or move");
        Check(!state.HasPointerCapture, "release clears pointer capture");

        Check(state.BeginPress(0, 52, 71, 40, 60, 1200, true),
              "second press captured");
        Check(state.EndPress(0, 52, 71, 1280, out activated,
              out moved, out _, out _), "second release consumed");
        Check(activated == 0 && moved == -1,
              "bounded nearby double click activates once");

        Check(state.BeginPress(0, 50, 70, 40, 60, 2000, true),
              "new isolated click captured");
        state.EndPress(0, 50, 70, 2100, out activated,
            out moved, out _, out _);
        Check(activated == -1, "click after timeout remains selection only");

        Check(state.BeginPress(1, 180, 70, 160, 60, 2200, true),
              "different icon press captured");
        Check(state.SelectedIconId == 1,
              "pressing another icon changes selection");
        state.EndPress(1, 180, 70, 2250, out activated,
            out moved, out _, out _);
        Check(activated == -1, "different icon does not inherit prior double click");

        Check(state.BeginPress(-1, 400, 200, 0, 0, 2300, true),
              "empty desktop press captured");
        Check(state.SelectedIconId == -1, "empty desktop clears selection");
        state.EndPress(-1, 400, 200, 2350, out activated,
            out moved, out _, out _);
        Check(activated == -1, "empty desktop release does not activate");
    }

    private static void DragThresholdAndOffsetAreStable() {
        UefiDesktopInteraction state = new UefiDesktopInteraction();
        Check(state.BeginPress(2, 55, 70, 40, 60, 1000, true),
              "drag candidate captured");
        Check(!state.UpdatePointer(58, 70, 0, 0, 32, 52, 800, 760),
              "movement below threshold stays a click");
        Check(!state.IsDragging, "sub-threshold movement does not drag");
        Check(state.UpdatePointer(61, 70, 0, 0, 32, 52, 800, 760),
              "movement above threshold updates drag");
        Check(state.IsDragging, "drag state begins past threshold");
        Check(state.DragX == 46 && state.DragY == 60,
              "pointer-to-icon offset is preserved");
        state.EndPress(-1, 61, 70, 1100, out int activated,
            out int moved, out int movedX, out int movedY);
        Check(moved == 2 && movedX == 46 && movedY == 60,
              "release commits the moved icon position");
        Check(activated == -1, "drag is not mistaken for activation");

        Check(state.BeginPress(2, 55, 70, 40, 60, 2000, true),
              "release re-arms pointer capture");
        Check(!state.BeginPress(1, 170, 70, 160, 60, 2050, true),
              "second gesture cannot steal an active capture");
        state.CancelPointerCapture();
        Check(!state.HasPointerCapture, "explicit cancellation clears capture");
    }

    private static void DragClampsAtWorkAreaEdges() {
        UefiDesktopIconGeometry icon =
            UefiDesktopIconGeometry.Create(0, 0, 0, 32, 40, 16, 4);
        UefiDesktopIconGeometry.ClampPosition(9000, 9000,
            icon.HitOffsetX, icon.HitOffsetY, icon.HitWidth,
            icon.HitHeight, 800, 760, out int right, out int bottom);
        UefiDesktopIconGeometry atRightBottom =
            UefiDesktopIconGeometry.Create(0, right, bottom, 32, 40, 16, 4);
        Check(atRightBottom.HitX + atRightBottom.HitWidth == 800,
              "right edge clamps the full hit rectangle");
        Check(atRightBottom.HitY + atRightBottom.HitHeight == 760,
              "bottom edge clamps above the taskbar work area");

        UefiDesktopIconGeometry.ClampPosition(-9000, -9000,
            icon.HitOffsetX, icon.HitOffsetY, icon.HitWidth,
            icon.HitHeight, 800, 760, out int left, out int top);
        UefiDesktopIconGeometry atLeftTop =
            UefiDesktopIconGeometry.Create(0, left, top, 32, 40, 16, 4);
        Check(atLeftTop.HitX == 0 && atLeftTop.HitY == 0,
              "negative restored coordinates clamp to the visible desktop");
    }

    private static void ArbitrationHonorsHigherPrioritySurfaces() {
        Check(UefiDesktopInputArbitration.CanBeginPointerCapture(
            80, 120, 800, 760, false, false, false),
            "unclaimed desktop point is available");
        Check(!UefiDesktopInputArbitration.CanBeginPointerCapture(
            80, 120, 800, 760, true, false, false),
            "taskbar or menu handled input blocks desktop capture");
        Check(!UefiDesktopInputArbitration.CanBeginPointerCapture(
            80, 120, 800, 760, false, false, true),
            "visible window or widget blocks desktop capture");
        Check(!UefiDesktopInputArbitration.CanBeginPointerCapture(
            80, 120, 800, 760, false, true, false),
            "active window movement blocks desktop capture");
        Check(!UefiDesktopInputArbitration.CanBeginPointerCapture(
            80, 780, 800, 760, false, false, false),
            "taskbar work area is outside desktop gesture bounds");
        Check(!new UefiDesktopInteraction().BeginPress(
            0, 80, 120, 40, 60, 1000, false),
            "context-menu ownership rejects desktop capture");
    }

    private static void CursorHotspotMatchesPointerCoordinates() {
        UefiCursorGeometry.GetTopLeft(0, 0, 1, 1,
            out int left, out int top);
        Check(left + 1 == 0 && top + 1 == 0,
              "arrow tip aligns at upper-left pointer coordinate");
        UefiCursorGeometry.GetTopLeft(799, 759, 11, 0,
            out left, out top);
        Check(left + 11 == 799 && top == 759,
              "pressed cursor hotspot aligns at lower-right pointer coordinate");
    }
}

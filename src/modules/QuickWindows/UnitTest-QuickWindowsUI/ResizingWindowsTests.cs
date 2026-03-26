// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using QuickWindows;
using QuickWindows.Features;
using QuickWindows.Interfaces;

namespace Microsoft.QuickWindows.UnitTests;

[TestClass]
public class ResizingWindowsTests
{
    private Mock<ITargetWindow> _mockTargetWindow = null!;
    private Mock<IRateLimiter> _mockRateLimiter = null!;
    private Mock<ISnappingWindows> _mockSnappingWindows = null!;
    private ResizingWindows _resizingWindows = null!;

    // Captured arguments passed to SnapResizingWindow
    private int _capturedLeft;
    private int _capturedTop;
    private int _capturedRight;
    private int _capturedBottom;

    [TestInitialize]
    public void Setup()
    {
        _mockTargetWindow = new Mock<ITargetWindow>();
        _mockRateLimiter = new Mock<IRateLimiter>();
        _mockSnappingWindows = new Mock<ISnappingWindows>();

        _mockRateLimiter.Setup(r => r.IsLimited()).Returns(false);

        // Capture the input values and pass them through unchanged
        _mockSnappingWindows
            .Setup(s => s.SnapResizingWindow(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<ResizeOperation>()))
            .Callback<int, int, int, int, ResizeOperation>((l, t, r, b, _) =>
            {
                _capturedLeft = l;
                _capturedTop = t;
                _capturedRight = r;
                _capturedBottom = b;
            })
            .Returns((int l, int t, int r, int b, ResizeOperation _) => (l, t, r, b));

        _resizingWindows = new ResizingWindows(
            _mockTargetWindow.Object,
            _mockRateLimiter.Object,
            _mockSnappingWindows.Object);
    }

    private void SetupTargetWindow(int left, int top, int right, int bottom)
    {
        _mockTargetWindow
            .Setup(t => t.InitialPlacement)
            .Returns(new NativeMethods.Rect { left = left, top = top, right = right, bottom = bottom });
        _mockTargetWindow.Setup(t => t.HWnd).Returns(new System.IntPtr(1));
    }

    [TestMethod]
    public void WhenResizingFromTopLeftAndWindowIsTooSmallTheLeftEdgeClampsNotRight()
    {
        // Window: 400x400 at (100,100)–(500,500). Drag top-left corner so far right/down
        // that the computed size would be only 5x5.
        SetupTargetWindow(left: 100, top: 100, right: 500, bottom: 500);
        _resizingWindows.StartResize(x: 100, y: 100); // top-left quadrant

        _resizingWindows.ResizeWindow(x: 495, y: 495); // deltaX=+395, deltaY=+395 → newLeft=495, newTop=495

        // The right edge is fixed at 500, bottom at 500.
        // Minimum width = 200, so newLeft should be clamped to 500-200=300 (not newRight pushed to 695).
        Assert.AreEqual(300, _capturedLeft, "Left edge should be clamped when ResizeTopLeft makes window too narrow");
        Assert.AreEqual(500, _capturedRight, "Right edge must stay fixed for ResizeTopLeft");

        Assert.AreEqual(300, _capturedTop, "Top edge should be clamped when ResizeTopLeft makes window too short");
        Assert.AreEqual(500, _capturedBottom, "Bottom edge must stay fixed for ResizeTopLeft");
    }

    [TestMethod]
    public void WhenResizingFromBottomRightAndWindowIsTooSmallTheRightEdgeClampsNotLeft()
    {
        // Window: 400x400 at (100,100)–(500,500). Drag bottom-right corner so far left/up
        // that the computed size would be only 5x5.
        SetupTargetWindow(left: 100, top: 100, right: 500, bottom: 500);
        _resizingWindows.StartResize(x: 499, y: 499); // bottom-right quadrant

        _resizingWindows.ResizeWindow(x: 105, y: 105); // deltaX=-395, deltaY=-395 → newRight=105, newBottom=105

        // The left edge is fixed at 100, top at 100.
        // Minimum width = 200, so newRight should be clamped to 100+200=300 (not newLeft pushed to -95).
        Assert.AreEqual(100, _capturedLeft, "Left edge must stay fixed for ResizeBottomRight");
        Assert.AreEqual(300, _capturedRight, "Right edge should be clamped when ResizeBottomRight makes window too narrow");

        Assert.AreEqual(100, _capturedTop, "Top edge must stay fixed for ResizeBottomRight");
        Assert.AreEqual(300, _capturedBottom, "Bottom edge should be clamped when ResizeBottomRight makes window too short");
    }

    [TestMethod]
    public void WhenResizingFromTopRightAndWindowIsTooSmallTheRightEdgeClampsNotLeft()
    {
        SetupTargetWindow(left: 100, top: 100, right: 500, bottom: 500);
        _resizingWindows.StartResize(x: 499, y: 100); // top-right quadrant (x>0.5, y<0.5)

        _resizingWindows.ResizeWindow(x: 105, y: 495); // newRight=105 (too small), newTop=495 (too small)

        Assert.AreEqual(100, _capturedLeft, "Left edge must stay fixed for ResizeTopRight");
        Assert.AreEqual(300, _capturedRight, "Right edge should be clamped for ResizeTopRight");
        Assert.AreEqual(300, _capturedTop, "Top edge should be clamped for ResizeTopRight");
        Assert.AreEqual(500, _capturedBottom, "Bottom edge must stay fixed for ResizeTopRight");
    }

    [TestMethod]
    public void WhenResizingFromBottomLeftAndWindowIsTooSmallTheLeftEdgeClampsNotRight()
    {
        SetupTargetWindow(left: 100, top: 100, right: 500, bottom: 500);
        _resizingWindows.StartResize(x: 100, y: 499); // bottom-left quadrant (x<0.5, y>0.5)

        _resizingWindows.ResizeWindow(x: 495, y: 105); // newLeft=495 (too small), newBottom=105 (too small)

        Assert.AreEqual(300, _capturedLeft, "Left edge should be clamped for ResizeBottomLeft");
        Assert.AreEqual(500, _capturedRight, "Right edge must stay fixed for ResizeBottomLeft");
        Assert.AreEqual(100, _capturedTop, "Top edge must stay fixed for ResizeBottomLeft");
        Assert.AreEqual(300, _capturedBottom, "Bottom edge should be clamped for ResizeBottomLeft");
    }
}

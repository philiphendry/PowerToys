// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using QuickWindows;
using QuickWindows.Interfaces;
using QuickWindows.Keyboard;
using QuickWindows.Mouse;

namespace Microsoft.QuickWindows.UnitTests;

[TestClass]
public class QuickWindowsManagerTests
{
    private Mock<IKeyboardMonitor> _mockKeyboardMonitor = null!;
    private Mock<IMouseHook> _mockMouseHook = null!;
    private Mock<ITargetWindow> _mockTargetWindow = null!;
    private Mock<IMovingWindows> _mockMovingWindows = null!;
    private Mock<IResizingWindows> _mockResizingWindows = null!;
    private Mock<ITransparentWindows> _mockTransparentWindows = null!;
    private Mock<IRolodexWindows> _mockRolodexWindows = null!;
    private Mock<ICursorForOperation> _mockCursorForOperation = null!;
    private Mock<IExclusionDetector> _mockExclusionDetector = null!;
    private Mock<IExclusionFilter> _mockExclusionFilter = null!;
    private Mock<IRestoreMaximised> _mockRestoreMaximised = null!;

    private QuickWindowsManager _quickWindowsManager = null!;

    [TestInitialize]
    public async Task Setup()
    {
        _mockKeyboardMonitor = new Mock<IKeyboardMonitor>();
        _mockMouseHook = new Mock<IMouseHook>();
        _mockTargetWindow = new Mock<ITargetWindow>();
        _mockMovingWindows = new Mock<IMovingWindows>();
        _mockResizingWindows = new Mock<IResizingWindows>();
        _mockTransparentWindows = new Mock<ITransparentWindows>();
        _mockRolodexWindows = new Mock<IRolodexWindows>();
        _mockCursorForOperation = new Mock<ICursorForOperation>();
        _mockExclusionDetector = new Mock<IExclusionDetector>();
        _mockExclusionFilter = new Mock<IExclusionFilter>();
        _mockRestoreMaximised = new Mock<IRestoreMaximised>();

        _quickWindowsManager = new QuickWindowsManager(
            _mockKeyboardMonitor.Object,
            _mockMouseHook.Object,
            _mockTargetWindow.Object,
            _mockMovingWindows.Object,
            _mockResizingWindows.Object,
            _mockTransparentWindows.Object,
            _mockRolodexWindows.Object,
            _mockCursorForOperation.Object,
            _mockExclusionDetector.Object,
            _mockExclusionFilter.Object,
            _mockRestoreMaximised.Object);
        await _quickWindowsManager.StartAsync(default);

        _mockTargetWindow.Setup(t => t.HaveTargetWindow).Returns(true);
    }

    private HotKeyEventArgs HotKeyPress()
    {
        var hotKeyEventArgs = new HotKeyEventArgs();
        _mockKeyboardMonitor.Raise(k => k.HotKeyPressed += null!, hotKeyEventArgs);
        return hotKeyEventArgs;
    }

    private HotKeyEventArgs HotKeyRelease()
    {
        var hotKeyEventArgs = new HotKeyEventArgs();
        _mockKeyboardMonitor.Raise(k => k.HotKeyReleased += null!, hotKeyEventArgs);
        return hotKeyEventArgs;
    }

    private void MouseLeftButtonDown()
    {
        _mockMouseHook.Raise(m => m.MouseDown += null!, new MouseHook.MouseButtonEventArgs(100, 100, MouseButton.Left));
    }

    private void MouseLeftButtonUp()
    {
        _mockMouseHook.Raise(m => m.MouseUp += null!, new MouseHook.MouseButtonEventArgs(100, 100, MouseButton.Left));
    }

    private void MouseRightButtonDown()
    {
        _mockMouseHook.Raise(m => m.MouseDown += null!, new MouseHook.MouseButtonEventArgs(100, 100, MouseButton.Right));
    }

    private void MouseRightButtonUp()
    {
        _mockMouseHook.Raise(m => m.MouseUp += null!, new MouseHook.MouseButtonEventArgs(100, 100, MouseButton.Right));
    }

    private void MouseMove()
    {
        _mockMouseHook.Raise(m => m.MouseMove += null!, new MouseHook.MouseMoveEventArgs(100, 100));
    }

    private void MouseWheelUp()
    {
        _mockMouseHook.Raise(m => m.MouseWheel += null!, new MouseHook.MouseMoveWheelEventArgs(100, 100, 1));
    }

    [TestMethod]
    public void WhenTheManagerIsStartedTheKeyboardHookIsInstalled()
    {
        _mockKeyboardMonitor.Verify(k => k.Install(), Times.Once);
    }

    [TestMethod]
    public async Task WhenTheManagerIsStoppedTheKeyboardHookIsUninstalled()
    {
        await _quickWindowsManager.StopAsync(default);
        _mockKeyboardMonitor.Verify(k => k.Uninstall(), Times.Once);
    }

    [TestMethod]
    public async Task WhenTheManagerIsStoppedTheMouseHookIsUninstalled()
    {
        await _quickWindowsManager.StopAsync(default);
        _mockMouseHook.Verify(m => m.Uninstall(), Times.Once);
    }

    [TestMethod]
    public void WhenTheHotKeyIsPressedTheManagerIsActivatedAndMouseHookInstalled()
    {
        HotKeyPress();
        Assert.IsTrue(_quickWindowsManager.IsHotKeyActivated);
        _mockMouseHook.Verify(m => m.Install(false), Times.Once);
    }

    [TestMethod]
    public void WhenTheHotKeyIsReleasedTheManagerIsDeactivatedAndMouseHookUninstalled()
    {
        HotKeyPress();
        HotKeyRelease();
        Assert.IsFalse(_quickWindowsManager.IsHotKeyActivated);
        _mockMouseHook.Verify(m => m.Uninstall(), Times.Once);
    }

    [TestMethod]
    public void WhenTheHotKeyIsReleasedWithAnOperationInProgressThenTheControlKeyIsPressedToSuppressMenuActivation()
    {
        HotKeyPress();
        MouseLeftButtonDown();
        var hotKeyEventArgs = HotKeyRelease();
        _mockKeyboardMonitor.Verify(k => k.SendControlKey(), Times.Once);
        Assert.IsFalse(_quickWindowsManager.IsHotKeyActivated);
        _mockMouseHook.Verify(m => m.Uninstall(), Times.Never);
    }

    [TestMethod]
    public void WhenTheHotKeyIsReleasedAfterAnOperationHasOccurredThenTheControlKeyIsPressedToSuppressMenuActivation()
    {
        HotKeyPress();
        MouseLeftButtonDown();
        HotKeyRelease();
        _mockMouseHook.Reset();
        var eventArgs = HotKeyPress();
        _mockKeyboardMonitor.Verify(k => k.SendControlKey(), Times.Once);
        _mockMouseHook.Verify(m => m.Install(false), Times.Never);
    }

    [TestMethod]
    public void WhenTheHotKeyIsPressedAndThenTheLeftMouseButtonIsClickedAnOperationNotInProgress()
    {
        HotKeyPress();
        MouseLeftButtonDown();
        MouseLeftButtonUp();
        Assert.IsFalse(_quickWindowsManager.OperationInProgress);
    }

    [TestMethod]
    public void WhenTheHotKeyIsPressedAndThenMousePressedMovedAndReleasedThenAnOperationHasOccurred()
    {
        HotKeyPress();
        MouseLeftButtonDown();
        MouseMove();
        MouseLeftButtonUp();
        Assert.IsTrue(_quickWindowsManager.OperationHasOccurred);
    }

    [TestMethod]
    public void WhenAnOperationEndsAndTheHotKeyIsReleasedTheManagerIsDeactivated()
    {
        HotKeyPress();
        MouseLeftButtonDown();
        MouseMove();
        MouseLeftButtonUp();
        HotKeyRelease();
        Assert.IsFalse(_quickWindowsManager.IsHotKeyActivated);
        _mockMouseHook.Verify(m => m.Uninstall(), Times.Once);
    }

    [TestMethod]
    public void WhenAnOperationEndsAndTheHotKeyIsNotPressedTheManagerIsDeactivated()
    {
        HotKeyPress();
        MouseLeftButtonDown();
        HotKeyRelease();
        MouseMove();
        MouseLeftButtonUp();
        Assert.IsFalse(_quickWindowsManager.IsHotKeyActivated);
        _mockMouseHook.Verify(m => m.Uninstall(), Times.Once);
    }

    [TestMethod]
    public void WhenAnOperationEndsAndTheHotKeyIsStillPressedTheManagerRemainsActive()
    {
        HotKeyPress();
        MouseLeftButtonDown();
        MouseMove();
        MouseLeftButtonUp();
        Assert.IsTrue(_quickWindowsManager.IsHotKeyActivated);
        _mockMouseHook.Verify(m => m.Uninstall(), Times.Never);
    }

    [TestMethod]
    public void WhenTheExclusionDetectorIsEnabledWhenTheHotKeyIsPressedAndTheMouseMovedTheDetectionBegins()
    {
        _mockExclusionDetector.Setup(m => m.IsEnabled).Returns(true);
        HotKeyPress();
        MouseMove();
        _mockCursorForOperation.Verify(c => c.StartExclusionDetection(It.IsAny<int>(), It.IsAny<int>()), Times.Once);
        Assert.IsTrue(_quickWindowsManager.OperationInProgress);
        _mockCursorForOperation.Verify(c => c.MoveToCursor(It.IsAny<int>(), It.IsAny<int>()), Times.Once);
        Assert.IsTrue(_quickWindowsManager.OperationHasOccurred);
    }

    [TestMethod]
    public void WhenTheExclusionDetectorIsEnabledAndTheMouseIsClickedTheWindowBeneathTheCursorIsExcluded()
    {
        _mockExclusionDetector.Setup(m => m.IsEnabled).Returns(true);
        HotKeyPress();
        MouseLeftButtonDown();
        _mockExclusionDetector.Verify(e => e.ExcludeWindowAtCursor(), Times.Once);
        _mockMovingWindows.Verify(m => m.MoveWindow(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [TestMethod]
    public void WhenTheHotKeyIsPressedAndTheMouseIsClickedOnAnExcludedWindowTheOperationIsSupressed()
    {
        _mockExclusionFilter.Setup(m => m.IsWindowAtCursorExcluded()).Returns(true);
        HotKeyPress();
        MouseLeftButtonDown();
        _mockMovingWindows.Verify(m => m.MoveWindow(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [TestMethod]
    public void WhenTheMouseIsPressedWithoutTheHotKeyPressedTheOperationIsNotStarted()
    {
        MouseLeftButtonDown();
        _mockMovingWindows.Verify(m => m.MoveWindow(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        _mockResizingWindows.Verify(m => m.ResizeWindow(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [TestMethod]
    public void WhenTheMouseIsMovedWithoutTheHotKeyPressedTheOperationIsNotStarted()
    {
        MouseMove();
        _mockMovingWindows.Verify(m => m.MoveWindow(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        _mockResizingWindows.Verify(m => m.ResizeWindow(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [TestMethod]
    public void WhenTheHotKeyIsPressedThenTheRightMouseButtonThenTheMouseMovedAResizeOperationIsStarted()
    {
        HotKeyPress();
        MouseRightButtonDown();
        MouseMove();
        _mockResizingWindows.Verify(m => m.ResizeWindow(It.IsAny<int>(), It.IsAny<int>()), Times.Once);
        _mockCursorForOperation.Verify(c => c.MoveToCursor(It.IsAny<int>(), It.IsAny<int>()), Times.Once);
    }

    [TestMethod]
    public void IfARightMouseButtonIsPressedDuringAMoveOperationThenTheMoveIsEndedButHotKeyIsStillActive()
    {
        HotKeyPress();
        MouseLeftButtonDown();
        MouseMove();
        MouseRightButtonDown();
        _mockCursorForOperation.Verify(m => m.HideCursor(), Times.Once);
        _mockTransparentWindows.Verify(m => m.EndTransparency(), Times.Once);
        _mockTargetWindow.Verify(m => m.ClearTargetWindow(), Times.Once);
        Assert.IsFalse(_quickWindowsManager.OperationInProgress);
        Assert.IsTrue(_quickWindowsManager.IsHotKeyActivated);
    }

    [TestMethod]
    public void IfALeftMouseButtonIsPressedDuringAResizeOperationThenTheResizeIsEndedButHotKeyIsStillActive()
    {
        HotKeyPress();
        MouseRightButtonDown();
        MouseMove();
        MouseLeftButtonDown();
        _mockCursorForOperation.Verify(m => m.HideCursor(), Times.Once);
        _mockTransparentWindows.Verify(m => m.EndTransparency(), Times.Once);
        _mockTargetWindow.Verify(m => m.ClearTargetWindow(), Times.Once);
        Assert.IsFalse(_quickWindowsManager.OperationInProgress);
        Assert.IsTrue(_quickWindowsManager.IsHotKeyActivated);
    }

    [TestMethod]
    public void IfTheMouseWheelIsUsedDuringAMoveOperationThenTheRolodexIsSuppressed()
    {
        HotKeyPress();
        MouseLeftButtonDown();
        MouseMove();
        MouseWheelUp();
        _mockRolodexWindows.Verify(m => m.SendWindowToBottom(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
        _mockRolodexWindows.Verify(m => m.BringBottomWindowToTop(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }
}

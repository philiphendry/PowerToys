// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using QuickWindows;
using QuickWindows.Interfaces;
using QuickWindows.Keyboard;
using QuickWindows.Settings;

namespace Microsoft.QuickWindows.UnitTests;

[TestClass]
public class KeyboardMonitorTests
{
    private Mock<IGlobalKeyboardHook> _mockHook = null!;
    private Mock<IDisabledInGameMode> _mockGameMode = null!;
    private Mock<IUserSettings> _mockUserSettings = null!;
    private KeyboardMonitor _monitor = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockHook = new Mock<IGlobalKeyboardHook>();
        _mockGameMode = new Mock<IDisabledInGameMode>();
        _mockUserSettings = new Mock<IUserSettings>();

        _mockGameMode.Setup(g => g.IsDisabledInGameMode()).Returns(false);

        // Configure Alt as the only activation key
        _mockUserSettings.Setup(u => u.ActivateOnAlt).Returns(new SettingItem<bool>(true));
        _mockUserSettings.Setup(u => u.ActivateOnCtrl).Returns(new SettingItem<bool>(false));
        _mockUserSettings.Setup(u => u.ActivateOnShift).Returns(new SettingItem<bool>(false));

        _monitor = new KeyboardMonitor(_mockHook.Object, _mockUserSettings.Object, _mockGameMode.Object);
        _monitor.Install();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _monitor.Dispose();
    }

    private void RaiseKeyEvent(int vKey, GlobalKeyboardHook.KeyboardState state, int flags = 0)
    {
        var data = new GlobalKeyboardHook.LowLevelKeyboardInputEvent
        {
            VirtualCode = vKey,
            Flags = flags,
        };
        var args = new GlobalKeyboardHookEventArgs(data, state);
        _mockHook.Raise(h => h.KeyboardPressed += null!, args);
    }

    [TestMethod]
    public void InjectedAltDownDoesNotFireHotKeyPressed()
    {
        var hotKeyPressedFired = false;
        _monitor.HotKeyPressed += (_, _) => hotKeyPressedFired = true;

        // Raise SysKeyDown for Alt with LLKHF_INJECTED set
        RaiseKeyEvent(NativeMethods.VK_MENU, GlobalKeyboardHook.KeyboardState.SysKeyDown, flags: NativeMethods.LLKHF_INJECTED);

        Assert.IsFalse(hotKeyPressedFired, "HotKeyPressed should not fire for an injected Alt down event");
    }

    [TestMethod]
    public void InjectedAltUpDoesNotFireHotKeyReleased()
    {
        // First press Alt physically to activate
        RaiseKeyEvent(NativeMethods.VK_MENU, GlobalKeyboardHook.KeyboardState.SysKeyDown);

        var hotKeyReleasedFired = false;
        _monitor.HotKeyReleased += (_, _) => hotKeyReleasedFired = true;

        // Raise SysKeyUp for Alt with LLKHF_INJECTED — should be ignored
        RaiseKeyEvent(NativeMethods.VK_MENU, GlobalKeyboardHook.KeyboardState.SysKeyUp, flags: NativeMethods.LLKHF_INJECTED);

        Assert.IsFalse(hotKeyReleasedFired, "HotKeyReleased should not fire for an injected Alt up event");
    }

    [TestMethod]
    public void PhysicalAltDownAfterInjectedEventStillFiresHotKeyPressed()
    {
        var hotKeyPressedFired = false;
        _monitor.HotKeyPressed += (_, _) => hotKeyPressedFired = true;

        // Injected event first — should be ignored
        RaiseKeyEvent(NativeMethods.VK_MENU, GlobalKeyboardHook.KeyboardState.SysKeyDown, flags: NativeMethods.LLKHF_INJECTED);
        // Physical Alt down — should fire
        RaiseKeyEvent(NativeMethods.VK_MENU, GlobalKeyboardHook.KeyboardState.SysKeyDown, flags: 0);

        Assert.IsTrue(hotKeyPressedFired, "HotKeyPressed should fire for a physical Alt down event after an injected one was ignored");
    }

    [TestMethod]
    public void PhysicalAltUpAfterInjectedAltUpStillFiresHotKeyReleased()
    {
        // Physical Alt down activates the hot key
        RaiseKeyEvent(NativeMethods.VK_MENU, GlobalKeyboardHook.KeyboardState.SysKeyDown, flags: 0);

        var hotKeyReleasedFired = false;
        _monitor.HotKeyReleased += (_, _) => hotKeyReleasedFired = true;

        // Injected Alt up (e.g. from another tool) — must be silently dropped
        RaiseKeyEvent(NativeMethods.VK_MENU, GlobalKeyboardHook.KeyboardState.SysKeyUp, flags: NativeMethods.LLKHF_INJECTED);
        Assert.IsFalse(hotKeyReleasedFired, "HotKeyReleased must not fire for injected Alt up");

        // Physical Alt up — must still fire HotKeyReleased because _altDown was not cleared by the injected event
        RaiseKeyEvent(NativeMethods.VK_MENU, GlobalKeyboardHook.KeyboardState.SysKeyUp, flags: 0);
        Assert.IsTrue(hotKeyReleasedFired, "HotKeyReleased must fire for physical Alt up after an injected Alt up was ignored");
    }
}

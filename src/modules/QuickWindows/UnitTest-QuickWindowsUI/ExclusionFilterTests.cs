// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using QuickWindows.Features;
using QuickWindows.Interfaces;
using QuickWindows.Settings;

namespace Microsoft.QuickWindows.UnitTests;

[TestClass]
public class ExclusionFilterTests
{
    private Mock<IWindowHelpers> _mockWindowHelpers = null!;
    private Mock<IUserSettings> _mockUserSettings = null!;

    // Note: SettingItem<T> is sealed and cannot be mocked — instantiate directly.
    private ExclusionFilter CreateFilterWithList(string listValue)
    {
        _mockWindowHelpers = new Mock<IWindowHelpers>();
        _mockUserSettings = new Mock<IUserSettings>();

        var setting = new SettingItem<string>(listValue);
        _mockUserSettings.Setup(u => u.ExcludedApplications).Returns(setting);

        return new ExclusionFilter(_mockWindowHelpers.Object, _mockUserSettings.Object);
    }

    [TestMethod]
    public void WhenExclusionListUsesWindowsLineEndingsEntriesAreParsedCorrectly()
    {
        // \r\n line endings (Windows style from settings UI)
        var filter = CreateFilterWithList("Notepad||Notepad\r\nCode - Insiders||Chrome_WidgetWin_1");

        _mockWindowHelpers
            .Setup(w => w.GetWindowInfoAtCursor())
            .Returns((true, "Code - Insiders", "Chrome_WidgetWin_1"));

        Assert.IsTrue(filter.IsWindowAtCursorExcluded(), "Window with \\r\\n-separated exclusion entry should be excluded");
    }

    [TestMethod]
    public void WhenExclusionListUsesUnixLineEndingsEntriesAreParsedCorrectly()
    {
        // \n only line endings
        var filter = CreateFilterWithList("Notepad||Notepad\nCode - Insiders||Chrome_WidgetWin_1");

        _mockWindowHelpers
            .Setup(w => w.GetWindowInfoAtCursor())
            .Returns((true, "Code - Insiders", "Chrome_WidgetWin_1"));

        Assert.IsTrue(filter.IsWindowAtCursorExcluded(), "Window with \\n-separated exclusion entry should be excluded");
    }
}

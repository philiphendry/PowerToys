// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using QuickWindows.Features;
using QuickWindows.Interfaces;
using QuickWindows.Settings;

namespace Microsoft.QuickWindows.UnitTests.Features;

[TestClass]
public class SnappingWindowsTests
{
    private Mock<IUserSettings> _mockUserSettings = default!;
    private Mock<IWindowHelpers> _mockWindowHelpers = default!;
    private SnappingWindows _snappingWindows = default!;

    [TestInitialize]
    public void Setup()
    {
        _mockUserSettings = new Mock<IUserSettings>();
        _mockUserSettings.SetupGet(x => x.SnappingThreshold).Returns(new SettingItem<int>(30));
        _mockUserSettings.SetupGet(x => x.SnappingPadding).Returns(new SettingItem<int>(10));

        _mockWindowHelpers = new Mock<IWindowHelpers>();
        _mockWindowHelpers.Setup(x => x.GetOpenWindows())
            .Returns([new() { left = 100, top = 100, right = 400, bottom = 400 }]);

        _snappingWindows = new SnappingWindows(_mockUserSettings.Object, _mockWindowHelpers.Object);
        _snappingWindows.StartSnap();
    }

    /*
     * Left, Right, Top, Bottom edge snapping tests. The basic scenarios where windows are side-by-side.
     */

    [TestMethod]
    public void GivenTheLeftEdgeIsWithinThresholdOfTheRightEdgeOfAWindowTheLeftEdgeIsSnapped()
        => Assert.AreEqual((410, 150, 510, 200), _snappingWindows.SnapMovingWindow(415, 150, 515, 200));

    [TestMethod]
    public void GivenTheRightEdgeIsWithinThresholdOfTheLeftEdgeOfAWindowTheRightEdgeIsSnapped()
        => Assert.AreEqual((55, 150, 90, 200), _snappingWindows.SnapMovingWindow(50, 150, 85, 200));

    [TestMethod]
    public void GivenTheTopEdgeIsWithinThresholdOfTheBottomEdgeOfAWindowTheTopEdgeIsSnapped()
        => Assert.AreEqual((50, 410, 150, 510), _snappingWindows.SnapMovingWindow(50, 415, 150, 515));

    [TestMethod]
    public void GivenTheBottomEdgeIsWithinThresholdOfTheTopEdgeOfAWindowTheBottomEdgeIsSnapped()
        => Assert.AreEqual((50, 55, 150, 90), _snappingWindows.SnapMovingWindow(50, 50, 150, 85));

    /*
     * Outer corner snapping tests where windows are corner-to-corner.
     */

    [TestMethod]
    public void GivenTheTopLeftCornerIsWithinThresholdOfTheBottomRightCornerOfAWindowTheTopLeftCornerIsSnapped()
        => Assert.AreEqual((410, 410, 510, 510), _snappingWindows.SnapMovingWindow(415, 415, 515, 515));

    [TestMethod]
    public void GivenTheTopRightCornerIsWithinThresholdOfTheBottomLeftCornerOfAWindowTheTopRightCornerIsSnapped()
        => Assert.AreEqual((-10, 410, 90, 510), _snappingWindows.SnapMovingWindow(-15, 415, 85, 515));

    [TestMethod]
    public void GivenTheBottomLeftCornerIsWithinThresholdOfTheTopRightCornerOfAWindowTheTopLeftCornerIsSnapped()
        => Assert.AreEqual((410, -10, 510, 90), _snappingWindows.SnapMovingWindow(415, -15, 515, 85));

    [TestMethod]
    public void GivenTheBottomRightCornerIsWithinThresholdOfTheTopLeftCornerOfAWindowTheTopRightCornerIsSnapped()
        => Assert.AreEqual((-10, 410, 90, 510), _snappingWindows.SnapMovingWindow(-15, 415, 85, 515));

    /*
     * Inline edge snapping. These cover the scenarios where, for example, a window is beside another with the
     * snapping padding between them and the top edges are within the snapping threshold and therefore snap but
     * without the padding.
     */

    [TestMethod]
    public void GivenTheLeftEdgeHasSnappedToTheRightThenTopEdgeWithinThresholdAndBelowWillAlignWithTheTopEdgeOfTheWindow()
        => Assert.AreEqual((410, 100, 510, 200), _snappingWindows.SnapMovingWindow(415, 115, 515, 215));

    [TestMethod]
    public void GivenTheLeftEdgeHasSnappedToTheRightThenTopEdgeWithinThresholdAndAboveWillAlignWithTheTopEdgeOfTheWindow()
        => Assert.AreEqual((410, 100, 510, 200), _snappingWindows.SnapMovingWindow(415, 85, 515, 185));

    [TestMethod]
    public void GivenTheLeftEdgeHasSnappedToTheRightThenBottomEdgeWithinThresholdAndBelowWillAlignWithTheBottomEdgeOfTheWindow()
        => Assert.AreEqual((410, 300, 510, 400), _snappingWindows.SnapMovingWindow(415, 315, 515, 415));

    [TestMethod]
    public void GivenTheLeftEdgeHasSnappedToTheRightThenBottomEdgeWithinThresholdAndAboveWillAlignWithTheBottomEdgeOfTheWindow()
        => Assert.AreEqual((410, 300, 510, 400), _snappingWindows.SnapMovingWindow(415, 285, 515, 385));

    [TestMethod]
    public void GivenTheRightEdgeHasSnappedToTheLeftThenTopEdgeWithinThresholdAndBelowWillAlignWithTheTopEdgeOfTheWindow()
        => Assert.AreEqual((-10, 100, 90, 200), _snappingWindows.SnapMovingWindow(-15, 115, 85, 215));

    [TestMethod]
    public void GivenTheRightEdgeHasSnappedToTheLeftThenTopEdgeWithinThresholdAndAboveWillAlignWithTheTopEdgeOfTheWindow()
        => Assert.AreEqual((-10, 100, 90, 200), _snappingWindows.SnapMovingWindow(-15, 85, 85, 185));

    [TestMethod]
    public void GivenTheRightEdgeHasSnappedToTheLeftThenBottomEdgeWithinThresholdAndBelowWillAlignWithTheBottomEdgeOfTheWindow()
        => Assert.AreEqual((-10, 300, 90, 400), _snappingWindows.SnapMovingWindow(-15, 315, 85, 415));

    [TestMethod]
    public void GivenTheRightEdgeHasSnappedToTheLeftThenBottomEdgeWithinThresholdAndAboveWillAlignWithTheBottomEdgeOfTheWindow()
        => Assert.AreEqual((-10, 300, 90, 400), _snappingWindows.SnapMovingWindow(-15, 285, 85, 385));

    [TestMethod]
    public void GivenTheTopEdgeHasSnappedToTheBottomThenLeftEdgeWithinThresholdAndRightWillAlignWithTheLeftEdgeOfTheWindow()
        => Assert.AreEqual((100, 410, 200, 510), _snappingWindows.SnapMovingWindow(115, 415, 215, 515));

    [TestMethod]
    public void GivenTheTopEdgeHasSnappedToTheBottomThenLeftEdgeWithinThresholdAndLeftWillAlignWithTheLeftEdgeOfTheWindow()
        => Assert.AreEqual((100, 410, 200, 510), _snappingWindows.SnapMovingWindow(85, 415, 185, 515));

    [TestMethod]
    public void GivenTheTopEdgeHasSnappedToTheBottomThenRightEdgeWithinThresholdAndRightWillAlignWithTheRightEdgeOfTheWindow()
        => Assert.AreEqual((0, 410, 100, 510), _snappingWindows.SnapMovingWindow(15, 415, 115, 515));

    [TestMethod]
    public void GivenTheTopEdgeHasSnappedToTheBottomThenRightEdgeWithinThresholdAndLeftWillAlignWithTheRightEdgeOfTheWindow()
        => Assert.AreEqual((0, 410, 100, 510), _snappingWindows.SnapMovingWindow(-15, 415, 85, 515));
}

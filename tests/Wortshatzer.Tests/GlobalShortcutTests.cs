using Wortshatzer.Core.Shortcuts;
using Xunit;

namespace Wortshatzer.Tests;

public sealed class GlobalShortcutTests
{
    [Fact]
    public void Gesture_FormatsModifiersInStableOrder()
    {
        var gesture = new GlobalShortcutGesture(
            ShortcutModifiers.Shift
                | ShortcutModifiers.Alt
                | ShortcutModifiers.Control,
            ShortcutKey.Z);

        Assert.Equal("Ctrl + Alt + Shift + Z", gesture.ToString());
    }

    [Fact]
    public void Registration_RejectsUnknownAction()
    {
        var gesture = new GlobalShortcutGesture(
            ShortcutModifiers.Control,
            ShortcutKey.O);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new GlobalShortcutRegistration(
                (GlobalShortcutAction)999,
                gesture));
    }
}

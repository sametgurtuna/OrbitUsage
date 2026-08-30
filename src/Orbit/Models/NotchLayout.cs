namespace Orbit.Models;

/// <summary>
/// Which edge of the screen the notch docks to and how its gauges are arranged. TopCenter is the
/// classic macOS-notch look (horizontal pill at the top); RightCenter is a vertical dock hugging
/// the right edge, useful when the top of the screen is already busy (multiple monitors, other
/// menu-bar-style utilities, etc).
/// </summary>
public enum NotchLayout
{
    TopCenter,
    RightCenter
}

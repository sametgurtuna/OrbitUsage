namespace Orbit.Models;

/// <summary>
/// How fast the notch grows/shrinks between its collapsed and expanded footprint. Scales all of
/// MainWindow's storyboard timings (see MainWindow.BuildStoryboard) uniformly, so the relative
/// choreography (fade delay vs. resize duration) stays the same at every speed.
/// </summary>
public enum NotchAnimationSpeed
{
    Fast,
    Normal,
    Fluid
}

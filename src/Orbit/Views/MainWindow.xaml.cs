using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using Orbit.Helpers;
using Orbit.Models;
using Orbit.Services;
using Orbit.ViewModels;
using Size = System.Windows.Size;

namespace Orbit.Views;

public partial class MainWindow : Window
{
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int GWL_EXSTYLE = -20;

    // Collapsed/expanded footprints per layout. The window itself is sized once (per layout, see
    // ScreenPositionHelper) and never animated; only NotchBorder's Width/Height animate inside it,
    // growing away from the anchored screen edge because of NotchBorder's alignment.
    private static readonly Size TopCenterCollapsed = new(140, 28);
    private static readonly Size TopCenterExpanded = new(330, 170);
    private static readonly Size RightCenterCollapsed = new(28, 140);
    private static readonly Size RightCenterExpanded = new(170, 360);

    // Multiplies every ms constant in BuildStoryboard, keeping the fade/resize choreography's
    // relative proportions identical at every speed. Normal == the app's original timings.
    private static readonly Dictionary<NotchAnimationSpeed, double> SpeedScale = new()
    {
        [NotchAnimationSpeed.Fast] = 0.65,
        [NotchAnimationSpeed.Normal] = 1.0,
        [NotchAnimationSpeed.Fluid] = 1.5,
    };

    private readonly NotchViewModel _viewModel;
    private readonly SettingsService? _settingsService;
    private NotchLayout _layout = NotchLayout.TopCenter;
    private Storyboard _expandStoryboard = new();
    private Storyboard _collapseStoryboard = new();

    public MainWindow() : this(new NotchViewModel())
    {
    }

    public MainWindow(NotchViewModel viewModel, SettingsService? settingsService = null)
    {
        InitializeComponent();

        _viewModel = viewModel;
        _settingsService = settingsService;
        DataContext = _viewModel;
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;

        SourceInitialized += MainWindow_SourceInitialized;
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        // Hide from Alt-Tab (ShowInTaskbar=False alone doesn't guarantee this for tool-style windows).
        var hwnd = new WindowInteropHelper(this).Handle;
        int exStyle = NativeMethods.GetWindowLong(hwnd, GWL_EXSTYLE);
        NativeMethods.SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW);

        ApplyLayoutCore();
    }

    public void ApplyLayout(NotchLayout layout, string? targetMonitorDeviceName = null)
    {
        _layout = layout;
        ApplyLayoutCore(targetMonitorOverride: targetMonitorDeviceName);
    }

    /// <summary>Live-previews a layout/monitor/offset/speed combination before it is saved (Settings window
    /// calls this as the user changes settings), without touching the persisted settings.</summary>
    public void PreviewSettings(NotchLayout layout, string? targetMonitorDeviceName, double offsetX, double offsetY, NotchAnimationSpeed speed)
    {
        _layout = layout;
        ApplyLayoutCore(targetMonitorOverride: targetMonitorDeviceName, offsetXOverride: offsetX, offsetYOverride: offsetY, speedOverride: speed);
    }

    private void ApplyLayoutCore(string? targetMonitorOverride = null, double? offsetXOverride = null, double? offsetYOverride = null, NotchAnimationSpeed? speedOverride = null)
    {
        bool vertical = _layout == NotchLayout.RightCenter;

        CollapsedHorizontalPanel.Visibility = vertical ? Visibility.Collapsed : Visibility.Visible;
        CollapsedVerticalPanel.Visibility = vertical ? Visibility.Visible : Visibility.Collapsed;
        ExpandedHorizontalPanel.Visibility = vertical ? Visibility.Collapsed : Visibility.Visible;
        ExpandedVerticalPanel.Visibility = vertical ? Visibility.Visible : Visibility.Collapsed;

        NotchBorder.HorizontalAlignment = vertical ? System.Windows.HorizontalAlignment.Right : System.Windows.HorizontalAlignment.Center;
        NotchBorder.VerticalAlignment = vertical ? System.Windows.VerticalAlignment.Center : System.Windows.VerticalAlignment.Top;
        NotchBorder.CornerRadius = vertical ? new CornerRadius(16, 0, 0, 16) : new CornerRadius(0, 0, 16, 16);

        var collapsed = vertical ? RightCenterCollapsed : TopCenterCollapsed;
        var expanded = vertical ? RightCenterExpanded : TopCenterExpanded;

        // Snap to the collapsed footprint immediately (no animation) so switching layouts doesn't
        // leave stale dimensions from the previous layout on screen for a frame.
        NotchBorder.Width = collapsed.Width;
        NotchBorder.Height = collapsed.Height;

        var settings = _settingsService?.Current;
        var speed = speedOverride ?? settings?.AnimationSpeed ?? NotchAnimationSpeed.Normal;
        double scale = SpeedScale[speed];
        _expandStoryboard = BuildStoryboard(collapsed, expanded, expanding: true, scale);
        _collapseStoryboard = BuildStoryboard(collapsed, expanded, expanding: false, scale);

        // Before SourceInitialized there's no HwndSource yet, so DPI lookup falls back to 1.0 inside
        // ScreenPositionHelper (harmless - SourceInitialized re-invokes this with the real scale).
        string? targetMonitor = targetMonitorOverride ?? settings?.TargetMonitorDeviceName;
        double offsetX = offsetXOverride ?? settings?.NotchOffsetX ?? 0;
        double offsetY = offsetYOverride ?? settings?.NotchOffsetY ?? 0;
        ScreenPositionHelper.Position(this, _layout, targetMonitor, offsetX, offsetY);
    }

    private static Storyboard BuildStoryboard(Size collapsed, Size expanded, bool expanding, double speedScale)
    {
        var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
        var target = expanding ? expanded : collapsed;
        var storyboard = new Storyboard();

        TimeSpan Ms(double baseMs) => TimeSpan.FromMilliseconds(baseMs * speedScale);

        var widthAnim = new DoubleAnimation { To = target.Width, Duration = Ms(200), EasingFunction = ease };
        Storyboard.SetTargetName(widthAnim, "NotchBorder");
        Storyboard.SetTargetProperty(widthAnim, new PropertyPath(FrameworkElement.WidthProperty));
        storyboard.Children.Add(widthAnim);

        var heightAnim = new DoubleAnimation { To = target.Height, Duration = Ms(200), EasingFunction = ease };
        Storyboard.SetTargetName(heightAnim, "NotchBorder");
        Storyboard.SetTargetProperty(heightAnim, new PropertyPath(FrameworkElement.HeightProperty));
        storyboard.Children.Add(heightAnim);

        var (fadeInName, fadeOutName, fadeInDelayMs, fadeInDurationMs) = expanding
            ? ("ExpandedContent", "CollapsedContent", 80, 150)
            : ("CollapsedContent", "ExpandedContent", 80, 120);

        var fadeIn = new DoubleAnimation { To = 1, BeginTime = Ms(fadeInDelayMs), Duration = Ms(fadeInDurationMs) };
        Storyboard.SetTargetName(fadeIn, fadeInName);
        Storyboard.SetTargetProperty(fadeIn, new PropertyPath(UIElement.OpacityProperty));
        storyboard.Children.Add(fadeIn);

        var fadeOut = new DoubleAnimation { To = 0, Duration = Ms(80) };
        Storyboard.SetTargetName(fadeOut, fadeOutName);
        Storyboard.SetTargetProperty(fadeOut, new PropertyPath(UIElement.OpacityProperty));
        storyboard.Children.Add(fadeOut);

        var showFrames = new ObjectAnimationUsingKeyFrames();
        showFrames.KeyFrames.Add(new DiscreteObjectKeyFrame(Visibility.Visible, KeyTime.FromTimeSpan(TimeSpan.Zero)));
        Storyboard.SetTargetName(showFrames, fadeInName);
        Storyboard.SetTargetProperty(showFrames, new PropertyPath(UIElement.VisibilityProperty));
        storyboard.Children.Add(showFrames);

        var hideFrames = new ObjectAnimationUsingKeyFrames();
        hideFrames.KeyFrames.Add(new DiscreteObjectKeyFrame(Visibility.Collapsed,
            KeyTime.FromTimeSpan(expanding ? Ms(fadeInDelayMs) : Ms(200))));
        Storyboard.SetTargetName(hideFrames, fadeOutName);
        Storyboard.SetTargetProperty(hideFrames, new PropertyPath(UIElement.VisibilityProperty));
        storyboard.Children.Add(hideFrames);

        return storyboard;
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NotchViewModel.AggregateStatus))
            UpdateAggregateDot();
        else if (e.PropertyName == nameof(NotchViewModel.IsRefreshing))
            UpdateRefreshAnimation();
    }

    private void UpdateAggregateDot()
    {
        var brush = StatusBrush(_viewModel.AggregateStatus);
        AggregateStatusDotH.Fill = brush;
        AggregateStatusDotV.Fill = brush;
    }

    private static System.Windows.Media.Brush StatusBrush(UsageStatus status) => status switch
    {
        UsageStatus.Critical => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE5, 0x4B, 0x4B)),
        UsageStatus.Warning => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF2, 0xB8, 0x3D)),
        UsageStatus.Unavailable => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x8A, 0x8A, 0x8A)),
        _ => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x3D, 0xDC, 0x84)),
    };

    private void UpdateRefreshAnimation()
    {
        var pulse = (Storyboard)Resources["RefreshPulseStoryboard"];
        if (_viewModel.IsRefreshing)
            pulse.Begin(this, true);
        else
            pulse.Stop(this);
    }

    public void Expand()
    {
        if (_viewModel.IsExpanded) return;
        _viewModel.IsExpanded = true;
        _expandStoryboard.Begin(this);
    }

    public void Collapse()
    {
        if (!_viewModel.IsExpanded) return;
        _viewModel.IsExpanded = false;
        _collapseStoryboard.Begin(this);
    }

    // HoverZone (the whole fixed-size window, see MainWindow.xaml) owns these - expand the instant
    // the pointer enters it, collapse the instant it leaves. No debounce/grace timer: HoverZone's
    // bounds never animate, so there's no flicker risk from mid-animation hit-test churn.
    private void NotchBorder_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e) => Expand();

    private void NotchBorder_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e) => Collapse();

    private void NotchBorder_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (!_viewModel.IsExpanded) Expand();
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e) => _viewModel.RefreshCommand.Execute(null);
}

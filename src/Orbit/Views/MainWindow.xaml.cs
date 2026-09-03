using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Orbit.Controls;
using Orbit.Helpers;
using Orbit.Models;
using Orbit.Services;
using Orbit.ViewModels;
using Point = System.Windows.Point;
using Size = System.Windows.Size;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace Orbit.Views;

public partial class MainWindow : Window
{
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_APPWINDOW = 0x00040000;
    private const int GWL_EXSTYLE = -20;

    private readonly NotchViewModel _viewModel;
    private readonly SettingsService? _settingsService;
    private readonly Func<SettingsWindow>? _settingsWindowFactory;
    private SettingsWindow? _openSettingsWindow;
    private NotchLayout _layout = NotchLayout.RightCenter;

    private DispatcherTimer? _flyoutCloseTimer;
    private DispatcherTimer? _dockAutoHideTimer;
    private bool _isDockSlidOut = false;
    private bool _isDockPinned = true;

    public MainWindow() : this(new NotchViewModel())
    {
    }

    public MainWindow(NotchViewModel viewModel, SettingsService? settingsService = null, Func<SettingsWindow>? settingsWindowFactory = null)
    {
        InitializeComponent();

        _viewModel = viewModel;
        _settingsService = settingsService;
        _settingsWindowFactory = settingsWindowFactory;
        DataContext = _viewModel;

        Owner = App.GetAltTabSuppressor();

        _dockAutoHideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        _dockAutoHideTimer.Tick += (s, e) =>
        {
            _dockAutoHideTimer.Stop();
            if (!_isDockPinned && !IsMouseOverDockOrFlyout())
            {
                SlideDockOut();
            }
        };

        SourceInitialized += MainWindow_SourceInitialized;
        IsVisibleChanged += (s, e) => EnsureAltTabHidden();
        Activated += (s, e) => EnsureAltTabHidden();
    }

    private void MainWindow_SourceInitialized(object? sender, EventArgs e)
    {
        EnsureAltTabHidden();

        var hwnd = new WindowInteropHelper(this).Handle;
        var source = HwndSource.FromHwnd(hwnd);
        source?.AddHook(WndProc);

        if (_settingsService?.Current != null)
        {
            HotkeyManager.Register(hwnd, _settingsService.Current);
        }

        Closed += (s, ev) => HotkeyManager.Unregister(hwnd);

        ApplyLayoutCore();
    }

    public void EnsureAltTabHidden()
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                NativeMethods.MakeToolWindow(hwnd);
            }
        }
        catch { }
    }

    public void Expand()
    {
        _viewModel.IsExpanded = true;
        Visibility = Visibility.Visible;
        Activate();
        SlideDockIn();
    }

    public void Collapse()
    {
        _viewModel.IsExpanded = false;
        HideFlyout();
        SlideDockOut();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && ((int)wParam == HotkeyManager.HotkeyId || (int)wParam == HotkeyManager.HotkeyIdAltOnly))
        {
            ToggleDock();
            handled = true;
            return IntPtr.Zero;
        }

        if (msg == NativeMethods.WM_NCHITTEST)
        {
            int x = (short)(lParam.ToInt64() & 0xFFFF);
            int y = (short)((lParam.ToInt64() >> 16) & 0xFFFF);
            var screenPt = new Point(x, y);

            try
            {
                if (_layout == NotchLayout.RightCenter)
                {
                    if (RightDockContainer != null && RightDockContainer.IsVisible)
                    {
                        var dockPt = RightDockContainer.PointFromScreen(screenPt);
                        var dockBounds = new Rect(0, 0, RightDockContainer.ActualWidth, RightDockContainer.ActualHeight);
                        if (dockBounds.Contains(dockPt))
                        {
                            return IntPtr.Zero;
                        }
                    }

                    if (FlyoutCard != null && FlyoutCard.IsVisible && FlyoutCard.Opacity > 0.05)
                    {
                        var cardPt = FlyoutCard.PointFromScreen(screenPt);
                        var cardBounds = new Rect(0, 0, FlyoutCard.ActualWidth, FlyoutCard.ActualHeight);
                        if (cardBounds.Contains(cardPt))
                        {
                            return IntPtr.Zero;
                        }
                    }

                    // Everywhere else is click-through
                    handled = true;
                    return (IntPtr)NativeMethods.HTTRANSPARENT;
                }
                else
                {
                    if (TopCenterDockBorder != null && TopCenterDockBorder.IsVisible)
                    {
                        var borderPt = TopCenterDockBorder.PointFromScreen(screenPt);
                        var bounds = new Rect(0, 0, TopCenterDockBorder.ActualWidth, TopCenterDockBorder.ActualHeight);
                        if (!bounds.Contains(borderPt))
                        {
                            handled = true;
                            return (IntPtr)NativeMethods.HTTRANSPARENT;
                        }
                    }
                }
            }
            catch
            {
                // Visual transformation not ready
            }
        }
        return IntPtr.Zero;
    }

    public void ApplyLayout(NotchLayout layout, string? targetMonitorDeviceName = null)
    {
        _layout = layout;
        ApplyLayoutCore(targetMonitorOverride: targetMonitorDeviceName);
    }

    public void PreviewSettings(NotchLayout layout, string? targetMonitorDeviceName, double offsetX, double offsetY, NotchAnimationSpeed speed)
    {
        _layout = layout;
        ApplyLayoutCore(targetMonitorOverride: targetMonitorDeviceName, offsetXOverride: offsetX, offsetYOverride: offsetY);
    }

    public void PreviewSettings(double? offsetX = null, double? offsetY = null)
    {
        ApplyLayoutCore(offsetXOverride: offsetX, offsetYOverride: offsetY);
    }

    public void RefreshHotkeysAndTopmost()
    {
        var settings = _settingsService?.Current;
        Topmost = settings?.AlwaysOnTop ?? true;
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero && settings != null)
        {
            HotkeyManager.Register(hwnd, settings);
        }
    }

    private void ApplyLayoutCore(string? targetMonitorOverride = null, double? offsetXOverride = null, double? offsetYOverride = null)
    {
        var settings = _settingsService?.Current;
        Topmost = settings?.AlwaysOnTop ?? true;

        bool vertical = _layout == NotchLayout.RightCenter;

        RightDockPanel.Visibility = vertical ? Visibility.Visible : Visibility.Collapsed;
        TopCenterDockBorder.Visibility = vertical ? Visibility.Collapsed : Visibility.Visible;

        string? targetMonitor = targetMonitorOverride ?? settings?.TargetMonitorDeviceName;
        double offsetX = offsetXOverride ?? settings?.NotchOffsetX ?? 0;
        double offsetY = offsetYOverride ?? settings?.NotchOffsetY ?? 0;
        ScreenPositionHelper.Position(this, _layout, targetMonitor, offsetX, offsetY);
    }

    // =========================================================================
    // Dock Slide & Edge Hover Animations
    // =========================================================================

    public void ToggleDock()
    {
        if (_layout != NotchLayout.RightCenter)
        {
            Visibility = Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            return;
        }

        if (Visibility != Visibility.Visible)
        {
            Visibility = Visibility.Visible;
            Topmost = true;
            _isDockPinned = true;
            SlideDockIn();
            return;
        }

        if (_isDockSlidOut)
        {
            _isDockPinned = true;
            Topmost = true;
            Activate();
            SlideDockIn();
        }
        else
        {
            _isDockPinned = false;
            HideFlyout();
            SlideDockOut();
        }
    }

    public void SlideDockIn()
    {
        _isDockSlidOut = false;
        _dockAutoHideTimer?.Stop();
        double scale = GetAnimationSpeedScale();
        var anim = new DoubleAnimation
        {
            To = 0.0,
            Duration = TimeSpan.FromMilliseconds(240 * scale),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        RightDockTranslate?.BeginAnimation(TranslateTransform.XProperty, anim);
    }

    public void SlideDockOut()
    {
        _isDockSlidOut = true;
        HideFlyout();
        double scale = GetAnimationSpeedScale();
        // Leaves 14px sleek tab peeking at the screen edge (width 68 - 54 = 14)
        var anim = new DoubleAnimation
        {
            To = 54.0,
            Duration = TimeSpan.FromMilliseconds(220 * scale),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        RightDockTranslate?.BeginAnimation(TranslateTransform.XProperty, anim);
    }

    private bool IsMouseOverDockOrFlyout()
    {
        return (RightDockContainer != null && RightDockContainer.IsMouseOver) ||
               (FlyoutCard != null && FlyoutCard.IsMouseOver);
    }

    private void RightDockContainer_MouseEnter(object sender, MouseEventArgs e)
    {
        _dockAutoHideTimer?.Stop();
        if (_isDockSlidOut)
        {
            SlideDockIn();
        }
    }

    private void RightDockContainer_MouseLeave(object sender, MouseEventArgs e)
    {
        if (!_isDockPinned)
        {
            _dockAutoHideTimer?.Stop();
            _dockAutoHideTimer?.Start();
        }
    }

    // =========================================================================
    // Hover & Flyout Interactive Positioning
    // =========================================================================

    private void ServiceItem_MouseEnter(object sender, MouseEventArgs e)
    {
        _flyoutCloseTimer?.Stop();
        _dockAutoHideTimer?.Stop();

        if (sender is FrameworkElement fe && fe.DataContext is ServiceUsageViewModel vm)
        {
            _viewModel.HoveredService = vm;

            try
            {
                // Ensure card layout is measured
                if (FlyoutCard.ActualHeight == 0)
                {
                    FlyoutCard.Measure(new Size(320, 400));
                    FlyoutCard.Arrange(new Rect(0, 0, FlyoutCard.DesiredSize.Width, FlyoutCard.DesiredSize.Height));
                }

                // Gauge circle is 44px diameter at the top of the item Grid
                var itemPos = fe.TransformToAncestor(this).Transform(new Point(0, 0));
                double gaugeCenterY = itemPos.Y + 22.0;
                double cardHeight = FlyoutCard.ActualHeight > 0 ? FlyoutCard.ActualHeight : 185.0;
                double targetY = gaugeCenterY - (cardHeight / 2.0);

                // Clamp within window bounds
                targetY = Math.Clamp(targetY, 15, Math.Max(15, ActualHeight - cardHeight - 15));

                double scale = GetAnimationSpeedScale();
                var yAnim = new DoubleAnimation
                {
                    To = targetY,
                    Duration = TimeSpan.FromMilliseconds(160 * scale),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                FlyoutTranslateY.BeginAnimation(TranslateTransform.YProperty, yAnim);
            }
            catch
            {
                // Fallback position
            }

            ShowFlyout();
        }
    }

    private void ServiceItem_MouseLeave(object sender, MouseEventArgs e)
    {
        ScheduleHideFlyout();
    }

    private void FlyoutCard_MouseEnter(object sender, MouseEventArgs e)
    {
        _flyoutCloseTimer?.Stop();
        _dockAutoHideTimer?.Stop();
    }

    private void FlyoutCard_MouseLeave(object sender, MouseEventArgs e)
    {
        ScheduleHideFlyout();
        if (!_isDockPinned)
        {
            _dockAutoHideTimer?.Stop();
            _dockAutoHideTimer?.Start();
        }
    }

    private void ScheduleHideFlyout()
    {
        _flyoutCloseTimer?.Stop();
        _flyoutCloseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
        _flyoutCloseTimer.Tick += (s, e) =>
        {
            _flyoutCloseTimer.Stop();
            HideFlyout();
        };
        _flyoutCloseTimer.Start();
    }

    private double GetAnimationSpeedScale()
    {
        var speed = _settingsService?.Current.AnimationSpeed ?? NotchAnimationSpeed.Normal;
        return speed switch
        {
            NotchAnimationSpeed.Fast => 0.6,
            NotchAnimationSpeed.Fluid => 1.6,
            _ => 1.0
        };
    }

    private void ShowFlyout()
    {
        double scale = GetAnimationSpeedScale();
        var opacityAnim = new DoubleAnimation
        {
            To = 1.0,
            Duration = TimeSpan.FromMilliseconds(160 * scale),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        var slideAnim = new DoubleAnimation
        {
            From = -14.0,
            To = 0.0,
            Duration = TimeSpan.FromMilliseconds(180 * scale),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        FlyoutCard.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
        FlyoutTranslateX.BeginAnimation(TranslateTransform.XProperty, slideAnim);
    }

    private void HideFlyout()
    {
        double scale = GetAnimationSpeedScale();
        var opacityAnim = new DoubleAnimation
        {
            To = 0.0,
            Duration = TimeSpan.FromMilliseconds(130 * scale),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        var slideAnim = new DoubleAnimation
        {
            To = -10.0,
            Duration = TimeSpan.FromMilliseconds(130 * scale),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        FlyoutCard.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
        FlyoutTranslateX.BeginAnimation(TranslateTransform.XProperty, slideAnim);
    }

    private void ServiceItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is ServiceUsageViewModel vm)
        {
            if (vm.HasSessionGauge)
            {
                vm.ToggleQuotaMode();
            }
            e.Handled = true;
        }
    }

    private void TopNotch_MouseEnter(object sender, MouseEventArgs e)
    {
    }

    private void TopNotch_MouseLeave(object sender, MouseEventArgs e)
    {
    }

    // =========================================================================
    // Menu Actions
    // =========================================================================

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_settingsWindowFactory == null) return;
        if (_openSettingsWindow != null)
        {
            _openSettingsWindow.Activate();
            return;
        }

        _openSettingsWindow = _settingsWindowFactory();
        _openSettingsWindow.Closed += (_, _) => _openSettingsWindow = null;
        _openSettingsWindow.Show();
        _openSettingsWindow.Activate();
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.RefreshCommand.Execute(null);
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.Application.Current.Shutdown();
    }
}

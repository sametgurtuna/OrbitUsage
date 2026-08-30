using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;
using Size = System.Windows.Size;
using UserControl = System.Windows.Controls.UserControl;

namespace Orbit.Controls;

public partial class RadialGauge : System.Windows.Controls.UserControl
{
    public static readonly DependencyProperty PercentProperty = DependencyProperty.Register(
        nameof(Percent), typeof(double), typeof(RadialGauge),
        new PropertyMetadata(0.0, OnPercentChanged));

    public static readonly DependencyProperty AccentProperty = DependencyProperty.Register(
        nameof(Accent), typeof(Brush), typeof(RadialGauge),
        new PropertyMetadata(Brushes.White, OnAppearanceChanged));

    public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register(
        nameof(IsActive), typeof(bool), typeof(RadialGauge),
        new PropertyMetadata(true, OnAppearanceChanged));

    public static readonly DependencyProperty StatusTextProperty = DependencyProperty.Register(
        nameof(StatusText), typeof(string), typeof(RadialGauge),
        new PropertyMetadata("0%", OnAppearanceChanged));

    public static readonly DependencyProperty DisplayLabelProperty = DependencyProperty.Register(
        nameof(DisplayLabel), typeof(string), typeof(RadialGauge),
        new PropertyMetadata(string.Empty, OnAppearanceChanged));

    public static readonly DependencyProperty SubTextProperty = DependencyProperty.Register(
        nameof(SubText), typeof(string), typeof(RadialGauge),
        new PropertyMetadata(string.Empty, OnAppearanceChanged));

    public static readonly DependencyProperty DiameterProperty = DependencyProperty.Register(
        nameof(Diameter), typeof(double), typeof(RadialGauge),
        new PropertyMetadata(64.0, OnAppearanceChanged));

    public static readonly DependencyProperty RingThicknessProperty = DependencyProperty.Register(
        nameof(RingThickness), typeof(double), typeof(RadialGauge),
        new PropertyMetadata(6.0, OnAppearanceChanged));

    private static readonly DependencyProperty AnimatedPercentProperty = DependencyProperty.Register(
        "AnimatedPercent", typeof(double), typeof(RadialGauge),
        new PropertyMetadata(0.0, OnAnimatedPercentChanged));

    public double Percent
    {
        get => (double)GetValue(PercentProperty);
        set => SetValue(PercentProperty, value);
    }

    public Brush Accent
    {
        get => (System.Windows.Media.Brush)GetValue(AccentProperty);
        set => SetValue(AccentProperty, value);
    }

    /// <summary>When false, the ring renders as an empty/dim track and StatusText is shown without an arc.</summary>
    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public string StatusText
    {
        get => (string)GetValue(StatusTextProperty);
        set => SetValue(StatusTextProperty, value);
    }

    public string DisplayLabel
    {
        get => (string)GetValue(DisplayLabelProperty);
        set => SetValue(DisplayLabelProperty, value);
    }

    public string SubText
    {
        get => (string)GetValue(SubTextProperty);
        set => SetValue(SubTextProperty, value);
    }

    public double Diameter
    {
        get => (double)GetValue(DiameterProperty);
        set => SetValue(DiameterProperty, value);
    }

    public double RingThickness
    {
        get => (double)GetValue(RingThicknessProperty);
        set => SetValue(RingThicknessProperty, value);
    }

    public RadialGauge()
    {
        InitializeComponent();
        Loaded += (_, _) => Redraw();
    }

    private static void OnPercentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var gauge = (RadialGauge)d;
        if (!gauge.IsLoaded)
        {
            gauge.SetValue(AnimatedPercentProperty, e.NewValue);
            return;
        }

        var animation = new DoubleAnimation
        {
            From = (double)e.OldValue,
            To = (double)e.NewValue,
            Duration = TimeSpan.FromMilliseconds(1000),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        gauge.BeginAnimation(AnimatedPercentProperty, animation);
    }

    private static void OnAnimatedPercentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((RadialGauge)d).Redraw();

    private static void OnAppearanceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((RadialGauge)d).Redraw();

    private void Redraw()
    {
        if (TrackEllipse == null || ArcPath == null) return;

        double diameter = Diameter;
        double thickness = RingThickness;
        double radius = (diameter - thickness) / 2;
        var center = new Point(diameter / 2, diameter / 2);

        TrackEllipse.Width = diameter;
        TrackEllipse.Height = diameter;
        TrackEllipse.StrokeThickness = thickness;

        ArcPath.StrokeThickness = thickness;
        ArcPath.Stroke = IsActive ? Accent : new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));

        double animatedPercent = IsActive ? (double)GetValue(AnimatedPercentProperty) : 0;
        ArcPath.Data = BuildArcGeometry(center, radius, animatedPercent);

        PercentText.Text = StatusText;
        PercentText.FontSize = Math.Max(11, diameter * 0.21);
        PercentText.Foreground = IsActive ? Accent : new SolidColorBrush(Color.FromRgb(0x8A, 0x8A, 0x8A));

        LabelText.Text = DisplayLabel;
        LabelText.FontSize = Math.Max(8.0, diameter * 0.11);
        LabelText.Visibility = string.IsNullOrEmpty(DisplayLabel) ? Visibility.Collapsed : Visibility.Visible;

        SubLabelText.Text = SubText;
        SubLabelText.FontSize = Math.Max(7.0, diameter * 0.09);
        SubLabelText.Visibility = string.IsNullOrEmpty(SubText) ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>Plays a smooth, cinematic sweep fill animation from 0 to target Percent.</summary>
    public void AnimateFill(double delayMs = 0)
    {
        if (!IsLoaded) return;

        double target = IsActive ? Percent : 0;
        if (target > 0.01)
        {
            var fillAnim = new DoubleAnimation
            {
                From = 0.0,
                To = target,
                BeginTime = TimeSpan.FromMilliseconds(delayMs),
                Duration = TimeSpan.FromMilliseconds(1100),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            BeginAnimation(AnimatedPercentProperty, fillAnim);
        }
        else
        {
            SetValue(AnimatedPercentProperty, 0.0);
        }
    }

    /// <summary>Builds a ring-slice path starting at 12 o'clock and sweeping clockwise by percent*360 degrees.</summary>
    private static Geometry BuildArcGeometry(Point center, double radius, double percent)
    {
        percent = Math.Clamp(percent, 0, 99.98); // 100 is a degenerate arc (start==end); render as near-full ring
        if (percent <= 0.01 || radius <= 0)
            return Geometry.Empty;

        double angle = percent / 100.0 * 360.0;
        var start = new Point(center.X, center.Y - radius);
        double rad = angle * Math.PI / 180.0;
        var end = new Point(
            center.X + radius * Math.Sin(rad),
            center.Y - radius * Math.Cos(rad));

        var figure = new PathFigure { StartPoint = start, IsClosed = false };
        figure.Segments.Add(new ArcSegment(
            end,
            new Size(radius, radius),
            0,
            angle > 180,
            SweepDirection.Clockwise,
            true));

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
    }
}

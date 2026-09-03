using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using Point = System.Windows.Point;

namespace Orbit.Controls;

/// <summary>
/// Procedural shape rendering the organic curved notch silhouette docked to the right edge of the screen.
/// Features concave fillet curves transitioning to/from the screen edge and rounded shoulders.
/// </summary>
public class NotchDockShape : Shape
{
    public static readonly DependencyProperty FilletRadiusProperty =
        DependencyProperty.Register(nameof(FilletRadius), typeof(double), typeof(NotchDockShape),
            new FrameworkPropertyMetadata(38.0, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));

    public double FilletRadius
    {
        get => (double)GetValue(FilletRadiusProperty);
        set => SetValue(FilletRadiusProperty, value);
    }

    protected override Geometry DefiningGeometry
    {
        get
        {
            double w = ActualWidth > 0 ? ActualWidth : (double.IsNaN(Width) ? 68.0 : Width);
            double h = ActualHeight > 0 ? ActualHeight : (double.IsNaN(Height) ? 260.0 : Height);

            if (w <= 0) w = 68.0;
            if (h <= 0) h = 260.0;

            double fillet = Math.Clamp(FilletRadius, 16.0, Math.Min(w * 0.9, h * 0.28));

            var geometry = new StreamGeometry { FillRule = FillRule.EvenOdd };
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(new Point(w, 0), isFilled: true, isClosed: true);

                // Top concave fillet from screen edge
                ctx.BezierTo(
                    new Point(w, fillet * 0.42),
                    new Point(w - fillet * 0.35, fillet * 0.82),
                    new Point(fillet * 0.75, fillet),
                    isStroked: true,
                    isSmoothJoin: true);

                // Top convex shoulder
                ctx.BezierTo(
                    new Point(fillet * 0.25, fillet * 1.15),
                    new Point(0, fillet * 1.45),
                    new Point(0, fillet * 1.9),
                    isStroked: true,
                    isSmoothJoin: true);

                // Straight vertical body edge
                ctx.LineTo(new Point(0, h - fillet * 1.9), isStroked: true, isSmoothJoin: true);

                // Bottom convex shoulder
                ctx.BezierTo(
                    new Point(0, h - fillet * 1.45),
                    new Point(fillet * 0.25, h - fillet * 1.15),
                    new Point(fillet * 0.75, h - fillet),
                    isStroked: true,
                    isSmoothJoin: true);

                // Bottom concave fillet back to screen edge
                ctx.BezierTo(
                    new Point(w - fillet * 0.35, h - fillet * 0.82),
                    new Point(w, h - fillet * 0.42),
                    new Point(w, h),
                    isStroked: true,
                    isSmoothJoin: true);

                // Close along right screen edge
                ctx.LineTo(new Point(w, 0), isStroked: true, isSmoothJoin: true);
            }

            geometry.Freeze();
            return geometry;
        }
    }
}

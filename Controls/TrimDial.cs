using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace TrimZero.Controls;

/// <summary>
/// A 270-degree trim instrument. The amber arc and needle are the aircraft's
/// trim; the white tick is where the wheel's datum says trim should be.
/// </summary>
public sealed class TrimDial : FrameworkElement
{
    public static readonly DependencyProperty SimTrimProperty = DependencyProperty.Register(
        nameof(SimTrim), typeof(double), typeof(TrimDial),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty GhostTrimProperty = DependencyProperty.Register(
        nameof(GhostTrim), typeof(double), typeof(TrimDial),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TakeoffTrimProperty = DependencyProperty.Register(
        nameof(TakeoffTrim), typeof(double), typeof(TrimDial),
        new FrameworkPropertyMetadata(-1d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty HasZeroProperty = DependencyProperty.Register(
        nameof(HasZero), typeof(bool), typeof(TrimDial),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsConnectedProperty = DependencyProperty.Register(
        nameof(IsConnected), typeof(bool), typeof(TrimDial),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public double SimTrim
    {
        get => (double)GetValue(SimTrimProperty);
        set => SetValue(SimTrimProperty, value);
    }

    public double GhostTrim
    {
        get => (double)GetValue(GhostTrimProperty);
        set => SetValue(GhostTrimProperty, value);
    }

    public double TakeoffTrim
    {
        get => (double)GetValue(TakeoffTrimProperty);
        set => SetValue(TakeoffTrimProperty, value);
    }

    public bool HasZero
    {
        get => (bool)GetValue(HasZeroProperty);
        set => SetValue(HasZeroProperty, value);
    }

    public bool IsConnected
    {
        get => (bool)GetValue(IsConnectedProperty);
        set => SetValue(IsConnectedProperty, value);
    }

    private const double StartAngle = 135d;
    private const double Sweep = 270d;

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        if (w < 40 || h < 40) return;

        var center = new Point(w / 2, h / 2);
        double radius = Math.Min(w, h) / 2 - 26;
        double sim = Math.Clamp(SimTrim, 0, 100);
        double ghost = Math.Clamp(GhostTrim, 0, 100);
        bool connected = IsConnected;

        // Disc and rim.
        var discBrush = new RadialGradientBrush(
            Color.FromRgb(0x16, 0x18, 0x1D),
            Color.FromRgb(0x0C, 0x0D, 0x10))
        {
            GradientOrigin = new Point(0.35, 0.25),
            Center = new Point(0.5, 0.5),
            RadiusX = 0.85,
            RadiusY = 0.85
        };
        dc.DrawEllipse(discBrush, new Pen(new SolidColorBrush(Color.FromRgb(0x1E, 0x21, 0x26)), 1), center, radius + 18, radius + 18);

        // Track.
        var trackPen = MakePen(Color.FromRgb(0x25, 0x29, 0x30), 6);
        DrawArc(dc, center, radius, 0, 100, trackPen);

        // Ticks.
        var minorPen = MakePen(Color.FromRgb(0x39, 0x3E, 0x47), 1, 0.9);
        var majorPen = MakePen(Color.FromRgb(0x5A, 0x61, 0x6C), 1.4);
        for (int i = 0; i <= 100; i += 5)
        {
            bool major = i % 10 == 0;
            double a = AngleFor(i);
            var p1 = PointOn(center, radius - (major ? 15 : 12), a);
            var p2 = PointOn(center, radius - 2, a);
            dc.DrawLine(major ? majorPen : minorPen, p1, p2);
        }

        // Labels.
        var typeface = new Typeface(new FontFamily("Bahnschrift"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        var labelBrush = new SolidColorBrush(Color.FromRgb(0x6E, 0x76, 0x81));
        double dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        foreach (int i in new[] { 0, 25, 50, 75, 100 })
        {
            var ft = new FormattedText(i.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, typeface, 10, labelBrush, dpi);
            var p = PointOn(center, radius - 28, AngleFor(i));
            dc.DrawText(ft, new Point(p.X - ft.Width / 2, p.Y - ft.Height / 2));
        }

        // Takeoff datum band.
        if (connected && TakeoffTrim >= 0)
        {
            var bandPen = MakePen(Color.FromRgb(0x63, 0xD2, 0x9B), 4, 0.95);
            DrawArc(dc, center, radius + 8, Math.Max(0, TakeoffTrim - 1.4), Math.Min(100, TakeoffTrim + 1.4), bandPen);
        }

        // The aircraft's trim.
        if (connected && sim > 0.15)
        {
            var simPen = MakePen(Color.FromRgb(0xF2, 0xB2, 0x3E), 6);
            DrawArc(dc, center, radius, 0, sim, simPen);
        }

        // Drift between what the wheel claims and what the aircraft says.
        double drift = ghost - sim;
        if (HasZero && Math.Abs(drift) > 0.6)
        {
            var driftPen = MakePen(Color.FromRgb(0xE4, 0x5B, 0x4F), 3, 0.85);
            double a1 = AngleFor(Math.Min(sim, ghost));
            double a2 = AngleFor(Math.Max(sim, ghost));
            DrawArcBetween(dc, center, radius - 15, a1, a2, driftPen);
        }

        // Ghost tick.
        if (HasZero)
        {
            double ga = AngleFor(ghost);
            var tickPen = MakePen(Color.FromArgb(0xD0, 0xFF, 0xFF, 0xFF), 2.4);
            dc.DrawLine(tickPen, PointOn(center, radius - 12, ga), PointOn(center, radius + 10, ga));
        }

        // Needle.
        if (connected)
        {
            double sa = AngleFor(sim);
            var needlePen = MakePen(Color.FromRgb(0xF2, 0xB2, 0x3E), 2.2);
            dc.DrawLine(needlePen, PointOn(center, radius * 0.34, sa), PointOn(center, radius - 12, sa));
            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(0xF2, 0xB2, 0x3E)), null, center, 4.2, 4.2);
        }
    }

    private static double AngleFor(double value) => StartAngle + Math.Clamp(value, 0, 100) / 100d * Sweep;

    private static Point PointOn(Point center, double radius, double angleDegrees)
    {
        double rad = angleDegrees * Math.PI / 180d;
        return new Point(center.X + radius * Math.Cos(rad), center.Y + radius * Math.Sin(rad));
    }

    private static Pen MakePen(Color color, double thickness, double opacity = 1)
    {
        var brush = new SolidColorBrush(color) { Opacity = opacity };
        return new Pen(brush, thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
    }

    private static void DrawArc(DrawingContext dc, Point center, double radius, double from, double to, Pen pen)
        => DrawArcBetween(dc, center, radius, AngleFor(from), AngleFor(to), pen);

    private static void DrawArcBetween(DrawingContext dc, Point center, double radius, double startAngle, double endAngle, Pen pen)
    {
        if (endAngle - startAngle < 0.05) return;
        bool large = endAngle - startAngle > 180;
        var start = PointOn(center, radius, startAngle);
        var end = PointOn(center, radius, endAngle);
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(start, false, false);
            ctx.ArcTo(end, new Size(radius, radius), 0, large, SweepDirection.Clockwise, true, false);
        }
        geo.Freeze();
        dc.DrawGeometry(null, pen, geo);
    }
}

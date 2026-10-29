using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using BackgroundChanged.Models;

namespace BackgroundChanged.Controls
{
    /// <summary>
    /// View-only transient animation. Configuration enters through dependency properties;
    /// mouse positions and render frames never pass through the ViewModel.
    /// </summary>
    public sealed class MouseTrailOverlay : FrameworkElement
    {
        public static readonly DependencyProperty InputSourceProperty = DependencyProperty.Register(nameof(InputSource), typeof(FrameworkElement),
            typeof(MouseTrailOverlay), new PropertyMetadata(null, InputSourceChanged));
        public static readonly DependencyProperty IsTrailEnabledProperty = Register(nameof(IsTrailEnabled), true);
        public static readonly DependencyProperty IsPausedProperty = Register(nameof(IsPaused), false);
        public static readonly DependencyProperty IsNightProperty = Register(nameof(IsNight), false);
        public static readonly DependencyProperty RespectReducedMotionProperty = Register(nameof(RespectReducedMotion), true);
        public static readonly DependencyProperty LifetimeProperty = DependencyProperty.Register(nameof(Lifetime), typeof(double), typeof(MouseTrailOverlay),
            new FrameworkPropertyMetadata(.65, SettingsChanged, (d, v) => MouseTrailSettings.Bound((double)v, .2, 1.5, .65)));
        public static readonly DependencyProperty IntensityProperty = DependencyProperty.Register(nameof(Intensity), typeof(double), typeof(MouseTrailOverlay),
            new FrameworkPropertyMetadata(.85, SettingsChanged, (d, v) => MouseTrailSettings.Bound((double)v, .1, 1, .85)));
        public static readonly DependencyProperty TrailWidthProperty = DependencyProperty.Register(nameof(TrailWidth), typeof(double), typeof(MouseTrailOverlay),
            new FrameworkPropertyMetadata(6.0, SettingsChanged, (d, v) => MouseTrailSettings.Bound((double)v, 3, 10, 6)));
        public static readonly DependencyProperty PaletteProperty = DependencyProperty.Register(nameof(Palette), typeof(string), typeof(MouseTrailOverlay),
            new PropertyMetadata("极光", SettingsChanged));

        public FrameworkElement InputSource { get => (FrameworkElement)GetValue(InputSourceProperty); set => SetValue(InputSourceProperty, value); }
        public bool IsTrailEnabled { get => (bool)GetValue(IsTrailEnabledProperty); set => SetValue(IsTrailEnabledProperty, value); }
        public bool IsPaused { get => (bool)GetValue(IsPausedProperty); set => SetValue(IsPausedProperty, value); }
        public bool IsNight { get => (bool)GetValue(IsNightProperty); set => SetValue(IsNightProperty, value); }
        public bool RespectReducedMotion { get => (bool)GetValue(RespectReducedMotionProperty); set => SetValue(RespectReducedMotionProperty, value); }
        public double Lifetime { get => (double)GetValue(LifetimeProperty); set => SetValue(LifetimeProperty, value); }
        public double Intensity { get => (double)GetValue(IntensityProperty); set => SetValue(IntensityProperty, value); }
        public double TrailWidth { get => (double)GetValue(TrailWidthProperty); set => SetValue(TrailWidthProperty, value); }
        public string Palette { get => (string)GetValue(PaletteProperty); set => SetValue(PaletteProperty, value); }

        private readonly TrailBuffer buffer = new TrailBuffer();
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly Dictionary<int, Pen> pens = new Dictionary<int, Pen>();
        private readonly List<RenderSegment> segments = new List<RenderSegment>(128);
        private struct RenderSegment
        {
            public StreamGeometry Geometry;
            public int Color;
            public int Age;
        }
        private readonly MouseEventHandler moveHandler;
        private FrameworkElement attachedSource;
        private Window host;
        private bool rendering;
        private double frameTime;
        private TimeSpan lastRenderingTime;

        public MouseTrailOverlay()
        {
            IsHitTestVisible = false;
            Focusable = false;
            ClipToBounds = true;
            moveHandler = OnMouseMove;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            IsVisibleChanged += OnVisibilityChanged;
            SizeChanged += (s, e) => Clear();
        }

        private static DependencyProperty Register(string name, bool value)
        { return DependencyProperty.Register(name, typeof(bool), typeof(MouseTrailOverlay), new PropertyMetadata(value, SettingsChanged)); }

        private static void InputSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        { var control = (MouseTrailOverlay)d; if (control.IsLoaded) control.AttachSource(); }

        private static void SettingsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (MouseTrailOverlay)d;
            control.pens.Clear();
            if (!control.CanAnimate || e.Property == PaletteProperty) control.Clear();
            else if (e.Property == IsPausedProperty) control.buffer.Break();
            control.InvalidateVisual();
        }

        private bool CanAnimate => IsLoaded && IsVisible && IsTrailEnabled &&
            (!RespectReducedMotion || SystemParameters.ClientAreaAnimation) && (host == null || host.WindowState != WindowState.Minimized);

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            AttachSource();
            host = Window.GetWindow(this);
            if (host != null)
            {
                host.Deactivated += OnDeactivated;
                host.StateChanged += OnWindowStateChanged;
            }
            SystemParameters.StaticPropertyChanged += OnSystemParametersChanged;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            DetachSource();
            if (host != null)
            {
                host.Deactivated -= OnDeactivated;
                host.StateChanged -= OnWindowStateChanged;
                host = null;
            }
            SystemParameters.StaticPropertyChanged -= OnSystemParametersChanged;
            Clear(); pens.Clear();
        }

        private void AttachSource()
        {
            DetachSource(); Clear();
            attachedSource = InputSource;
            if (attachedSource == null) return;
            // Preview + handledEventsToo also observes movement over buttons and custom controls.
            attachedSource.AddHandler(Mouse.PreviewMouseMoveEvent, moveHandler, true);
            attachedSource.MouseLeave += OnMouseLeave;
        }
        private void DetachSource()
        {
            if (attachedSource == null) return;
            attachedSource.RemoveHandler(Mouse.PreviewMouseMoveEvent, moveHandler);
            attachedSource.MouseLeave -= OnMouseLeave;
            attachedSource = null;
        }
        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (host != null && !host.IsActive) return;
            RecordSample(e.GetPosition(this));
        }
        private void RecordSample(Point point)
        {
            if (!CanAnimate || IsPaused || point.X < 0 || point.Y < 0 || point.X > ActualWidth || point.Y > ActualHeight) return;
            double now = clock.Elapsed.TotalSeconds;
            buffer.Prune(now, Lifetime);
            if (!buffer.Add(point, now)) return;
            frameTime = now;
            StartRendering();
            InvalidateVisual();
        }
        private void OnMouseLeave(object sender, MouseEventArgs e) { buffer.Break(); }
        private void OnDeactivated(object sender, EventArgs e) { Clear(); }
        private void OnWindowStateChanged(object sender, EventArgs e) { Clear(); }
        private void OnVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e) { if (!IsVisible) Clear(); }
        private void OnSystemParametersChanged(object sender, PropertyChangedEventArgs e)
        {
            // System preference notifications can originate outside the UI dispatcher.
            if (!Dispatcher.CheckAccess())
            { Dispatcher.BeginInvoke(new Action(() => { if (!CanAnimate) Clear(); })); return; }
            if (!CanAnimate) Clear();
        }
        private void StartRendering()
        {
            if (rendering) return;
            rendering = true;
            lastRenderingTime = TimeSpan.MinValue;
            CompositionTarget.Rendering += OnRendering;
        }
        private void StopRendering()
        {
            if (!rendering) return;
            CompositionTarget.Rendering -= OnRendering;
            rendering = false;
        }
        private void Clear()
        { buffer.Clear(); StopRendering(); InvalidateVisual(); }

        private void OnRendering(object sender, EventArgs e)
        {
            if (!CanAnimate) { Clear(); return; }
            var args = e as RenderingEventArgs;
            if (args != null && args.RenderingTime == lastRenderingTime) return;
            if (args != null) lastRenderingTime = args.RenderingTime;
            frameTime = clock.Elapsed.TotalSeconds;
            buffer.Prune(frameTime, Lifetime);
            if (buffer.Samples.Count == 0) { StopRendering(); }
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            if (!CanAnimate) return;
            var points = buffer.Samples;
            segments.Clear();
            int strokeStart = 0;
            for (int i = 1; i < points.Count; i++)
            {
                    var a = points[i - 1]; var b = points[i];
                    if (a.StartsStroke) strokeStart = i - 1;
                    if (b.StartsStroke) continue;
                    double remaining = 1 - Math.Max(0, frameTime - (a.Time + b.Time) * .5) / Lifetime;
                    if (remaining <= 0) continue;
                    remaining *= Math.Min(1, (i - strokeStart) / 6.0);
                    int ageBucket = Math.Max(0, Math.Min(23, (int)(remaining * 23)));
                    int colorBucket = Math.Max(0, Math.Min(95, (int)(b.Phase * 96)));
                    var p0 = i > 1 && !a.StartsStroke ? points[i - 2].Position : a.Position;
                    var p3 = i + 1 < points.Count && !points[i + 1].StartsStroke ? points[i + 1].Position : b.Position;
                    var geometry = new StreamGeometry();
                    using (var context = geometry.Open())
                    {
                        context.BeginFigure(a.Position, false, false);
                        double limit = (b.Position - a.Position).Length * .5;
                        context.BezierTo(a.Position + Limit((b.Position - p0) / 6, limit),
                            b.Position - Limit((p3 - a.Position) / 6, limit), b.Position, true, false);
                    }
                    geometry.Freeze();
                    segments.Add(new RenderSegment { Geometry = geometry, Color = colorBucket, Age = ageBucket });
            }
            // Reuse each segment across the halo, body and core passes.
            for (int layer = 0; layer < 4; layer++)
                foreach (var segment in segments)
                    dc.DrawGeometry(null, GetPen(segment.Color, segment.Age, layer), segment.Geometry);
        }

        private static Vector Limit(Vector value, double maximum)
        { return value.Length > maximum && value.Length > 0 ? value * (maximum / value.Length) : value; }

        private Pen GetPen(int colorBucket, int ageBucket, int layer)
        {
            int key = (colorBucket * 24 + ageBucket) * 4 + layer;
            Pen pen;
            if (pens.TryGetValue(key, out pen)) return pen;
            if (pens.Count >= 4096) pens.Clear();
            double life = (ageBucket + 1) / 24.0;
            double fade = Math.Pow(life, 1.5) * Intensity;
            double width = TrailWidth * (.12 + .88 * Math.Sqrt(life));
            double phase = colorBucket / 96.0;
            Color color = PaletteColor(phase);
            if (Palette == "单色呼吸") fade *= .8 + .2 * Math.Sin(phase * Math.PI * 2);
            switch (layer)
            {
                case 0: width *= 3.6; fade *= IsNight ? .055 : .035; break;
                case 1: width *= 2.2; fade *= IsNight ? .12 : .08; break;
                case 2: fade *= .72; break;
                default: width = Math.Max(.5, width * .22); fade *= IsNight ? .85 : .65; color = Mix(color, Colors.White, .72); break;
            }
            color.A = (byte)Math.Min(255, Math.Max(0, fade * 255));
            var brush = new SolidColorBrush(color); brush.Freeze();
            // Flat internal caps avoid bright beads where translucent neighboring segments meet.
            pen = new Pen(brush, width) { StartLineCap = PenLineCap.Flat, EndLineCap = PenLineCap.Flat, LineJoin = PenLineJoin.Round };
            pen.Freeze(); pens[key] = pen;
            return pen;
        }

        private Color PaletteColor(double phase)
        {
            if (Palette == "单色呼吸") return Color.FromRgb(64, 238, 102);
            Color[] colors = Palette == "霓虹" ? Neon : Aurora;
            double position = phase * colors.Length;
            int index = (int)position % colors.Length;
            return Mix(colors[index], colors[(index + 1) % colors.Length], position - Math.Floor(position));
        }
        private static readonly Color[] Aurora = { Color.FromRgb(48, 233, 106), Color.FromRgb(26, 219, 204), Color.FromRgb(73, 152, 251), Color.FromRgb(160, 107, 245) };
        private static readonly Color[] Neon = { Color.FromRgb(177, 88, 255), Color.FromRgb(251, 100, 192), Color.FromRgb(64, 186, 255) };
        private static Color Mix(Color a, Color b, double amount)
        { return Color.FromRgb((byte)(a.R + (b.R - a.R) * amount), (byte)(a.G + (b.G - a.G) * amount), (byte)(a.B + (b.B - a.B) * amount)); }
    }
}

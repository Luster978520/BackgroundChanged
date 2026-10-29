using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Media3D;

namespace WpfCustomControlLibrary1
{
    /// <summary>A two-sided, interactive WPF surface rotated in a perspective viewport.</summary>
    [TemplatePart(Name = "PART_Viewport", Type = typeof(Viewport3D))]
    [TemplatePart(Name = "PART_Front", Type = typeof(ContentPresenter))]
    [TemplatePart(Name = "PART_Back", Type = typeof(ContentPresenter))]
    public class FlipPanel : Control
    {
        public static readonly RoutedUICommand FlipCommand = new RoutedUICommand("Flip", "Flip", typeof(FlipPanel));
        public static readonly DependencyProperty FrontContentProperty = DependencyProperty.Register(
            nameof(FrontContent), typeof(object), typeof(FlipPanel));
        public static readonly DependencyProperty BackContentProperty = DependencyProperty.Register(
            nameof(BackContent), typeof(object), typeof(FlipPanel));
        public static readonly DependencyProperty IsFlippedProperty = DependencyProperty.Register(
            nameof(IsFlipped), typeof(bool), typeof(FlipPanel),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnFlipChanged));
        public static readonly DependencyProperty AnimationDurationProperty = DependencyProperty.Register(
            nameof(AnimationDuration), typeof(double), typeof(FlipPanel), new PropertyMetadata(650.0),
            value => !double.IsNaN((double)value) && !double.IsInfinity((double)value) && (double)value >= 0);
        public static readonly DependencyProperty RadiusProperty = DependencyProperty.Register(
            nameof(Radius), typeof(CornerRadius), typeof(FlipPanel), new PropertyMetadata(new CornerRadius(24)));
        private static readonly DependencyProperty AngleProperty = DependencyProperty.Register(
            "Angle", typeof(double), typeof(FlipPanel), new PropertyMetadata(0.0, OnAngleChanged));
        private static readonly DependencyPropertyKey IsAnimatingPropertyKey = DependencyProperty.RegisterReadOnly(
            nameof(IsAnimating), typeof(bool), typeof(FlipPanel), new PropertyMetadata(false));
        public static readonly DependencyProperty IsAnimatingProperty = IsAnimatingPropertyKey.DependencyProperty;

        public object FrontContent { get => GetValue(FrontContentProperty); set => SetValue(FrontContentProperty, value); }
        public object BackContent { get => GetValue(BackContentProperty); set => SetValue(BackContentProperty, value); }
        public bool IsFlipped { get => (bool)GetValue(IsFlippedProperty); set => SetValue(IsFlippedProperty, value); }
        public double AnimationDuration { get => (double)GetValue(AnimationDurationProperty); set => SetValue(AnimationDurationProperty, value); }
        public CornerRadius Radius { get => (CornerRadius)GetValue(RadiusProperty); set => SetValue(RadiusProperty, value); }
        public bool IsAnimating => (bool)GetValue(IsAnimatingProperty);
        public double RotationAngle => (double)GetValue(AngleProperty);

        private Viewport3D viewport;
        private ContentPresenter front;
        private ContentPresenter back;
        private AxisAngleRotation3D frontRotation;
        private AxisAngleRotation3D backRotation;
        private int animationVersion;

        static FlipPanel()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(FlipPanel), new FrameworkPropertyMetadata(typeof(FlipPanel)));
        }

        public FlipPanel()
        {
            CommandBindings.Add(new CommandBinding(FlipCommand, (s, e) => { Flip(); e.Handled = true; }));
            SizeChanged += (s, e) => UpdateGeometry();
            Loaded += (s, e) => Settle();
            Unloaded += (s, e) => Settle();
        }

        public void Flip() { SetCurrentValue(IsFlippedProperty, !IsFlipped); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            // Leave typing and the native keyboard behavior of child controls untouched.
            if (!e.Handled && IsKeyboardFocused && Keyboard.Modifiers == ModifierKeys.None &&
                (e.Key == Key.Space || e.Key == Key.Enter))
            { Flip(); e.Handled = true; }
        }

        public override void OnApplyTemplate()
        {
            Settle();
            base.OnApplyTemplate();
            viewport = GetTemplateChild("PART_Viewport") as Viewport3D;
            front = GetTemplateChild("PART_Front") as ContentPresenter;
            back = GetTemplateChild("PART_Back") as ContentPresenter;
            frontRotation = GetTemplateChild("PART_FrontRotation") as AxisAngleRotation3D;
            backRotation = GetTemplateChild("PART_BackRotation") as AxisAngleRotation3D;
            UpdateGeometry();
            UpdateFaces();
        }

        private static void OnFlipChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        { ((FlipPanel)d).AnimateFlip(); }

        private static void OnAngleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        { ((FlipPanel)d).UpdateFaces(); }

        private void AnimateFlip()
        {
            if (!IsLoaded || !SystemParameters.ClientAreaAnimation || AnimationDuration == 0)
            { Settle(); return; }
            double start = RotationAngle;
            double target = IsFlipped ? 180 : 0;
            int version = ++animationVersion;
            // A new request begins at the current animated angle, including a mid-flight reversal.
            BeginAnimation(AngleProperty, null);
            SetValue(AngleProperty, start);
            if (Math.Abs(target - start) < .001) { Settle(); return; }
            SetValue(IsAnimatingPropertyKey, true);
            if ((front != null && front.IsKeyboardFocusWithin) || (back != null && back.IsKeyboardFocusWithin)) Focus();
            UpdateFaces();
            var animation = new DoubleAnimation(start, target,
                TimeSpan.FromMilliseconds(AnimationDuration * Math.Abs(target - start) / 180))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }, FillBehavior = FillBehavior.HoldEnd };
            animation.Completed += (s, e) => { if (version == animationVersion) Settle(); };
            BeginAnimation(AngleProperty, animation, HandoffBehavior.SnapshotAndReplace);
        }

        private void Settle()
        {
            ++animationVersion;
            BeginAnimation(AngleProperty, null);
            SetValue(AngleProperty, IsFlipped ? 180.0 : 0.0);
            SetValue(IsAnimatingPropertyKey, false);
            UpdateFaces();
        }

        private void UpdateFaces()
        {
            if (frontRotation != null) frontRotation.Angle = RotationAngle;
            // At 180 degrees this second face reaches 360 and reads normally, without mirrored text.
            if (backRotation != null) backRotation.Angle = RotationAngle + 180;
            if (front != null) front.IsEnabled = !IsAnimating && !IsFlipped;
            if (back != null) back.IsEnabled = !IsAnimating && IsFlipped;
        }

        private void UpdateGeometry()
        {
            if (viewport == null || ActualWidth <= 0 || ActualHeight <= 0) return;
            double aspect = ActualWidth / ActualHeight;
            // FieldOfView is horizontal. Leave enough room for perspective while the card rotates.
            viewport.Camera = new PerspectiveCamera(new Point3D(0, 0, aspect / Math.Tan(Math.PI / 9) * 1.12),
                new Vector3D(0, 0, -1), new Vector3D(0, 1, 0), 40);
            foreach (var child in viewport.Children)
            {
                var surface = child as Viewport2DVisual3D;
                if (surface == null) continue;
                surface.Geometry = new MeshGeometry3D
                {
                    Positions = new Point3DCollection { new Point3D(-aspect,-1,0), new Point3D(aspect,-1,0),
                        new Point3D(aspect,1,0), new Point3D(-aspect,1,0) },
                    TextureCoordinates = new PointCollection { new Point(0,1), new Point(1,1), new Point(1,0), new Point(0,0) },
                    TriangleIndices = new Int32Collection { 0,1,2,0,2,3 }
                };
            }
        }
    }

    /// <summary>Compatibility with the original misspelled control and IsFilp property.</summary>
    public class FilpPanel : FlipPanel
    {
        public static readonly DependencyProperty IsFilpProperty = IsFlippedProperty;
        public bool IsFilp { get => IsFlipped; set => SetValue(IsFlippedProperty, value); }
    }
}

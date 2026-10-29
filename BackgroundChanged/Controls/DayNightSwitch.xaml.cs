using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace BackgroundChanged.Controls
{
    public partial class DayNightSwitch : UserControl
    {
        private const double Travel = 192;
        private const double Radius = 74;
        private static readonly double RollingAngle = Travel / Radius * 180 / Math.PI;
        // A single animated progress drives both transforms, including interrupted reversals.
        private static readonly DependencyProperty RollProgressProperty = DependencyProperty.Register("RollProgress", typeof(double),
            typeof(DayNightSwitch), new PropertyMetadata(0.0, OnRollProgressChanged));
        private TranslateTransform translation;
        private RotateTransform rotation;
        public static readonly DependencyProperty IsNightProperty = DependencyProperty.Register(nameof(IsNight), typeof(bool),
            typeof(DayNightSwitch), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnModeChanged));
        public bool IsNight { get => (bool)GetValue(IsNightProperty); set => SetValue(IsNightProperty, value); }
        public DayNightSwitch()
        {
            InitializeComponent();
            Loaded += (s, e) => ApplyState(false);
        }
        private static void OnModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        { var control = (DayNightSwitch)d; if (control.IsLoaded) control.ApplyState(true); }
        private void ApplyState(bool animate)
        {
            Switch.ApplyTemplate();
            translation = (TranslateTransform)Switch.Template.FindName("KnobTranslation", Switch);
            rotation = (RotateTransform)Switch.Template.FindName("KnobRotation", Switch);
            var moonOrientation = (RotateTransform)Switch.Template.FindName("MoonOrientation", Switch);
            if (translation == null || rotation == null) return;
            if (moonOrientation != null) moonOrientation.Angle = -RollingAngle;
            Animate(this, RollProgressProperty, IsNight ? 1 : 0, animate);
            foreach (var name in new[] { "NightSky", "Stars", "Moon" })
                Animate((UIElement)Switch.Template.FindName(name, Switch), OpacityProperty, IsNight ? 1 : 0, animate);
            // Keep the opaque sun underneath the fading moon so the rolling disc never becomes see-through.
            foreach (var name in new[] { "Clouds" })
                Animate((UIElement)Switch.Template.FindName(name, Switch), OpacityProperty, IsNight ? 0 : 1, animate);
        }
        private static void OnRollProgressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (DayNightSwitch)d;
            double progress = (double)e.NewValue;
            if (control.translation != null) control.translation.X = Travel * progress;
            if (control.rotation != null) control.rotation.Angle = RollingAngle * progress;
        }
        private static void Animate(Animatable target, DependencyProperty property, double value, bool animate)
        { target.BeginAnimation(property, Animation(value, animate)); }
        private static void Animate(UIElement target, DependencyProperty property, double value, bool animate)
        { target.BeginAnimation(property, Animation(value, animate)); }
        private static DoubleAnimation Animation(double value, bool animate)
        {
            return new DoubleAnimation(value, TimeSpan.FromMilliseconds(animate && SystemParameters.ClientAreaAnimation ? 620 : 0))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } };
        }
    }
}

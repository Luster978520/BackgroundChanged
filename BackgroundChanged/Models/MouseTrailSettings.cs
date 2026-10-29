using System;

namespace BackgroundChanged.Models
{
    public class MouseTrailSettings
    {
        public bool IsEnabled { get; set; } = true;
        public double Lifetime { get; set; } = 0.65;
        public double Intensity { get; set; } = 0.85;
        public double TrailWidth { get; set; } = 6;
        public string Palette { get; set; } = "极光";
        public bool RespectReducedMotion { get; set; } = true;

        public void Normalize()
        {
            Lifetime = Bound(Lifetime, 0.2, 1.5, 0.65);
            Intensity = Bound(Intensity, 0.1, 1, 0.85);
            TrailWidth = Bound(TrailWidth, 3, 10, 6);
            if (Palette != "极光" && Palette != "霓虹" && Palette != "单色呼吸") Palette = "极光";
        }

        internal static double Bound(double value, double min, double max, double fallback)
        { return double.IsNaN(value) || double.IsInfinity(value) ? fallback : Math.Max(min, Math.Min(max, value)); }
    }
}

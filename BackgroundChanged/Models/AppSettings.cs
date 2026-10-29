namespace BackgroundChanged.Models
{
    public class BackgroundSettings
    {
        public string Kind { get; set; } = "风景渐变";
        public string ImagePath { get; set; }
        public string Color { get; set; } = "#89B8DF";
        public bool ShowWholeImage { get; set; }
        public double Overlay { get; set; } = 0.12;
    }

    public class AppSettings
    {
        public bool IsNight { get; set; }
        public MouseTrailSettings MouseTrail { get; set; } = new MouseTrailSettings();
        public BackgroundSettings Day { get; set; } = new BackgroundSettings();
        public BackgroundSettings Night { get; set; } = new BackgroundSettings
        {
            Color = "#182442", Overlay = 0.2
        };
    }
}

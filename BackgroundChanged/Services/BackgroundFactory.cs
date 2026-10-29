using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BackgroundChanged.Models;

namespace BackgroundChanged.Services
{
    public static class BackgroundFactory
    {
        public static Brush Create(BackgroundSettings settings, bool night, out bool fallback)
        {
            fallback = false;
            if (settings.Kind == "图片")
            {
                try
                {
                    var bitmap = LoadImage(settings.ImagePath);
                    var image = new ImageBrush(bitmap)
                    {
                        Stretch = settings.ShowWholeImage ? Stretch.Uniform : Stretch.UniformToFill
                    };
                    image.Freeze();
                    return image;
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException ||
                    ex is NotSupportedException || ex is ArgumentException || ex is InvalidOperationException ||
                    ex is System.Runtime.InteropServices.COMException)
                { fallback = true; }
            }
            if (settings.Kind == "纯色")
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(settings.Color)) throw new FormatException();
                    var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(settings.Color));
                    brush.Freeze();
                    return brush;
                }
                catch (Exception ex) when (ex is FormatException || ex is ArgumentException || ex is NotSupportedException)
                { fallback = true; }
            }
            return Landscape(night);
        }

        public static BitmapImage LoadImage(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new IOException("未设置图片路径。");
            var bitmap = new BitmapImage();
            using (var stream = File.OpenRead(path))
            {
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.DecodePixelWidth = 2560;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze();
            }
            return bitmap;
        }

        private static Brush Landscape(bool night)
        {
            var group = new DrawingGroup();
            var sky = new LinearGradientBrush();
            sky.StartPoint = new Point(0, 0); sky.EndPoint = new Point(0.7, 1);
            sky.GradientStops.Add(new GradientStop(Hex(night ? "#111A38" : "#ADCFE8"), 0));
            sky.GradientStops.Add(new GradientStop(Hex(night ? "#29395D" : "#DCE9ED"), 0.65));
            sky.GradientStops.Add(new GradientStop(Hex(night ? "#52617B" : "#F1E2C9"), 1));
            group.Children.Add(new GeometryDrawing(sky, null, new RectangleGeometry(new Rect(0, 0, 1440, 900))));
            if (night)
            {
                var random = new Random(12);
                for (int i = 0; i < 65; i++)
                {
                    double radius = random.NextDouble() * 1.3 + 0.6;
                    group.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromArgb((byte)random.Next(60, 180), 233, 240, 255)),
                        null, new EllipseGeometry(new Point(random.Next(20, 1420), random.Next(25, 570)), radius, radius)));
                }
            }
            AddMountain(group, night ? "#303F61" : "#A5C0D0", "M0,670 C170,590 270,680 430,623 C600,555 710,670 870,615 C1050,550 1210,588 1440,510 L1440,900 L0,900 Z");
            AddMountain(group, night ? "#243552" : "#83A6B9", "M0,735 C200,650 340,753 510,703 C670,651 770,744 990,677 C1180,622 1310,690 1440,649 L1440,900 L0,900 Z");
            AddMountain(group, night ? "#1B2A44" : "#648C9F", "M0,795 C230,738 330,810 580,771 C840,720 950,822 1190,752 C1300,728 1380,758 1440,730 L1440,900 L0,900 Z");
            var brush = new DrawingBrush(group) { Stretch = Stretch.UniformToFill };
            brush.Freeze();
            return brush;
        }

        private static void AddMountain(DrawingGroup group, string color, string path)
        { group.Children.Add(new GeometryDrawing(new SolidColorBrush(Hex(color)), null, Geometry.Parse(path))); }
        private static Color Hex(string value) { return (Color)ColorConverter.ConvertFromString(value); }
    }
}

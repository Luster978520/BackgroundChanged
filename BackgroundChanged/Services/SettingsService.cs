using System;
using System.IO;
using System.Xml.Serialization;
using BackgroundChanged.Models;

namespace BackgroundChanged.Services
{
    public sealed class SettingsService
    {
        public string DataDirectory { get; }
        private string SettingsPath => Path.Combine(DataDirectory, "settings.xml");
        private readonly XmlSerializer serializer = new XmlSerializer(typeof(AppSettings));

        public SettingsService(string dataDirectory = null)
        {
            DataDirectory = dataDirectory ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BackgroundChanged");
        }

        public AppSettings Load(out string message)
        {
            message = null;
            try
            {
                if (!File.Exists(SettingsPath)) return new AppSettings();
                using (var stream = File.OpenRead(SettingsPath))
                {
                    var value = (AppSettings)serializer.Deserialize(stream);
                    value.Day = value.Day ?? new BackgroundSettings();
                    value.Night = value.Night ?? new AppSettings().Night;
                    value.MouseTrail = value.MouseTrail ?? new MouseTrailSettings();
                    value.MouseTrail.Normalize();
                    Sanitize(value.Day);
                    Sanitize(value.Night);
                    return value;
                }
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidOperationException || ex is UnauthorizedAccessException)
            {
                message = "设置读取失败，已使用默认主题。";
                return new AppSettings();
            }
        }

        private static void Sanitize(BackgroundSettings settings)
        {
            if (settings.Kind != "风景渐变" && settings.Kind != "纯色" && settings.Kind != "图片")
                settings.Kind = "风景渐变";
            settings.Overlay = double.IsNaN(settings.Overlay) ? 0.12 : Math.Max(0, Math.Min(0.7, settings.Overlay));
        }

        public void Save(AppSettings settings)
        {
            Directory.CreateDirectory(DataDirectory);
            string temporary = SettingsPath + ".tmp";
            using (var stream = File.Create(temporary)) serializer.Serialize(stream, settings);
            if (File.Exists(SettingsPath)) File.Replace(temporary, SettingsPath, null);
            else File.Move(temporary, SettingsPath);
        }

        public string ImportImage(string source, bool night)
        {
            string directory = Path.Combine(DataDirectory, "Backgrounds");
            Directory.CreateDirectory(directory);
            string target = Path.Combine(directory, (night ? "night-" : "day-") + Guid.NewGuid().ToString("N") + Path.GetExtension(source));
            File.Copy(source, target);
            return target;
        }

        // Only remove images owned by this app after the replacement settings were saved.
        public void CleanUnusedImages(AppSettings settings)
        {
            string directory = Path.Combine(DataDirectory, "Backgrounds");
            if (!Directory.Exists(directory)) return;
            foreach (string file in Directory.GetFiles(directory))
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(file),
                    @"^(day|night)-[0-9a-f]{32}\.[a-zA-Z0-9]+$")) continue;
                if (string.Equals(file, settings.Day.ImagePath, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(file, settings.Night.ImagePath, StringComparison.OrdinalIgnoreCase)) continue;
                try { File.Delete(file); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }
    }
}

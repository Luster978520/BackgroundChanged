using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using BackgroundChanged.Infrastructure;
using BackgroundChanged.Models;

namespace BackgroundChanged.ViewModels
{
    public sealed class MouseTrailSettingsViewModel : INotifyPropertyChanged
    {
        private readonly MouseTrailSettings settings;
        public MouseTrailSettingsViewModel(MouseTrailSettings settings)
        {
            this.settings = settings;
            ResetCommand = new RelayCommand(_ => Reset());
        }

        public string[] Palettes { get; } = { "极光", "霓虹", "单色呼吸" };
        public ICommand ResetCommand { get; }
        public bool IsEnabled
        {
            get => settings.IsEnabled;
            set { if (value == settings.IsEnabled) return; settings.IsEnabled = value; Changed(); }
        }
        public double Lifetime
        {
            get => settings.Lifetime;
            set
            {
                value = MouseTrailSettings.Bound(value, .2, 1.5, .65);
                if (Math.Abs(settings.Lifetime - value) < .001) return;
                settings.Lifetime = value; Changed(); Notify(nameof(LifetimeText));
            }
        }
        public string LifetimeText => Lifetime.ToString("0.00") + " 秒";
        public double Intensity
        {
            get => settings.Intensity;
            set
            {
                value = MouseTrailSettings.Bound(value, .1, 1, .85);
                if (Math.Abs(settings.Intensity - value) < .001) return;
                settings.Intensity = value; Changed(); Notify(nameof(IntensityText));
            }
        }
        public string IntensityText => Math.Round(Intensity * 100) + "%";
        public double TrailWidth
        {
            get => settings.TrailWidth;
            set
            {
                value = MouseTrailSettings.Bound(value, 3, 10, 6);
                if (Math.Abs(settings.TrailWidth - value) < .001) return;
                settings.TrailWidth = value; Changed(); Notify(nameof(WidthText));
            }
        }
        public string WidthText => TrailWidth.ToString("0.0") + " px";
        public string Palette
        {
            get => settings.Palette;
            set
            {
                if (value == settings.Palette || Array.IndexOf(Palettes, value) < 0) return;
                settings.Palette = value; Changed();
            }
        }
        public bool RespectReducedMotion
        {
            get => settings.RespectReducedMotion;
            set { if (value == settings.RespectReducedMotion) return; settings.RespectReducedMotion = value; Changed(); }
        }

        private void Reset()
        {
            var defaults = new MouseTrailSettings();
            IsEnabled = defaults.IsEnabled; Lifetime = defaults.Lifetime; Intensity = defaults.Intensity;
            TrailWidth = defaults.TrailWidth; Palette = defaults.Palette; RespectReducedMotion = defaults.RespectReducedMotion;
        }
        public event EventHandler SettingsChanged;
        public event PropertyChangedEventHandler PropertyChanged;
        private void Changed([CallerMemberName] string name = null)
        { Notify(name); SettingsChanged?.Invoke(this, EventArgs.Empty); }
        private void Notify(string name) { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)); }
    }
}

using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using BackgroundChanged.Infrastructure;
using BackgroundChanged.Models;
using BackgroundChanged.Services;
using Microsoft.Win32;

namespace BackgroundChanged.ViewModels
{
    public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly SettingsService service;
        private readonly DispatcherTimer clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        private readonly DispatcherTimer saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
        private AppSettings settings;
        private int editingMode;
        private bool settingsOpen;
        private bool importing;
        private string status;
        private Brush dayBackground;
        private Brush nightBackground;

        public MainViewModel(SettingsService service = null)
        {
            this.service = service ?? new SettingsService();
            string message;
            settings = this.service.Load(out message);
            MouseTrailSettings = new MouseTrailSettingsViewModel(settings.MouseTrail);
            MouseTrailSettings.SettingsChanged += OnTrailSettingsChanged;
            editingMode = settings.IsNight ? 1 : 0;
            ReloadBackground(false);
            ReloadBackground(true);
            Status = message ?? status ?? "设置自动保存到本机";
            clock.Tick += ClockTick;
            saveTimer.Tick += SaveTick;
            clock.Start();
            ToggleSettingsCommand = new RelayCommand(_ => IsSettingsOpen = !IsSettingsOpen);
            CloseSettingsCommand = new RelayCommand(_ => IsSettingsOpen = false);
            ChooseImageCommand = new RelayCommand(async _ => await ChooseImageAsync());
            ResetCommand = new RelayCommand(_ => Reset());
            SelectDayCommand = new RelayCommand(_ => EditingMode = 0);
            SelectNightCommand = new RelayCommand(_ => EditingMode = 1);
        }

        public ICommand ToggleSettingsCommand { get; }
        public MouseTrailSettingsViewModel MouseTrailSettings { get; }
        public ICommand CloseSettingsCommand { get; }
        public ICommand ChooseImageCommand { get; }
        public ICommand ResetCommand { get; }
        public ICommand SelectDayCommand { get; }
        public ICommand SelectNightCommand { get; }
        public string TimeText => DateTime.Now.ToString("HH:mm");
        public string DateText => DateTime.Now.ToString("M月d日  ·  dddd", CultureInfo.GetCultureInfo("zh-CN"));
        public string ModeTitle => IsNight ? "把片刻，留给星光。" : "让今天，慢慢亮起来。";
        public string ModeLabel => IsNight ? "夜间模式" : "日间模式";
        public string ModeCaption => IsNight ? "NIGHTFALL / 静享此刻" : "DAYLIGHT / 美好日常";
        public string SwitchHint => IsNight ? "点击月亮，唤醒白昼" : "点击太阳，进入夜晚";
        public bool IsNight
        {
            get => settings.IsNight;
            set
            {
                if (settings.IsNight == value) return;
                settings.IsNight = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ModeTitle)); OnPropertyChanged(nameof(ModeLabel));
                OnPropertyChanged(nameof(ModeCaption)); OnPropertyChanged(nameof(SwitchHint));
                QueueSave();
            }
        }

        public bool IsSettingsOpen { get => settingsOpen; set { settingsOpen = value; OnPropertyChanged(); } }
        public string Status { get => status; private set { status = value; OnPropertyChanged(); } }
        public bool CanImport { get => !importing; }
        public Brush DayBackground { get => dayBackground; private set { dayBackground = value; OnPropertyChanged(); } }
        public Brush NightBackground { get => nightBackground; private set { nightBackground = value; OnPropertyChanged(); } }
        public double DayOverlay => settings.Day.Overlay;
        public double NightOverlay => settings.Night.Overlay;
        public int EditingMode
        {
            get => editingMode;
            set { if (editingMode == value) return; editingMode = value; RefreshEditor(); }
        }
        public string EditorTitle => editingMode == 0 ? "日间背景" : "夜间背景";
        private BackgroundSettings Editing => editingMode == 0 ? settings.Day : settings.Night;
        public string[] BackgroundKinds { get; } = { "风景渐变", "纯色", "图片" };
        public string BackgroundKind
        {
            get => Editing.Kind;
            set { if (value == null || value == Editing.Kind) return; Editing.Kind = value; UpdateBackground(); RefreshEditor(); }
        }
        public string SolidColor
        {
            get => Editing.Color;
            set
            {
                if (value == Editing.Color) return;
                try
                {
                    if (string.IsNullOrWhiteSpace(value)) throw new FormatException();
                    ColorConverter.ConvertFromString(value);
                    Editing.Color = value;
                    UpdateBackground(); OnPropertyChanged();
                }
                catch (Exception ex) when (ex is FormatException || ex is ArgumentException || ex is NotSupportedException)
                { Status = "请输入有效颜色，例如 #89B8DF。"; OnPropertyChanged(); }
            }
        }
        public bool ShowWholeImage
        {
            get => Editing.ShowWholeImage;
            set { if (value == Editing.ShowWholeImage) return; Editing.ShowWholeImage = value; UpdateBackground(); OnPropertyChanged(); }
        }
        public double Overlay
        {
            get => Editing.Overlay;
            set
            {
                if (Math.Abs(Editing.Overlay - value) < .001) return;
                Editing.Overlay = value;
                OnPropertyChanged(); OnPropertyChanged(nameof(OverlayText));
                OnPropertyChanged(nameof(DayOverlay)); OnPropertyChanged(nameof(NightOverlay)); QueueSave();
            }
        }
        public string OverlayText => Math.Round(Overlay * 100) + "%";
        public string ImageName => string.IsNullOrEmpty(Editing.ImagePath) ? "尚未选择图片" : Path.GetFileName(Editing.ImagePath);

        private async Task ChooseImageAsync()
        {
            if (importing) return;
            var dialog = new OpenFileDialog
            {
                Title = "选择背景图片", Filter = "图片文件|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tif;*.tiff|所有文件|*.*",
                CheckFileExists = true, Multiselect = false
            };
            if (dialog.ShowDialog() != true) return;
            bool night = editingMode == 1;
            importing = true; OnPropertyChanged(nameof(CanImport));
            Status = "正在导入图片…";
            try
            {
                string imported = await Task.Run(() =>
                {
                    // Decode before importing so a corrupt file never replaces the current background.
                    BackgroundFactory.LoadImage(dialog.FileName);
                    return service.ImportImage(dialog.FileName, night);
                });
                var target = night ? settings.Night : settings.Day;
                target.ImagePath = imported;
                target.Kind = "图片";
                ReloadBackground(night);
                RefreshEditor(); QueueSave();
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is NotSupportedException ||
                ex is ArgumentException || ex is InvalidOperationException || ex is System.Runtime.InteropServices.COMException)
            { Status = "无法导入该图片，请确认文件有效且可以读取。"; }
            finally { importing = false; OnPropertyChanged(nameof(CanImport)); }
        }

        private void Reset()
        {
            if (editingMode == 0) settings.Day = new AppSettings().Day;
            else settings.Night = new AppSettings().Night;
            UpdateBackground(); RefreshEditor();
        }
        private void UpdateBackground() { ReloadBackground(editingMode == 1); QueueSave(); }
        private void ReloadBackground(bool night)
        {
            bool fallback;
            var brush = BackgroundFactory.Create(night ? settings.Night : settings.Day, night, out fallback);
            if (night) NightBackground = brush; else DayBackground = brush;
            if (fallback) Status = (night ? "夜间" : "日间") + "背景不可用，已显示默认风景。可重新选择图片。";
        }
        private void RefreshEditor()
        {
            foreach (string name in new[] { nameof(EditingMode), nameof(EditorTitle), nameof(BackgroundKind), nameof(SolidColor),
                nameof(ShowWholeImage), nameof(Overlay), nameof(OverlayText), nameof(ImageName), nameof(DayOverlay), nameof(NightOverlay) })
                OnPropertyChanged(name);
        }
        private void QueueSave() { saveTimer.Stop(); saveTimer.Start(); Status = "正在保存设置…"; }
        private void OnTrailSettingsChanged(object sender, EventArgs e) { QueueSave(); }
        private void SaveTick(object sender, EventArgs e) { saveTimer.Stop(); Save(); }
        private void ClockTick(object sender, EventArgs e) { OnPropertyChanged(nameof(TimeText)); OnPropertyChanged(nameof(DateText)); }
        private void Save()
        {
            try { service.Save(settings); Status = "设置已保存 · 下次启动自动恢复"; if (!importing) service.CleanUnusedImages(settings); }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is InvalidOperationException)
            { Status = "设置保存失败，请检查本地目录权限。"; }
        }
        public void Dispose()
        {
            clock.Stop(); saveTimer.Stop();
            MouseTrailSettings.SettingsChanged -= OnTrailSettingsChanged;
            clock.Tick -= ClockTick; saveTimer.Tick -= SaveTick;
            Save();
        }
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
        { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)); }
    }
}

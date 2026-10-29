using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace BackgroundChanged.Controls
{
    public partial class GlassLauncher : UserControl
    {
        public static readonly DependencyProperty IsExpandedProperty = DependencyProperty.Register(
            nameof(IsExpanded), typeof(bool), typeof(GlassLauncher), new PropertyMetadata(false));

        public bool IsExpanded
        {
            get => (bool)GetValue(IsExpandedProperty);
            set => SetValue(IsExpandedProperty, value);
        }

        private void ToggleList_Click(object sender, RoutedEventArgs e)
        {
            SetCurrentValue(IsExpandedProperty, !IsExpanded);
        }

        public IReadOnlyList<LauncherFeature> Features { get; } = new[]
        {
            new LauncherFeature("notes", "随手记", "留住一闪而过的灵感", "\uE70B", "#9880CF"),
            new LauncherFeature("focus", "专注时刻", "给自己一段安静的时间", "\uE916", "#668FCD"),
            new LauncherFeature("tasks", "今日待办", "让每一件小事有着落", "\uE73A", "#57A6A3"),
            new LauncherFeature("calendar", "日历计划", "为期待的日子做个标记", "\uE787", "#C08EAB"),
            new LauncherFeature("files", "文件空间", "收纳你的日常与灵感", "\uE8B7", "#C7A16C"),
            new LauncherFeature("photos", "拾光相册", "收藏值得回看的瞬间", "\uEB9F", "#7F99C9"),
            new LauncherFeature("music", "音乐角落", "让旋律陪伴此刻", "\uE8D6", "#A07DC2"),
            new LauncherFeature("reading", "阅读清单", "在文字里发现新世界", "\uE736", "#6BACA4"),
            new LauncherFeature("weather", "窗外天气", "感受今天的晴雨冷暖", "\uE706", "#719FC8"),
            new LauncherFeature("tools", "实用工具", "让日常多一点轻松", "\uE90F", "#A08BB5"),
            new LauncherFeature("favorites", "我的收藏", "喜欢的事物，触手可及", "\uE734", "#C29579"),
            new LauncherFeature("ideas", "灵感画板", "给想象留一点空间", "\uE790", "#8396C1")
        };

        public event EventHandler<FeatureRequestedEventArgs> FeatureRequested;

        public GlassLauncher() { InitializeComponent(); }

        private void Feature_Click(object sender, RoutedEventArgs e)
        {
            var feature = ((Button)sender).DataContext as LauncherFeature;
            if (feature != null) FeatureRequested?.Invoke(this, new FeatureRequestedEventArgs(feature));
        }
    }

    public sealed class LauncherFeature
    {
        public string Id { get; }
        public string Title { get; }
        public string Description { get; }
        public string Glyph { get; }
        public Brush Tint { get; }

        public LauncherFeature(string id, string title, string description, string glyph, string tint)
        {
            Id = id; Title = title; Description = description; Glyph = glyph;
            Tint = (Brush)new BrushConverter().ConvertFromString(tint);
            Tint.Freeze();
        }
    }

    public sealed class FeatureRequestedEventArgs : EventArgs
    {
        public LauncherFeature Feature { get; }
        public FeatureRequestedEventArgs(LauncherFeature feature) { Feature = feature; }
    }
}

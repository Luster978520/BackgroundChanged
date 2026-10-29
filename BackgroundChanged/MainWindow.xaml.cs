using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using BackgroundChanged.ViewModels;
using BackgroundChanged.Controls;
using BackgroundChanged.View;

namespace BackgroundChanged
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel viewModel;
        private IInputElement previousFocus;
        private readonly Dictionary<string, Window> featureWindows = new Dictionary<string, Window>();
        private FlipPanelWindow flipPanelWindow;
        private StudyBingdingFrm studyBingdingFrm;


        public MainWindow() : this(new MainViewModel()) { }

        public MainWindow(MainViewModel model)
        {
            InitializeComponent();
            viewModel = model;
            DataContext = viewModel;
            viewModel.PropertyChanged += OnViewModelChanged;
            ApplyTheme(false);
            Closed += (s, e) => { viewModel.PropertyChanged -= OnViewModelChanged; viewModel.Dispose(); };
        }

        private void ToggleLauncher_Click(object sender, RoutedEventArgs e)
        {
            bool expand = Launcher.Visibility != Visibility.Visible;
            Launcher.Visibility = expand ? Visibility.Visible : Visibility.Collapsed;
            LauncherColumn.Width = new GridLength(expand ? 250 : 0);
            LauncherGap.Width = new GridLength(expand ? 24 : 0);
            LauncherToggleLabel.Text = expand ? "收起空间" : "功能空间";
            string action = expand ? "收起功能面板" : "展开功能面板";
            LauncherToggle.ToolTip = action;
            System.Windows.Automation.AutomationProperties.SetName(LauncherToggle, action);
        }

        private void OnFeatureRequested(object sender, FeatureRequestedEventArgs e)
        {
            Window existing;
            if (featureWindows.TryGetValue(e.Feature.Id, out existing))
            {
                if (existing.WindowState == WindowState.Minimized) existing.WindowState = WindowState.Normal;
                existing.Activate();
                return;
            }
           

            // Replace this factory with a switch on e.Feature.Id to open your own feature windows.
            var window = CreateFeatureWindow(e.Feature);
            window.Owner = this;
            featureWindows.Add(e.Feature.Id, window);
            window.Closed += (s, args) => featureWindows.Remove(e.Feature.Id);
            window.Show();
        }

        private static Window CreateFeatureWindow(LauncherFeature feature)
        {
            var window = new Window
            {
                Title = feature.Title + " · 昼夜之间", Width = 560, Height = 400,
                MinWidth = 380, MinHeight = 280, WindowStartupLocation = WindowStartupLocation.CenterOwner,
                FontFamily = new FontFamily("Microsoft YaHei UI, Segoe UI")
            };
            window.SetResourceReference(BackgroundProperty, "SurfaceBrush");
            window.SetResourceReference(ForegroundProperty, "ForegroundBrush");
            var content = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(36) };
            content.Children.Add(new TextBlock { Text = feature.Glyph, FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 32, Foreground = feature.Tint, HorizontalAlignment = HorizontalAlignment.Center });
            content.Children.Add(new TextBlock { Text = feature.Title, FontSize = 25, FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 22, 0, 10) });
            content.Children.Add(new TextBlock { Text = "功能窗体预留", FontSize = 13, HorizontalAlignment = HorizontalAlignment.Center });
            var hint = new TextBlock { Text = "这里将放置「" + feature.Title + "」的内容。", FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 12, 0, 0) };
            hint.SetResourceReference(TextBlock.ForegroundProperty, "SecondaryBrush");
            content.Children.Add(hint);
            window.Content = content;
            return window;
        }

        private void OnViewModelChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.IsNight)) ApplyTheme(true);
            if (e.PropertyName == nameof(MainViewModel.IsSettingsOpen))
            {
                MainContent.IsEnabled = !viewModel.IsSettingsOpen;
                if (viewModel.IsSettingsOpen)
                {
                    previousFocus = Keyboard.FocusedElement;
                    DrawerTranslation.BeginAnimation(TranslateTransform.XProperty,
                        new DoubleAnimation(35, 0, AnimationDuration(240)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
                    Dispatcher.BeginInvoke(new Action(() => SettingsDrawer.MoveFocus(new TraversalRequest(FocusNavigationDirection.First))));
                }
                else if (previousFocus != null) Keyboard.Focus(previousFocus);
            }
        }

        private void ApplyTheme(bool animate)
        {
            var dictionaries = Application.Current.Resources.MergedDictionaries;
            var theme = new ResourceDictionary { Source = new Uri("Themes/" + (viewModel.IsNight ? "Night" : "Day") + ".xaml", UriKind.Relative) };
            if (dictionaries.Count > 0) dictionaries[0] = theme; else dictionaries.Add(theme);
            NightLayer.BeginAnimation(OpacityProperty, new DoubleAnimation(viewModel.IsNight ? 1 : 0, AnimationDuration(animate ? 620 : 0))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut } });
        }

        private static Duration AnimationDuration(int milliseconds)
        { return new Duration(TimeSpan.FromMilliseconds(SystemParameters.ClientAreaAnimation ? milliseconds : 0)); }

        private void FlipPanel_Click(object sender, RoutedEventArgs e)
        {
            if (flipPanelWindow == null)
            {
                flipPanelWindow = new FlipPanelWindow { Owner = this };
                flipPanelWindow.Closed += (s, args) => flipPanelWindow = null;
                flipPanelWindow.Show();
            }
            else
            {
                if (flipPanelWindow.WindowState == WindowState.Minimized) flipPanelWindow.WindowState = WindowState.Normal;
                flipPanelWindow.Activate();
            }
        }

        private void SelfUserControl_Click(object sender, RoutedEventArgs e)
        {
            SelfUserControlFrm selfUserControlFrm=new SelfUserControlFrm();
            selfUserControlFrm.Show();
        }

        private void dataSource_Click(object sender, RoutedEventArgs e)
        {

            if (studyBingdingFrm==null)
            {
                studyBingdingFrm=new StudyBingdingFrm { Owner = this };
                studyBingdingFrm.Closed += (s, args) => studyBingdingFrm = null;
                studyBingdingFrm.Show();
            }
            else
            {
                if (studyBingdingFrm.WindowState == WindowState.Minimized) studyBingdingFrm.WindowState = WindowState.Normal;
                studyBingdingFrm.Activate();
            }
            
        }
    }
}

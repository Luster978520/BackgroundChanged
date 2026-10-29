using System.Windows;

namespace BackgroundChanged.View
{
    public partial class FlipPanelWindow : Window
    {
        public FlipPanelWindow() { InitializeComponent(); }
        private void ShowFront_Click(object sender, RoutedEventArgs e) { DemoPanel.SetCurrentValue(WpfCustomControlLibrary1.FlipPanel.IsFlippedProperty, false); }
        private void ShowBack_Click(object sender, RoutedEventArgs e) { DemoPanel.SetCurrentValue(WpfCustomControlLibrary1.FlipPanel.IsFlippedProperty, true); }
    }
}

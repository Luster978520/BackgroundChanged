using DelegateEventDemo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace BackgroundChanged.View
{
    /// <summary>
    /// SelfUserControlFrm.xaml 的交互逻辑
    /// </summary>
    public partial class SelfUserControlFrm : Window
    {
        private readonly Device _device = new Device();
        public SelfUserControlFrm()
        {
            InitializeComponent();// 给委托字段赋值
            _device.DelegateNotice = ShowDelegateMessage;

            // 订阅事件
            _device.EventNotice += ShowEventMessage;

        }

        // 符合 MessageHandler 格式的方法
        private void ShowDelegateMessage(string message)
        {
            MessageBox.Show(message, "委托");
        }

        // 同样符合 MessageHandler 格式
        private void ShowEventMessage(string message)
        {
            MessageBox.Show(message, "事件");
        }

        private void CallDelegate_Click(object sender, RoutedEventArgs e)
        {
            // MainWindow 是 Device 的外部。
            // 公开的委托字段，可以在这里直接调用。
            _device.DelegateNotice?.Invoke("窗口直接调用了委托！");
        }

        private void StartDevice_Click(object sender, RoutedEventArgs e)
        {
            // 外部不能这样直接调用事件：
            // _device.EventNotice?.Invoke("窗口直接引发事件");
            // 取消上面这行注释会编译报错。

            // 可以调用公开方法，由 Device 内部引发事件。
            _device.Start();
        }

        private void Unsubscribe_Click(object sender, RoutedEventArgs e)
        {
            _device.EventNotice -= ShowEventMessage;

            MessageBox.Show("已取消订阅，再点击按钮 2 将不弹出事件消息。");
        }

        private void Subscribe_Click(
            object sender, RoutedEventArgs e)
        {
            // 先移除再添加，避免反复点击造成重复订阅
            _device.EventNotice -= ShowEventMessage;
            _device.EventNotice += ShowEventMessage;

            MessageBox.Show("已订阅，再点击按钮 2 会收到事件消息。");
        }

        private void ColorPickerUC_ColorChanged(object sender, RoutedPropertyChangedEventArgs<Color> e)
        {
            ucColorTestShow.Text = $"新颜色的值：{e.NewValue}";
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            ColorPickerUc.MyColor = Colors.BlueViolet;
        }
    }
}

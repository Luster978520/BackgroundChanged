using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace WpfCustomControlLibrary1
{
    /// <summary>
    /// 自定义控件
    /// </summary>
    public class ColorPicker:Control
    {
        public ColorPicker()
        {
            //InitializeComponent();
            //使用资源文件定义默认外观
           
            SetUpCommands();
        }
        static ColorPicker() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ColorPicker),
                new FrameworkPropertyMetadata(typeof(ColorPicker)));
        }

        public Color MyColor
        {
            get { return (Color)GetValue(MyColorProperty); }
            set { SetValue(MyColorProperty, value); }

        }

        // Using a DependencyProperty as the backing store for MyColor.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty MyColorProperty =
            DependencyProperty.Register("MyColor", typeof(Color), typeof(ColorPicker), new PropertyMetadata(Colors.Red, OnColorChangedCallback));

        //Color属性发生变化了，红绿蓝都要发生变化
        private static void OnColorChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ColorPicker colorPickerUC)
            {
                Color newColor = (Color)e.NewValue;
                colorPickerUC.Red = newColor.R;
                colorPickerUC.Blue = newColor.B;
                colorPickerUC.Green = newColor.G;


                //触发路由事件
                Color oldColor = (Color)e.OldValue;
                RoutedPropertyChangedEventArgs<Color> args = new RoutedPropertyChangedEventArgs<Color>(oldColor, newColor);
                args.RoutedEvent = ColorChangedEvent;
                colorPickerUC.RaiseEvent(args);


                colorPickerUC.previousColor = oldColor;
            }
        }

        public byte Red
        {
            get { return (byte)GetValue(RedProperty); }
            set { SetValue(RedProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Red.  This enables animation, styling, binding, etc...
        private static readonly DependencyProperty RedProperty =
            DependencyProperty.Register("Red", typeof(byte), typeof(ColorPicker), new PropertyMetadata(byte.Parse("255"), OnRgbChangedCallback));


        //红绿蓝发生改变，MyColor也要发生改变
        private static void OnRgbChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ColorPicker colorPickerUC)
            {
                byte newbyte = (byte)e.NewValue;
                Color newMyColor = colorPickerUC.MyColor;
                if (e.Property == RedProperty)
                {
                    newMyColor.R = newbyte;
                }
                if (e.Property == GreenProperty)
                {
                    newMyColor.G = newbyte;
                }
                if (e.Property == BlueProperty)
                {
                    newMyColor.B = newbyte;
                }
                // 将修改后的颜色写回依赖属性
                colorPickerUC.SetCurrentValue(MyColorProperty, newMyColor);
            }


        }

        public byte Green
        {
            get { return (byte)GetValue(GreenProperty); }
            set { SetValue(GreenProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Red.  This enables animation, styling, binding, etc...
        private static readonly DependencyProperty GreenProperty =
            DependencyProperty.Register("Green", typeof(byte), typeof(ColorPicker), new PropertyMetadata(byte.Parse("0"), OnRgbChangedCallback));



        public byte Blue
        {
            get { return (byte)GetValue(BlueProperty); }
            set { SetValue(BlueProperty, value); }
        }

        // Using a DependencyProperty as the backing store for Red.  This enables animation, styling, binding, etc...
        private static readonly DependencyProperty BlueProperty =
            DependencyProperty.Register("Blue", typeof(byte), typeof(ColorPicker), new PropertyMetadata(byte.Parse("0"), OnRgbChangedCallback));



        //注册事件  
        public static readonly RoutedEvent ColorChangedEvent = EventManager.RegisterRoutedEvent(
            "ColorChanged",
            RoutingStrategy.Bubble,
            typeof(RoutedPropertyChangedEventHandler<Color>),
            typeof(ColorPicker)
            );


        //公开事件  evet 委托类型 事件名
        public event RoutedPropertyChangedEventHandler<Color> ColorChanged
        {
            add { AddHandler(ColorChangedEvent, value); }
            remove
            {
                RemoveHandler(ColorChangedEvent, value);
            }

        }



        #region  实现undo命令

        public Color? previousColor;
        //1、命令绑定(构造函数的时候绑定）
        private void SetUpCommands()
        {
            //定义命令绑定
            CommandBinding commandBinding = new CommandBinding(ApplicationCommands.Undo, UndoCommand_Executed, UndoCommand_CanExecuted);
            CommandBindings.Add(commandBinding);
        }

        //2、命令是否能执行（什么时候执行）
        private void UndoCommand_CanExecuted(object sender, CanExecuteRoutedEventArgs e)
        {
            e.CanExecute = previousColor != null;
            //e.CanExecute=previousColor.HasValue;
        }

        //3、如何执行
        private void UndoCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            this.MyColor = (Color)previousColor;
        }



        #endregion
    }
}

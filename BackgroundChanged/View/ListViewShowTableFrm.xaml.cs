using BackgroundChanged.Models;
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
    /// ListViewShowTableFrm.xaml 的交互逻辑
    /// </summary>
    public partial class ListViewShowTableFrm : Window
    {
        public ListViewShowTableFrm()
        {
            InitializeComponent();
            ClassStuInfo classStuInfo = new ClassStuInfo();
            classStuInfo.ClassName = "某大学附附属中学";
            classStuInfo.StuList = new List<StuInfo> {
               new StuInfo(){ Age=13,Description="张三",Name="张三"},
               new StuInfo(){ Age=13,Description="张三",Name="李四"},
               new StuInfo(){ Age=13,Description="张三",Name="王五"},
               new StuInfo(){ Age=13,Description="张三",Name="郭六"}
               };

            this.DataContext = classStuInfo;//指定数据上下文
        }
    }
}

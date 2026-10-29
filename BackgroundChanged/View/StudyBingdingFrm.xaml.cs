using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
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
using BackgroundChanged.Models;


namespace BackgroundChanged.View
{
    /// <summary>
    /// StudyBingdingFrm.xaml 的交互逻辑
    /// </summary>
    public partial class StudyBingdingFrm : Window
    {
        public StudyBingdingFrm()
        {
            InitializeComponent();
            string connStr = "Server=.;Database=Ruster;User Id=sa;Password=123456;TrustServerCertificate=True;";
            using (SqlConnection sql=new SqlConnection(connStr))
            {
                string sqlStr = "select * from student";
                SqlCommand sqlCommand =new SqlCommand(sqlStr,sql);
                SqlDataAdapter adapter = new SqlDataAdapter(sqlCommand);
                DataTable dt=new DataTable();
                adapter.Fill(dt);  //填充再dt表格中
                StuListView.ItemsSource = dt.DefaultView;
              

          
                
            }
        
        }

        private ListViewShowTableFrm listViewShowTableFrm;
        private void Button_Click(object sender, RoutedEventArgs e)
        {

            if (listViewShowTableFrm == null)
            {
                listViewShowTableFrm=new ListViewShowTableFrm() { Owner = this };
                listViewShowTableFrm.Closed += (o, Args) =>
                {
                    listViewShowTableFrm = null;
                };
                listViewShowTableFrm.Show();

            }
            else
            {

                if (listViewShowTableFrm.WindowState == WindowState.Minimized) listViewShowTableFrm.WindowState = WindowState.Normal;
                listViewShowTableFrm.Activate();
            }
        }
    }


    
}

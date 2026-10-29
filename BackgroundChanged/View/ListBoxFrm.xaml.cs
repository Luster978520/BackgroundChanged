using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace BackgroundChanged.View
{
    public partial class ListBoxFrm : Window
    {
        private string connectionString;
        private bool identity;
        private object selectedId;
        public ListBoxFrm() { InitializeComponent(); }
        private async Task Run(Func<Task> operation)
        {
            IsEnabled = false;
            try { await operation(); }
            catch (Exception ex)
            {
                StatusText.Text = "操作失败：" + ex.Message;
                MessageBox.Show(this, ex.Message, "提示", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { IsEnabled = true; }
        }
        private void RequireConnection()
        {
            if (connectionString == null) throw new InvalidOperationException("请先点击“连接 / 刷新”。");
        }
        private async void Connect_Click(object sender, RoutedEventArgs e)
        {
            await Run(async () =>
            {
                connectionString = null;
                StudentGrid.ItemsSource = null;
                Clear();
                var builder = new SqlConnectionStringBuilder {
                    DataSource = ServerBox.Text.Trim(), InitialCatalog = "Ruster",
                    IntegratedSecurity = WindowsAuth.IsChecked == true, ConnectTimeout = 8,
                    TrustServerCertificate = true
                };
                if (!builder.IntegratedSecurity) { builder.UserID = UserBox.Text.Trim(); builder.Password = PasswordBox.Password; }
                using (var connection = new SqlConnection(builder.ConnectionString))
                {
                    await connection.OpenAsync();
                    using (var command = new SqlCommand("SELECT COLUMNPROPERTY(OBJECT_ID(N'dbo.student'),N'StudentID','IsIdentity')", connection))
                    {
                        var result = await command.ExecuteScalarAsync();
                        if (result == null || result == DBNull.Value) throw new InvalidOperationException("未找到 dbo.student.StudentID，请检查表结构或权限。");
                        identity = Convert.ToInt32(result) == 1;
                    }
                }
                connectionString = builder.ConnectionString;
                HintText.Text = identity ? "StudentID 为自增编号，新增时自动生成。Sex 保留数据库的 0/1 编码。" : "新增请填写唯一 StudentID；选中记录时不可修改编号。Sex 保留 0/1 编码。";
                await Query();
            });
        }
        private async Task Query()
        {
            RequireConnection();
            using (var connection = new SqlConnection(connectionString))
            using (var command = new SqlCommand("SELECT StudentID,Age,Sex,Birthday,StudentName FROM dbo.student WHERE @name=N'' OR CHARINDEX(@name,StudentName)>0 ORDER BY StudentID", connection))
            {
                command.Parameters.Add("@name", SqlDbType.NVarChar, -1).Value = SearchBox.Text.Trim();
                await connection.OpenAsync();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    var table = new DataTable();
                    table.Load(reader);
                    StudentGrid.ItemsSource = table.DefaultView;
                    StatusText.Text = "查询完成，共 " + table.Rows.Count + " 条记录。";
                }
            }
            Clear();
        }
        private async void Query_Click(object sender, RoutedEventArgs e) { await Run(Query); }
        private void Clear()
        {
            selectedId = null; StudentGrid.SelectedItem = null;
            IdBox.Text = NameBox.Text = AgeBox.Text = SexBox.Text = "";
            IdBox.IsReadOnly = identity; BirthdayBox.SelectedDate = null;
        }
        private void Clear_Click(object sender, RoutedEventArgs e) { Clear(); }
        private void SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var row = StudentGrid.SelectedItem as DataRowView;
            if (row == null) return;
            selectedId = row["StudentID"];
            IdBox.Text = Convert.ToString(selectedId); IdBox.IsReadOnly = true;
            NameBox.Text = Convert.ToString(row["StudentName"]);
            AgeBox.Text = Convert.ToString(row["Age"]);
            SexBox.Text = Convert.ToString(row["Sex"]);
            BirthdayBox.SelectedDate = row["Birthday"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(row["Birthday"]);
        }
        private async Task Save(bool add)
        {
            RequireConnection();
            if (!add && selectedId == null) throw new InvalidOperationException("请先选中要修改的学生。");
            int age, sex, id = 0;
            if (string.IsNullOrWhiteSpace(NameBox.Text)) throw new InvalidOperationException("姓名不能为空。");
            if (!int.TryParse(AgeBox.Text, out age) || age < 0 || age > 150) throw new InvalidOperationException("年龄须为 0～150 的整数。");
            if (!int.TryParse(SexBox.Text, out sex) || (sex != 0 && sex != 1)) throw new InvalidOperationException("Sex 须为 0 或 1。");
            if (!BirthdayBox.SelectedDate.HasValue) throw new InvalidOperationException("请选择生日。");
            if (add && !identity && !int.TryParse(IdBox.Text, out id)) throw new InvalidOperationException("StudentID 须为整数。");
            string sql = add
                ? (identity ? "INSERT INTO dbo.student(Age,Sex,Birthday,StudentName) VALUES(@age,@sex,@birthday,@name)" : "INSERT INTO dbo.student(StudentID,Age,Sex,Birthday,StudentName) VALUES(@id,@age,@sex,@birthday,@name)")
                : "UPDATE dbo.student SET Age=@age,Sex=@sex,Birthday=@birthday,StudentName=@name WHERE StudentID=@id";
            await Write(sql, command =>
            {
                command.Parameters.Add("@id", SqlDbType.Int).Value = add ? (object)id : selectedId;
                command.Parameters.Add("@age", SqlDbType.Int).Value = age;
                command.Parameters.Add("@sex", SqlDbType.Int).Value = sex;
                command.Parameters.Add("@birthday", SqlDbType.DateTime).Value = BirthdayBox.SelectedDate.Value.Date;
                command.Parameters.Add("@name", SqlDbType.NVarChar, -1).Value = NameBox.Text.Trim();
            });
        }
        private async Task Write(string sql, Action<SqlCommand> parameters)
        {
            RequireConnection();
            using (var connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
                using (var transaction = connection.BeginTransaction())
                using (var command = new SqlCommand(sql, connection, transaction))
                {
                    parameters(command);
                    int affected = await command.ExecuteNonQueryAsync();
                    if (affected != 1) { transaction.Rollback(); throw new InvalidOperationException("影响行数不是 1，已回滚。请刷新并检查 StudentID 唯一性。"); }
                    transaction.Commit();
                }
            }
            try { await Query(); StatusText.Text += " 操作成功。"; }
            catch (Exception ex) { StatusText.Text = "数据库写入成功，但刷新失败，请重新查询：" + ex.Message; }
        }
        private async void Add_Click(object sender, RoutedEventArgs e) { await Run(() => Save(true)); }
        private async void Update_Click(object sender, RoutedEventArgs e) { await Run(() => Save(false)); }
        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (selectedId == null) { MessageBox.Show(this, "请先选择要删除的学生。"); return; }
            var id = selectedId;
            if (MessageBox.Show(this, "确定删除编号 " + id + " 的学生？", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            await Run(() => Write("DELETE FROM dbo.student WHERE StudentID=@id", command => command.Parameters.Add("@id", SqlDbType.Int).Value = id));
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DelegateEventDemo
{
    // 定义委托类型：返回 void，接收一个 string 参数
    public delegate void MessageHandler(string message);

    public class Device
    {
        // 公开的委托字段
        public MessageHandler DelegateNotice;

        // 公开的事件：使用相同的委托类型
        public event MessageHandler EventNotice;

        public void Start()
        {
            // 这里属于 Device 类内部，可以引发事件
            EventNotice?.Invoke("事件通知：设备已启动！");
        }
    }
}
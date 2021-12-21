using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace MemorySwapper
{
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string name = Process.GetCurrentProcess().ProcessName;
            if (Process.GetProcessesByName(name).Length > 1)
            {
                MessageBox.Show("已經有其他程式實例正在執行", "資訊", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            for (int i = 0; i < args.Length; ++i)
            {
                args[i] = args[i].ToLower();
            }
            bool clipboard = args.Contains("--clipboard");
            bool fileSystem = args.Contains("--filesystem");
            bool fill = args.Contains("--fill");
            bool skip = args.Contains("--skip");

            if (!skip)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("請問是否進行記憶體空間交換 ?").AppendLine().AppendLine("套用交換功能 :").Append("淨空工作區");
                if (clipboard)
                {
                    sb.AppendLine().Append("清除剪貼簿");
                }
                if (fileSystem)
                {
                    sb.AppendLine().Append("清除檔案系統緩存");
                }
                if (fill)
                {
                    sb.AppendLine().Append("填滿記憶體後清除");
                }
                if (MessageBox.Show(sb.ToString(), "詢問", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                {
                    MessageBox.Show("使用者終止操作", "資訊", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
            }

            MemoryUtil.emptyWorkSet();
            if (clipboard)
            {
                MemoryUtil.clearClipboard();
            }

            if (fileSystem)
            {
                MemoryUtil.clearFileSystemCache();
            }

            if (fill)
            {
                MemoryUtil.fillRAM();
            }
            MessageBox.Show("記憶體空間交換完成", "資訊", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
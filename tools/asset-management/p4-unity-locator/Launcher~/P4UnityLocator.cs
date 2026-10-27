using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Reflection;

[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
[assembly: AssemblyProduct("P4 Unity Locator")]

internal static class P4UnityLocator
{
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr handle);
    [DllImport("user32.dll")] private static extern bool ShowWindowAsync(IntPtr handle, int command);

    [STAThread]
    private static int Main(string[] args)
    {
        bool quiet = args.Length > 0 && args[0] == "--quiet";
        try
        {
            int start = quiet ? 1 : 0;
            if (args.Length <= start) throw new Exception("请从 P4V 选择资源后运行此工具。");
            for (int i = start; i < args.Length; i++) Locate(args[i]);
            return 0;
        }
        catch (Exception e)
        {
            if (!quiet) MessageBox.Show(e.Message, "P4 → Unity 资源定位", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 1;
        }
    }

    private static void Locate(string input)
    {
        string full = Path.GetFullPath(input);
        if (!File.Exists(full) && !Directory.Exists(full)) throw new Exception("本地文件不存在，请确认该文件已同步到当前工作区：\n" + full);
        DirectoryInfo parent = new DirectoryInfo(Path.GetDirectoryName(full));
        string root = null;
        while (parent != null)
        {
            if (Directory.Exists(Path.Combine(parent.FullName, "Assets")) && File.Exists(Path.Combine(parent.FullName, "ProjectSettings", "ProjectVersion.txt")))
            { root = parent.FullName; break; }
            parent = parent.Parent;
        }
        if (root == null || !full.StartsWith(Path.Combine(root, "Assets") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new Exception("请选择 Unity 项目 Assets 目录内的本地资源。\n" + full);
        string inbox = Path.Combine(root, "Library", "P4UnityLocator");
        string heartbeat = Path.Combine(inbox, "editor.pid");
        if (!File.Exists(heartbeat) || DateTime.UtcNow - File.GetLastWriteTimeUtc(heartbeat) > TimeSpan.FromSeconds(120))
            throw new Exception("对应 Unity 项目尚未打开、脚本正在编译或定位服务未加载。\n请打开项目并等待编译完成，再重试。\n" + root);
        Process editor;
        try { editor = Process.GetProcessById(int.Parse(File.ReadAllText(heartbeat).Trim())); }
        catch { throw new Exception("对应 Unity 编辑器已关闭，请打开项目后重试。"); }
        if (!editor.ProcessName.Equals("Unity", StringComparison.OrdinalIgnoreCase)) throw new Exception("Unity 连接已失效，请重新打开项目。");
        IntPtr handle = editor.MainWindowHandle;
        if (handle != IntPtr.Zero) { ShowWindowAsync(handle, 9); SetForegroundWindow(handle); }
        string request = Path.Combine(inbox, Guid.NewGuid().ToString("N") + ".request");
        string temporary = request + ".tmp";
        File.WriteAllText(temporary, full, new UTF8Encoding(false));
        File.Move(temporary, request);
        string response = request + ".result";
        Stopwatch timer = Stopwatch.StartNew();
        while (timer.Elapsed.TotalSeconds < 30)
        {
            if (File.Exists(response))
            {
                string result = File.ReadAllText(response, Encoding.UTF8);
                File.Delete(response);
                if (!result.StartsWith("OK\n", StringComparison.Ordinal)) throw new Exception(result.Replace("ERROR\n", ""));
                return;
            }
            Thread.Sleep(100);
        }
        if (File.Exists(request)) File.Delete(request);
        throw new Exception("Unity 在 30 秒内未响应。请关闭模态窗口、等待编译完成后重试。\n" + full);
    }
}

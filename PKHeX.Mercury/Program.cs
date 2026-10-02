using System.Windows.Forms;

namespace PKHeX.Mercury;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => ReportFatal(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => ReportFatal(e.ExceptionObject as Exception);

        try
        {
            AppState.Initialize();
            Application.Run(new MainForm());
        }
        catch (Exception ex)
        {
            ReportFatal(ex);
        }
    }

    private static void ReportFatal(Exception? ex)
    {
        var text = ex?.ToString() ?? "未知错误";
        try
        {
            MessageBox.Show(
                "程序遇到未处理的错误：\r\n\r\n" + text,
                "PKHeX Mercury / 水银版 - 错误",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        catch
        {
            // 连消息框都无法显示时放弃，避免递归崩溃。
        }
    }
}

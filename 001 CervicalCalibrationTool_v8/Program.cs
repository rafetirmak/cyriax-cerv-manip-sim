using System.Text;

namespace CervicalCalibrationTool;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        try
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new Form1());
        }
        catch (Exception ex)
        {
            string logPath = WriteStartupLog(ex);
            MessageBox.Show(
                $"The application could not start.\r\n\r\n{ex.Message}" +
                $"\r\n\r\nDiagnostic log: {logPath}",
                "Startup error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private static string WriteStartupLog(Exception ex)
    {
        string contents =
            $"Timestamp: {DateTimeOffset.Now:O}{Environment.NewLine}" +
            $"OS: {Environment.OSVersion}{Environment.NewLine}" +
            $"64-bit process: {Environment.Is64BitProcess}{Environment.NewLine}" +
            $".NET: {Environment.Version}{Environment.NewLine}{Environment.NewLine}" +
            ex;

        string[] candidatePaths =
        {
            Path.Combine(AppContext.BaseDirectory, "startup_error.log"),
            Path.Combine(Path.GetTempPath(), "CervicalCalibrationTool_startup_error.log")
        };

        foreach (string path in candidatePaths)
        {
            try
            {
                File.WriteAllText(path, contents, new UTF8Encoding(false));
                return path;
            }
            catch
            {
                // Try the next writable location.
            }
        }

        return "The diagnostic log could not be written.";
    }
}

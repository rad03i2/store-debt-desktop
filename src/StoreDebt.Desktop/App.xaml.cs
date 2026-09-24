using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;

namespace StoreDebt.Desktop;

public partial class App : Application
{
    private string _logPath = string.Empty;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _logPath = CreateLogPath();

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                WriteCrash("AppDomain.UnhandledException", ex);
        };

        DispatcherUnhandledException += OnDispatcherUnhandledException;

        try
        {
            WriteLine("=== StoreDebt.Desktop startup ===");
            WriteLine($"UTC: {DateTime.UtcNow:O}");
            WriteLine($"OS: {Environment.OSVersion}");
            WriteLine($"64-bit OS: {Environment.Is64BitOperatingSystem}");
            WriteLine($"Process architecture: {RuntimeInformation.ProcessArchitecture}");
            WriteLine($".NET: {RuntimeInformation.FrameworkDescription}");
            WriteLine($"Base directory: {AppContext.BaseDirectory}");

            var window = new MainWindow();
            MainWindow = window;
            window.Show();

            WriteLine("MainWindow shown successfully.");
        }
        catch (Exception ex)
        {
            WriteCrash("Startup", ex);
            ShowStartupError(ex);
            Shutdown(-1);
        }
    }

    private void OnDispatcherUnhandledException(
        object sender,
        DispatcherUnhandledExceptionEventArgs e)
    {
        WriteCrash("DispatcherUnhandledException", e.Exception);

        MessageBox.Show(
            $"حدث خطأ غير متوقع داخل البرنامج.\n\nتم حفظ التفاصيل هنا:\n{_logPath}",
            "خطأ في دفتر المحل",
            MessageBoxButton.OK,
            MessageBoxImage.Error,
            MessageBoxResult.OK,
            MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);

        e.Handled = true;
    }

    private static string CreateLogPath()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StoreDebtDesktop",
            "Logs");
        Directory.CreateDirectory(folder);

        return Path.Combine(folder, "startup.log");
    }

    private void WriteLine(string message)
    {
        try
        {
            File.AppendAllText(
                _logPath,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch
        {
        }
    }

    private void WriteCrash(string stage, Exception ex)
    {
        WriteLine($"ERROR at {stage}");
        WriteLine(ex.ToString());
    }

    private void ShowStartupError(Exception ex)
    {
        MessageBox.Show(
            $"تعذر تشغيل برنامج دفتر المحل.\n\n{ex.Message}\n\nتم حفظ تفاصيل الخطأ هنا:\n{_logPath}",
            "تعذر تشغيل البرنامج",
            MessageBoxButton.OK,
            MessageBoxImage.Error,
            MessageBoxResult.OK,
            MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
    }
}

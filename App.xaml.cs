using System;
using System.IO;
using System.Windows;

namespace LaserWelderApp;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private static readonly string LogFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.log");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Log("=== Application Starting ===");

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            Log($"[FATAL] AppDomain Unhandled: {args.ExceptionObject}");
        };

        DispatcherUnhandledException += (s, args) =>
        {
            Log($"[FATAL] Dispatcher Unhandled: {args.Exception}");
            args.Handled = true;
            MessageBox.Show($"오류 발생:\n{args.Exception.Message}\n\n상세 정보:\n{args.Exception.StackTrace}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        };
    }

    public static void Log(string message)
    {
        try
        {
            File.AppendAllText(LogFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}");
        }
        catch { }
    }
}

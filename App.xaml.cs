using System.Windows;
using System.Windows.Threading;
using DCUOTracker.Services;

namespace DCUOTracker;

public partial class App : System.Windows.Application
{
    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandled;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandled;
        TaskScheduler.UnobservedTaskException += OnUnobservedTask;
        Startup += OnStartup;
    }

    private async void OnStartup(object sender, System.Windows.StartupEventArgs e)
    {
        // Background update check — non-blocking
        _ = Task.Run(async () =>
        {
            await Task.Delay(3000); // wait for app to fully load
            try
            {
                var update = await DCUOTracker.Services.AutoUpdater.CheckForUpdateAsync();
                if (update == null) return;

                Dispatcher.Invoke(() =>
                {
                    var result = System.Windows.MessageBox.Show(
                        $"Update available: v{update.Version}\n\n{update.ReleaseNotes.Split('\n').FirstOrDefault()}\n\nDownload and install now?",
                        "DCUO Assistant — Update Available",
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Information);

                    if (result == System.Windows.MessageBoxResult.Yes)
                        _ = DCUOTracker.Services.AutoUpdater.DownloadAndInstallAsync(update);
                });
            }
            catch (Exception ex)
            {
                DCUOTracker.Services.Logger.Error("App.UpdateCheck", ex);
            }
        });
    }

    private void OnDispatcherUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // Log full exception chain including InnerException
        var ex = e.Exception;
        var sb = new System.Text.StringBuilder();
        while (ex != null)
        {
            sb.AppendLine($"[{ex.GetType().Name}] {ex.Message}");
            sb.AppendLine(ex.StackTrace);
            ex = ex.InnerException;
            if (ex != null) sb.AppendLine("--- Inner Exception ---");
        }
        Logger.Error("App.DispatcherUnhandled", e.Exception);
        Logger.Warn("App.DispatcherUnhandled.Chain", sb.ToString());

        System.Windows.MessageBox.Show(
            $"An unexpected error occurred:\n{e.Exception.InnerException?.Message ?? e.Exception.Message}\n\nDetails logged to AppData\\DCUOTracker\\app.log",
            "DCUO Quality of Life — Error",
            MessageBoxButton.OK, MessageBoxImage.Warning);
        e.Handled = true;
    }

    private static void OnDomainUnhandled(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            Logger.Error("App.DomainUnhandled", ex);
    }

    private static void OnUnobservedTask(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Logger.Error("App.UnobservedTask", e.Exception);
        e.SetObserved(); // prevent process crash from forgotten Task exceptions
    }
}

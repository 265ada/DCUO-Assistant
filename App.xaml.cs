using System.Windows;
using System.Windows.Threading;
using DCUOTracker.Services;

namespace DCUOTracker;

public partial class App : System.Windows.Application
{
    public App()
    {
        // I-2: global exception handlers — prevent raw crash dialogs
        DispatcherUnhandledException += OnDispatcherUnhandled;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandled;
        TaskScheduler.UnobservedTaskException += OnUnobservedTask;
    }

    private void OnDispatcherUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Logger.Error("App.DispatcherUnhandled", e.Exception);
        System.Windows.MessageBox.Show(
            $"An unexpected error occurred:\n{e.Exception.Message}\n\nDetails logged to AppData\\DCUOTracker\\app.log",
            "DCUO Quality of Life — Error",
            MessageBoxButton.OK, MessageBoxImage.Warning);
        e.Handled = true; // don't crash — keep running
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

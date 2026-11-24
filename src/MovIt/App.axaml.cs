using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using MovIt.Views;

namespace MovIt;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow();
            desktop.MainWindow = window;

            // Files/folders passed on the command line (or via "Open with") go straight into the queue.
            if (desktop.Args is { Length: > 0 } args)
                window.ViewModel.AddPaths(args);

            // Surface unexpected UI-thread errors in the status bar instead of crashing the app.
            Dispatcher.UIThread.UnhandledException += (_, e) =>
            {
                window.ViewModel.ReportError(e.Exception.Message);
                e.Handled = true;
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}

using Avalonia;
using Avalonia.Markup.Xaml;
using SMUI.App.Services;
using SMUI.App.ViewModels;
using SMUI.App.Views;

namespace SMUI.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        AppServices.Initialize(() =>
            (ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow);

        var settings = AppServices.Settings;
        var dialogs = AppServices.Dialogs;
        var log = AppServices.Log;

        var queue = new QueuePageViewModel(settings, dialogs, log);
        var updates = new UpdatesPageViewModel(settings, dialogs, log);
        var mods = new ModsPageViewModel(settings, dialogs, log, queue, updates);
        var home = new HomeViewModel(settings, dialogs, log);
        var logVm = new LogViewModel();
        var settingsVm = new SettingsPageViewModel(settings, dialogs, log);
        var mainVm = new MainWindowViewModel(home, mods, queue, updates, logVm, settingsVm, log, dialogs);

        var window = new MainWindow { DataContext = mainVm };

        if (ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = window;
            desktop.ShutdownRequested += (_, _) => SaveOnExit();
            desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnMainWindowClose;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void SaveOnExit()
    {
        try
        {
            AppServices.SaveSettings();
        }
        catch
        {
            // 退出时保存尽力而为
        }
    }
}

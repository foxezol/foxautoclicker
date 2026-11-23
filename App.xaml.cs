using System.Windows;
using System.Windows.Threading;

namespace FoxAutoClicker;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var cfg = Config.Load();
        Loc.Set(cfg.Lang);
        Theme.Apply(cfg.Dark, cfg.Accent);

        var splash = new SplashWindow();
        splash.Show();
        splash.SetProgress(0.15);
        await Dispatcher.Yield(DispatcherPriority.Background);   // açılış ekranı çizilsin

        splash.SetProgress(0.35);
        await Dispatcher.Yield(DispatcherPriority.Background);

        var main = new MainWindow(cfg);                           // en ağır adım: pencere ve stiller
        splash.SetProgress(0.8);
        await Dispatcher.Yield(DispatcherPriority.Background);

        MainWindow = main;
        main.Show();                                              // kısayol kaydı ve ilk çizim
        main.Activate();
        splash.SetProgress(1);
        await Task.Delay(SystemParameters.ClientAreaAnimation ? 260 : 0);
        splash.FadeAndClose();
    }
}

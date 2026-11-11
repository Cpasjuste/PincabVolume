using System.Threading;
using System.Windows;
using PincabVolume.Views;

namespace PincabVolume;

public partial class App : Application
{
    public static ConfigService Store { get; private set; }
    public static AppConfig Config => Store.Config;
    public static InputService Input { get; private set; }
    public static VolumeManager Volume { get; private set; }
    public static OsdWindow Osd { get; private set; }
    public static bool IsExiting { get; private set; }

    Mutex _singleInstance;
    TableDetector _detector;
    TrayIcon _tray;
    MainWindow _main;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstance = new Mutex(true, "PincabVolume.SingleInstance", out bool isFirst);
        if (!isFirst)
        {
            Shutdown();
            return;
        }

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        Store = new ConfigService();
        Store.Load();

        Osd = new OsdWindow();
        Input = new InputService();
        Volume = new VolumeManager(Store, Input, Osd);
        Input.BindingTriggered += Volume.OnBinding;
        Input.Start();
        Volume.ApplyContext();

        _detector = new TableDetector();
        _detector.TableChanged += Volume.OnTableChanged;
        _detector.Start();

        _main = new MainWindow();
        _tray = new TrayIcon(ShowSettings, ExitApp);

        if (!Config.Configured || !Config.StartMinimized) ShowSettings();
    }

    void ShowSettings()
    {
        _main.Show();
        _main.WindowState = WindowState.Normal;
        _main.Activate();
    }

    void ExitApp()
    {
        IsExiting = true;
        _detector.Stop();
        Input.Dispose();
        _tray.Dispose();
        Store.SaveNow();
        Shutdown();
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        Store?.SaveNow();
        base.OnSessionEnding(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}

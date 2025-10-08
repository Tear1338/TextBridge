using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using System;
using System.Threading.Tasks;
using TextBridge.Core;

namespace TextBridge;

public class App : Application
{
    private Extension? ext;
    private MainWindow? mainwindow;
    private IClassicDesktopStyleApplicationLifetime? desktop;
    private bool isrunning = false;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime)
        {
            desktop = desktopLifetime;
            Task.Run(() => startextension());
        }
        base.OnFrameworkInitializationCompleted();
    }

    private async void startextension()
    {
        try
        {
            var cfg = Settings.load();
            ext = new Extension(cfg);

            ext.Activated += () =>
            {
                Dispatcher.UIThread.Invoke(() =>
                {
                    if (mainwindow == null)
                    {
                        mainwindow = new MainWindow(ext, cfg);
                        mainwindow.Closing += OnMainWindowClosing;
                        if (desktop != null)
                        {
                            desktop.MainWindow = mainwindow;
                        }
                    }
                    mainwindow.Show();
                    mainwindow.Activate();
                    if (ext != null)
                    {
                        ext.translationenabled = true;
                    }
                });
            };

            isrunning = true;
            ext.Run();
        }
        catch
        {
        }
        finally
        {
            isrunning = false;
            await Task.Delay(2000);
            desktop?.Shutdown();
        }
    }

    private void OnMainWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (e.CloseReason is not WindowCloseReason.WindowClosing)
            return;

        if (!isrunning)
        {
            desktop?.Shutdown();
        }
        else
        {
            e.Cancel = true;
            if (sender is Window window)
            {
                window.Hide();
                if (ext != null)
                {
                    ext.translationenabled = false;
                }
            }
        }
    }
}

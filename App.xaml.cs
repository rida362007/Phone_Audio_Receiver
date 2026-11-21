using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PhoneAudioReceiver;

public partial class App : Application
{
    static Mutex? mutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        mutex = new Mutex(true, "PhoneAudioReceiver.SingleInstance", out bool created);
        if (!created) { Shutdown(); return; }

        var s = Settings.Load();
        if (s.Language is not ("ar" or "en"))   // أول تشغيل: اسأل عن اللغة ثم احفظها
        {
            s.Language = AskLanguage();
            s.Save();
        }
        Loc.Lang = s.Language!;

        var w = new MainWindow(s);
        if (!e.Args.Contains("--tray")) w.Show();
    }

    static string AskLanguage()
    {
        string chosen = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar" ? "ar" : "en";
        var win = new Window
        {
            Title = "Language / اللغة", Width = 340, Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            ResizeMode = ResizeMode.NoResize,
            Background = new SolidColorBrush(Color.FromRgb(15, 23, 42))
        };
        var panel = new StackPanel { Margin = new Thickness(24), VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(new TextBlock
        {
            Text = "Choose language / اختر اللغة", Foreground = Brushes.White, FontSize = 16,
            Margin = new Thickness(0, 0, 0, 16), HorizontalAlignment = HorizontalAlignment.Center
        });
        var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        foreach (var (code, label) in new[] { ("ar", "العربية"), ("en", "English") })
        {
            var b = new Button { Content = label, Width = 120, Height = 38, Margin = new Thickness(6), FontSize = 14 };
            b.Click += (_, _) => { chosen = code; win.Close(); };
            row.Children.Add(b);
        }
        panel.Children.Add(row);
        win.Content = panel;
        win.ShowDialog();
        return chosen;
    }
}

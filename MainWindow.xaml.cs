using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Drawing = System.Drawing;
using WinForms = System.Windows.Forms;

namespace PhoneAudioReceiver;

public record DevVM(string Name, string Id, bool Connected)
{
    public Brush Dot => Connected ? Brushes.LimeGreen : Brushes.Gray;
}

public partial class MainWindow : Window
{
    readonly Settings s;
    readonly AudioService audio = new();
    readonly WinForms.NotifyIcon tray;
    readonly DispatcherTimer timer;
    bool exiting, busy, hintShown;

    public MainWindow(Settings settings)
    {
        s = settings;
        InitializeComponent();
        FlowDirection = Loc.IsRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        ApplyLoc();
        AutoCheck.IsChecked = s.Auto;
        StartCheck.IsChecked = s.StartWithWindows;

        tray = new WinForms.NotifyIcon { Icon = LoadIcon(), Visible = true, Text = Loc.Get("title") };
        tray.DoubleClick += (_, _) => ShowWindow();
        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add(Loc.Get("tray_open"), null, (_, _) => ShowWindow());
        menu.Items.Add(Loc.Get("tray_exit"), null, (_, _) => ExitApp());
        tray.ContextMenuStrip = menu;

        Application.Current.SessionEnding += (_, _) => exiting = true;

        timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
        timer.Tick += async (_, _) => await AutoTick();
        timer.Start();

        _ = StartupAsync();
    }

    async Task StartupAsync()
    {
        await RefreshAsync();
        await AutoTick();
    }

    void ApplyLoc()
    {
        Title = Loc.Get("title");
        TitleText.Text = Loc.Get("title");
        SubText.Text = Loc.Get("subtitle");
        DevicesLabel.Text = Loc.Get("devices");
        ConnectBtn.Content = Loc.Get("connect");
        DisconnectBtn.Content = Loc.Get("disconnect");
        RefreshBtn.Content = Loc.Get("refresh");
        AutoCheck.Content = Loc.Get("auto");
        StartCheck.Content = Loc.Get("startup");
        EmptyText.Text = Loc.Get("nodevices");
        HintText.Text = Loc.Get("hint");
        StatusText.Text = Loc.Get("ready");
    }

    static Drawing.Icon LoadIcon()
    {
        try
        {
            var info = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/icon.ico"));
            if (info != null) { using var st = info.Stream; return new Drawing.Icon(st); }
        }
        catch { }
        return MakeIcon();
    }

    static Drawing.Icon MakeIcon()
    {
        using var bmp = new Drawing.Bitmap(32, 32);
        using (var g = Drawing.Graphics.FromImage(bmp))
        {
            g.SmoothingMode = Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.FillEllipse(new Drawing.SolidBrush(Drawing.Color.FromArgb(37, 99, 235)), 1, 1, 30, 30);
            g.FillPolygon(Drawing.Brushes.White, new Drawing.Point[]
                { new(8, 13), new(13, 13), new(19, 8), new(19, 24), new(13, 19), new(8, 19) });
        }
        return Drawing.Icon.FromHandle(bmp.GetHicon());
    }

    void SetStatus(string text)
    {
        StatusText.Text = text;
        tray.Text = text.Length > 60 ? text[..60] : text;
    }

    void Notify(string msg) => tray.ShowBalloonTip(3000, Loc.Get("title"), msg, WinForms.ToolTipIcon.Info);

    void ShowWindow() { Show(); WindowState = WindowState.Normal; Activate(); }

    void ExitApp()
    {
        exiting = true;
        timer.Stop();
        audio.DisconnectAll();
        tray.Visible = false;
        tray.Dispose();
        Application.Current.Shutdown();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!exiting)
        {
            e.Cancel = true;
            Hide();
            if (!hintShown) { hintShown = true; Notify(Loc.Get("bg")); }
        }
        base.OnClosing(e);
    }

    // ---------- الأجهزة ----------
    async Task RefreshAsync()
    {
        try
        {
            var devs = await audio.ListAsync();
            var keep = (DeviceList.SelectedItem as DevVM)?.Name ?? s.Device;
            DeviceList.ItemsSource = devs.Select(d => new DevVM(d.Name, d.Id, audio.IsConnected(d.Id))).ToList();
            foreach (DevVM v in DeviceList.Items) if (v.Name == keep) DeviceList.SelectedItem = v;
            EmptyText.Visibility = devs.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            UpdateButtons();
        }
        catch (Exception ex) { SetStatus(ex.Message); }
    }

    void UpdateButtons()
    {
        var v = DeviceList.SelectedItem as DevVM;
        ConnectBtn.IsEnabled = v != null && !v.Connected;
        DisconnectBtn.IsEnabled = v != null && v.Connected;
    }

    void DeviceList_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateButtons();
    async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

    async void Connect_Click(object sender, RoutedEventArgs e)
    {
        if (DeviceList.SelectedItem is not DevVM v) { SetStatus(Loc.Get("pickfirst")); return; }
        await DoConnect(v.Id, v.Name, false);
    }

    async Task<bool> DoConnect(string id, string name, bool quiet)
    {
        if (busy) return false;
        busy = true;
        bool ok = false;
        try
        {
            if (!quiet) SetStatus(Loc.Get("connecting"));
            ok = await audio.ConnectAsync(id);
            if (ok)
            {
                s.Device = name; s.Save();
                var msg = string.Format(Loc.Get("connected"), name);
                SetStatus(msg);
                Notify(msg);
            }
            else if (!quiet) SetStatus(Loc.Get("failed"));
        }
        catch (Exception ex) { if (!quiet) SetStatus(ex.Message); }
        finally { busy = false; }
        if (ok || !quiet) await RefreshAsync();
        return ok;
    }

    void Disconnect_Click(object sender, RoutedEventArgs e)
    {
        if (DeviceList.SelectedItem is not DevVM v) return;
        audio.Disconnect(v.Id);
        AutoCheck.IsChecked = false;   // القطع اليدوي يوقف الاتصال التلقائي
        SetStatus(Loc.Get("disconnected"));
        _ = RefreshAsync();
    }

    async Task AutoTick()
    {
        if (!s.Auto || string.IsNullOrEmpty(s.Device) || busy || audio.AnyConnected) return;
        try
        {
            var d = (await audio.ListAsync()).FirstOrDefault(x => x.Name == s.Device);
            if (d != null) await DoConnect(d.Id, d.Name, true);
        }
        catch { }
    }

    // ---------- الإعدادات ----------
    void Auto_Changed(object sender, RoutedEventArgs e)
    {
        s.Auto = AutoCheck.IsChecked == true;
        if (s.Auto && DeviceList.SelectedItem is DevVM v) s.Device = v.Name;
        s.Save();
    }

    void Start_Changed(object sender, RoutedEventArgs e)
    {
        s.StartWithWindows = StartCheck.IsChecked == true;
        s.Save();
        try
        {
            using var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", true);
            if (k == null) return;
            if (s.StartWithWindows) k.SetValue("PhoneAudioReceiver", $"\"{Environment.ProcessPath}\" --tray");
            else k.DeleteValue("PhoneAudioReceiver", false);
        }
        catch { }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Media.Audio;

namespace PhoneAudioReceiver;

public class Settings
{
    public string? Language { get; set; }
    public string? Device { get; set; }
    public bool Auto { get; set; }
    public bool StartWithWindows { get; set; }

    static string Dir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PhoneAudioReceiver");
    static string FilePath => Path.Combine(Dir, "settings.json");

    public static Settings Load()
    {
        try { return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new Settings(); }
        catch { return new Settings(); }
    }

    public void Save()
    {
        try { Directory.CreateDirectory(Dir); File.WriteAllText(FilePath, JsonSerializer.Serialize(this)); }
        catch { }
    }
}

public static class Loc
{
    public static string Lang = "en";
    public static bool IsRtl => Lang == "ar";

    static readonly Dictionary<string, (string ar, string en)> T = new()
    {
        ["title"] = ("صوت الهاتف على اللابتوب", "Phone Audio Receiver"),
        ["subtitle"] = ("حوّل لابتوبك إلى سماعة بلوتوث لهاتفك", "Turn your laptop into a Bluetooth speaker for your phone"),
        ["devices"] = ("هواتفك المقترنة", "Your paired phones"),
        ["refresh"] = ("تحديث", "Refresh"),
        ["connect"] = ("اتصال", "Connect"),
        ["disconnect"] = ("قطع الاتصال", "Disconnect"),
        ["auto"] = ("اتصال تلقائي وإعادة الاتصال", "Auto-connect & reconnect"),
        ["startup"] = ("التشغيل مع ويندوز (في الخلفية)", "Start with Windows (in background)"),
        ["nodevices"] = ("لا توجد هواتف. اقترن بالهاتف أولاً من إعدادات بلوتوث ويندوز.", "No phones found. Pair your phone in Windows Bluetooth settings first."),
        ["pickfirst"] = ("اختر هاتفاً أولاً", "Select a phone first"),
        ["connecting"] = ("جارٍ الاتصال...", "Connecting..."),
        ["connected"] = ("متصل بـ {0} — شغّل أي صوت على الهاتف", "Connected to {0} — play any sound on your phone"),
        ["failed"] = ("فشل الاتصال. تأكد من البلوتوث على الهاتف وأعد المحاولة.", "Connection failed. Check Bluetooth on your phone and try again."),
        ["disconnected"] = ("تم قطع الاتصال (الاتصال التلقائي متوقف)", "Disconnected (auto-connect off)"),
        ["hint"] = ("بعد «اتصال»: على الهاتف اضغط اسم اللابتوب وفعّل «صوت الوسائط». إغلاق النافذة يُبقي التطبيق في الخلفية.",
                    "After Connect: on your phone tap this laptop's name and enable Media audio. Closing the window keeps the app in the background."),
        ["tray_open"] = ("فتح", "Open"),
        ["tray_exit"] = ("خروج", "Exit"),
        ["bg"] = ("التطبيق يعمل في الخلفية", "Running in the background"),
        ["ready"] = ("جاهز", "Ready"),
    };

    public static string Get(string k) => IsRtl ? T[k].ar : T[k].en;
}

public class AudioService
{
    public record Dev(string Name, string Id);
    readonly Dictionary<string, AudioPlaybackConnection> conns = new();

    public async Task<List<Dev>> ListAsync()
    {
        var infos = await DeviceInformation.FindAllAsync(AudioPlaybackConnection.GetDeviceSelector());
        return infos.Select(i => new Dev(i.Name, i.Id)).ToList();
    }

    public bool IsConnected(string id)
    {
        if (!conns.TryGetValue(id, out var c)) return false;
        if (c.State == AudioPlaybackConnectionState.Opened) return true;
        c.Dispose();
        conns.Remove(id);
        return false;
    }

    public bool AnyConnected => conns.Keys.ToList().Any(IsConnected);

    public async Task<bool> ConnectAsync(string id)
    {
        if (IsConnected(id)) return true;
        var c = AudioPlaybackConnection.TryCreateFromId(id);
        if (c == null) return false;
        await c.StartAsync();
        var r = await c.OpenAsync();
        if (r.Status == AudioPlaybackConnectionOpenResultStatus.Success) { conns[id] = c; return true; }
        c.Dispose();
        return false;
    }

    public void Disconnect(string id)
    {
        if (conns.Remove(id, out var c)) c.Dispose();
    }

    public void DisconnectAll()
    {
        foreach (var c in conns.Values) c.Dispose();
        conns.Clear();
    }
}

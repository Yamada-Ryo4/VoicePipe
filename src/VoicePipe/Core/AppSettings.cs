using System.IO;
using System.Text.Json;

namespace VoicePipe.Core;

public class AppSettings
{
    public string LastAppProcessName { get; set; } = "";
    public int    LastAppPid         { get; set; } = 0;
    public string LastMicDeviceId    { get; set; } = "";
    public float  AppGain            { get; set; } = 0.75f;
    public float  MicGain            { get; set; } = 1.0f;
    public bool   MinimizeToTray     { get; set; } = true;
    public bool   AutoStartPipeline  { get; set; } = false;
    public string Language           { get; set; } = "zh-CN";

    private static string GetFilePath()
    {
        string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VoicePipe");
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        return Path.Combine(dir, "appsettings.json");
    }

    private static readonly string FilePath = GetFilePath();

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new();
        }
        catch { }
        return new();
    }

    public void Save()
    {
        try
        {
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this,
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}

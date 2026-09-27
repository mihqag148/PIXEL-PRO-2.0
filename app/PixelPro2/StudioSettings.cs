using System.Text.Json;

namespace PixelPro2;

public sealed class StudioSettings {
    public int Schema { get; set; }=1;
    public bool DarkTheme { get; set; }
    public bool PcMonitorPlugin { get; set; }
    public bool MusicPlugin { get; set; }
    public string Language { get; set; }="en";

    public static string PathName=>Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PixelPro2","studio-settings.json");

    public static StudioSettings Load() {
        try {
            if(!File.Exists(PathName))return new();
            var settings=JsonSerializer.Deserialize<StudioSettings>(File.ReadAllText(PathName))??new();
            if(settings.Schema!=1)return new();
            if(settings.Language is not ("en" or "vi" or "zh"))settings.Language="en";
            return settings;
        } catch { return new(); }
    }

    public void Save() {
        Directory.CreateDirectory(Path.GetDirectoryName(PathName)!);
        string temp=PathName+".tmp";
        File.WriteAllText(temp,JsonSerializer.Serialize(this,new JsonSerializerOptions{WriteIndented=true}));
        File.Move(temp,PathName,true);
    }
}

using System.Text.Json;

namespace PixelPro2;

public readonly record struct MonitorSnapshot(
    int CpuPercent,int GpuPercent,int RamPercent,int DiskPercent,int NetKbps,
    int CpuTempC,int GpuTempC);

public readonly record struct NowPlayingSnapshot(
    string Title,string Artist,string Album,string Source,bool Playing);

public sealed class PluginEnvelope {
    public string Type { get; set; }="";
    public string Plugin { get; set; }="";
    public MonitorSnapshot? Monitor { get; set; }
    public NowPlayingSnapshot? Music { get; set; }

    public static string Serialize(PluginEnvelope message)=>
        JsonSerializer.Serialize(message);

    public static PluginEnvelope? Parse(string line) {
        try{return JsonSerializer.Deserialize<PluginEnvelope>(line);}
        catch{return null;}
    }
}

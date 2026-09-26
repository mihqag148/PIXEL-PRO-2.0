using System.Text.Json;
namespace PixelPro2;
public sealed class Step {
    public string Type { get; set; } = "Text";
    public string Value { get; set; } = "";
}
public sealed class Binding {
    public string Type { get; set; } = "K";
    public int Code { get; set; } = 4;
    public int Modifiers { get; set; }
    public int Color { get; set; } = 1215;
    public string Label { get; set; } = "Key A";
    public List<Step> Steps { get; set; } = [];
    public void Validate() {
        if (Label is null || Label.Length>12 || Label.Any(c=>c<32||c>126||c=='|'))
            throw new FormatException("Nhãn màn hình: tối đa 12 ký tự ASCII, không chứa |.");
        if (Modifiers is <0 or >255 || Color is <0 or >65535) throw new FormatException("Giá trị ngoài phạm vi.");
        bool valid=Type switch {
            "K" => Code is >=4 and <=115,
            "C" => Modifiers==0 && new[]{233,234,226,205,181,182,183}.Contains(Code),
            "P" => Modifiers==0 && Code is >=0 and <5,
            "H" or "D" => Code==0 && Modifiers==0,
            _ => false
        };
        if (!valid) throw new FormatException("Loại hành động, mã hoặc modifier không hợp lệ.");
        if(Steps is null || Steps.Count>32) throw new FormatException("Macro tối đa 32 bước.");
        foreach(var s in Steps) {
            if(s is null || s.Value is null || s.Value.Length>4096) throw new FormatException("Bước macro quá dài.");
            if(!new[]{"Text","Shortcut","Open","Delay"}.Contains(s.Type))throw new FormatException("Bước macro không hợp lệ.");
            if(s.Type=="Delay" && (!int.TryParse(s.Value,out int ms)||ms<0||ms>10000))throw new FormatException("Delay từ 0 đến 10000 ms.");
            if(s.Type=="Open" && !(Uri.TryCreate(s.Value,UriKind.Absolute,out var uri)&&uri.Scheme is "http" or "https") && !Path.IsPathFullyQualified(s.Value))
                throw new FormatException("Open cần URL http/https hoặc đường dẫn đầy đủ.");
            if(s.Type=="Shortcut")Shortcut.Parse(s.Value);
        }
    }
    public string Wire(int p,int k) {Validate();return $"SET|{p}|{k}|{Type}|{Code}|{Modifiers}|{Color}|{Label}";}
    public static Binding Parse(string response) {
        var t=response.Split('|');
        if(t.Length!=5)throw new FormatException("Phản hồi binding không hợp lệ.");
        var b=new Binding {Type=t[0],Code=int.Parse(t[1]),Modifiers=int.Parse(t[2]),Color=int.Parse(t[3]),Label=t[4]};
        b.Validate();return b;
    }
}
public sealed class Preset {
    public int Schema { get; set; }=2;
    public Binding[][] Profiles { get; set; }=Enumerable.Range(0,5)
        .Select(p=>Enumerable.Range(0,8).Select(k=>new Binding{Code=4+k,Label=$"Key {(char)('A'+k)}"}).ToArray()).ToArray();
    public void Validate() {
        if(Schema!=2||Profiles is null||Profiles.Length!=5)throw new FormatException("Cần preset PIXEL PRO 2.0 với 5 profile.");
        foreach(var p in Profiles) {
            if(p is null||p.Length!=8)throw new FormatException("Mỗi profile cần 8 phím.");
            foreach(var b in p){if(b is null)throw new FormatException("Thiếu binding.");b.Validate();}
        }
    }
    public static Preset Load(string path) {
        if(new FileInfo(path).Length>2_000_000)throw new FormatException("Preset quá lớn.");
        var p=JsonSerializer.Deserialize<Preset>(File.ReadAllText(path))??throw new FormatException("Preset rỗng.");p.Validate();return p;
    }
    public void Save(string path) {
        Validate();var temp=path+".tmp";
        File.WriteAllText(temp,JsonSerializer.Serialize(this,new JsonSerializerOptions{WriteIndented=true}));
        File.Move(temp,path,true);
    }
}
public static class Shortcut {
    public static ushort[] Parse(string text) {
        var parts=text.ToUpperInvariant().Split('+',StringSplitOptions.TrimEntries);
        if(parts.Length is <1 or >5)throw new FormatException("Ví dụ shortcut: CTRL+SHIFT+S.");
        var result=new List<ushort>();
        foreach(var p in parts) {
            ushort code=p switch {
                "CTRL"=>0x11,"ALT"=>0x12,"SHIFT"=>0x10,"WIN"=>0x5B,
                "ENTER"=>0x0D,"ESC"=>0x1B,"SPACE"=>0x20,"TAB"=>9,
                "LEFT"=>0x25,"UP"=>0x26,"RIGHT"=>0x27,"DOWN"=>0x28,
                "DELETE"=>0x2E,"BACKSPACE"=>8,
                _ when p.Length==1&&char.IsAsciiLetterOrDigit(p[0])=>(ushort)p[0],
                _ when p.StartsWith('F')&&int.TryParse(p[1..],out int f)&&f is >=1 and <=24=>(ushort)(0x6F+f),
                _=>throw new FormatException($"Phím shortcut không hỗ trợ: {p}")
            };result.Add(code);
        }
        return result.Distinct().ToArray();
    }
}

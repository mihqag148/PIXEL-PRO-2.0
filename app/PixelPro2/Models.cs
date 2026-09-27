using System.Text.Json;

namespace PixelPro2;

public static class Protocol {
    public static bool CompatibleHello(string hello) {
        var parts=hello.Split('|');
        return parts.Length>=6 && parts[0]=="PIXELPRO2" &&
            Version.TryParse(parts[1],out var version) && version.Major==2 &&
            parts[2]=="5" && parts[3]=="8" && parts[4]=="HX8357B";
    }
}

public sealed class Step {
    public string Type { get; set; } = "Text";
    public string Value { get; set; } = "";
}

public static class MacroValue {
    public static (int x,int y) Point(string value) {
        var p=value.Split(',',StringSplitOptions.TrimEntries);
        if(p.Length!=2||!int.TryParse(p[0],out int x)||!int.TryParse(p[1],out int y)||
           x is <-32767 or >32767||y is <-32767 or >32767)
            throw new FormatException("MouseMove dùng dx,dy; mỗi giá trị từ -32767 đến 32767.");
        return (x,y);
    }
    public static int Wheel(string value) {
        if(!int.TryParse(value,out int n)||n is <-1200 or >1200)
            throw new FormatException("Wheel từ -1200 đến 1200.");
        return n;
    }
    public static string Click(string value) {
        string v=value.Trim().ToUpperInvariant();
        if(v is not ("LEFT" or "RIGHT" or "MIDDLE" or "DOUBLELEFT"))
            throw new FormatException("MouseClick: LEFT, RIGHT, MIDDLE hoặc DOUBLELEFT.");
        return v;
    }
}

public sealed class Binding {
    public string Type { get; set; } = "K";
    public int Code { get; set; } = 4;
    public int Modifiers { get; set; }
    public int Color { get; set; } = 1215;
    public string Label { get; set; } = "Key A";
    public List<Step> Steps { get; set; } = [];

    public void Validate() {
        if(Label is null||Label.Length>12||Label.Any(c=>c<32||c>126||c=='|'))
            throw new FormatException("Nhãn màn hình: tối đa 12 ký tự ASCII, không chứa |.");
        if(Modifiers is <0 or >255||Color is <0 or >65535)
            throw new FormatException("Giá trị ngoài phạm vi.");

        bool valid=Type switch {
            "K" => Code is >=4 and <=115,
            "C" => Modifiers==0 && new[]{233,234,226,205,181,182,183}.Contains(Code),
            "M" => Modifiers==0 && Code is >=1 and <=6,
            "S" => Code==0 && Modifiers==0,
            "P" => Modifiers==0 && Code is >=0 and <5,
            "H" or "D" => Code==0 && Modifiers==0,
            _ => false
        };
        if(!valid) throw new FormatException("Loại hành động, mã hoặc modifier không hợp lệ.");

        if(Steps is null||Steps.Count>64) throw new FormatException("Macro tối đa 64 bước.");
        foreach(var s in Steps) {
            if(s is null||s.Value is null||s.Value.Length>4096)
                throw new FormatException("Bước macro quá dài.");
            switch(s.Type) {
                case "Text":
                    break;
                case "Shortcut":
                case "KeyDown":
                case "KeyUp":
                    Shortcut.Parse(s.Value);
                    break;
                case "Open":
                    if(!(Uri.TryCreate(s.Value,UriKind.Absolute,out var uri)&&uri.Scheme is "http" or "https") &&
                       !Path.IsPathFullyQualified(s.Value))
                        throw new FormatException("Open cần URL http/https hoặc đường dẫn đầy đủ.");
                    break;
                case "Website":
                    if(!(Uri.TryCreate(s.Value,UriKind.Absolute,out var web)&&web.Scheme is "http" or "https"))
                        throw new FormatException("Website cần URL http/https.");
                    break;
                case "LaunchApp":
                case "OpenFolder":
                case "OpenFile":
                    if(!Path.IsPathFullyQualified(s.Value))
                        throw new FormatException($"{s.Type} cần đường dẫn đầy đủ.");
                    break;
                case "Media":
                    if(s.Value.Trim().ToUpperInvariant() is not ("VOLUP" or "VOLDOWN" or "MUTE" or "PLAYPAUSE" or "NEXT" or "PREV" or "STOP"))
                        throw new FormatException("Media: VOLUP/VOLDOWN/MUTE/PLAYPAUSE/NEXT/PREV/STOP.");
                    break;
                case "ChangeProfile":
                    if(!int.TryParse(s.Value,out int profile)||profile is <1 or >5)
                        throw new FormatException("ChangeProfile dùng 1..5.");
                    break;
                case "FunctionalKey":
                    Shortcut.Parse(s.Value);
                    break;
                case "DeviceCtrl":
                    if(s.Value.Trim().ToUpperInvariant() is not ("MONITOR_TOGGLE" or "PROFILE_NEXT" or "PROFILE_PREV"))
                        throw new FormatException("DeviceCtrl không hợp lệ.");
                    break;
                case "Delay":
                    if(!int.TryParse(s.Value,out int ms)||ms<0||ms>30000)
                        throw new FormatException("Delay từ 0 đến 30000 ms.");
                    break;
                case "MouseMove":
                    MacroValue.Point(s.Value);
                    break;
                case "MouseClick":
                    MacroValue.Click(s.Value);
                    break;
                case "Wheel":
                    MacroValue.Wheel(s.Value);
                    break;
                default:
                    throw new FormatException("Bước macro không hợp lệ.");
            }
        }
    }

    public string Wire(int p,int k) {
        Validate();
        return $"SET|{p}|{k}|{Type}|{Code}|{Modifiers}|{Color}|{Label}";
    }

    public static Binding Parse(string response) {
        var t=response.Split('|');
        if(t.Length!=5) throw new FormatException("Phản hồi binding không hợp lệ.");
        var b=new Binding {
            Type=t[0],
            Code=int.Parse(t[1]),
            Modifiers=int.Parse(t[2]),
            Color=int.Parse(t[3]),
            Label=t[4]
        };
        b.Validate();
        return b;
    }
}

public sealed class AutoProfileRule {
    public string Process { get; set; }="";
    public int Profile { get; set; }
    public bool Enabled { get; set; }=true;

    public void Validate() {
        if(Profile is <0 or >4)throw new FormatException("Auto profile phải nằm trong 1..5.");
        if(Process is null||Process.Length>128||Process.IndexOfAny(Path.GetInvalidFileNameChars())>=0)
            throw new FormatException("Tên process auto profile không hợp lệ.");
    }
}

public static class HidShortcut {
    public static bool TryParse(string text,out int usage,out int modifiers) {
        usage=0;modifiers=0;
        var parts=text.ToUpperInvariant().Split('+',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries);
        if(parts.Length<1||parts.Length>5)return false;
        foreach(var p in parts) {
            switch(p) {
                case "CTRL":modifiers|=1;continue;
                case "SHIFT":modifiers|=2;continue;
                case "ALT":modifiers|=4;continue;
                case "WIN":modifiers|=8;continue;
            }
            if(usage!=0)return false;
            usage=p switch {
                _ when p.Length==1&&p[0] is >= 'A' and <= 'Z' => 4+(p[0]-'A'),
                "1"=>30,"2"=>31,"3"=>32,"4"=>33,"5"=>34,"6"=>35,"7"=>36,"8"=>37,"9"=>38,"0"=>39,
                "ENTER"=>40,"ESC"=>41,"BACKSPACE"=>42,"TAB"=>43,"SPACE"=>44,
                "INSERT"=>73,"HOME"=>74,"PAGEUP"=>75,"DELETE"=>76,"END"=>77,"PAGEDOWN"=>78,
                "RIGHT"=>79,"LEFT"=>80,"DOWN"=>81,"UP"=>82,
                _ when p.StartsWith('F')&&int.TryParse(p[1..],out int fn)&&fn is >=1 and <=12 => 57+fn,
                _=>0
            };
            if(usage==0)return false;
        }
        return usage!=0;
    }

    public static string Format(int usage,int modifiers) {
        var parts=new List<string>();
        if((modifiers&1)!=0)parts.Add("CTRL");
        if((modifiers&2)!=0)parts.Add("SHIFT");
        if((modifiers&4)!=0)parts.Add("ALT");
        if((modifiers&8)!=0)parts.Add("WIN");
        string key=usage switch {
            >=4 and <=29 => ((char)('A'+usage-4)).ToString(),
            >=30 and <=38 => (usage-29).ToString(),
            39 => "0",
            40 => "ENTER",41=>"ESC",42=>"BACKSPACE",43=>"TAB",44=>"SPACE",
            73=>"INSERT",74=>"HOME",75=>"PAGEUP",76=>"DELETE",77=>"END",78=>"PAGEDOWN",
            79=>"RIGHT",80=>"LEFT",81=>"DOWN",82=>"UP",
            >=58 and <=69 => $"F{usage-57}",
            _=>$"HID{usage}"
        };
        parts.Add(key);
        return string.Join("+",parts);
    }
}

public sealed class Preset {
    public int Schema { get; set; }=3;
    public List<AutoProfileRule> AutoProfiles { get; set; }=[];
    public Binding[][] Profiles { get; set; }=Enumerable.Range(0,5)
        .Select(_=>Enumerable.Range(0,8)
            .Select(k=>new Binding{Code=4+k,Label=$"Key {(char)('A'+k)}"})
            .ToArray()).ToArray();

    public void Validate() {
        if(Schema is not (2 or 3)||Profiles is null||Profiles.Length!=5)
            throw new FormatException("Cần preset PIXEL PRO 2.0 với 5 profile.");
        foreach(var p in Profiles) {
            if(p is null||p.Length!=8) throw new FormatException("Mỗi profile cần 8 phím.");
            foreach(var b in p) {
                if(b is null) throw new FormatException("Thiếu binding.");
                b.Validate();
            }
        }
        AutoProfiles ??=[];
        if(AutoProfiles.Count>64)throw new FormatException("Tối đa 64 luật auto profile.");
        foreach(var rule in AutoProfiles)rule.Validate();
    }

    public static Preset Load(string path) {
        if(new FileInfo(path).Length>2_000_000) throw new FormatException("Preset quá lớn.");
        var p=JsonSerializer.Deserialize<Preset>(File.ReadAllText(path)) ??
              throw new FormatException("Preset rỗng.");
        p.Validate();
        p.Schema=3;
        return p;
    }

    public void Save(string path) {
        Validate();
        var temp=path+".tmp";
        File.WriteAllText(temp,JsonSerializer.Serialize(this,new JsonSerializerOptions{WriteIndented=true}));
        File.Move(temp,path,true);
    }
}

public static class Shortcut {
    public static ushort[] Parse(string text) {
        var parts=text.ToUpperInvariant().Split('+',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries);
        if(parts.Length is <1 or >6) throw new FormatException("Ví dụ shortcut: CTRL+SHIFT+S.");
        var result=new List<ushort>();
        foreach(var p in parts) {
            ushort code=p switch {
                "CTRL"=>0x11,"ALT"=>0x12,"SHIFT"=>0x10,"WIN"=>0x5B,
                "ENTER"=>0x0D,"ESC"=>0x1B,"SPACE"=>0x20,"TAB"=>9,
                "LEFT"=>0x25,"UP"=>0x26,"RIGHT"=>0x27,"DOWN"=>0x28,
                "DELETE"=>0x2E,"BACKSPACE"=>8,"HOME"=>0x24,"END"=>0x23,
                "PAGEUP"=>0x21,"PAGEDOWN"=>0x22,"INSERT"=>0x2D,
                _ when p.Length==1&&char.IsAsciiLetterOrDigit(p[0])=>(ushort)p[0],
                _ when p.StartsWith('F')&&int.TryParse(p[1..],out int f)&&f is >=1 and <=24=>(ushort)(0x6F+f),
                _=>throw new FormatException($"Phím shortcut không hỗ trợ: {p}")
            };
            result.Add(code);
        }
        return result.Distinct().ToArray();
    }
}

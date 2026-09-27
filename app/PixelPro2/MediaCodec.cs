using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace PixelPro2;

public sealed class MediaPackage {
    public required byte[] Data { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public int Frames { get; init; }
    public int DelayMs { get; init; }
    public double DurationSeconds => Frames*DelayMs/1000.0;
}

public static class MediaCodec {
    public const int Width=160;
    public const int Height=106;
    public const int HeaderSize=12;
    public const int MaxBytes=1_800_000;
    public const int IconSize=48;

    public static MediaPackage FromGif(string path) {
        using var source=Image.FromFile(path);
        var dimension=new FrameDimension(source.FrameDimensionsList[0]);
        int sourceFrames=Math.Max(1,source.GetFrameCount(dimension));
        int maxFrames=Math.Max(1,(MaxBytes-HeaderSize)/(Width*Height));
        int frames=Math.Min(sourceFrames,maxFrames);

        int totalDurationMs=ReadTotalDuration(source,sourceFrames);
        int delayMs=Math.Clamp((int)Math.Round(totalDurationMs/(double)frames),60,1000);

        using var stream=new MemoryStream(HeaderSize+frames*Width*Height);
        using var writer=new BinaryWriter(stream);
        writer.Write(new byte[]{(byte)'P',(byte)'X',(byte)'G',(byte)'1'});
        writer.Write((ushort)Width);
        writer.Write((ushort)Height);
        writer.Write((ushort)frames);
        writer.Write((ushort)delayMs);

        for(int i=0;i<frames;i++) {
            int sourceIndex=Math.Min(sourceFrames-1,(int)Math.Floor(i*(sourceFrames/(double)frames)));
            source.SelectActiveFrame(dimension,sourceIndex);
            using var bitmap=Render(source,Width,Height);
            WriteRgb332(bitmap,writer);
        }

        byte[] data=stream.ToArray();
        if(data.Length>MaxBytes)throw new IOException("GIF sau chuyển đổi vượt 1.8 MB.");
        return new MediaPackage{Data=data,Width=Width,Height=Height,Frames=frames,DelayMs=delayMs};
    }

    public static byte[] FromIcon(string path) {
        using var source=Image.FromFile(path);
        if(source.FrameDimensionsList.Length>0) {
            var dimension=new FrameDimension(source.FrameDimensionsList[0]);
            source.SelectActiveFrame(dimension,0);
        }
        using var bitmap=Render(source,IconSize,IconSize);
        using var stream=new MemoryStream(8+IconSize*IconSize);
        using var writer=new BinaryWriter(stream);
        writer.Write(new byte[]{(byte)'P',(byte)'X',(byte)'I',(byte)'1'});
        writer.Write((ushort)IconSize);
        writer.Write((ushort)IconSize);
        WriteRgb332(bitmap,writer);
        return stream.ToArray();
    }

    public static byte[] FromNativeScript(IEnumerable<Step> sourceSteps) {
        var input=sourceSteps?.ToList()??throw new ArgumentNullException(nameof(sourceSteps));
        var steps=new List<Step>();
        foreach(var step in input) {
            if(step.Type=="Script")steps.AddRange(EezScript.Expand(step.Value,true));
            else steps.Add(step);
        }
        if(steps.Count is <1 or >512)throw new FormatException("HID script cần 1..512 action sau khi biên dịch.");

        using var stream=new MemoryStream();
        using var writer=new BinaryWriter(stream);
        writer.Write(new byte[]{(byte)'P',(byte)'X',(byte)'S',(byte)'2'});
        writer.Write((ushort)steps.Count);

        foreach(var step in steps) {
            string type=step.Type;
            string value=step.Value??"";
            switch(type) {
                case "Text": {
                    byte[] text=System.Text.Encoding.ASCII.GetBytes(value);
                    if(text.Length is <1 or >96||value.Any(ch=>ch>127))
                        throw new FormatException("HID Text mỗi action cần 1..96 ký tự ASCII.");
                    writer.Write((byte)1);writer.Write((byte)text.Length);writer.Write(text);
                    break;
                }
                case "Shortcut":
                case "FunctionalKey": {
                    if(!HidShortcut.TryParse(value,out int usage,out int modifiers))
                        throw new FormatException($"HID {type} chỉ hỗ trợ modifier + 1 phím chuẩn.");
                    writer.Write((byte)2);writer.Write((byte)modifiers);writer.Write((byte)1);writer.Write((byte)usage);
                    break;
                }
                case "Delay": {
                    if(!ushort.TryParse(value,out ushort ms)||ms>30000)
                        throw new FormatException("HID Wait từ 0 đến 30000 ms.");
                    writer.Write((byte)3);writer.Write(ms);
                    break;
                }
                case "MouseClick": {
                    int code=MacroValue.Click(value) switch {
                        "LEFT"=>1,"RIGHT"=>2,"MIDDLE"=>3,"DOUBLELEFT"=>4,_=>0
                    };
                    writer.Write((byte)4);writer.Write((byte)code);
                    break;
                }
                case "Wheel": {
                    int wheel=MacroValue.Wheel(value);
                    if(wheel==0||wheel is <-127 or >127)
                        throw new FormatException("HID wheel dùng -127..127 và khác 0.");
                    writer.Write((byte)5);writer.Write(unchecked((byte)(sbyte)wheel));
                    break;
                }
                case "Media": {
                    ushort code=value.Trim().ToUpperInvariant() switch {
                        "VOLUP"=>233,"VOLDOWN"=>234,"MUTE"=>226,"PLAYPAUSE"=>205,
                        "NEXT"=>181,"PREV"=>182,"STOP"=>183,
                        _=>throw new FormatException("Media action không hợp lệ.")
                    };
                    writer.Write((byte)6);writer.Write(code);
                    break;
                }
                case "ChangeProfile": {
                    if(!int.TryParse(value,out int profile)||profile is <1 or >DeviceLimits.Profiles)
                        throw new FormatException($"ChangeProfile dùng 1..{DeviceLimits.Profiles}.");
                    writer.Write((byte)7);writer.Write((byte)(profile-1));
                    break;
                }
                case "DeviceCtrl": {
                    byte code=value.Trim().ToUpperInvariant() switch {
                        "PROFILE_NEXT"=>1,"PROFILE_PREV"=>2,
                        _=>throw new FormatException("HID DeviceCtrl hiện hỗ trợ PROFILE_NEXT/PROFILE_PREV.")
                    };
                    writer.Write((byte)8);writer.Write(code);
                    break;
                }
                case "NativeMouseMove": {
                    var (x,y)=MacroValue.Point(value);
                    if(x is <-127 or >127||y is <-127 or >127)
                        throw new FormatException("Script MOUSE_MOVE chỉ hỗ trợ -127..127.");
                    writer.Write((byte)9);
                    writer.Write(unchecked((byte)(sbyte)x));
                    writer.Write(unchecked((byte)(sbyte)y));
                    break;
                }
                case "NativeTextTimed": {
                    int sep=value.IndexOf('|');
                    if(sep<1||!ushort.TryParse(value[..sep],out ushort delay)||delay>30000)
                        throw new FormatException("DEFAULTCHARDELAY không hợp lệ.");
                    string textValue=value[(sep+1)..];
                    byte[] text=System.Text.Encoding.ASCII.GetBytes(textValue);
                    if(text.Length is <1 or >255||textValue.Any(ch=>ch>127))
                        throw new FormatException("Script STRING hỗ trợ 1..255 ký tự ASCII.");
                    writer.Write((byte)12);writer.Write(delay);writer.Write((byte)text.Length);writer.Write(text);
                    break;
                }
                case "NativeChordTimed": {
                    int sep=value.IndexOf('|');
                    if(sep<1||!ushort.TryParse(value[..sep],out ushort duration)||duration>30000)
                        throw new FormatException("DEFAULTDURATION không hợp lệ.");
                    string chord=value[(sep+1)..];
                    if(!HidShortcut.TryParse(chord,out int usage,out int modifiers))
                        throw new FormatException($"Script chord không hợp lệ: {chord}");
                    writer.Write((byte)13);writer.Write(duration);
                    writer.Write((byte)modifiers);writer.Write((byte)1);writer.Write((byte)usage);
                    break;
                }
                default:
                    throw new FormatException($"{type} cần Studio chạy, không thể lưu vào HID script.");
            }
        }

        byte[] data=stream.ToArray();
        if(data.Length>8192)throw new FormatException("HID script vượt 8192 byte.");
        return data;
    }

    public static bool CanEncodeNative(IEnumerable<Step> steps) {
        try { _=FromNativeScript(steps);return true; }
        catch(FormatException) { return false; }
    }

    static Bitmap Render(Image source,int width,int height) {
        var bitmap=new Bitmap(width,height,PixelFormat.Format24bppRgb);
        using var graphics=Graphics.FromImage(bitmap);
        graphics.Clear(Color.Black);
        graphics.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode=System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
        graphics.CompositingQuality=System.Drawing.Drawing2D.CompositingQuality.HighQuality;

        double scale=Math.Min(width/(double)source.Width,height/(double)source.Height);
        int w=Math.Max(1,(int)Math.Round(source.Width*scale));
        int h=Math.Max(1,(int)Math.Round(source.Height*scale));
        int x=(width-w)/2,y=(height-h)/2;
        graphics.DrawImage(source,new Rectangle(x,y,w,h));
        return bitmap;
    }

    static int ReadTotalDuration(Image source,int frames) {
        try {
            var item=source.GetPropertyItem(0x5100);
            if(item.Value is {Length:>=4}) {
                long total=0;
                for(int i=0;i<frames&&i*4+3<item.Value.Length;i++) {
                    int hundredths=BitConverter.ToInt32(item.Value,i*4);
                    if(hundredths<=0)hundredths=10;
                    total+=hundredths*10L;
                }
                if(total>0&&total<int.MaxValue)return(int)total;
            }
        }catch(ArgumentException){}
        return frames*100;
    }

    static void WriteRgb332(Bitmap bitmap,BinaryWriter writer) {
        var rect=new Rectangle(0,0,bitmap.Width,bitmap.Height);
        var bits=bitmap.LockBits(rect,ImageLockMode.ReadOnly,PixelFormat.Format24bppRgb);
        try {
            int stride=Math.Abs(bits.Stride);
            byte[] raw=new byte[stride*bitmap.Height];
            Marshal.Copy(bits.Scan0,raw,0,raw.Length);
            for(int y=0;y<bitmap.Height;y++) {
                int row=bits.Stride>=0?y*stride:(bitmap.Height-1-y)*stride;
                for(int x=0;x<bitmap.Width;x++) {
                    int p=row+x*3;
                    byte b=raw[p],g=raw[p+1],r=raw[p+2];
                    writer.Write((byte)((r&0xE0)|((g&0xE0)>>3)|(b>>6)));
                }
            }
        }finally{bitmap.UnlockBits(bits);}
    }
}

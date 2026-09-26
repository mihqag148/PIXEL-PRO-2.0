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

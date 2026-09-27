namespace PixelPro2;

public static class IconStore {
    static string Folder=>Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PixelPro2","icons");

    static string BaseName(int profile,int key)=>$"p{profile:D2}-k{key}";
    public static string BinaryPath(int profile,int key)=>Path.Combine(Folder,BaseName(profile,key)+".pxi");
    public static string PreviewPath(int profile,int key)=>Path.Combine(Folder,BaseName(profile,key)+".png");
    static string DeletePath(int profile,int key)=>Path.Combine(Folder,BaseName(profile,key)+".delete");

    static void Ensure()=>Directory.CreateDirectory(Folder);

    public static void Save(string imagePath,int profile,int key) {
        Ensure();
        File.WriteAllBytes(BinaryPath(profile,key),MediaCodec.FromIcon(imagePath));

        using var source=Image.FromFile(imagePath);
        using var preview=new Bitmap(52,52);
        using(var g=Graphics.FromImage(preview)) {
            g.Clear(Color.Transparent);
            g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode=System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            g.DrawImage(source,new Rectangle(0,0,52,52));
        }
        preview.Save(PreviewPath(profile,key),System.Drawing.Imaging.ImageFormat.Png);
        if(File.Exists(DeletePath(profile,key)))File.Delete(DeletePath(profile,key));
    }

    public static void SaveMediaIcon(string mediaAction,int profile,int key) {
        Ensure();
        string glyph=mediaAction.Trim().ToUpperInvariant() switch {
            "NEXT"=>"▶▶",
            "PREV"=>"◀◀",
            "STOP"=>"■",
            "VOLUP"=>"VOL+",
            "VOLDOWN"=>"VOL-",
            "MUTE"=>"MUTE",
            _=>"▶❚❚"
        };

        using var preview=new Bitmap(52,52);
        using(var g=Graphics.FromImage(preview)) {
            g.Clear(Color.FromArgb(28,30,34));
            g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            float fontSize=glyph.Length>3?9f:glyph.Length>2?12f:18f;
            using var font=new Font("Segoe UI",fontSize,FontStyle.Bold,GraphicsUnit.Point);
            using var brush=new SolidBrush(Color.White);
            var size=g.MeasureString(glyph,font);
            g.DrawString(glyph,font,brush,(52-size.Width)/2f,(52-size.Height)/2f);
        }
        preview.Save(PreviewPath(profile,key),System.Drawing.Imaging.ImageFormat.Png);
        File.WriteAllBytes(BinaryPath(profile,key),MediaCodec.FromIcon(PreviewPath(profile,key)));
        if(File.Exists(DeletePath(profile,key)))File.Delete(DeletePath(profile,key));
    }

    public static void MarkDeleted(int profile,int key) {
        Ensure();
        if(File.Exists(BinaryPath(profile,key)))File.Delete(BinaryPath(profile,key));
        if(File.Exists(PreviewPath(profile,key)))File.Delete(PreviewPath(profile,key));
        File.WriteAllText(DeletePath(profile,key),"delete");
    }

    public static byte[]? ReadBinary(int profile,int key) {
        string path=BinaryPath(profile,key);
        return File.Exists(path)?File.ReadAllBytes(path):null;
    }

    public static bool DeletePending(int profile,int key)=>File.Exists(DeletePath(profile,key));
    public static void ClearDelete(int profile,int key) {
        string path=DeletePath(profile,key);
        if(File.Exists(path))File.Delete(path);
    }

    public static Image? LoadPreview(int profile,int key) {
        string path=PreviewPath(profile,key);
        if(!File.Exists(path))return null;
        using var source=Image.FromFile(path);
        return new Bitmap(source);
    }
}

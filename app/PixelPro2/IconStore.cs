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

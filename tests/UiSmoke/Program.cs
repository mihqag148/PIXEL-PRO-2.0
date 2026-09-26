using PixelPro2;
static class Program {
    [STAThread] static void Main(string[] args) {
        ApplicationConfiguration.Initialize();
        using var form=new MainForm();
        form.ShowInTaskbar=false;form.Opacity=0;
        form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-32000,-32000);
        form.Show();Application.DoEvents();form.PerformLayout();
        using var image=new Bitmap(form.Width,form.Height);
        form.DrawToBitmap(image,new Rectangle(0,0,image.Width,image.Height));
        string output=args.Length>0?args[0]:"studio-preview.png";
        image.Save(output,System.Drawing.Imaging.ImageFormat.Png);
        if(form.Controls.Count==0)throw new Exception("Studio did not construct controls");
        Console.WriteLine("Studio construction and offscreen rendering passed: "+output);
    }
}

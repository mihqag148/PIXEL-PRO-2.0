using System.IO.Pipes;
using PixelPro2;

internal static class Program {
    [STAThread]
    static async Task Main() {
        using var music=new MusicPlugin();
        while(true) {
            try {
                using var pipe=new NamedPipeClientStream(
                    ".","PIXEL_PRO_2_PLUGINS",PipeDirection.Out,PipeOptions.Asynchronous);
                using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await pipe.ConnectAsync(timeout.Token);
                using var writer=new StreamWriter(pipe){AutoFlush=true};
                await writer.WriteLineAsync(PluginEnvelope.Serialize(new(){
                    Type="hello",Plugin="Music Player"
                }));

                while(pipe.IsConnected) {
                    var snapshot=await music.Read() ?? new NowPlayingSnapshot("","","","",false);
                    await writer.WriteLineAsync(PluginEnvelope.Serialize(new(){
                        Type="music",Plugin="Music Player",Music=snapshot
                    }));
                    await Task.Delay(1000);
                }
            } catch(OperationCanceledException) {}
            catch(IOException) {}
            catch(UnauthorizedAccessException) {}
            music.Reset();
            await Task.Delay(1000);
        }
    }
}

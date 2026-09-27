using System.IO.Pipes;
using PixelPro2;

internal static class Program {
    [STAThread]
    static async Task Main() {
        using var collector=new SystemMonitorCollector();
        while(true) {
            try {
                using var pipe=new NamedPipeClientStream(
                    ".","PIXEL_PRO_2_PLUGINS",PipeDirection.Out,PipeOptions.Asynchronous);
                using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await pipe.ConnectAsync(timeout.Token);
                using var writer=new StreamWriter(pipe){AutoFlush=true};
                await writer.WriteLineAsync(PluginEnvelope.Serialize(new(){
                    Type="hello",Plugin="PC Monitor"
                }));

                while(pipe.IsConnected) {
                    var snapshot=collector.Read();
                    await writer.WriteLineAsync(PluginEnvelope.Serialize(new(){
                        Type="monitor",Plugin="PC Monitor",Monitor=snapshot
                    }));
                    await Task.Delay(1000);
                }
            } catch(OperationCanceledException) {}
            catch(IOException) {}
            catch(UnauthorizedAccessException) {}
            await Task.Delay(1000);
        }
    }
}

using System.Diagnostics;
using System.IO.Pipes;

namespace PixelPro2;

public sealed class NamedPipePluginHost : IDisposable {
    public const string PipeName="PIXEL_PRO_2_PLUGINS";
    readonly CancellationTokenSource shutdown=new();
    readonly object sync=new();
    readonly Dictionary<string,int> connected=new(StringComparer.OrdinalIgnoreCase);
    MonitorSnapshot? monitor;
    NowPlayingSnapshot? music;
    Task? acceptLoop;

    public event Action? Updated;
    public event Action<string,bool>? ConnectionChanged;

    public bool Running=>acceptLoop is {IsCompleted:false};

    public MonitorSnapshot? Monitor {
        get{lock(sync)return monitor;}
    }
    public NowPlayingSnapshot? Music {
        get{lock(sync)return music;}
    }
    public IReadOnlyCollection<string> ConnectedPlugins {
        get{lock(sync)return connected.Where(x=>x.Value>0).Select(x=>x.Key).ToArray();}
    }

    public void Start() {
        if(Running)return;
        acceptLoop=Task.Run(()=>AcceptLoop(shutdown.Token));
    }

    async Task AcceptLoop(CancellationToken token) {
        while(!token.IsCancellationRequested) {
            NamedPipeServerStream? pipe=null;
            try {
                pipe=new NamedPipeServerStream(
                    PipeName,PipeDirection.In,8,PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);
                await pipe.WaitForConnectionAsync(token);
                _=Task.Run(()=>HandleClient(pipe,token),token);
                pipe=null;
            } catch(OperationCanceledException) {
                pipe?.Dispose();break;
            } catch {
                pipe?.Dispose();
                try{await Task.Delay(250,token);}catch(OperationCanceledException){break;}
            }
        }
    }

    async Task HandleClient(NamedPipeServerStream pipe,CancellationToken token) {
        string plugin="";
        try {
            using(pipe)
            using(var reader=new StreamReader(pipe)) {
                while(!token.IsCancellationRequested&&pipe.IsConnected) {
                    string? line=await reader.ReadLineAsync(token);
                    if(line==null)break;
                    var message=PluginEnvelope.Parse(line);
                    if(message==null)continue;

                    if(!string.IsNullOrWhiteSpace(message.Plugin)&&plugin.Length==0) {
                        plugin=message.Plugin.Trim();
                        lock(sync)connected[plugin]=connected.GetValueOrDefault(plugin)+1;
                        ConnectionChanged?.Invoke(plugin,true);
                    }

                    lock(sync) {
                        if(message.Monitor.HasValue)monitor=message.Monitor;
                        if(message.Music.HasValue)music=message.Music;
                    }
                    Updated?.Invoke();
                }
            }
        } catch(OperationCanceledException) {}
        catch(IOException) {}
        finally {
            if(plugin.Length>0) {
                lock(sync) {
                    int count=connected.GetValueOrDefault(plugin);
                    if(count<=1)connected.Remove(plugin);
                    else connected[plugin]=count-1;
                }
                ConnectionChanged?.Invoke(plugin,false);
            }
        }
    }

    public async Task<MonitorSnapshot?> WaitForMonitor(TimeSpan timeout) {
        var until=DateTime.UtcNow+timeout;
        while(DateTime.UtcNow<until) {
            var value=Monitor;
            if(value.HasValue)return value;
            await Task.Delay(100);
        }
        return Monitor;
    }

    public async Task<NowPlayingSnapshot?> WaitForMusic(TimeSpan timeout) {
        var until=DateTime.UtcNow+timeout;
        while(DateTime.UtcNow<until) {
            var value=Music;
            if(value.HasValue)return value;
            await Task.Delay(100);
        }
        return Music;
    }

    public void ClearMonitor(){lock(sync)monitor=null;Updated?.Invoke();}
    public void ClearMusic(){lock(sync)music=null;Updated?.Invoke();}

    public void Dispose() {
        shutdown.Cancel();
        shutdown.Dispose();
    }
}

public sealed class PluginProcessManager : IDisposable {
    readonly Dictionary<string,Process> launched=new(StringComparer.OrdinalIgnoreCase);

    public bool IsRunning(string exeName)=>
        launched.TryGetValue(exeName,out var process)&&!process.HasExited;

    public void Start(string exeName) {
        if(IsRunning(exeName))return;
        string path=Path.Combine(AppContext.BaseDirectory,exeName);
        if(!File.Exists(path))throw new FileNotFoundException(
            $"Plugin executable not found: {exeName}",path);
        var process=Process.Start(new ProcessStartInfo(path){
            UseShellExecute=false,
            CreateNoWindow=true,
            WorkingDirectory=AppContext.BaseDirectory
        })??throw new IOException($"Cannot start plugin {exeName}.");
        launched[exeName]=process;
    }

    public void Stop(string exeName) {
        if(!launched.Remove(exeName,out var process))return;
        try {
            if(!process.HasExited)process.Kill(entireProcessTree:true);
            process.WaitForExit(1500);
        } catch {}
        process.Dispose();
    }

    public void Dispose() {
        foreach(var name in launched.Keys.ToArray())Stop(name);
    }
}

using System.IO.Ports;
using System.Collections.Concurrent;
namespace PixelPro2;
public sealed class Device : IDisposable {
    SerialPort? port;
    CancellationTokenSource? lifetime;
    readonly ConcurrentDictionary<int,TaskCompletionSource<string>> pending=new();
    readonly SemaphoreSlim gate=new(1,1);
    int sequence;
    public event Action<string>? Event;
    public event Action<string>? Disconnected;
    public bool Connected => port?.IsOpen==true;
    public async Task Connect(string name) {
        Dispose();
        var serial=new SerialPort(name,115200){NewLine="\n",ReadTimeout=300,WriteTimeout=1500,DtrEnable=true,RtsEnable=false};
        serial.Open();port=serial;lifetime=new();var token=lifetime.Token;
        _=Task.Run(()=>Read(serial,token));
        try {
            var hello=await Request("HELLO");
            if(!Protocol.CompatibleHello(hello))throw new IOException("Thiết bị không phải PIXEL PRO 2.0 tương thích.");
        }catch {Dispose();throw;}
    }
    void Read(SerialPort serial,CancellationToken token) {
        string line="";
        try {
            while(!token.IsCancellationRequested) {
                try {
                    int c=serial.ReadChar();if(c=='\r')continue;
                    if(c!='\n'){line+=(char)c;if(line.Length>2048)throw new IOException("Phản hồi thiết bị quá dài.");continue;}
                    var t=line.Split('|');line="";
                    if(t.Length>=4&&t[0]=="R"&&int.TryParse(t[1],out int id)&&pending.TryRemove(id,out var request)) {
                        if(t[2]=="OK")request.TrySetResult(string.Join('|',t.Skip(3)));
                        else request.TrySetException(new IOException(string.Join('|',t.Skip(3))));
                    }else if(t.Length>=2&&t[0]=="E")Event?.Invoke(string.Join('|',t));
                }catch(TimeoutException) {}
            }
        }catch(Exception ex) when(ex is IOException or InvalidOperationException or UnauthorizedAccessException) {
            if(!token.IsCancellationRequested) {
                foreach(var pair in pending)if(pending.TryRemove(pair.Key,out var request))request.TrySetException(ex);
                serial.Close();Disconnected?.Invoke(ex.Message);
            }
        }
    }
    public async Task<string> Request(string command) {
        await gate.WaitAsync();int id=0;
        try {
            if(!Connected)throw new IOException("Chưa kết nối thiết bị.");
            id=sequence=sequence%65535+1;
            var result=new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);pending[id]=result;
            port!.WriteLine($"{id}|{command}");
            return await result.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }finally {if(id!=0)pending.TryRemove(id,out _);gate.Release();}
    }
    public void Dispose() {
        lifetime?.Cancel();lifetime?.Dispose();lifetime=null;
        try{port?.Close();}catch(IOException){}port?.Dispose();port=null;
        foreach(var p in pending)if(pending.TryRemove(p.Key,out var request))request.TrySetCanceled();
    }
}

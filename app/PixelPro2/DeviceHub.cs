namespace PixelPro2;

public sealed class DeviceHub : IDisposable {
    readonly Dictionary<string,Device> devices=new(StringComparer.OrdinalIgnoreCase);
    string? activePort;

    public event Action<string>? Event;
    public event Action<string>? Disconnected;
    public event Action? Changed;

    public IReadOnlyCollection<string> ConnectedPorts =>
        devices.Where(x=>x.Value.Connected).Select(x=>x.Key)
            .Order(StringComparer.OrdinalIgnoreCase).ToArray();

    Device Active {
        get {
            if(activePort==null||!devices.TryGetValue(activePort,out var device)||!device.Connected)
                throw new IOException("Chưa chọn PIXEL PRO đang kết nối.");
            return device;
        }
    }

    public bool Connected =>
        activePort!=null&&devices.TryGetValue(activePort,out var active)&&active.Connected;
    public string? PortName=>Connected?activePort:null;

    public bool IsConnected(string port)=>devices.TryGetValue(port,out var d)&&d.Connected;

    public async Task Connect(string port) {
        if(devices.TryGetValue(port,out var existing)&&existing.Connected) {
            activePort=port;Changed?.Invoke();return;
        }

        existing?.Dispose();
        var device=new Device();
        device.Event+=text=>{
            if(string.Equals(activePort,port,StringComparison.OrdinalIgnoreCase))
                Event?.Invoke(text);
        };
        device.Disconnected+=reason=>{
            if(string.Equals(activePort,port,StringComparison.OrdinalIgnoreCase))
                Disconnected?.Invoke(reason);
            Changed?.Invoke();
        };

        await device.Connect(port);
        devices[port]=device;
        activePort=port;
        Changed?.Invoke();
    }

    public bool Activate(string port) {
        if(!IsConnected(port))return false;
        activePort=port;Changed?.Invoke();return true;
    }

    public void DisconnectActive() {
        if(activePort==null)return;
        string port=activePort;
        if(devices.Remove(port,out var device))device.Dispose();
        activePort=devices.FirstOrDefault(x=>x.Value.Connected).Key;
        Changed?.Invoke();
    }

    public async Task<string> Request(string command)=>await Active.Request(command);
    public async Task<string> Request(string command,TimeSpan timeout)=>await Active.Request(command,timeout);
    public Task UploadMedia(byte[] data,IProgress<int>? progress=null,CancellationToken token=default)=>
        Active.UploadMedia(data,progress,token);
    public Task UploadScript(int profile,int key,byte[] data,IProgress<int>? progress=null,CancellationToken token=default)=>
        Active.UploadScript(profile,key,data,progress,token);
    public Task UploadIcon(int profile,int key,byte[] data,IProgress<int>? progress=null,CancellationToken token=default)=>
        Active.UploadIcon(profile,key,data,progress,token);

    public void Dispose() {
        foreach(var device in devices.Values)device.Dispose();
        devices.Clear();activePort=null;Changed?.Invoke();
    }
}

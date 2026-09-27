using System.Collections.Concurrent;
using System.IO.Ports;

namespace PixelPro2;

public sealed class Device : IDisposable {
    SerialPort? port;
    CancellationTokenSource? lifetime;
    readonly ConcurrentDictionary<int,TaskCompletionSource<string>> pending=new();
    readonly SemaphoreSlim gate=new(1,1);
    TaskCompletionSource<int>? mediaAck;
    TaskCompletionSource<string>? mediaDone;
    int sequence;

    public event Action<string>? Event;
    public event Action<string>? Disconnected;
    public bool Connected => port?.IsOpen==true;
    public string? PortName => port?.PortName;

    public async Task Connect(string name) {
        Dispose();
        var serial=new SerialPort(name,115200) {
            NewLine="\n",
            ReadTimeout=250,
            WriteTimeout=2500,
            DtrEnable=false,
            RtsEnable=false,
            Handshake=Handshake.None
        };
        serial.Open();
        try { serial.DiscardInBuffer(); serial.DiscardOutBuffer(); } catch(InvalidOperationException) {}
        // Assert DTR first, then RTS. On Arduino-ESP32 USBCDC this avoids
        // accidentally walking the legacy reboot-to-bootloader line-state
        // sequence while still ending with both lines asserted for CDC.
        serial.DtrEnable=true;
        await Task.Delay(25);
        serial.RtsEnable=true;
        port=serial;
        lifetime=new CancellationTokenSource();
        var token=lifetime.Token;
        _=Task.Run(()=>Read(serial,token),token);

        Exception? last=null;
        try {
            await Task.Delay(220,token);
            for(int attempt=0;attempt<6;attempt++) {
                try {
                    var hello=await Request("HELLO",TimeSpan.FromSeconds(2));
                    if(!Protocol.CompatibleHello(hello))
                        throw new IOException("Cổng này không phải PIXEL PRO 2.0 tương thích.");
                    return;
                } catch(Exception ex) when(ex is TimeoutException or IOException) {
                    last=ex;
                    if(attempt<5) await Task.Delay(350,token);
                }
            }
            throw new IOException("PIXEL PRO 2.0 có cổng COM nhưng không trả lời handshake.",last);
        } catch {
            Dispose();
            throw;
        }
    }

    void Read(SerialPort serial,CancellationToken token) {
        string line="";
        try {
            while(!token.IsCancellationRequested) {
                try {
                    int c=serial.ReadChar();
                    if(c=='\r') continue;
                    if(c!='\n') {
                        line+=(char)c;
                        if(line.Length>4096) line="";
                        continue;
                    }
                    if(line.Length==0) continue;
                    var t=line.Split('|');
                    line="";
                    if(t.Length>=4&&t[0]=="R"&&int.TryParse(t[1],out int id)&&pending.TryRemove(id,out var request)) {
                        if(t[2]=="OK") request.TrySetResult(string.Join('|',t.Skip(3)));
                        else request.TrySetException(new IOException(string.Join('|',t.Skip(3))));
                        continue;
                    }
                    if(t.Length>=2&&t[0]=="E") {
                        if(t.Length>=3&&t[1]=="MEDIAACK"&&int.TryParse(t[2],out int received))
                            mediaAck?.TrySetResult(received);
                        else if(t.Length>=3&&t[1]=="MEDIADONE") {
                            if(t[2]=="OK") mediaDone?.TrySetResult("OK");
                            else {
                                var ex=new IOException("Media upload: "+t[2]);
                                mediaAck?.TrySetException(ex);
                                mediaDone?.TrySetException(ex);
                            }
                        }
                        Event?.Invoke(string.Join('|',t));
                    }
                } catch(TimeoutException) {}
            }
        } catch(Exception ex) when(ex is IOException or InvalidOperationException or UnauthorizedAccessException) {
            if(!token.IsCancellationRequested) {
                foreach(var pair in pending)
                    if(pending.TryRemove(pair.Key,out var request)) request.TrySetException(ex);
                mediaAck?.TrySetException(ex);
                mediaDone?.TrySetException(ex);
                try { serial.Close(); } catch {}
                Disconnected?.Invoke(ex.Message);
            }
        }
    }

    async Task<string> RequestLocked(string command,TimeSpan timeout) {
        if(!Connected) throw new IOException("Chưa kết nối thiết bị.");
        int id=sequence=sequence%65535+1;
        var result=new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        pending[id]=result;
        try {
            port!.WriteLine($"{id}|{command}");
            return await result.Task.WaitAsync(timeout);
        } finally {
            pending.TryRemove(id,out _);
        }
    }

    public async Task<string> Request(string command) =>
        await Request(command,TimeSpan.FromSeconds(5));

    public async Task<string> Request(string command,TimeSpan timeout) {
        await gate.WaitAsync();
        try { return await RequestLocked(command,timeout); }
        finally { gate.Release(); }
    }

    public Task UploadMedia(byte[] data,IProgress<int>? progress=null,CancellationToken token=default) =>
        UploadBinary($"MEDIA|BEGIN|{data.Length}|{Crc32(data)}",data,12,1_900_000,progress,token);

    public Task UploadScript(int profile,int key,byte[] data,IProgress<int>? progress=null,CancellationToken token=default) {
        if(profile is <0 or >4||key is <0 or >7)throw new ArgumentOutOfRangeException();
        return UploadBinary($"SCRIPT|BEGIN|{profile}|{key}|{data.Length}|{Crc32(data)}",data,6,2048,progress,token);
    }

    public Task UploadIcon(int profile,int key,byte[] data,IProgress<int>? progress=null,CancellationToken token=default) {
        if(profile is <0 or >4||key is <0 or >7)throw new ArgumentOutOfRangeException();
        return UploadBinary($"ICON|BEGIN|{profile}|{key}|{data.Length}|{Crc32(data)}",data,8,8192,progress,token);
    }

    async Task UploadBinary(string begin,byte[] data,int minBytes,int maxBytes,IProgress<int>? progress,CancellationToken token) {
        if(data is null||data.Length<minBytes)throw new ArgumentException("Binary package không hợp lệ.",nameof(data));
        if(data.Length>maxBytes)throw new IOException("Binary package vượt giới hạn.");
        await gate.WaitAsync(token);
        try {
            if(!Connected)throw new IOException("Chưa kết nối thiết bị.");
            var ready=await RequestLocked(begin,TimeSpan.FromSeconds(8));
            if(!ready.StartsWith("READY|",StringComparison.Ordinal))
                throw new IOException("Thiết bị không sẵn sàng nhận binary: "+ready);

            mediaDone=new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            const int chunkSize=512;
            int sent=0;
            while(sent<data.Length) {
                token.ThrowIfCancellationRequested();
                int count=Math.Min(chunkSize,data.Length-sent);
                var ack=new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
                mediaAck=ack;
                await port!.BaseStream.WriteAsync(data.AsMemory(sent,count),token);
                await port.BaseStream.FlushAsync(token);
                int received=await ack.Task.WaitAsync(TimeSpan.FromSeconds(6),token);
                if(received<sent+count) throw new IOException($"ACK binary sai: {received}/{sent+count}");
                sent+=count;
                progress?.Report((int)((long)sent*100/data.Length));
            }
            mediaAck=null;
            string done=await mediaDone.Task.WaitAsync(TimeSpan.FromSeconds(20),token);
            if(done!="OK") throw new IOException("Thiết bị từ chối binary: "+done);
            progress?.Report(100);
        } finally {
            mediaAck=null;
            mediaDone=null;
            gate.Release();
        }
    }

    public static uint Crc32(ReadOnlySpan<byte> data) {
        uint crc=0xFFFFFFFF;
        foreach(byte b in data) {
            crc^=b;
            for(int i=0;i<8;i++) crc=(crc&1)!=0?(crc>>1)^0xEDB88320:crc>>1;
        }
        return crc^0xFFFFFFFF;
    }

    public void Dispose() {
        lifetime?.Cancel();
        lifetime?.Dispose();
        lifetime=null;
        try { port?.Close(); } catch {}
        port?.Dispose();
        port=null;
        foreach(var p in pending)
            if(pending.TryRemove(p.Key,out var request)) request.TrySetCanceled();
        mediaAck?.TrySetCanceled();
        mediaDone?.TrySetCanceled();
        mediaAck=null;
        mediaDone=null;
    }
}

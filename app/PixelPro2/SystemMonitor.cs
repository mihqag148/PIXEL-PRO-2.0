using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace PixelPro2;

public readonly record struct MonitorSnapshot(
    int CpuPercent,int GpuPercent,int RamPercent,int DiskPercent,int NetKbps);

public sealed class SystemMonitorCollector {
    ulong lastIdle,lastKernel,lastUser,lastBytes;
    DateTime lastNet=DateTime.UtcNow;
    bool haveCpu,haveNet;

    [StructLayout(LayoutKind.Sequential)]
    struct FileTimeNative { public uint Low,High; }

    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Auto)]
    struct MemoryStatusEx {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys,AvailPhys,TotalPageFile,AvailPageFile,TotalVirtual,AvailVirtual,AvailExtendedVirtual;
    }

    [DllImport("kernel32.dll",SetLastError=true)]
    static extern bool GetSystemTimes(out FileTimeNative idle,out FileTimeNative kernel,out FileTimeNative user);

    [DllImport("kernel32.dll",SetLastError=true,CharSet=CharSet.Auto)]
    static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx status);

    static ulong Ft(FileTimeNative t)=>((ulong)t.High<<32)|t.Low;

    int Cpu() {
        if(!GetSystemTimes(out var idle,out var kernel,out var user))return 0;
        ulong i=Ft(idle),k=Ft(kernel),u=Ft(user);
        if(!haveCpu){haveCpu=true;lastIdle=i;lastKernel=k;lastUser=u;return 0;}
        ulong di=i-lastIdle,dk=k-lastKernel,du=u-lastUser;
        lastIdle=i;lastKernel=k;lastUser=u;
        ulong total=dk+du;
        if(total==0)return 0;
        double busy=(total>di?total-di:0)/(double)total*100.0;
        return Math.Clamp((int)Math.Round(busy),0,100);
    }

    static int Ram() {
        var s=new MemoryStatusEx{Length=(uint)Marshal.SizeOf<MemoryStatusEx>()};
        return GlobalMemoryStatusEx(ref s)?Math.Clamp((int)s.MemoryLoad,0,100):0;
    }

    static int Disk() {
        try {
            string root=Path.GetPathRoot(Environment.SystemDirectory)??"C:\\";
            var d=new DriveInfo(root);
            if(!d.IsReady||d.TotalSize<=0)return 0;
            return Math.Clamp((int)Math.Round((d.TotalSize-d.AvailableFreeSpace)*100.0/d.TotalSize),0,100);
        } catch { return 0; }
    }

    int NetworkKbps() {
        try {
            ulong bytes=0;
            foreach(var nic in NetworkInterface.GetAllNetworkInterfaces()) {
                if(nic.OperationalStatus!=OperationalStatus.Up)continue;
                if(nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)continue;
                var s=nic.GetIPv4Statistics();
                bytes+=(ulong)Math.Max(0,s.BytesReceived)+(ulong)Math.Max(0,s.BytesSent);
            }
            var now=DateTime.UtcNow;
            if(!haveNet){haveNet=true;lastBytes=bytes;lastNet=now;return 0;}
            double seconds=Math.Max(0.05,(now-lastNet).TotalSeconds);
            ulong delta=bytes>=lastBytes?bytes-lastBytes:0;
            lastBytes=bytes;lastNet=now;
            long kbps=(long)Math.Round(delta*8.0/1000.0/seconds);
            return (int)Math.Clamp(kbps,0,9999);
        } catch { return 0; }
    }

    public MonitorSnapshot Read() => new(Cpu(),0,Ram(),Disk(),NetworkKbps());
}

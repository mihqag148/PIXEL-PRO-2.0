using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using LibreHardwareMonitor.Hardware;

namespace PixelPro2;

public readonly record struct MonitorSnapshot(
    int CpuPercent,int GpuPercent,int RamPercent,int DiskPercent,int NetKbps,
    int CpuTempC,int GpuTempC);

public sealed class SystemMonitorCollector : IDisposable {
    ulong lastIdle,lastKernel,lastUser,lastBytes;
    DateTime lastNet=DateTime.UtcNow;
    bool haveCpu,haveNet;
    Computer? computer;
    readonly UpdateVisitor visitor=new();

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

    sealed class UpdateVisitor : IVisitor {
        public void VisitComputer(IComputer computer)=>computer.Traverse(this);
        public void VisitHardware(IHardware hardware) {
            hardware.Update();
            foreach(var child in hardware.SubHardware)child.Accept(this);
        }
        public void VisitSensor(ISensor sensor) {}
        public void VisitParameter(IParameter parameter) {}
    }

    static ulong Ft(FileTimeNative t)=>((ulong)t.High<<32)|t.Low;

    int CpuFallback() {
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

    void EnsureHardware() {
        if(computer!=null)return;
        try {
            computer=new Computer{
                IsCpuEnabled=true,IsGpuEnabled=true,IsMemoryEnabled=true,
                IsStorageEnabled=true,IsMotherboardEnabled=true
            };
            computer.Open();
        } catch {
            try{computer?.Close();}catch{}
            computer=null;
        }
    }

    static IEnumerable<IHardware> Walk(IHardware h) {
        yield return h;
        foreach(var child in h.SubHardware)
            foreach(var nested in Walk(child))yield return nested;
    }

    (int cpuLoad,int gpuLoad,int cpuTemp,int gpuTemp) HardwareSensors() {
        EnsureHardware();
        if(computer==null)return(0,0,0,0);
        try {
            computer.Accept(visitor);
            double cpuLoad=0,gpuLoad=0,cpuTemp=0,gpuTemp=0;
            foreach(var root in computer.Hardware)foreach(var h in Walk(root)) {
                bool cpu=h.HardwareType==HardwareType.Cpu;
                bool gpu=h.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel;
                if(!cpu&&!gpu)continue;
                foreach(var s in h.Sensors) {
                    if(!s.Value.HasValue)continue;
                    double value=s.Value.Value;
                    if(s.SensorType==SensorType.Load) {
                        if(cpu&&s.Name.Contains("Total",StringComparison.OrdinalIgnoreCase))cpuLoad=Math.Max(cpuLoad,value);
                        if(gpu&&(s.Name.Contains("Core",StringComparison.OrdinalIgnoreCase)||
                                 s.Name.Contains("GPU",StringComparison.OrdinalIgnoreCase)||
                                 s.Name.Contains("D3D",StringComparison.OrdinalIgnoreCase)))
                            gpuLoad=Math.Max(gpuLoad,value);
                    } else if(s.SensorType==SensorType.Temperature) {
                        if(cpu&&(s.Name.Contains("Package",StringComparison.OrdinalIgnoreCase)||
                                 s.Name.Contains("Core Max",StringComparison.OrdinalIgnoreCase)))
                            cpuTemp=Math.Max(cpuTemp,value);
                        if(gpu)gpuTemp=Math.Max(gpuTemp,value);
                    }
                }
            }
            return(
                Math.Clamp((int)Math.Round(cpuLoad),0,100),
                Math.Clamp((int)Math.Round(gpuLoad),0,100),
                Math.Clamp((int)Math.Round(cpuTemp),0,125),
                Math.Clamp((int)Math.Round(gpuTemp),0,125)
            );
        } catch { return(0,0,0,0); }
    }

    public MonitorSnapshot Read() {
        int fallback=CpuFallback();
        var hw=HardwareSensors();
        int cpu=hw.cpuLoad>0?hw.cpuLoad:fallback;
        return new(cpu,hw.gpuLoad,Ram(),Disk(),NetworkKbps(),hw.cpuTemp,hw.gpuTemp);
    }

    public void Dispose() {
        try{computer?.Close();}catch{}
        computer=null;
    }
}

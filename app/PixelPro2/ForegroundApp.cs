using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PixelPro2;

public static class ForegroundApp {
    [DllImport("user32.dll")]
    static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    static extern uint GetWindowThreadProcessId(IntPtr hWnd,out uint processId);

    public static string? ProcessName() {
        try {
            var hwnd=GetForegroundWindow();
            if(hwnd==IntPtr.Zero)return null;
            GetWindowThreadProcessId(hwnd,out uint pid);
            if(pid==0)return null;
            using var p=Process.GetProcessById((int)pid);
            return p.ProcessName;
        } catch {
            return null;
        }
    }
}

using System.Diagnostics;
using System.Runtime.InteropServices;
namespace PixelPro2;
public sealed class MacroRunner {
    bool running;
    [StructLayout(LayoutKind.Sequential)] struct INPUT {public uint type;public UNION data;}
    [StructLayout(LayoutKind.Explicit)] struct UNION {
        [FieldOffset(0)] public KEYBOARD keyboard;
        [FieldOffset(0)] public MOUSE mouse;
    }
    [StructLayout(LayoutKind.Sequential)] struct KEYBOARD {public ushort key,scan;public uint flags,time;public nuint extra;}
    [StructLayout(LayoutKind.Sequential)] struct MOUSE {public int x,y;public uint data,flags,time;public nuint extra;}
    [DllImport("user32.dll",SetLastError=true)] static extern uint SendInput(uint count,INPUT[] inputs,int size);
    static void Send(ushort key,ushort scan,uint flags) {
        var input=new INPUT{type=1,data=new UNION{keyboard=new KEYBOARD{key=key,scan=scan,flags=flags}}};
        if(SendInput(1,[input],Marshal.SizeOf<INPUT>())!=1)throw new IOException("Windows chặn input. Không chạy app đích với quyền cao hơn PIXEL PRO.");
    }
    public async Task Run(Binding binding,CancellationToken token) {
        if(running)return;binding.Validate();running=true;
        try {
            foreach(var step in binding.Steps) {
                token.ThrowIfCancellationRequested();
                switch(step.Type) {
                    case "Delay":await Task.Delay(int.Parse(step.Value),token);break;
                    case "Open":Process.Start(new ProcessStartInfo(step.Value){UseShellExecute=true});break;
                    case "Text":
                        foreach(char c in step.Value) {
                            token.ThrowIfCancellationRequested();Send(0,c,4);Send(0,c,6);
                            await Task.Delay(1,token);
                        }break;
                    case "Shortcut":
                        var codes=Shortcut.Parse(step.Value);var pressed=new List<ushort>();
                        try{foreach(var code in codes){Send(code,0,0);pressed.Add(code);}await Task.Delay(25,token);}
                        finally{foreach(var code in pressed.AsEnumerable().Reverse())Send(code,0,2);}break;
                }
            }
        }finally {running=false;}
    }
}

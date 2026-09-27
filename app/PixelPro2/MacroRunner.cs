using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PixelPro2;

public sealed class MacroRunner {
    bool running;

    [StructLayout(LayoutKind.Sequential)]
    struct INPUT { public uint type; public UNION data; }

    [StructLayout(LayoutKind.Explicit)]
    struct UNION {
        [FieldOffset(0)] public KEYBOARD keyboard;
        [FieldOffset(0)] public MOUSE mouse;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct KEYBOARD { public ushort key,scan; public uint flags,time; public nuint extra; }

    [StructLayout(LayoutKind.Sequential)]
    struct MOUSE { public int x,y; public uint data,flags,time; public nuint extra; }

    [DllImport("user32.dll",SetLastError=true)]
    static extern uint SendInput(uint count,INPUT[] inputs,int size);

    static void SendKey(ushort key,ushort scan,uint flags) {
        var input=new INPUT{type=1,data=new UNION{keyboard=new KEYBOARD{key=key,scan=scan,flags=flags}}};
        if(SendInput(1,[input],Marshal.SizeOf<INPUT>())!=1)
            throw new IOException("Windows chặn input. Không chạy app đích với quyền cao hơn PIXEL PRO.");
    }

    static void SendMouse(int x,int y,uint data,uint flags) {
        var input=new INPUT{type=0,data=new UNION{mouse=new MOUSE{x=x,y=y,data=data,flags=flags}}};
        if(SendInput(1,[input],Marshal.SizeOf<INPUT>())!=1)
            throw new IOException("Windows chặn mouse input.");
    }

    static void Click(string value) {
        static void Pair(uint down,uint up){SendMouse(0,0,0,down);SendMouse(0,0,0,up);}
        switch(MacroValue.Click(value)) {
            case "LEFT":Pair(0x0002,0x0004);break;
            case "RIGHT":Pair(0x0008,0x0010);break;
            case "MIDDLE":Pair(0x0020,0x0040);break;
            case "DOUBLELEFT":Pair(0x0002,0x0004);Pair(0x0002,0x0004);break;
        }
    }

    static void Media(string value) {
        ushort vk=value.Trim().ToUpperInvariant() switch {
            "VOLUP"=>0xAF,"VOLDOWN"=>0xAE,"MUTE"=>0xAD,"PLAYPAUSE"=>0xB3,
            "NEXT"=>0xB0,"PREV"=>0xB1,"STOP"=>0xB2,
            _=>throw new FormatException("Media action không hợp lệ.")
        };
        SendKey(vk,0,0);SendKey(vk,0,2);
    }

    public async Task Run(Binding binding,CancellationToken token,Func<Step,Task>? deviceAction=null) {
        if(running) return;
        binding.Validate();
        running=true;
        var held=new List<ushort>();
        var executionSteps=new List<Step>();
        foreach(var source in binding.Steps) {
            if(source.Type=="Script")executionSteps.AddRange(EezScript.Expand(source.Value,false));
            else executionSteps.Add(source);
        }
        try {
            foreach(var step in executionSteps) {
                token.ThrowIfCancellationRequested();
                switch(step.Type) {
                    case "Delay":
                        await Task.Delay(int.Parse(step.Value),token);
                        break;
                    case "Open":
                    case "Website":
                    case "LaunchApp":
                    case "OpenFolder":
                    case "OpenFile":
                        Process.Start(new ProcessStartInfo(step.Value){UseShellExecute=true});
                        break;
                    case "Text":
                        foreach(char c in step.Value) {
                            token.ThrowIfCancellationRequested();
                            SendKey(0,c,4);
                            SendKey(0,c,6);
                            await Task.Delay(1,token);
                        }
                        break;
                    case "Shortcut": {
                        var codes=Shortcut.Parse(step.Value);
                        var pressed=new List<ushort>();
                        try {
                            foreach(var code in codes){SendKey(code,0,0);pressed.Add(code);}
                            await Task.Delay(25,token);
                        } finally {
                            foreach(var code in pressed.AsEnumerable().Reverse()) SendKey(code,0,2);
                        }
                        break;
                    }
                    case "KeyDown":
                        foreach(var code in Shortcut.Parse(step.Value))
                            if(!held.Contains(code)){SendKey(code,0,0);held.Add(code);}
                        break;
                    case "KeyUp":
                        foreach(var code in Shortcut.Parse(step.Value))
                            if(held.Remove(code)) SendKey(code,0,2);
                        break;
                    case "MouseMove": {
                        var (x,y)=MacroValue.Point(step.Value);
                        SendMouse(x,y,0,0x0001);
                        break;
                    }
                    case "MouseClick":
                        Click(step.Value);
                        break;
                    case "Wheel":
                        SendMouse(0,0,unchecked((uint)MacroValue.Wheel(step.Value)),0x0800);
                        break;
                    case "Media":
                        Media(step.Value);
                        break;
                    case "FunctionalKey": {
                        var codes=Shortcut.Parse(step.Value);
                        var pressed=new List<ushort>();
                        try {
                            foreach(var code in codes){SendKey(code,0,0);pressed.Add(code);}
                            await Task.Delay(25,token);
                        } finally {
                            foreach(var code in pressed.AsEnumerable().Reverse()) SendKey(code,0,2);
                        }
                        break;
                    }
                    case "ChangeProfile":
                    case "DeviceCtrl":
                        if(deviceAction is null)throw new IOException("Action cần PIXEL PRO đang kết nối.");
                        await deviceAction(step);
                        break;
                    case "PowerOff":
                        Process.Start(new ProcessStartInfo("shutdown.exe","/s /t 0"){
                            UseShellExecute=false,
                            CreateNoWindow=true
                        });
                        break;
                }
            }
        } finally {
            foreach(var code in held.AsEnumerable().Reverse()) {
                try{SendKey(code,0,2);}catch{}
            }
            running=false;
        }
    }
}

using System.IO.Ports;

namespace PixelPro2;

public sealed class MainForm : Form {
    readonly Device device=new();
    readonly MacroRunner runner=new();
    readonly CancellationTokenSource shutdown=new();
    Preset preset=new();

    readonly ComboBox ports=new(){Width=115,DropDownStyle=ComboBoxStyle.DropDownList};
    readonly ComboBox profiles=new(){Width=130,DropDownStyle=ComboBoxStyle.DropDownList};
    readonly ComboBox kind=new(){Width=250,DropDownStyle=ComboBoxStyle.DropDownList};
    readonly ComboBox orientation=new(){Width=215,DropDownStyle=ComboBoxStyle.DropDownList};
    readonly TextBox label=new(){Width=230,MaxLength=12};
    readonly NumericUpDown code=new(){Width=90,Maximum=65535};
    readonly NumericUpDown modifiers=new(){Width=90,Maximum=255};
    readonly NumericUpDown color=new(){Width=90,Maximum=65535};
    readonly NumericUpDown brightness=new(){Width=75,Maximum=80,Value=24};
    readonly NumericUpDown saverSeconds=new(){Width=75,Minimum=0,Maximum=3600,Value=30};
    readonly TextBox steps=new(){Multiline=true,Width=500,Height=155,ScrollBars=ScrollBars.Vertical};
    readonly TextBox log=new(){Multiline=true,ReadOnly=true,Dock=DockStyle.Fill,ScrollBars=ScrollBars.Vertical};
    readonly Label status=new(){Text="Chưa kết nối · Có thể soạn preset offline",AutoSize=true,Padding=new Padding(8)};
    readonly Label mediaInfo=new(){Text="Screensaver: chưa đọc",AutoSize=true,Padding=new Padding(6)};
    readonly Label monitorInfo=new(){Text="Monitor: OFF",AutoSize=true,Padding=new Padding(6)};
    readonly CheckBox armed=new(){Text="Cho phép macro trên PC này",AutoSize=true};
    readonly System.Windows.Forms.Timer monitorTimer=new(){Interval=1000};
    readonly SystemMonitorCollector monitorCollector=new();
    readonly ProgressBar transfer=new(){Width=150,Height=23,Minimum=0,Maximum=100};
    readonly Button[] tiles=new Button[8];
    readonly FlowLayoutPanel controls=new(){Dock=DockStyle.Top,AutoSize=true,WrapContents=true,Padding=new Padding(8)};
    readonly NotifyIcon tray=new(){Icon=SystemIcons.Application,Text="PIXEL PRO 2.0"};

    readonly string localPath=Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PixelPro2","preset.json");

    int currentProfile,currentKey;
    bool loading,busy,monitorEnabled,monitorSending;

    public MainForm() {
        Text="PIXEL PRO 2.0 · Studio 2.1";
        MinimumSize=new Size(1120,830);
        Size=new Size(1240,900);
        Font=new Font("Segoe UI",10);
        BackColor=Color.FromArgb(242,244,248);

        var root=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=4,ColumnCount=1};
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,250));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,325));
        root.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        Controls.Add(root);
        root.Controls.Add(controls);

        controls.Controls.Add(new Label{
            Text="PIXEL PRO 2.0",AutoSize=true,
            Font=new Font(Font.FontFamily,13,FontStyle.Bold),
            Padding=new Padding(8,6,8,4)
        });
        controls.Controls.Add(ports);
        Button(controls,"Quét cổng",()=>{RefreshPorts();return Task.CompletedTask;});
        Button(controls,"Tự tìm phím",AutoConnect);
        Button(controls,"Kết nối",Connect);
        Button(controls,"Ngắt",()=>{
            monitorEnabled=false;monitorTimer.Stop();monitorInfo.Text="Monitor: OFF";
            device.Dispose();armed.Checked=false;
            status.Text="Đã ngắt kết nối";
            return Task.CompletedTask;
        });

        profiles.Items.AddRange(Enumerable.Range(1,5).Select(p=>(object)$"Profile {p}").ToArray());
        profiles.SelectedIndex=0;
        controls.Controls.Add(profiles);
        Button(controls,"Đọc thiết bị",ReadDevice);
        Button(controls,"Gửi & lưu",Upload);
        Button(controls,"Nhập preset",Import);
        Button(controls,"Xuất preset",Export);

        orientation.Items.AddRange(new object[]{
            "Hướng gốc","Xoay 180°","Lật ngang","180° + lật ngang"
        });
        orientation.SelectedIndex=1;
        controls.Controls.Add(orientation);
        Button(controls,"Áp dụng hướng",async()=>{
            await device.Request($"DISPLAY|{orientation.SelectedIndex}");
            status.Text="Đã lưu hướng màn hình";
        });
        Button(controls,"Calibrate touch",CalibrateTouch);

        controls.Controls.Add(new Label{Text="Saver(s)",AutoSize=true,Padding=new Padding(5,6,0,0)});
        controls.Controls.Add(saverSeconds);
        Button(controls,"Lưu saver",async()=>{
            await device.Request($"SAVER|{saverSeconds.Value}");
            status.Text=saverSeconds.Value==0?"Đã tắt screensaver":"Đã lưu thời gian screensaver";
        });
        Button(controls,"Tải GIF",UploadGif);
        Button(controls,"Xóa GIF",DeleteGif);
        Button(controls,"Tải icon phím",UploadIcon);
        Button(controls,"Xóa icon phím",DeleteIcon);
        Button(controls,"PC Monitor",ToggleMonitor);
        controls.Controls.Add(monitorInfo);
        controls.Controls.Add(transfer);
        controls.Controls.Add(mediaInfo);
        controls.Controls.Add(status);

        var grid=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=4,RowCount=2,Padding=new Padding(12)};
        for(int c=0;c<4;c++)grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));
        for(int r=0;r<2;r++)grid.RowStyles.Add(new RowStyle(SizeType.Percent,50));
        for(int k=0;k<8;k++) {
            int selected=k;
            tiles[k]=new Button{
                Dock=DockStyle.Fill,FlatStyle=FlatStyle.Flat,Margin=new Padding(7),
                Font=new Font(Font.FontFamily,13,FontStyle.Bold),
                BackColor=Color.White
            };
            tiles[k].FlatAppearance.BorderColor=Color.FromArgb(205,210,220);
            tiles[k].Click+=(_,_)=>Guard(()=>{
                SaveEditor();currentKey=selected;LoadEditor();return Task.CompletedTask;
            });
            grid.Controls.Add(tiles[k],k%4,k/4);
        }
        root.Controls.Add(grid);

        var editor=new FlowLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(16),WrapContents=true,AutoScroll=true};
        root.Controls.Add(editor);

        var fields=new GroupBox{Text="Key assignment",Width=545,Height=285};
        editor.Controls.Add(fields);
        var fieldFlow=new FlowLayoutPanel{
            Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,
            WrapContents=false,Padding=new Padding(10)
        };
        fields.Controls.Add(fieldFlow);

        kind.Items.AddRange(new object[]{
            "K · Phím / tổ hợp HID","C · Media HID","H · Macro qua app",
            "P · Chuyển profile","D · Tắt phím"
        });
        Row(fieldFlow,"Nhãn",label);
        Row(fieldFlow,"Hành động",kind);

        var numeric=new FlowLayoutPanel{Width=510,Height=36};
        fieldFlow.Controls.Add(numeric);
        numeric.Controls.Add(new Label{Text="Mã",AutoSize=true,Padding=new Padding(0,5,0,0)});
        numeric.Controls.Add(code);
        numeric.Controls.Add(new Label{Text="Modifier",AutoSize=true,Padding=new Padding(6,5,0,0)});
        numeric.Controls.Add(modifiers);
        numeric.Controls.Add(new Label{Text="RGB565",AutoSize=true,Padding=new Padding(6,5,0,0)});
        numeric.Controls.Add(color);
        Button(fieldFlow,"Chọn màu",()=>{
            using var dialog=new ColorDialog();
            if(dialog.ShowDialog()==DialogResult.OK)
                color.Value=((dialog.Color.R>>3)<<11)|((dialog.Color.G>>2)<<5)|(dialog.Color.B>>3);
            return Task.CompletedTask;
        });
        fieldFlow.Controls.Add(new Label{
            AutoSize=true,MaximumSize=new Size(500,0),
            Text="HID: A=4…Z=29, Enter=40, F1=58…F12=69 · Modifier Ctrl=1, Shift=2, Alt=4, Win=8.\nMedia: Vol+=233, Vol−=234, Mute=226, Play/Pause=205, Next=181, Prev=182. Profile: mã 0–4."
        });

        var macroBox=new GroupBox{Text="Macro / Stream-Deck action",Width=565,Height=285};
        editor.Controls.Add(macroBox);
        var macro=new FlowLayoutPanel{
            Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,
            WrapContents=false,Padding=new Padding(10)
        };
        macroBox.Controls.Add(macro);
        macro.Controls.Add(new Label{
            AutoSize=true,MaximumSize=new Size(520,0),
            Text="Mỗi dòng: Text|... · Shortcut|CTRL+C · Open|... · Delay|250 · MouseMove|20,-10 · MouseClick|LEFT · Wheel|120 · KeyDown|CTRL · KeyUp|CTRL"
        });
        macro.Controls.Add(steps);
        macro.Controls.Add(armed);

        var lower=new FlowLayoutPanel{Width=520,Height=38};
        macro.Controls.Add(lower);
        lower.Controls.Add(new Label{Text="RGB",AutoSize=true,Padding=new Padding(0,5,0,0)});
        lower.Controls.Add(brightness);
        Button(lower,"Lưu bản nháp",()=>{
            SaveEditor();SaveLocal();RefreshTiles();
            status.Text="Đã lưu bản nháp trên PC";
            return Task.CompletedTask;
        });
        Button(lower,"Thu vào khay",()=>{
            tray.Visible=true;Hide();return Task.CompletedTask;
        });

        root.Controls.Add(log);

        profiles.SelectedIndexChanged+=(_,_)=>Guard(async()=>{
            if(loading)return;
            int next=profiles.SelectedIndex;
            try{SaveEditor();}
            catch{
                loading=true;profiles.SelectedIndex=currentProfile;loading=false;throw;
            }
            currentProfile=next;
            LoadEditor();
            if(device.Connected)await device.Request($"PROFILE|{currentProfile}");
        });

        kind.SelectedIndexChanged+=(_,_)=>{
            if(!loading&&kind.SelectedIndex>=2){code.Value=0;modifiers.Value=0;}
        };

        device.Event+=e=>OnUi(()=>HandleEvent(e));
        device.Disconnected+=e=>OnUi(()=>{
            monitorEnabled=false;monitorTimer.Stop();monitorInfo.Text="Monitor: OFF";
            armed.Checked=false;
            status.Text="Mất kết nối";
            Log(e);
        });

        monitorTimer.Tick+=async (_,_)=>{
            if(!monitorEnabled||monitorSending||!device.Connected)return;
            monitorSending=true;
            try{await SendMonitorFrame();}
            catch(Exception ex){
                monitorEnabled=false;monitorTimer.Stop();monitorInfo.Text="Monitor: lỗi";
                Log("MONITOR: "+ex.Message);
            }finally{monitorSending=false;}
        };

        tray.DoubleClick+=(_,_)=>{
            Show();WindowState=FormWindowState.Normal;Activate();tray.Visible=false;
        };
        var menu=new ContextMenuStrip();
        menu.Items.Add("Mở Studio",null,(_,_)=>{Show();Activate();});
        menu.Items.Add("Thoát",null,(_,_)=>Close());
        tray.ContextMenuStrip=menu;

        FormClosing+=(_,e)=>{
            try{SaveEditor();SaveLocal();}
            catch(Exception ex){
                if(MessageBox.Show(ex.Message+"\nThoát và bỏ thay đổi?","PIXEL PRO",
                   MessageBoxButtons.YesNo)!=DialogResult.Yes){e.Cancel=true;return;}
            }
            monitorEnabled=false;monitorTimer.Stop();monitorTimer.Dispose();
            shutdown.Cancel();device.Dispose();tray.Dispose();
        };

        if(File.Exists(localPath))
            try{preset=Preset.Load(localPath);}
            catch(Exception ex){Log("Không tải được bản nháp: "+ex.Message);}

        LoadEditor();
        RefreshPorts();
    }

    void OnUi(Action action) {
        if(!IsDisposed&&IsHandleCreated)
            try{BeginInvoke(action);}catch(InvalidOperationException){}
    }

    void Button(Control parent,string text,Func<Task> action) {
        var b=new Button{Text=text,AutoSize=true,Height=32};
        b.Click+=(_,_)=>Guard(action);
        parent.Controls.Add(b);
    }

    static void Row(Control parent,string title,Control input) {
        var row=new FlowLayoutPanel{Width=510,Height=36};
        row.Controls.Add(new Label{Text=title,Width=105,Padding=new Padding(0,5,0,0)});
        row.Controls.Add(input);
        parent.Controls.Add(row);
    }

    async void Guard(Func<Task> action) {
        if(busy)return;
        busy=true;
        controls.Enabled=false;
        try{await action();}
        catch(Exception ex){
            status.Text="Thao tác chưa hoàn tất";
            Log(ex.ToString());
            MessageBox.Show(this,ex.Message,"PIXEL PRO",MessageBoxButtons.OK,MessageBoxIcon.Warning);
        } finally {
            busy=false;
            controls.Enabled=true;
        }
    }

    void Log(string text) {
        if(log.TextLength>30000)log.Clear();
        log.AppendText($"{DateTime.Now:HH:mm:ss}  {text}\r\n");
    }

    void RefreshPorts() {
        string? keep=ports.SelectedItem as string;
        ports.Items.Clear();
        ports.Items.AddRange(SerialPort.GetPortNames().Order().Cast<object>().ToArray());
        if(keep!=null&&ports.Items.Contains(keep))ports.SelectedItem=keep;
        else if(ports.Items.Count>0)ports.SelectedIndex=0;
    }

    void SaveEditor() {
        if(loading||kind.SelectedIndex<0)return;
        var b=new Binding{
            Type="KCHPD"[kind.SelectedIndex].ToString(),
            Code=(int)code.Value,
            Modifiers=(int)modifiers.Value,
            Color=(int)color.Value,
            Label=label.Text
        };
        foreach(var line in steps.Lines.Where(l=>!string.IsNullOrWhiteSpace(l))) {
            int split=line.IndexOf('|');
            if(split<1)throw new FormatException("Mỗi bước macro cần Type|Value.");
            b.Steps.Add(new Step{Type=line[..split].Trim(),Value=line[(split+1)..]});
        }
        b.Validate();
        preset.Profiles[currentProfile][currentKey]=b;
    }

    void LoadEditor() {
        loading=true;
        var b=preset.Profiles[currentProfile][currentKey];
        kind.SelectedIndex="KCHPD".IndexOf(b.Type);
        label.Text=b.Label;
        code.Value=b.Code;
        modifiers.Value=b.Modifiers;
        color.Value=b.Color;
        steps.Lines=b.Steps.Select(s=>$"{s.Type}|{s.Value}").ToArray();
        profiles.SelectedIndex=currentProfile;
        loading=false;
        RefreshTiles();
    }

    void RefreshTiles() {
        for(int k=0;k<8;k++) {
            var b=preset.Profiles[currentProfile][k];
            tiles[k].Text=$"K{k+1}   {b.Label}\n{ActionText(b)}";
            tiles[k].BackColor=k==currentKey?Color.FromArgb(214,233,255):Color.White;
        }
    }

    static string ActionText(Binding b)=>b.Type switch {
        "K"=>$"HID {b.Modifiers}+{b.Code}",
        "C"=>$"Media {b.Code}",
        "H"=>"Macro / App",
        "P"=>$"Profile {b.Code+1}",
        _=>"Disabled"
    };

    void SaveLocal() {
        Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
        preset.Save(localPath);
    }

    async Task AfterConnect() {
        armed.Checked=false;
        if(int.TryParse(await device.Request("PANEL"),out int mode)&&mode is >=0 and <=3)
            orientation.SelectedIndex=mode;
        await RefreshMediaInfo();
        status.Text=$"Đã kết nối {device.PortName} · PIXEL PRO 2.0";
        Log($"Handshake OK trên {device.PortName}.");
    }

    async Task Connect() {
        SaveEditor();
        if(ports.SelectedItem is not string port)
            throw new IOException("Không có cổng COM. Cắm thiết bị rồi quét lại.");
        await device.Connect(port);
        await AfterConnect();
    }

    async Task AutoConnect() {
        SaveEditor();
        RefreshPorts();
        var names=SerialPort.GetPortNames().Order(StringComparer.OrdinalIgnoreCase).ToArray();
        if(names.Length==0)throw new IOException("Windows chưa thấy cổng COM nào.");
        Exception? last=null;
        foreach(string name in names) {
            try {
                status.Text=$"Đang thử {name}…";
                await device.Connect(name);
                if(ports.Items.Contains(name))ports.SelectedItem=name;
                await AfterConnect();
                return;
            } catch(Exception ex) {
                last=ex;
                Log($"{name}: {ex.Message}");
            }
        }
        throw new IOException("Không tìm thấy PIXEL PRO 2.0 trên các cổng COM.",last);
    }

    async Task ReadDevice() {
        SaveEditor();
        var next=new Preset();
        for(int p=0;p<5;p++)for(int k=0;k<8;k++) {
            var b=Binding.Parse(await device.Request($"GET|{p}|{k}"));
            b.Steps=preset.Profiles[p][k].Steps;
            next.Profiles[p][k]=b;
        }
        var state=(await device.Request("STATE")).Split('|');
        if(state.Length<2||!int.TryParse(state[0],out int active)||active is <0 or >4||
           !int.TryParse(state[1],out int light)||light is <0 or >80)
            throw new IOException("STATE không hợp lệ.");
        preset=next;
        currentProfile=active;
        brightness.Value=light;
        if(state.Length>=3&&int.TryParse(state[2],out int saver)&&saver is >=0 and <=3600)
            saverSeconds.Value=saver;
        LoadEditor();
        SaveLocal();
        await RefreshMediaInfo();
        status.Text="Đã đọc 5 profile từ thiết bị";
    }

    async Task Upload() {
        SaveEditor();
        preset.Validate();
        armed.Checked=false;
        SaveLocal();
        for(int p=0;p<5;p++)for(int k=0;k<8;k++)
            await device.Request(preset.Profiles[p][k].Wire(p,k));
        await device.Request($"RGB|{brightness.Value}");
        await device.Request($"SAVER|{saverSeconds.Value}");
        await device.Request("SAVE");
        await device.Request($"PROFILE|{currentProfile}");
        status.Text="Đã lưu toàn bộ keymap/profile vào thiết bị";
        RefreshTiles();
    }

    Task Import() {
        using var dialog=new OpenFileDialog{Filter="PIXEL PRO preset|*.json"};
        if(dialog.ShowDialog()==DialogResult.OK) {
            var next=Preset.Load(dialog.FileName);
            preset=next;armed.Checked=false;LoadEditor();SaveLocal();
            status.Text="Đã nhập preset · Chưa gửi vào thiết bị";
        }
        return Task.CompletedTask;
    }

    Task Export() {
        SaveEditor();
        using var dialog=new SaveFileDialog{
            Filter="PIXEL PRO preset|*.json",
            FileName="pixel-pro-2-preset.json"
        };
        if(dialog.ShowDialog()==DialogResult.OK)preset.Save(dialog.FileName);
        return Task.CompletedTask;
    }

    async Task ToggleMonitor() {
        if(monitorEnabled) {
            monitorEnabled=false;
            monitorTimer.Stop();
            if(device.Connected)await device.Request("MONITOR|OFF");
            monitorInfo.Text="Monitor: OFF";
            status.Text="Đã trở về giao diện phím";
            return;
        }
        if(!device.Connected)throw new IOException("Kết nối PIXEL PRO trước khi bật PC Monitor.");
        monitorEnabled=true;
        await SendMonitorFrame();
        monitorTimer.Start();
        status.Text="PC Monitor fullscreen đang chạy";
    }

    async Task SendMonitorFrame() {
        var s=monitorCollector.Read();
        await device.Request($"MONITOR|SET|{s.CpuPercent}|{s.GpuPercent}|{s.RamPercent}|{s.DiskPercent}|{s.NetKbps}");
        monitorInfo.Text=$"Monitor: CPU {s.CpuPercent}% · RAM {s.RamPercent}% · NET {s.NetKbps} kbps";
    }

    async Task CalibrateTouch() {
        var rsp=await device.Request("TOUCHCAL|START");
        if(rsp!="STARTED")throw new IOException("Firmware không vào calibration.");
        status.Text="Touch calibration: chạm lần lượt 4 dấu + trên màn hình";
        MessageBox.Show(this,
            "Chạm chính xác vào dấu + đang hiện trên màn hình.\nThứ tự: trên-trái → trên-phải → dưới-phải → dưới-trái.",
            "Touch calibration",MessageBoxButtons.OK,MessageBoxIcon.Information);
    }

    async Task UploadIcon() {
        using var dialog=new OpenFileDialog{Filter="Image|*.png;*.jpg;*.jpeg;*.bmp;*.gif"};
        if(dialog.ShowDialog()!=DialogResult.OK)return;
        if(!device.Connected)throw new IOException("Kết nối PIXEL PRO trước khi tải icon.");
        status.Text=$"Đang xử lý icon P{currentProfile+1} K{currentKey+1}…";
        transfer.Value=0;
        byte[] data=await Task.Run(()=>MediaCodec.FromIcon(dialog.FileName));
        var progress=new Progress<int>(v=>transfer.Value=Math.Clamp(v,0,100));
        await device.UploadIcon(currentProfile,currentKey,data,progress,shutdown.Token);
        status.Text=$"Đã lưu icon P{currentProfile+1} K{currentKey+1} vào flash";
    }

    async Task DeleteIcon() {
        await device.Request($"ICON|DELETE|{currentProfile}|{currentKey}");
        transfer.Value=0;
        status.Text=$"Đã xóa icon P{currentProfile+1} K{currentKey+1}";
    }

    async Task UploadGif() {
        using var dialog=new OpenFileDialog{Filter="Animated GIF|*.gif"};
        if(dialog.ShowDialog()!=DialogResult.OK)return;
        if(!device.Connected)throw new IOException("Kết nối PIXEL PRO trước khi tải GIF.");
        status.Text="Đang chuyển GIF sang RGB332…";
        transfer.Value=0;
        var package=await Task.Run(()=>MediaCodec.FromGif(dialog.FileName));
        mediaInfo.Text=$"GIF: {package.Frames} frame · {package.DelayMs} ms · {package.Data.Length/1024} KB";
        var progress=new Progress<int>(v=>transfer.Value=Math.Clamp(v,0,100));
        status.Text="Đang truyền GIF…";
        await device.UploadMedia(package.Data,progress,shutdown.Token);
        await RefreshMediaInfo();
        status.Text="Đã lưu GIF vào flash · sẽ chạy khi idle";
    }

    async Task DeleteGif() {
        await device.Request("MEDIA|DELETE");
        transfer.Value=0;
        await RefreshMediaInfo();
        status.Text="Đã xóa screensaver GIF";
    }

    async Task RefreshMediaInfo() {
        try {
            var info=await device.Request("MEDIA|INFO");
            if(info=="ABSENT")mediaInfo.Text="Screensaver: chưa có GIF";
            else if(info.StartsWith("READY|")) {
                var t=info.Split('|');
                mediaInfo.Text=t.Length>=6
                    ?$"Screensaver: {int.Parse(t[1])/1024} KB · {t[2]}×{t[3]} · {t[4]} frame · {t[5]} ms"
                    :"Screensaver: READY";
            } else mediaInfo.Text="Screensaver: "+info;
        } catch(Exception ex) {
            mediaInfo.Text="Screensaver: không đọc được";
            Log("MEDIA INFO: "+ex.Message);
        }
    }

    async void HandleEvent(string text) {
        var t=text.Split('|');
        if(t.Length==4&&t[1]=="HOST"&&armed.Checked&&!busy&&
           int.TryParse(t[2],out int p)&&p is >=0 and <5&&
           int.TryParse(t[3],out int k)&&k is >=0 and <8) {
            var binding=preset.Profiles[p][k];
            if(binding.Type!="H")return;
            try{await runner.Run(binding,shutdown.Token);}
            catch(OperationCanceledException){}
            catch(Exception ex){Log(ex.Message);}
        } else if(t.Length==3&&t[1]=="PROFILE") {
            Log($"Thiết bị chuyển sang profile {int.Parse(t[2])+1}.");
        } else if(t.Length>=2&&t[1]=="CALDONE") {
            status.Text="Touch calibration hoàn tất và đã lưu";
            Log(text);
        } else if(t.Length>=2&&t[1]=="CALFAIL") {
            status.Text="Touch calibration thất bại · thử lại";
            Log(text);
        } else if(t.Length>=2&&t[1]=="TOUCH") {
            Log(text);
        }
    }
}

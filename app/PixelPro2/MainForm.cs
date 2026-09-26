using System.IO.Ports;
namespace PixelPro2;
public sealed class MainForm : Form {
    readonly Device device=new();
    readonly MacroRunner runner=new();
    readonly CancellationTokenSource shutdown=new();
    Preset preset=new();
    readonly ComboBox ports=new(){Width=120,DropDownStyle=ComboBoxStyle.DropDownList};
    readonly ComboBox profiles=new(){Width=160,DropDownStyle=ComboBoxStyle.DropDownList};
    readonly ComboBox kind=new(){Width=260,DropDownStyle=ComboBoxStyle.DropDownList};
    readonly ComboBox orientation=new(){Width=230,DropDownStyle=ComboBoxStyle.DropDownList};
    readonly TextBox label=new(){Width=240,MaxLength=12};
    readonly NumericUpDown code=new(){Width=100,Maximum=65535};
    readonly NumericUpDown modifiers=new(){Width=100,Maximum=255};
    readonly NumericUpDown color=new(){Width=100,Maximum=65535};
    readonly NumericUpDown brightness=new(){Width=80,Maximum=80,Value=24};
    readonly TextBox steps=new(){Multiline=true,Width=490,Height=130,ScrollBars=ScrollBars.Vertical};
    readonly TextBox log=new(){Multiline=true,ReadOnly=true,Dock=DockStyle.Fill,ScrollBars=ScrollBars.Vertical};
    readonly Label status=new(){Text="Chưa kết nối · Có thể soạn preset offline",AutoSize=true,Padding=new Padding(8)};
    readonly CheckBox armed=new(){Text="Cho phép macro trên PC này",AutoSize=true};
    readonly Button[] tiles=new Button[8];
    readonly FlowLayoutPanel controls=new(){Dock=DockStyle.Top,AutoSize=true,WrapContents=true,Padding=new Padding(8)};
    readonly NotifyIcon tray=new(){Icon=SystemIcons.Application,Text="PIXEL PRO 2.0"};
    readonly string localPath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"PixelPro2","preset.json");
    int currentProfile,currentKey;
    bool loading,busy;
    public MainForm() {
        Text="PIXEL PRO 2.0 · Studio";MinimumSize=new Size(1080,800);Size=new Size(1180,850);
        Font=new Font("Segoe UI",10);BackColor=Color.FromArgb(243,245,249);
        var root=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=4,ColumnCount=1};
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));root.RowStyles.Add(new RowStyle(SizeType.Absolute,260));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,290));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));Controls.Add(root);
        root.Controls.Add(controls);
        controls.Controls.Add(new Label{Text="PIXEL PRO 2.0",AutoSize=true,Font=new Font(Font,FontStyle.Bold),Padding=new Padding(8)});
        controls.Controls.Add(ports);Button(controls,"Quét cổng",()=>{RefreshPorts();return Task.CompletedTask;});
        Button(controls,"Kết nối",Connect);Button(controls,"Ngắt kết nối",()=>{device.Dispose();armed.Checked=false;status.Text="Đã ngắt kết nối";return Task.CompletedTask;});
        profiles.Items.AddRange(Enumerable.Range(1,5).Select(p=>(object)$"Profile {p}").ToArray());profiles.SelectedIndex=0;controls.Controls.Add(profiles);
        Button(controls,"Đọc thiết bị",ReadDevice);Button(controls,"Gửi & lưu",Upload);
        Button(controls,"Nhập preset",Import);Button(controls,"Xuất preset",Export);
        orientation.Items.AddRange(new object[]{"Màn hình: hướng bản 2.0.0","Màn hình: xoay 180°","Màn hình: lật ngang","Màn hình: 180° + lật ngang"});
        orientation.SelectedIndex=1;controls.Controls.Add(orientation);
        Button(controls,"Áp dụng hướng",async()=> {
            await device.Request($"DISPLAY|{orientation.SelectedIndex}");
            status.Text="Đã lưu hướng màn hình và cảm ứng";
        });
        controls.Controls.Add(status);
        var grid=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=4,RowCount=2,Padding=new Padding(12)};
        for(int c=0;c<4;c++)grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));
        for(int r=0;r<2;r++)grid.RowStyles.Add(new RowStyle(SizeType.Percent,50));
        for(int k=0;k<8;k++) {
            int selected=k;tiles[k]=new Button{Dock=DockStyle.Fill,FlatStyle=FlatStyle.Flat,Margin=new Padding(6),Font=new Font(Font.FontFamily,14,FontStyle.Bold)};
            tiles[k].Click+=(_,_)=>Guard(()=>{SaveEditor();currentKey=selected;LoadEditor();return Task.CompletedTask;});grid.Controls.Add(tiles[k],k%4,k/4);
        }root.Controls.Add(grid);
        var editor=new FlowLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(16),WrapContents=true,AutoScroll=true};root.Controls.Add(editor);
        var fields=new FlowLayoutPanel{Width=540,Height=240,FlowDirection=FlowDirection.TopDown,WrapContents=false};editor.Controls.Add(fields);
        kind.Items.AddRange(new object[]{"K · Phím / tổ hợp HID","C · Media HID","H · Macro qua app","P · Chuyển profile","D · Tắt phím"});
        Row(fields,"Nhãn",label);Row(fields,"Hành động",kind);
        var numeric=new FlowLayoutPanel{Width=530,Height=40};fields.Controls.Add(numeric);
        numeric.Controls.Add(new Label{Text="Mã",AutoSize=true});numeric.Controls.Add(code);
        numeric.Controls.Add(new Label{Text="Modifier",AutoSize=true});numeric.Controls.Add(modifiers);
        numeric.Controls.Add(new Label{Text="RGB565",AutoSize=true});numeric.Controls.Add(color);
        Button(fields,"Chọn màu",()=>{using var dialog=new ColorDialog();if(dialog.ShowDialog()==DialogResult.OK)color.Value=((dialog.Color.R>>3)<<11)|((dialog.Color.G>>2)<<5)|(dialog.Color.B>>3);return Task.CompletedTask;});
        fields.Controls.Add(new Label{AutoSize=true,Text="Mã HID: A=4…Z=29, Enter=40, F1=58…F12=69\nModifier: Ctrl=1, Shift=2, Alt=4, Win=8 (cộng các giá trị)\nMedia: Vol+=233, Vol−=234, Mute=226, Play/Pause=205\nChuyển profile: mã 0–4. Macro/Tắt: mã=0, modifier=0."});
        var macro=new FlowLayoutPanel{Width=520,Height=255,FlowDirection=FlowDirection.TopDown,WrapContents=false};editor.Controls.Add(macro);
        macro.Controls.Add(new Label{AutoSize=true,Text="Macro · mỗi dòng: Text|nội dung / Shortcut|CTRL+C\nOpen|https://... hoặc đường dẫn / Delay|250"});macro.Controls.Add(steps);
        macro.Controls.Add(armed);
        var lower=new FlowLayoutPanel{Width=510,Height=40};macro.Controls.Add(lower);lower.Controls.Add(new Label{Text="Độ sáng RGB",AutoSize=true});lower.Controls.Add(brightness);
        Button(lower,"Lưu bản nháp",()=>{SaveEditor();SaveLocal();RefreshTiles();status.Text="Đã lưu bản nháp trên PC";return Task.CompletedTask;});
        Button(lower,"Thu vào khay",()=>{tray.Visible=true;Hide();return Task.CompletedTask;});
        root.Controls.Add(log);
        profiles.SelectedIndexChanged+=(_,_)=>Guard(async()=> {
            if(loading)return;
            int next=profiles.SelectedIndex;
            try{SaveEditor();}catch {loading=true;profiles.SelectedIndex=currentProfile;loading=false;throw;}
            currentProfile=next;LoadEditor();if(device.Connected)await device.Request($"PROFILE|{currentProfile}");
        });
        kind.SelectedIndexChanged+=(_,_)=>{if(!loading&&kind.SelectedIndex>=2){code.Value=0;modifiers.Value=0;}};
        device.Event+=e=>OnUi(()=>HandleEvent(e));
        device.Disconnected+=e=>OnUi(()=>{armed.Checked=false;status.Text="Mất kết nối";Log(e);});
        tray.DoubleClick+=(_,_)=>{Show();WindowState=FormWindowState.Normal;Activate();tray.Visible=false;};
        var menu=new ContextMenuStrip();menu.Items.Add("Mở Studio",null,(_,_)=>{Show();Activate();});menu.Items.Add("Thoát",null,(_,_)=>Close());tray.ContextMenuStrip=menu;
        FormClosing+=(_,e)=> {
            try{SaveEditor();SaveLocal();}catch(Exception ex){if(MessageBox.Show(ex.Message+"\nThoát và bỏ thay đổi?","PIXEL PRO",MessageBoxButtons.YesNo)!=DialogResult.Yes){e.Cancel=true;return;}}
            shutdown.Cancel();device.Dispose();tray.Dispose();
        };
        if(File.Exists(localPath))try{preset=Preset.Load(localPath);}catch(Exception ex){Log("Không tải được bản nháp: "+ex.Message);}
        LoadEditor();RefreshPorts();
    }
    void OnUi(Action action){if(!IsDisposed&&IsHandleCreated)try{BeginInvoke(action);}catch(InvalidOperationException){}}
    void Button(Control parent,string text,Func<Task> action){var b=new Button{Text=text,AutoSize=true,Height=32};b.Click+=(_,_)=>Guard(action);parent.Controls.Add(b);}
    static void Row(Control parent,string title,Control input){var row=new FlowLayoutPanel{Width=530,Height=36};row.Controls.Add(new Label{Text=title,Width=110});row.Controls.Add(input);parent.Controls.Add(row);}
    async void Guard(Func<Task> action) {
        if(busy)return;busy=true;controls.Enabled=false;
        try{await action();}catch(Exception ex){status.Text="Thao tác chưa hoàn tất";Log(ex.Message);MessageBox.Show(this,ex.Message,"PIXEL PRO",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
        finally{busy=false;controls.Enabled=true;}
    }
    void Log(string text){if(log.TextLength>20000)log.Clear();log.AppendText($"{DateTime.Now:HH:mm:ss}  {text}\r\n");}
    void RefreshPorts(){ports.Items.Clear();ports.Items.AddRange(SerialPort.GetPortNames().Order().Cast<object>().ToArray());if(ports.Items.Count>0)ports.SelectedIndex=0;}
    void SaveEditor() {
        if(loading||kind.SelectedIndex<0)return;
        var b=new Binding{Type="KCHPD"[kind.SelectedIndex].ToString(),Code=(int)code.Value,Modifiers=(int)modifiers.Value,Color=(int)color.Value,Label=label.Text};
        foreach(var line in steps.Lines.Where(l=>!string.IsNullOrWhiteSpace(l))) {
            int split=line.IndexOf('|');if(split<1)throw new FormatException("Mỗi bước macro cần Type|Value.");
            b.Steps.Add(new Step{Type=line[..split].Trim(),Value=line[(split+1)..]});
        }
        b.Validate();preset.Profiles[currentProfile][currentKey]=b;
    }
    void LoadEditor() {
        loading=true;var b=preset.Profiles[currentProfile][currentKey];
        kind.SelectedIndex="KCHPD".IndexOf(b.Type);label.Text=b.Label;code.Value=b.Code;modifiers.Value=b.Modifiers;color.Value=b.Color;
        steps.Lines=b.Steps.Select(s=>$"{s.Type}|{s.Value}").ToArray();profiles.SelectedIndex=currentProfile;loading=false;RefreshTiles();
    }
    void RefreshTiles() {
        for(int k=0;k<8;k++){var b=preset.Profiles[currentProfile][k];tiles[k].Text=$"{k+1}  {b.Label}\n{b.Type}";tiles[k].BackColor=k==currentKey?Color.FromArgb(210,232,255):Color.White;}
    }
    void SaveLocal(){Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);preset.Save(localPath);}
    async Task Connect() {
        SaveEditor();if(ports.SelectedItem is not string port)throw new IOException("Không có cổng COM. Cắm thiết bị rồi quét lại.");
        await device.Connect(port);armed.Checked=false;
        try {
            if(int.TryParse(await device.Request("PANEL"),out int mode)&&mode is >=0 and <=3)orientation.SelectedIndex=mode;
        }catch(IOException){Log("Firmware chưa hỗ trợ chỉnh hướng; cần bản 2.0.1 trở lên.");}
        status.Text="Đã kết nối · Chọn Đọc thiết bị hoặc Gửi & lưu";Log("Đã nhận diện PIXEL PRO 2.0; chưa thay đổi preset.");
    }
    async Task ReadDevice() {
        SaveEditor();var next=new Preset();
        for(int p=0;p<5;p++)for(int k=0;k<8;k++) {
            var b=Binding.Parse(await device.Request($"GET|{p}|{k}"));
            b.Steps=preset.Profiles[p][k].Steps;next.Profiles[p][k]=b;
        }
        var state=(await device.Request("STATE")).Split('|');
        if(state.Length!=2||!int.TryParse(state[0],out int active)||active is <0 or >4||!int.TryParse(state[1],out int light)||light is <0 or >80)
            throw new IOException("STATE không hợp lệ.");
        preset=next;currentProfile=active;brightness.Value=light;LoadEditor();SaveLocal();
        status.Text="Đã đọc 5 profile · Macro được lưu trên PC";
    }
    async Task Upload() {
        SaveEditor();preset.Validate();armed.Checked=false;SaveLocal();
        for(int p=0;p<5;p++)for(int k=0;k<8;k++)await device.Request(preset.Profiles[p][k].Wire(p,k));
        await device.Request($"RGB|{brightness.Value}");await device.Request("SAVE");
        await device.Request($"PROFILE|{currentProfile}");status.Text="Đã lưu 5 profile vào thiết bị";RefreshTiles();
    }
    Task Import() {
        using var dialog=new OpenFileDialog{Filter="PIXEL PRO preset|*.json"};
        if(dialog.ShowDialog()==DialogResult.OK){var next=Preset.Load(dialog.FileName);preset=next;armed.Checked=false;LoadEditor();SaveLocal();status.Text="Đã nhập preset · Chưa gửi vào thiết bị";}
        return Task.CompletedTask;
    }
    Task Export() {
        SaveEditor();using var dialog=new SaveFileDialog{Filter="PIXEL PRO preset|*.json",FileName="pixel-pro-2-preset.json"};
        if(dialog.ShowDialog()==DialogResult.OK)preset.Save(dialog.FileName);return Task.CompletedTask;
    }
    async void HandleEvent(string text) {
        var t=text.Split('|');
        if(t.Length==4&&t[1]=="HOST"&&armed.Checked&&!busy&&int.TryParse(t[2],out int p)&&p is >=0 and <5&&int.TryParse(t[3],out int k)&&k is >=0 and <8) {
            var binding=preset.Profiles[p][k];if(binding.Type!="H")return;
            try{await runner.Run(binding,shutdown.Token);}catch(OperationCanceledException){}catch(Exception ex){Log(ex.Message);}
        }else if(t.Length==3&&t[1]=="PROFILE") {Log($"Thiết bị đang dùng profile {t[2]} (chỉ số 0–4).");}
    }
}

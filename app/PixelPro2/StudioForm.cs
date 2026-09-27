using System.Diagnostics;
using System.IO.Ports;

namespace PixelPro2;

public sealed class StudioForm : Form {
    sealed record ActionDef(string Name,string Type,string DefaultValue,string Hint,bool HidCapable);
    sealed record KeyDrag(int Profile,int Key);
    sealed record ProfileDrag(int Profile);
    sealed record DeviceEntry(string Port,bool Connected) {
        public override string ToString()=>Connected?$"{Port}    Connected":$"{Port}    Available";
    }
    sealed class StepItem {
        public Step Step { get; }
        public StepItem(Step step){Step=step;}
        public override string ToString()=>StepText(Step);
    }

    static readonly ActionDef[] ActionCatalog=[
        new("Access Website","Website","https://","Open a web URL. Requires Studio.",false),
        new("Launch APP","LaunchApp",@"C:\","Launch an EXE/app. Requires Studio.",false),
        new("Open Folder","OpenFolder",@"C:\","Open a Windows folder. Requires Studio.",false),
        new("Open File","OpenFile",@"C:\","Open a file with its default app. Requires Studio.",false),
        new("Input Text","Text","Hello","HID capable. ASCII text, up to 96 chars per action.",true),
        new("Shortcut","Shortcut","CTRL+C","HID capable: modifiers + one standard key.",true),
        new("Wait","Delay","100","HID capable. Delay 0..30000 ms.",true),
        new("Mouse Move","MouseMove","20,0","Relative mouse movement. Requires Studio.",false),
        new("Mouse Click","MouseClick","LEFT","HID capable: LEFT / RIGHT / MIDDLE / DOUBLELEFT.",true),
        new("Mouse Wheel","Wheel","1","HID capable: -127..127 for native mode.",true),
        new("Media Control","Media","PLAYPAUSE","HID: VOLUP/VOLDOWN/MUTE/PLAYPAUSE/NEXT/PREV/STOP.",true),
        new("Change Profile","ChangeProfile","1",$"HID capable. Profile 1..{DeviceLimits.Profiles}.",true),
        new("Functional Key","FunctionalKey","F1","HID capable. Example: HOME, PAGEUP, F1.",true),
        new("Device Control","DeviceCtrl","PROFILE_NEXT","HID: PROFILE_NEXT/PROFILE_PREV. MONITOR_TOGGLE needs Studio.",true),
        new("Power Off Computer","PowerOff","","Windows host action. Requires Studio.",false)
    ];

    readonly DeviceHub device=new();
    readonly MacroRunner runner=new();
    readonly CancellationTokenSource shutdown=new();
    readonly SystemMonitorCollector monitorCollector=new();
    readonly MusicPlugin musicPlugin=new();

    Preset preset=new();
    int currentProfile,currentKey,activeProfile;
    int keyColor=1215;
    bool loading,busy,monitorEnabled,monitorSending,musicEnabled,musicSending,autoSwitching,dark,touchDiagEnabled;
    string lastMusicPayload="";

    readonly ListBox deviceList=new(){Dock=DockStyle.Fill,IntegralHeight=false};
    readonly ListBox toolbox=new(){Dock=DockStyle.Fill,IntegralHeight=false};
    readonly ListBox sequence=new(){Dock=DockStyle.Fill,IntegralHeight=false,AllowDrop=true};
    readonly Button[] profileButtons=new Button[DeviceLimits.Profiles];
    readonly Button[] keyButtons=new Button[8];

    readonly TextBox alias=new(){Width=230,MaxLength=12};
    readonly TextBox stepValue=new(){Dock=DockStyle.Fill};
    readonly Label stepHint=new(){AutoSize=true,MaximumSize=new Size(650,0),ForeColor=Color.DimGray};
    readonly Label status=new(){Text="Offline · presets can be edited",AutoSize=true,Padding=new Padding(8,5,8,5)};
    readonly Label modeInfo=new(){AutoSize=true,Padding=new Padding(6,5,6,5)};
    readonly Label mediaInfo=new(){AutoSize=true,Text="Screensaver: not read"};
    readonly Label monitorInfo=new(){AutoSize=true,Text="PC Monitor: OFF"};
    readonly Label musicInfo=new(){AutoSize=true,Text="Music Player: OFF"};
    readonly Label pluginStatus=new(){AutoSize=true,Padding=new Padding(8,5,8,5),Text="Plugins: PC Monitor OFF · Music OFF"};
    readonly Label touchInfo=new(){AutoSize=true,Text="Touch: --"};
    readonly TextBox log=new(){Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical};

    readonly CheckBox hidMode=new(){Text="HID Mode · runs without Studio",AutoSize=true,Checked=true};
    readonly CheckBox armed=new(){Text="Allow App-mode actions on this PC",AutoSize=true,Checked=true};
    readonly CheckBox autoProfileEnabled=new(){Text="Enable dynamic profile switching",AutoSize=true};
    readonly CheckBox monitorPluginToggle=new(){Text="PC Monitoring Plugin",AutoSize=true};
    readonly CheckBox musicPluginToggle=new(){Text="Music Player Plugin (SMTC)",AutoSize=true};

    readonly NumericUpDown brightness=new(){Minimum=0,Maximum=80,Value=24,Width=80};
    readonly NumericUpDown saverSeconds=new(){Minimum=0,Maximum=3600,Value=30,Width=90};
    readonly ComboBox screenOff=new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=150};
    readonly ComboBox language=new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=125};
    readonly ComboBox orientation=new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=220};
    readonly ProgressBar transfer=new(){Width=260,Height=22,Minimum=0,Maximum=100};
    readonly DataGridView autoGrid=new(){
        Dock=DockStyle.Fill,AllowUserToAddRows=false,AllowUserToDeleteRows=false,
        RowHeadersVisible=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill
    };

    readonly System.Windows.Forms.Timer monitorTimer=new(){Interval=1000};
    readonly System.Windows.Forms.Timer musicTimer=new(){Interval=1000};
    readonly System.Windows.Forms.Timer autoProfileTimer=new(){Interval=600};
    readonly NotifyIcon tray=new(){Icon=SystemIcons.Application,Text="PIXEL PRO 2.0"};
    readonly Dictionary<Control,string> languageBase=new();
    string languageCode="en";

    readonly string localPath=Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PixelPro2","preset-v4.json");
    readonly string legacyLocalPathV3=Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PixelPro2","preset-v3.json");
    readonly string legacyLocalPathV2=Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PixelPro2","preset.json");
    readonly string languagePath=Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PixelPro2","language.txt");

    public StudioForm() {
        Text="PIXEL PRO 2.0 · Studio 2.4";
        MinimumSize=new Size(1180,860);
        Size=new Size(1360,940);
        StartPosition=FormStartPosition.CenterScreen;
        Font=new Font("Segoe UI",10);
        BackColor=Color.FromArgb(245,246,248);

        orientation.Items.AddRange(["Hướng gốc","Xoay 180°","Lật ngang","180° + lật ngang"]);
        orientation.SelectedIndex=0;
        screenOff.Items.AddRange(["Always On","30 seconds","5 minutes","15 minutes"]);
        screenOff.SelectedIndex=0;
        language.Items.AddRange(["English","Tiếng Việt","简体中文"]);
        language.SelectedIndex=0;

        var root=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=3,ColumnCount=1,Margin=Padding.Empty};
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,62));
        root.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,34));
        Controls.Add(root);

        root.Controls.Add(BuildHeader(),0,0);

        var split=new SplitContainer{
            Dock=DockStyle.Fill,Orientation=Orientation.Vertical,
            SplitterDistance=365,FixedPanel=FixedPanel.Panel1,IsSplitterFixed=false
        };
        split.Panel1.Padding=new Padding(10);
        split.Panel2.Padding=new Padding(4,10,10,10);
        split.Panel1.Controls.Add(BuildLeftPane());
        split.Panel2.Controls.Add(BuildRightPane());
        root.Controls.Add(split,0,1);

        var footer=new Panel{Dock=DockStyle.Fill,Padding=new Padding(6,0,6,0)};
        status.Dock=DockStyle.Left;
        footer.Controls.Add(status);
        modeInfo.Dock=DockStyle.Right;
        footer.Controls.Add(modeInfo);
        pluginStatus.Dock=DockStyle.Right;
        footer.Controls.Add(pluginStatus);
        root.Controls.Add(footer,0,2);

        WireEvents();

        string? draftPath=File.Exists(localPath)?localPath:
            File.Exists(legacyLocalPathV3)?legacyLocalPathV3:
            File.Exists(legacyLocalPathV2)?legacyLocalPathV2:null;
        if(draftPath!=null) {
            try {
                preset=Preset.Load(draftPath);
                if(!string.Equals(draftPath,localPath,StringComparison.OrdinalIgnoreCase))SaveLocal();
            } catch(Exception ex){Log("Local preset: "+ex.Message);}
        }

        autoProfileEnabled.Checked=preset.AutoProfileEnabled;
        LoadRulesGrid();
        RefreshPorts();
        LoadEditor();
        CaptureLanguageBase(this);
        if(File.Exists(languagePath)) {
            string saved=File.ReadAllText(languagePath).Trim().ToLowerInvariant();
            if(saved is "vi" or "zh")languageCode=saved;
        }
        loading=true;
        language.SelectedIndex=languageCode=="vi"?1:languageCode=="zh"?2:0;
        loading=false;
        ApplyLanguage();
        autoProfileTimer.Start();
        ApplyTheme(false);
    }

    Control BuildHeader() {
        var panel=new Panel{Dock=DockStyle.Fill,Padding=new Padding(14,10,12,8),BackColor=Color.White};
        var title=new Label{
            Text="PIXEL PRO 2.0",AutoSize=true,
            Font=new Font("Segoe UI",17,FontStyle.Bold),Location=new Point(14,14)
        };
        panel.Controls.Add(title);

        var subtitle=new Label{
            Text="8-Key Configurator",AutoSize=true,ForeColor=Color.DimGray,
            Location=new Point(190,20)
        };
        panel.Controls.Add(subtitle);

        var tools=new FlowLayoutPanel{
            Dock=DockStyle.Right,AutoSize=true,FlowDirection=FlowDirection.LeftToRight,
            WrapContents=false,Padding=new Padding(0,2,0,0)
        };
        tools.Controls.Add(MakeButton("Import",ImportPreset));
        tools.Controls.Add(MakeButton("Export",ExportPreset));
        tools.Controls.Add(MakeButton("Import Profile",ImportProfile));
        tools.Controls.Add(MakeButton("Export Profile",ExportProfile));
        tools.Controls.Add(MakeButton("Presets",OpenPresetGallery));
        tools.Controls.Add(MakeButton("Firmware",OpenFirmwarePage));
        tools.Controls.Add(MakeButton("Light / Dark",()=>{dark=!dark;ApplyTheme(dark);return Task.CompletedTask;}));
        tools.Controls.Add(language);
        panel.Controls.Add(tools);
        return panel;
    }

    Control BuildLeftPane() {
        var root=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=2,ColumnCount=1};
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,570));
        root.RowStyles.Add(new RowStyle(SizeType.Percent,100));

        var keysBox=new GroupBox{Text=$"Profile & Key Selection · {DeviceLimits.Profiles} profiles",Dock=DockStyle.Fill,Padding=new Padding(10)};
        var keysLayout=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=3,ColumnCount=1};
        keysLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,175));
        keysLayout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        keysLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,40));

        var profiles=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=5,RowCount=5,Padding=new Padding(2)};
        for(int col=0;col<5;col++)profiles.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,20));
        for(int row=0;row<5;row++)profiles.RowStyles.Add(new RowStyle(SizeType.Percent,20));
        for(int i=0;i<DeviceLimits.Profiles;i++) {
            int p=i;
            var b=new Button{
                Text=(i+1).ToString(),Dock=DockStyle.Fill,Margin=new Padding(3),
                FlatStyle=FlatStyle.Flat,Font=new Font("Segoe UI",10,FontStyle.Bold),
                AllowDrop=true
            };
            b.Click+=(_,_)=>Guard(async()=>{
                SaveEditor();
                currentProfile=p;
                activeProfile=p;
                LoadEditor();
                RefreshProfileButtons();
                if(device.Connected)await device.Request($"PROFILE|{p}");
            });
            b.MouseDown+=(_,e)=>{
                if(e.Button!=MouseButtons.Left)return;
                try{SaveEditor();b.DoDragDrop(new ProfileDrag(p),DragDropEffects.Copy);}
                catch(Exception ex){Log(ex.Message);}
            };
            b.DragEnter+=(_,e)=>{
                if(e.Data?.GetDataPresent(typeof(ProfileDrag))==true)e.Effect=DragDropEffects.Copy;
            };
            b.DragDrop+=(_,e)=>{
                if(e.Data?.GetData(typeof(ProfileDrag)) is ProfileDrag src&&src.Profile!=p)CopyProfile(src.Profile,p);
            };
            profileButtons[i]=b;
            profiles.Controls.Add(b,i%5,i/5);
        }
        keysLayout.Controls.Add(profiles,0,0);

        var grid=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=4,RowCount=2,Padding=new Padding(4)};
        for(int i=0;i<4;i++)grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));
        for(int i=0;i<2;i++)grid.RowStyles.Add(new RowStyle(SizeType.Percent,50));
        for(int i=0;i<8;i++) {
            int key=i;
            var b=new Button{
                Dock=DockStyle.Fill,Margin=new Padding(5),FlatStyle=FlatStyle.Flat,
                Font=new Font("Segoe UI",10,FontStyle.Bold),TextAlign=ContentAlignment.MiddleCenter,
                AllowDrop=true
            };
            b.Click+=(_,_)=>Guard(()=>{
                SaveEditor();currentKey=key;LoadEditor();return Task.CompletedTask;
            });
            b.MouseDown+=(_,e)=>{
                if(e.Button!=MouseButtons.Left)return;
                try{SaveEditor();b.DoDragDrop(new KeyDrag(currentProfile,key),DragDropEffects.Copy);}
                catch(Exception ex){Log(ex.Message);}
            };
            b.DragEnter+=(_,e)=>{
                if(e.Data?.GetDataPresent(typeof(KeyDrag))==true)e.Effect=DragDropEffects.Copy;
            };
            b.DragDrop+=(_,e)=>{
                if(e.Data?.GetData(typeof(KeyDrag)) is KeyDrag src)CopyKey(src.Profile,src.Key,currentProfile,key);
            };
            keyButtons[i]=b;
            grid.Controls.Add(b,i%4,i/4);
        }
        keysLayout.Controls.Add(grid,0,1);

        var keyTools=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};
        keyTools.Controls.Add(MakeButton("Upload Icon",UploadIcon));
        keyTools.Controls.Add(MakeButton("Delete Icon",DeleteIcon));
        keyTools.Controls.Add(MakeButton("Key Color",ChooseKeyColor));
        keysLayout.Controls.Add(keyTools,0,2);

        keysBox.Controls.Add(keysLayout);
        root.Controls.Add(keysBox,0,0);

        var devBox=new GroupBox{Text="Device List",Dock=DockStyle.Fill,Padding=new Padding(10)};
        var devLayout=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=2,ColumnCount=1};
        devLayout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        devLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,78));
        devLayout.Controls.Add(deviceList,0,0);

        var devButtons=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=true};
        devButtons.Controls.Add(MakeButton("Refresh",()=>{RefreshPorts();return Task.CompletedTask;}));
        devButtons.Controls.Add(MakeButton("Auto Find",AutoConnect));
        devButtons.Controls.Add(MakeButton("Connect",ConnectSelected));
        devButtons.Controls.Add(MakeButton("Disconnect",DisconnectDevice));
        devButtons.Controls.Add(MakeButton("Read",ReadDevice));
        devButtons.Controls.Add(MakeButton("Sync / Save",UploadAll));
        devLayout.Controls.Add(devButtons,0,1);
        devBox.Controls.Add(devLayout);
        root.Controls.Add(devBox,0,1);

        var menu=new ContextMenuStrip();
        menu.Items.Add("Connect",null,(_,_)=>Guard(ConnectSelected));
        menu.Items.Add("Refresh",null,(_,_)=>RefreshPorts());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Set RGB backlight",null,(_,_)=>Guard(ChooseDeviceBrightness));
        menu.Items.Add("Sync configuration to device",null,(_,_)=>Guard(UploadAll));
        menu.Items.Add("Read configuration to computer",null,(_,_)=>Guard(ReadDevice));
        menu.Items.Add("Switch theme",null,(_,_)=>{dark=!dark;ApplyTheme(dark);});
        menu.Items.Add("Upgrade firmware",null,(_,_)=>Guard(OpenFirmwarePage));
        var pairing=menu.Items.Add("Clear pairing information");
        pairing.Enabled=false;
        pairing.ToolTipText="PIXEL PRO 2.0 is wired USB; there is no wireless pairing record.";
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Disconnect",null,(_,_)=>Guard(DisconnectDevice));
        deviceList.ContextMenuStrip=menu;

        return root;
    }

    Control BuildRightPane() {
        var tabs=new TabControl{Dock=DockStyle.Fill};

        var startTab=new TabPage("Getting Started"){Padding=new Padding(12)};
        startTab.Controls.Add(BuildGettingStarted());
        tabs.TabPages.Add(startTab);

        var keyTab=new TabPage("Key Configuration"){Padding=new Padding(8)};
        keyTab.Controls.Add(BuildKeyEditor());
        tabs.TabPages.Add(keyTab);

        var pluginsTab=new TabPage("Plugins"){Padding=new Padding(14)};
        pluginsTab.Controls.Add(BuildPlugins());
        tabs.TabPages.Add(pluginsTab);

        var displayTab=new TabPage("Display & Media"){Padding=new Padding(14)};
        displayTab.Controls.Add(BuildDisplayMedia());
        tabs.TabPages.Add(displayTab);

        var autoTab=new TabPage("Auto Profile"){Padding=new Padding(14)};
        autoTab.Controls.Add(BuildAutoProfile());
        tabs.TabPages.Add(autoTab);

        var logTab=new TabPage("Log"){Padding=new Padding(8)};
        logTab.Controls.Add(log);
        tabs.TabPages.Add(logTab);

        return tabs;
    }

    Control BuildGettingStarted() {
        var root=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=3,ColumnCount=1,Padding=new Padding(12)};
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,70));
        root.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,52));

        root.Controls.Add(new Label{
            Text="PIXEL PRO 2.0 · Quick Start",
            AutoSize=true,Font=new Font("Segoe UI",18,FontStyle.Bold),
            Padding=new Padding(4,8,4,4)
        },0,0);

        var guide=new Label{
            Dock=DockStyle.Fill,AutoSize=false,
            Font=new Font("Segoe UI",11),
            Text=
                "1. Connect PIXEL PRO from Device List.\n\n"+
                "2. Choose one of 25 profiles and K1…K8.\n\n"+
                "3. Drag Actions into Action Sequence and edit the selected action parameters.\n\n"+
                "4. HID Mode runs directly on the device without Studio. Website / Launch App / Open Folder / Open File / Mouse Move / Power Off require App Mode and Studio running.\n\n"+
                "5. SAVE KEY saves one key, SAVE PROFILE saves eight keys, SAVE TO DEVICE synchronizes all 25 profiles.\n\n"+
                "6. Drag a key onto another key or a profile onto another profile to copy it.\n\n"+
                "7. Auto Profile can switch layouts from the foreground Windows application.\n\n"+
                "USB Safe Mode: ON by design — PIXEL PRO does not expose a mass-storage drive. Native HID/CDC remains available."
        };
        root.Controls.Add(guide,0,1);

        var row=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};
        row.Controls.Add(MakeButton("Auto Find Device",AutoConnect));
        row.Controls.Add(MakeButton("Read Device",ReadDevice));
        row.Controls.Add(MakeButton("Sync To Device",UploadAll));
        root.Controls.Add(row,0,2);
        return root;
    }

    Control BuildKeyEditor() {
        var split=new SplitContainer{
            Dock=DockStyle.Fill,Orientation=Orientation.Vertical,
            SplitterDistance=650,FixedPanel=FixedPanel.Panel2
        };

        var right=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=4,ColumnCount=1};
        right.RowStyles.Add(new RowStyle(SizeType.Absolute,86));
        right.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute,112));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute,52));

        var top=new GroupBox{Text="Selected Key",Dock=DockStyle.Fill};
        var topFlow=new FlowLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(10),WrapContents=false};
        topFlow.Controls.Add(new Label{Text="Display name",AutoSize=true,Padding=new Padding(0,7,4,0)});
        topFlow.Controls.Add(alias);
        topFlow.Controls.Add(hidMode);
        topFlow.Controls.Add(armed);
        top.Controls.Add(topFlow);
        right.Controls.Add(top,0,0);

        var seqBox=new GroupBox{Text="Action Sequence",Dock=DockStyle.Fill,Padding=new Padding(8)};
        seqBox.Controls.Add(sequence);
        right.Controls.Add(seqBox,0,1);

        var param=new GroupBox{Text="Action Parameters",Dock=DockStyle.Fill};
        var paramLayout=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=3,ColumnCount=3,Padding=new Padding(10)};
        paramLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,85));
        paramLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        paramLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,100));
        paramLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,34));
        paramLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,32));
        paramLayout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        paramLayout.Controls.Add(new Label{Text="Value",AutoSize=true,Padding=new Padding(0,7,0,0)},0,0);
        paramLayout.Controls.Add(stepValue,1,0);
        paramLayout.Controls.Add(MakeButton("Browse…",BrowseStepValue),2,0);
        paramLayout.SetColumnSpan(stepHint,2);
        paramLayout.Controls.Add(stepHint,1,1);
        var seqTools=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};
        seqTools.Controls.Add(MakeButton("↑ Up",()=>{MoveStep(-1);return Task.CompletedTask;}));
        seqTools.Controls.Add(MakeButton("↓ Down",()=>{MoveStep(1);return Task.CompletedTask;}));
        seqTools.Controls.Add(MakeButton("Delete",()=>{DeleteStep();return Task.CompletedTask;}));
        paramLayout.SetColumnSpan(seqTools,3);
        paramLayout.Controls.Add(seqTools,0,2);
        param.Controls.Add(paramLayout);
        right.Controls.Add(param,0,2);

        var saveRow=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,WrapContents=false,Padding=new Padding(0,6,8,0)};
        var save=MakeButton("SAVE TO DEVICE",UploadAll);
        save.Width=170;save.Height=36;save.BackColor=Color.FromArgb(40,160,90);save.ForeColor=Color.White;
        save.FlatStyle=FlatStyle.Flat;save.FlatAppearance.BorderSize=0;
        saveRow.Controls.Add(save);
        saveRow.Controls.Add(MakeButton("SAVE PROFILE",UploadCurrentProfile));
        saveRow.Controls.Add(MakeButton("SAVE KEY",UploadCurrentKey));
        saveRow.Controls.Add(MakeButton("Save Draft",()=>{SaveEditor();SaveLocal();return Task.CompletedTask;}));
        right.Controls.Add(saveRow,0,3);

        split.Panel1.Controls.Add(right);

        var actionsBox=new GroupBox{Text="Actions",Dock=DockStyle.Fill,Padding=new Padding(8)};
        var palette=new FlowLayoutPanel{
            Dock=DockStyle.Fill,AutoScroll=true,Padding=new Padding(6),
            FlowDirection=FlowDirection.LeftToRight,WrapContents=true
        };
        foreach(var action in ActionCatalog) {
            var card=new Button{
                Width=128,Height=62,Margin=new Padding(5),
                Text=ActionGlyph(action.Type)+"\n"+action.Name,
                TextAlign=ContentAlignment.MiddleCenter,
                FlatStyle=FlatStyle.Flat,Tag=action,
                Font=new Font("Segoe UI",9,FontStyle.Regular)
            };
            card.FlatAppearance.BorderColor=Color.FromArgb(210,214,220);
            card.MouseDown+=(_,e)=>{
                if(e.Button==MouseButtons.Left&&card.Tag is ActionDef def)
                    card.DoDragDrop(def,DragDropEffects.Copy);
            };
            card.DoubleClick+=(_,_)=>{if(card.Tag is ActionDef def)AddAction(def);};
            palette.Controls.Add(card);
        }
        actionsBox.Controls.Add(palette);
        split.Panel2.Controls.Add(actionsBox);
        return split;
    }

    static string ActionGlyph(string type)=>type switch {
        "Website"=>"🌐","LaunchApp"=>"▶","OpenFolder"=>"📁","OpenFile"=>"📄",
        "Text"=>"T","Shortcut"=>"⌨","Delay"=>"⏱","MouseMove"=>"↔",
        "MouseClick"=>"🖱","Wheel"=>"↕","Media"=>"⏯","ChangeProfile"=>"▣",
        "FunctionalKey"=>"Fn","DeviceCtrl"=>"⚙","PowerOff"=>"⏻",_=>"＋"
    };

    Control BuildPlugins() {
        var root=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=4,ColumnCount=1,Padding=new Padding(14)};
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,64));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,110));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,110));
        root.RowStyles.Add(new RowStyle(SizeType.Percent,100));

        root.Controls.Add(new Label{
            Text="Integrated Plugins",
            AutoSize=true,Font=new Font("Segoe UI",18,FontStyle.Bold),
            Padding=new Padding(2,8,2,4)
        },0,0);

        var pcBox=new GroupBox{Text="PC Monitoring",Dock=DockStyle.Fill,Padding=new Padding(12)};
        var pcFlow=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false};
        pcFlow.Controls.Add(monitorPluginToggle);
        pcFlow.Controls.Add(monitorInfo);
        pcFlow.Controls.Add(new Label{
            AutoSize=true,ForeColor=Color.DimGray,
            Text="CPU / GPU / RAM / Disk / Network telemetry is rendered full-screen on PIXEL PRO."
        });
        pcBox.Controls.Add(pcFlow);
        root.Controls.Add(pcBox,0,1);

        var musicBox=new GroupBox{Text="Music Player",Dock=DockStyle.Fill,Padding=new Padding(12)};
        var musicFlow=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false};
        musicFlow.Controls.Add(musicPluginToggle);
        musicFlow.Controls.Add(musicInfo);
        musicFlow.Controls.Add(new Label{
            AutoSize=true,ForeColor=Color.DimGray,
            Text="Uses Windows SMTC: Spotify, Apple Music, browsers, VLC and other compatible players."
        });
        musicBox.Controls.Add(musicFlow);
        root.Controls.Add(musicBox,0,2);

        root.Controls.Add(new Label{
            Dock=DockStyle.Fill,AutoSize=false,ForeColor=Color.DimGray,
            Text="Plugin status is also shown in the bottom bar. Enabling one display plugin replaces the other full-screen plugin on the active device; HID keys continue working."
        },0,3);
        return root;
    }

    Control BuildDisplayMedia() {
        var flow=new FlowLayoutPanel{
            Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,
            WrapContents=false,AutoScroll=true,Padding=new Padding(10)
        };

        var orientationRow=new FlowLayoutPanel{AutoSize=true,WrapContents=false};
        orientationRow.Controls.Add(new Label{Text="LCD orientation",AutoSize=true,Padding=new Padding(0,7,8,0)});
        orientationRow.Controls.Add(orientation);
        orientationRow.Controls.Add(MakeButton("Apply",async()=>{
            NeedDevice();await device.Request($"DISPLAY|{orientation.SelectedIndex}");
            status.Text="LCD orientation saved";
        }));
        orientationRow.Controls.Add(MakeButton("Calibrate Touch",CalibrateTouch));
        orientationRow.Controls.Add(MakeButton("Reset Touch",ResetTouchCalibration));
        orientationRow.Controls.Add(MakeButton("Touch Diagnostics",ToggleTouchDiagnostics));
        flow.Controls.Add(orientationRow);

        var mediaRow=new FlowLayoutPanel{AutoSize=true,WrapContents=false};
        mediaRow.Controls.Add(new Label{Text="Screensaver after (s)",AutoSize=true,Padding=new Padding(0,7,8,0)});
        mediaRow.Controls.Add(saverSeconds);
        mediaRow.Controls.Add(MakeButton("Save timeout",async()=>{
            NeedDevice();await device.Request($"SAVER|{saverSeconds.Value}");
        }));
        mediaRow.Controls.Add(MakeButton("Upload GIF",UploadGif));
        mediaRow.Controls.Add(MakeButton("Delete GIF",DeleteGif));
        flow.Controls.Add(mediaRow);

        var screenRow=new FlowLayoutPanel{AutoSize=true,WrapContents=false};
        screenRow.Controls.Add(new Label{Text="Auto screen off",AutoSize=true,Padding=new Padding(0,7,8,0)});
        screenRow.Controls.Add(screenOff);
        screenRow.Controls.Add(MakeButton("Apply",async()=>{
            NeedDevice();
            int seconds=screenOff.SelectedIndex switch {1=>30,2=>300,3=>900,_=>0};
            await device.Request($"SCREENOFF|{seconds}");
            status.Text=seconds==0?"Screen always on":$"Screen off after {seconds}s";
        }));
        flow.Controls.Add(screenRow);

        var rgbRow=new FlowLayoutPanel{AutoSize=true,WrapContents=false};
        rgbRow.Controls.Add(new Label{Text="RGB brightness",AutoSize=true,Padding=new Padding(0,7,8,0)});
        rgbRow.Controls.Add(brightness);
        rgbRow.Controls.Add(MakeButton("Apply RGB",ApplyBrightness));
        rgbRow.Controls.Add(MakeButton("Toggle PC Monitor",ToggleMonitor));
        flow.Controls.Add(rgbRow);

        flow.Controls.Add(mediaInfo);
        flow.Controls.Add(touchInfo);
        flow.Controls.Add(transfer);

        var note=new Label{
            AutoSize=true,MaximumSize=new Size(820,0),Padding=new Padding(0,12,0,0),
            Text="Touch 2.4: raw axes follow the actual MCUFRIEND wiring (screen X comes from rawY, screen Y from rawX). Calibration uses a 4-point affine solve that handles axis swap, inversion and panel skew. Hold each target briefly, then release before touching the next one."
        };
        flow.Controls.Add(note);
        return flow;
    }

    Control BuildAutoProfile() {
        autoGrid.Columns.Add(new DataGridViewCheckBoxColumn{Name="Enabled",HeaderText="On",FillWeight=15});
        autoGrid.Columns.Add(new DataGridViewTextBoxColumn{Name="Process",HeaderText="Process / app",FillWeight=55});
        autoGrid.Columns.Add(new DataGridViewTextBoxColumn{Name="Profile",HeaderText=$"Profile (1..{DeviceLimits.Profiles})",FillWeight=30});

        var root=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=3,ColumnCount=1};
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,42));
        root.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,50));

        var top=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};
        top.Controls.Add(autoProfileEnabled);
        top.Controls.Add(new Label{
            Text="Switch device profile when the foreground Windows process matches a rule.",
            AutoSize=true,Padding=new Padding(12,7,0,0),ForeColor=Color.DimGray
        });
        root.Controls.Add(top,0,0);
        root.Controls.Add(autoGrid,0,1);

        var buttons=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};
        buttons.Controls.Add(MakeButton("Add current app",()=>{AddForegroundRule();return Task.CompletedTask;}));
        buttons.Controls.Add(MakeButton("Add row",()=>{autoGrid.Rows.Add(true,"",1);return Task.CompletedTask;}));
        buttons.Controls.Add(MakeButton("Delete row",()=>{DeleteRule();return Task.CompletedTask;}));
        buttons.Controls.Add(MakeButton("Save rules",()=>{SaveRulesGrid();SaveLocal();return Task.CompletedTask;}));
        root.Controls.Add(buttons,0,2);
        return root;
    }

    void WireEvents() {
        toolbox.MouseDown+=(_,e)=>{
            int index=toolbox.IndexFromPoint(e.Location);
            if(index>=0&&toolbox.Items[index] is ActionDef action)
                toolbox.DoDragDrop(action,DragDropEffects.Copy);
        };
        toolbox.DoubleClick+=(_,_)=>{
            if(toolbox.SelectedItem is ActionDef action)AddAction(action);
        };

        sequence.DragEnter+=(_,e)=>{
            if(e.Data?.GetDataPresent(typeof(ActionDef))==true)e.Effect=DragDropEffects.Copy;
        };
        sequence.DragDrop+=(_,e)=>{
            if(e.Data?.GetData(typeof(ActionDef)) is ActionDef action)AddAction(action);
        };
        sequence.SelectedIndexChanged+=(_,_)=>LoadStepEditor();
        sequence.DoubleClick+=(_,_)=>LoadStepEditor();
        stepValue.TextChanged+=(_,_)=>{
            if(loading||sequence.SelectedItem is not StepItem item)return;
            item.Step.Value=stepValue.Text;
            sequence.Refresh();
        };

        var seqMenu=new ContextMenuStrip();
        seqMenu.Items.Add("Move Up",null,(_,_)=>MoveStep(-1));
        seqMenu.Items.Add("Move Down",null,(_,_)=>MoveStep(1));
        seqMenu.Items.Add("Delete",null,(_,_)=>DeleteStep());
        sequence.ContextMenuStrip=seqMenu;

        hidMode.CheckedChanged+=(_,_)=>UpdateModeInfo();
        autoProfileEnabled.CheckedChanged+=(_,_)=>{
            if(loading)return;
            preset.AutoProfileEnabled=autoProfileEnabled.Checked;
            SaveLocalQuiet();
        };
        monitorPluginToggle.CheckedChanged+=(_,_)=>{
            if(loading)return;
            Guard(()=>SetMonitorEnabled(monitorPluginToggle.Checked));
        };
        musicPluginToggle.CheckedChanged+=(_,_)=>{
            if(loading)return;
            Guard(()=>SetMusicEnabled(musicPluginToggle.Checked));
        };
        language.SelectedIndexChanged+=(_,_)=>{
            if(loading)return;
            languageCode=language.SelectedIndex switch {1=>"vi",2=>"zh",_=>"en"};
            Directory.CreateDirectory(Path.GetDirectoryName(languagePath)!);
            File.WriteAllText(languagePath,languageCode);
            ApplyLanguage();
        };

        device.Event+=e=>OnUi(()=>HandleEvent(e));
        device.Disconnected+=e=>OnUi(()=>{
            monitorEnabled=false;monitorTimer.Stop();monitorInfo.Text="PC Monitor: OFF";
            musicEnabled=false;musicTimer.Stop();musicInfo.Text="Music Player: OFF";
            loading=true;monitorPluginToggle.Checked=false;musicPluginToggle.Checked=false;loading=false;
            UpdatePluginStatus();
            status.Text="Disconnected";
            Log(e);
            RefreshPorts();
        });
        device.Changed+=()=>OnUi(RefreshPorts);
        deviceList.SelectedIndexChanged+=(_,_)=>{
            if(loading)return;
            if(deviceList.SelectedItem is DeviceEntry entry&&entry.Connected&&
               !string.Equals(device.PortName,entry.Port,StringComparison.OrdinalIgnoreCase)) {
                device.Activate(entry.Port);
                Guard(AfterConnect);
            }
        };

        monitorTimer.Tick+=async (_,_)=>{
            if(!monitorEnabled||monitorSending||!device.Connected)return;
            monitorSending=true;
            try{await SendMonitorFrame();}
            catch(Exception ex){
                monitorEnabled=false;monitorTimer.Stop();monitorInfo.Text="PC Monitor: error";
                Log("MONITOR: "+ex.Message);
            } finally {monitorSending=false;}
        };

        musicTimer.Tick+=async (_,_)=>{
            if(!musicEnabled||musicSending||!device.Connected)return;
            musicSending=true;
            try{await SendMusicFrame();}
            catch(Exception ex){Log("MUSIC: "+ex.Message);}
            finally{musicSending=false;}
        };

        autoProfileTimer.Tick+=async (_,_)=>await CheckAutoProfile();

        tray.DoubleClick+=(_,_)=>{Show();WindowState=FormWindowState.Normal;Activate();tray.Visible=false;};
        var trayMenu=new ContextMenuStrip();
        trayMenu.Items.Add("Open Studio",null,(_,_)=>{Show();Activate();tray.Visible=false;});
        trayMenu.Items.Add("Exit",null,(_,_)=>Close());
        tray.ContextMenuStrip=trayMenu;

        FormClosing+=(_,e)=>{
            try{SaveEditor();SaveRulesGrid();SaveLocal();}
            catch(Exception ex) {
                if(MessageBox.Show(ex.Message+"\nExit without saving?","PIXEL PRO",MessageBoxButtons.YesNo)!=DialogResult.Yes) {
                    e.Cancel=true;return;
                }
            }
            monitorTimer.Stop();musicTimer.Stop();autoProfileTimer.Stop();
            monitorTimer.Dispose();musicTimer.Dispose();autoProfileTimer.Dispose();
            monitorCollector.Dispose();musicPlugin.Dispose();
            shutdown.Cancel();device.Dispose();tray.Dispose();
        };
    }

    Button MakeButton(string text,Func<Task> action) {
        var b=new Button{
            Text=text,AutoSize=true,Height=31,Margin=new Padding(4,2,4,2),
            FlatStyle=FlatStyle.System
        };
        b.Click+=(_,_)=>Guard(action);
        return b;
    }

    async void Guard(Func<Task> action) {
        if(busy)return;
        busy=true;
        try{await action();}
        catch(Exception ex){
            status.Text="Action failed";
            Log(ex.ToString());
            MessageBox.Show(this,ex.Message,"PIXEL PRO",MessageBoxButtons.OK,MessageBoxIcon.Warning);
        } finally {busy=false;}
    }

    void NeedDevice() {
        if(!device.Connected)throw new IOException("Connect PIXEL PRO first.");
    }

    void OnUi(Action action) {
        if(!IsDisposed&&IsHandleCreated)
            try{BeginInvoke(action);}catch(InvalidOperationException){}
    }

    void Log(string text) {
        if(log.TextLength>50000)log.Clear();
        log.AppendText($"{DateTime.Now:HH:mm:ss}  {text}\r\n");
    }

    static string StepText(Step s) {
        string name=ActionCatalog.FirstOrDefault(a=>a.Type==s.Type)?.Name??s.Type;
        string value=s.Value??"";
        if(value.Length>72)value=value[..69]+"…";
        return string.IsNullOrWhiteSpace(value)?name:$"{name}    {value}";
    }

    static Step CloneStep(Step s)=>new(){Type=s.Type,Value=s.Value};

    static Binding CloneBinding(Binding b)=>new(){
        Type=b.Type,Code=b.Code,Modifiers=b.Modifiers,Color=b.Color,Label=b.Label,
        Steps=b.Steps?.Select(CloneStep).ToList()??[]
    };

    void CopyKey(int sourceProfile,int sourceKey,int targetProfile,int targetKey) {
        if(sourceProfile==targetProfile&&sourceKey==targetKey)return;
        preset.Profiles[targetProfile][targetKey]=CloneBinding(preset.Profiles[sourceProfile][sourceKey]);
        if(targetProfile==currentProfile&&targetKey==currentKey)LoadEditor();
        else RefreshTiles();
        SaveLocalQuiet();
        status.Text=$"Copied P{sourceProfile+1} K{sourceKey+1} → P{targetProfile+1} K{targetKey+1}";
    }

    void CopyProfile(int source,int target) {
        if(source==target)return;
        for(int k=0;k<DeviceLimits.Keys;k++)preset.Profiles[target][k]=CloneBinding(preset.Profiles[source][k]);
        if(target==currentProfile)LoadEditor(); else RefreshTiles();
        SaveLocalQuiet();
        status.Text=$"Copied Profile {source+1} → Profile {target+1}";
    }

    void AddAction(ActionDef action) {
        var item=new StepItem(new Step{Type=action.Type,Value=action.DefaultValue});
        sequence.Items.Add(item);
        sequence.SelectedIndex=sequence.Items.Count-1;
        hidMode.Checked=action.HidCapable&&hidMode.Checked;
        UpdateModeInfo();
    }

    void MoveStep(int delta) {
        int i=sequence.SelectedIndex;
        if(i<0)return;
        int n=i+delta;
        if(n<0||n>=sequence.Items.Count)return;
        object item=sequence.Items[i]!;
        sequence.Items.RemoveAt(i);
        sequence.Items.Insert(n,item);
        sequence.SelectedIndex=n;
    }

    void DeleteStep() {
        int i=sequence.SelectedIndex;
        if(i<0)return;
        sequence.Items.RemoveAt(i);
        if(sequence.Items.Count>0)sequence.SelectedIndex=Math.Min(i,sequence.Items.Count-1);
        else LoadStepEditor();
    }

    void LoadStepEditor() {
        loading=true;
        try {
            if(sequence.SelectedItem is StepItem item) {
                stepValue.Enabled=true;
                stepValue.Text=item.Step.Value;
                var def=ActionCatalog.FirstOrDefault(a=>a.Type==item.Step.Type);
                stepHint.Text=def?.Hint??item.Step.Type;
            } else {
                stepValue.Enabled=false;stepValue.Text="";stepHint.Text="Drag an action into Action Sequence.";
            }
        } finally {loading=false;}
    }

    Task BrowseStepValue() {
        if(sequence.SelectedItem is not StepItem item)return Task.CompletedTask;
        if(item.Step.Type is "LaunchApp" or "OpenFile") {
            using var dialog=new OpenFileDialog{Filter="All files|*.*"};
            if(dialog.ShowDialog()==DialogResult.OK)stepValue.Text=dialog.FileName;
        } else if(item.Step.Type=="OpenFolder") {
            using var dialog=new FolderBrowserDialog();
            if(dialog.ShowDialog()==DialogResult.OK)stepValue.Text=dialog.SelectedPath;
        } else if(item.Step.Type=="Website") {
            stepValue.Focus();stepValue.SelectAll();
        }
        return Task.CompletedTask;
    }

    void SaveEditor() {
        if(loading)return;
        string name=alias.Text.Trim();
        if(string.IsNullOrEmpty(name))name=$"Key {currentKey+1}";
        var steps=sequence.Items.Cast<StepItem>().Select(x=>CloneStep(x.Step)).ToList();

        var b=new Binding{Label=name,Color=keyColor,Steps=steps};
        if(steps.Count==0) {
            b.Type="D";b.Code=0;b.Modifiers=0;
        } else if(hidMode.Checked) {
            if(!TrySimpleNative(steps,b)) {
                _=MediaCodec.FromNativeScript(steps);
                b.Type="S";b.Code=0;b.Modifiers=0;
            }
        } else {
            b.Type="H";b.Code=0;b.Modifiers=0;
        }
        b.Validate();
        preset.Profiles[currentProfile][currentKey]=b;
        preset.Schema=4;
        RefreshTiles();
    }

    static bool TrySimpleNative(IReadOnlyList<Step> steps,Binding b) {
        if(steps.Count!=1)return false;
        var s=steps[0];
        if(s.Type is "Shortcut" or "FunctionalKey") {
            if(!HidShortcut.TryParse(s.Value,out int usage,out int modifiers))return false;
            b.Type="K";b.Code=usage;b.Modifiers=modifiers;return true;
        }
        if(s.Type=="Media") {
            b.Type="C";b.Code=s.Value.Trim().ToUpperInvariant() switch {
                "VOLUP"=>233,"VOLDOWN"=>234,"MUTE"=>226,"PLAYPAUSE"=>205,
                "NEXT"=>181,"PREV"=>182,"STOP"=>183,_=>-1
            };
            b.Modifiers=0;return b.Code>0;
        }
        if(s.Type=="MouseClick") {
            b.Type="M";b.Code=MacroValue.Click(s.Value) switch {
                "LEFT"=>1,"RIGHT"=>2,"MIDDLE"=>3,"DOUBLELEFT"=>4,_=>0
            };
            b.Modifiers=0;return b.Code>0;
        }
        if(s.Type=="Wheel") {
            int v=MacroValue.Wheel(s.Value);
            if(v==0)return false;
            b.Type="M";b.Code=v>0?5:6;b.Modifiers=0;return true;
        }
        if(s.Type=="ChangeProfile"&&int.TryParse(s.Value,out int p)&&p is >=1 and <=DeviceLimits.Profiles) {
            b.Type="P";b.Code=p-1;b.Modifiers=0;return true;
        }
        return false;
    }

    List<Step> StepsFor(Binding b) {
        if(b.Steps is {Count:>0})return b.Steps.Select(CloneStep).ToList();
        return b.Type switch {
            "K"=>[new Step{Type="Shortcut",Value=HidShortcut.Format(b.Code,b.Modifiers)}],
            "C"=>[new Step{Type="Media",Value=b.Code switch {
                233=>"VOLUP",234=>"VOLDOWN",226=>"MUTE",205=>"PLAYPAUSE",
                181=>"NEXT",182=>"PREV",183=>"STOP",_=>"PLAYPAUSE"
            }}],
            "M"=>[new Step{Type=b.Code is 5 or 6?"Wheel":"MouseClick",Value=b.Code switch {
                1=>"LEFT",2=>"RIGHT",3=>"MIDDLE",4=>"DOUBLELEFT",5=>"1",6=>"-1",_=>"LEFT"
            }}],
            "P"=>[new Step{Type="ChangeProfile",Value=(b.Code+1).ToString()}],
            _=>[]
        };
    }

    void LoadEditor() {
        loading=true;
        try {
            var b=preset.Profiles[currentProfile][currentKey];
            alias.Text=b.Label;
            keyColor=b.Color;
            hidMode.Checked=b.Type is not ("H" or "D");
            sequence.Items.Clear();
            foreach(var s in StepsFor(b))sequence.Items.Add(new StepItem(s));
            if(sequence.Items.Count>0)sequence.SelectedIndex=0;
            RefreshTiles();
            RefreshProfileButtons();
            UpdateModeInfo();
        } finally {loading=false;}
        LoadStepEditor();
    }

    void RefreshTiles() {
        for(int k=0;k<DeviceLimits.Keys;k++) {
            var b=preset.Profiles[currentProfile][k];
            string mode=b.Type=="H"?"APP":b.Type=="D"?"OFF":"HID";
            keyButtons[k].Text=$"K{k+1}\n{b.Label}\n[{mode}]";
            keyButtons[k].BackColor=k==currentKey?Color.FromArgb(215,235,255):dark?Color.FromArgb(45,48,54):Color.White;
            keyButtons[k].ForeColor=dark?Color.White:Color.Black;
            keyButtons[k].FlatAppearance.BorderColor=k==currentKey?Color.FromArgb(40,120,210):Color.FromArgb(205,210,220);
        }
    }

    void RefreshProfileButtons() {
        for(int p=0;p<DeviceLimits.Profiles;p++) {
            profileButtons[p].Text=(p==activeProfile?"▶ ":"")+(p+1);
            profileButtons[p].BackColor=p==currentProfile?Color.FromArgb(40,120,210):dark?Color.FromArgb(50,53,60):Color.White;
            profileButtons[p].ForeColor=p==currentProfile?Color.White:dark?Color.White:Color.Black;
        }
    }

    void UpdateModeInfo() {
        if(hidMode.Checked) {
            var steps=sequence.Items.Cast<StepItem>().Select(x=>x.Step).ToList();
            modeInfo.Text=steps.Count==0?"HID Mode":MediaCodec.CanEncodeNative(steps)?"HID Mode · native":"HID Mode · contains App-only action";
        } else modeInfo.Text="App Mode · Studio required";
    }

    void RefreshPorts() {
        string? keep=(deviceList.SelectedItem as DeviceEntry)?.Port??device.PortName;
        var ports=SerialPort.GetPortNames()
            .Union(device.ConnectedPorts,StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase).ToArray();
        loading=true;
        try {
            deviceList.Items.Clear();
            foreach(var port in ports)deviceList.Items.Add(new DeviceEntry(port,device.IsConnected(port)));
            var target=deviceList.Items.Cast<DeviceEntry>().FirstOrDefault(x=>
                string.Equals(x.Port,keep,StringComparison.OrdinalIgnoreCase));
            if(target!=null)deviceList.SelectedItem=target;
            else if(deviceList.Items.Count>0)deviceList.SelectedIndex=0;
        } finally {loading=false;}
    }

    async Task ConnectSelected() {
        SaveEditor();
        if(deviceList.SelectedItem is not DeviceEntry entry)throw new IOException("No COM port selected.");
        await device.Connect(entry.Port);
        RefreshPorts();
        await AfterConnect();
    }

    async Task AutoConnect() {
        SaveEditor();
        var ports=SerialPort.GetPortNames().Order(StringComparer.OrdinalIgnoreCase).ToArray();
        if(ports.Length==0)throw new IOException("Windows does not expose a COM port.");
        Exception? last=null;
        int found=0;
        foreach(var port in ports) {
            if(device.IsConnected(port)){found++;continue;}
            try {
                status.Text=$"Probing {port}…";
                await device.Connect(port);
                found++;
                Log($"PIXEL PRO found on {port}");
            } catch(Exception ex) {
                last=ex;Log($"{port}: {ex.Message}");
            }
        }
        RefreshPorts();
        if(found==0)throw new IOException("PIXEL PRO 2.0 not found.",last);
        await AfterConnect();
        status.Text=$"{found} PIXEL PRO device(s) connected";
    }

    Task DisconnectDevice() {
        monitorEnabled=false;monitorTimer.Stop();monitorInfo.Text="PC Monitor: OFF";
        device.DisconnectActive();
        RefreshPorts();
        status.Text=device.Connected?$"Active device: {device.PortName}":"Disconnected";
        return Task.CompletedTask;
    }

    async Task AfterConnect() {
        if(int.TryParse(await device.Request("PANEL"),out int mode)&&mode is >=0 and <=3)
            orientation.SelectedIndex=mode;
        await RefreshMediaInfo();
        status.Text=$"Connected {device.PortName} · PIXEL PRO 2.0";
        Log($"Handshake OK on {device.PortName}");
        await CheckAutoSync();
    }

    async Task CheckAutoSync() {
        try {
            string remoteText=await device.Request("KEYHASH");
            if(!uint.TryParse(remoteText,out uint remote))return;
            uint local=preset.BindingHash();
            if(remote==local) {
                status.Text=$"Connected {device.PortName} · configuration synchronized";
                return;
            }

            var choice=MessageBox.Show(this,
                "Cấu hình trên PIXEL PRO khác với bản đang lưu trên PC.\n\n"+
                "YES  = dùng cấu hình PC và ghi xuống thiết bị\n"+
                "NO   = đọc cấu hình từ thiết bị về PC\n"+
                "CANCEL = giữ nguyên, xử lý sau",
                "PIXEL PRO · Auto Sync",
                MessageBoxButtons.YesNoCancel,MessageBoxIcon.Question);

            if(choice==DialogResult.Yes)await UploadAll();
            else if(choice==DialogResult.No)await ReadDevice();
            else status.Text="Connected · configuration differs (sync postponed)";
        } catch(Exception ex) {
            Log("AUTO SYNC: "+ex.Message);
        }
    }

    async Task ReadDevice() {
        NeedDevice();
        SaveEditor();
        SaveRulesGrid();
        var next=new Preset{
            AutoProfiles=preset.AutoProfiles.Select(r=>new AutoProfileRule{Process=r.Process,Profile=r.Profile,Enabled=r.Enabled}).ToList(),
            AutoProfileEnabled=preset.AutoProfileEnabled
        };
        for(int p=0;p<DeviceLimits.Profiles;p++)for(int k=0;k<DeviceLimits.Keys;k++) {
            var old=preset.Profiles[p][k];
            var b=Binding.Parse(await device.Request($"GET|{p}|{k}"));
            b.Steps=old.Steps?.Select(CloneStep).ToList()??[];
            next.Profiles[p][k]=b;
        }
        var state=(await device.Request("STATE")).Split('|');
        if(state.Length<2||!int.TryParse(state[0],out int active)||active is <0 or >=DeviceLimits.Profiles||
           !int.TryParse(state[1],out int light)||light is <0 or >80)
            throw new IOException("Invalid STATE.");
        preset=next;activeProfile=currentProfile=active;currentKey=0;
        brightness.Value=light;
        if(state.Length>=3&&int.TryParse(state[2],out int saver)&&saver is >=0 and <=3600)
            saverSeconds.Value=saver;
        if(state.Length>=4&&int.TryParse(state[3],out int off))
            screenOff.SelectedIndex=off switch {30=>1,300=>2,900=>3,_=>0};
        LoadEditor();LoadRulesGrid();SaveLocal();
        await RefreshMediaInfo();
        status.Text=$"Read {DeviceLimits.Profiles} profiles from device";
    }

    async Task UploadBinding(int p,int k,IProgress<int>? progress=null) {
        var b=preset.Profiles[p][k];
        if(b.Type=="S") {
            byte[] script=MediaCodec.FromNativeScript(b.Steps);
            await device.UploadScript(p,k,script,progress,shutdown.Token);
        }
        await device.Request(b.Wire(p,k));
    }

    async Task UploadCurrentKey() {
        NeedDevice();SaveEditor();SaveRulesGrid();preset.Validate();SaveLocal();
        transfer.Value=0;
        await UploadBinding(currentProfile,currentKey,new Progress<int>(v=>transfer.Value=Math.Clamp(v,0,100)));
        await device.Request("SAVE");
        transfer.Value=100;
        status.Text=$"Saved P{currentProfile+1} K{currentKey+1}";
    }

    async Task UploadCurrentProfile() {
        NeedDevice();SaveEditor();SaveRulesGrid();preset.Validate();SaveLocal();
        transfer.Value=0;
        for(int k=0;k<DeviceLimits.Keys;k++) {
            int key=k;
            await UploadBinding(currentProfile,k,new Progress<int>(v=>
                transfer.Value=Math.Clamp((key*100+v)/DeviceLimits.Keys,0,100)));
        }
        await device.Request("SAVE");
        transfer.Value=100;
        status.Text=$"Saved Profile {currentProfile+1}";
    }

    async Task UploadAll() {
        NeedDevice();
        SaveEditor();SaveRulesGrid();preset.Validate();SaveLocal();
        transfer.Value=0;
        int scriptCount=0;
        for(int p=0;p<DeviceLimits.Profiles;p++)for(int k=0;k<DeviceLimits.Keys;k++)if(preset.Profiles[p][k].Type=="S")scriptCount++;
        int scriptDone=0;

        for(int p=0;p<DeviceLimits.Profiles;p++)for(int k=0;k<DeviceLimits.Keys;k++) {
            var b=preset.Profiles[p][k];
            if(b.Type=="S") {
                byte[] script=MediaCodec.FromNativeScript(b.Steps);
                var prog=new Progress<int>(v=>{
                    int basePercent=scriptCount==0?0:(scriptDone*100/scriptCount);
                    int span=scriptCount==0?0:100/scriptCount;
                    transfer.Value=Math.Clamp(basePercent+v*span/100,0,100);
                });
                await device.UploadScript(p,k,script,prog,shutdown.Token);
                scriptDone++;
            }
            await device.Request(b.Wire(p,k));
        }
        await device.Request($"RGB|{brightness.Value}");
        await device.Request($"SAVER|{saverSeconds.Value}");
        int offSeconds=screenOff.SelectedIndex switch {1=>30,2=>300,3=>900,_=>0};
        await device.Request($"SCREENOFF|{offSeconds}");
        await device.Request("SAVE");
        await device.Request($"PROFILE|{activeProfile}");
        transfer.Value=100;
        status.Text="Configuration synced to device";
        RefreshTiles();
    }

    Task OpenPresetGallery() {
        SaveEditor();
        using var gallery=new PresetGalleryForm(profile=>{
            for(int k=0;k<DeviceLimits.Keys;k++)
                preset.Profiles[currentProfile][k]=CloneBinding(profile.Keys[k]);
            currentKey=0;
            LoadEditor();
            SaveLocal();
            status.Text=$"Preset added to Profile {currentProfile+1} · sync when ready";
        });
        gallery.ShowDialog(this);
        return Task.CompletedTask;
    }

    Task ImportPreset() {
        using var dialog=new OpenFileDialog{Filter="PIXEL PRO preset|*.json"};
        if(dialog.ShowDialog()==DialogResult.OK) {
            SaveEditor();
            preset=Preset.Load(dialog.FileName);
            currentProfile=currentKey=activeProfile=0;
            autoProfileEnabled.Checked=preset.AutoProfileEnabled;
            LoadRulesGrid();LoadEditor();SaveLocal();
            status.Text="Preset imported · not synced yet";
        }
        return Task.CompletedTask;
    }

    Task ExportPreset() {
        SaveEditor();SaveRulesGrid();
        using var dialog=new SaveFileDialog{Filter="PIXEL PRO preset|*.json",FileName="pixel-pro-2-preset.json"};
        if(dialog.ShowDialog()==DialogResult.OK)preset.Save(dialog.FileName);
        return Task.CompletedTask;
    }

    Task ImportProfile() {
        SaveEditor();
        using var dialog=new OpenFileDialog{Filter="PIXEL PRO profile|*.profile.json;*.json"};
        if(dialog.ShowDialog()==DialogResult.OK) {
            var document=ProfilePreset.Load(dialog.FileName);
            for(int k=0;k<DeviceLimits.Keys;k++)
                preset.Profiles[currentProfile][k]=CloneBinding(document.Keys[k]);
            currentKey=0;
            LoadEditor();
            SaveLocal();
            status.Text=$"Imported Profile {currentProfile+1} · not synced yet";
        }
        return Task.CompletedTask;
    }

    Task ExportProfile() {
        SaveEditor();
        var document=new ProfilePreset {
            Keys=preset.Profiles[currentProfile].Select(CloneBinding).ToArray()
        };
        using var dialog=new SaveFileDialog{
            Filter="PIXEL PRO profile|*.profile.json",
            FileName=$"pixel-pro-2-profile-{currentProfile+1}.profile.json"
        };
        if(dialog.ShowDialog()==DialogResult.OK)document.Save(dialog.FileName);
        return Task.CompletedTask;
    }

    Task OpenFirmwarePage() {
        Process.Start(new ProcessStartInfo("https://github.com/mihqag148/PIXEL-PRO-2.0/releases/latest"){UseShellExecute=true});
        return Task.CompletedTask;
    }

    Task ChooseDeviceBrightness() {
        NeedDevice();
        using var dialog=new Form{
            Text="RGB Backlight",Width=390,Height=170,
            FormBorderStyle=FormBorderStyle.FixedDialog,
            StartPosition=FormStartPosition.CenterParent,
            MaximizeBox=false,MinimizeBox=false
        };
        var slider=new TrackBar{
            Minimum=0,Maximum=80,Value=(int)brightness.Value,
            TickFrequency=10,Width=330,Location=new Point(20,18)
        };
        var value=new Label{Text=slider.Value.ToString(),AutoSize=true,Location=new Point(170,75)};
        slider.ValueChanged+=(_,_)=>value.Text=slider.Value.ToString();
        var ok=new Button{Text="Apply",DialogResult=DialogResult.OK,Width=90,Location=new Point(140,95)};
        dialog.Controls.Add(slider);dialog.Controls.Add(value);dialog.Controls.Add(ok);
        dialog.AcceptButton=ok;
        if(dialog.ShowDialog(this)==DialogResult.OK) {
            brightness.Value=slider.Value;
            return ApplyBrightness();
        }
        return Task.CompletedTask;
    }

    async Task ApplyBrightness() {
        NeedDevice();
        await device.Request($"RGB|{brightness.Value}");
        await device.Request("SAVE");
        status.Text=$"RGB brightness {brightness.Value}";
    }

    Task ChooseKeyColor() {
        using var dialog=new ColorDialog{Color=From565(keyColor)};
        if(dialog.ShowDialog()==DialogResult.OK) {
            keyColor=To565(dialog.Color);
            SaveEditor();
        }
        return Task.CompletedTask;
    }

    static int To565(Color c)=>((c.R>>3)<<11)|((c.G>>2)<<5)|(c.B>>3);
    static Color From565(int c)=>Color.FromArgb(((c>>11)&31)*255/31,((c>>5)&63)*255/63,(c&31)*255/31);

    async Task UploadIcon() {
        NeedDevice();SaveEditor();
        using var dialog=new OpenFileDialog{Filter="Image|*.png;*.jpg;*.jpeg;*.bmp;*.gif"};
        if(dialog.ShowDialog()!=DialogResult.OK)return;
        transfer.Value=0;
        byte[] data=await Task.Run(()=>MediaCodec.FromIcon(dialog.FileName));
        await device.UploadIcon(currentProfile,currentKey,data,new Progress<int>(v=>transfer.Value=Math.Clamp(v,0,100)),shutdown.Token);
        status.Text=$"Icon saved P{currentProfile+1} K{currentKey+1}";
    }

    async Task DeleteIcon() {
        NeedDevice();
        await device.Request($"ICON|DELETE|{currentProfile}|{currentKey}");
        status.Text=$"Icon deleted P{currentProfile+1} K{currentKey+1}";
    }

    async Task UploadGif() {
        NeedDevice();
        using var dialog=new OpenFileDialog{Filter="Animated GIF|*.gif"};
        if(dialog.ShowDialog()!=DialogResult.OK)return;
        transfer.Value=0;status.Text="Converting GIF…";
        var package=await Task.Run(()=>MediaCodec.FromGif(dialog.FileName));
        await device.UploadMedia(package.Data,new Progress<int>(v=>transfer.Value=Math.Clamp(v,0,100)),shutdown.Token);
        await RefreshMediaInfo();
        status.Text="GIF saved to flash";
    }

    async Task DeleteGif() {
        NeedDevice();await device.Request("MEDIA|DELETE");transfer.Value=0;await RefreshMediaInfo();
    }

    async Task RefreshMediaInfo() {
        if(!device.Connected)return;
        try {
            var info=await device.Request("MEDIA|INFO");
            if(info=="ABSENT")mediaInfo.Text="Screensaver: none";
            else if(info.StartsWith("READY|")) {
                var t=info.Split('|');
                mediaInfo.Text=t.Length>=6?$"Screensaver: {int.Parse(t[1])/1024} KB · {t[2]}×{t[3]} · {t[4]} frames · {t[5]} ms":"Screensaver: ready";
            } else mediaInfo.Text="Screensaver: "+info;
        } catch(Exception ex){Log("MEDIA INFO: "+ex.Message);}
    }

    async Task CalibrateTouch() {
        NeedDevice();
        if(touchDiagEnabled) {
            await device.Request("TOUCHDIAG|OFF");
            touchDiagEnabled=false;
        }
        var rsp=await device.Request("TOUCHCAL|START");
        if(rsp!="STARTED")throw new IOException("Device did not enter touch calibration.");
        touchInfo.Text="Touch calibration: point 1/4";
        status.Text="Hold each target briefly, then release before the next target";
        MessageBox.Show(this,
            "Chạm và GIỮ nhẹ từng dấu + khoảng 0,1 giây rồi nhả tay hoàn toàn.\n\nThứ tự: trên-trái → trên-phải → dưới-phải → dưới-trái.\n\nMỗi điểm chỉ chuyển tiếp sau khi bạn nhả tay.",
            "Touch Calibration 2.4",MessageBoxButtons.OK,MessageBoxIcon.Information);
    }

    async Task ResetTouchCalibration() {
        NeedDevice();
        await device.Request("TOUCHCAL|RESET");
        touchInfo.Text="Touch: default hardware mapping";
        status.Text="Touch calibration reset · dùng mapping MCUFRIEND mặc định";
    }

    async Task ToggleTouchDiagnostics() {
        NeedDevice();
        touchDiagEnabled=!touchDiagEnabled;
        var rsp=await device.Request($"TOUCHDIAG|{(touchDiagEnabled?"ON":"OFF")}");
        if(rsp!=(touchDiagEnabled?"ON":"OFF"))throw new IOException("Touch diagnostics state was not accepted.");
        touchInfo.Text=touchDiagEnabled
            ?"Touch diagnostics: ON · chạm các góc để xem RAW/MAPPED"
            :"Touch diagnostics: OFF";
        status.Text=touchInfo.Text;
    }

    async Task ToggleMonitor()=>await SetMonitorEnabled(!monitorEnabled);

    async Task SetMonitorEnabled(bool enabled) {
        NeedDevice();
        monitorEnabled=enabled;
        loading=true;monitorPluginToggle.Checked=enabled;loading=false;
        if(enabled) {
            if(musicEnabled)await SetMusicEnabled(false);
            await SendMonitorFrame();
            monitorTimer.Start();
        } else {
            monitorTimer.Stop();
            await device.Request("MONITOR|OFF");
            monitorInfo.Text="PC Monitor: OFF";
        }
        UpdatePluginStatus();
    }

    async Task SendMonitorFrame() {
        var s=monitorCollector.Read();
        await device.Request($"MONITOR|SET|{s.CpuPercent}|{s.GpuPercent}|{s.RamPercent}|{s.DiskPercent}|{s.NetKbps}|{s.CpuTempC}|{s.GpuTempC}");
        monitorInfo.Text=$"PC Monitor: CPU {s.CpuPercent}% · GPU {s.GpuPercent}% · RAM {s.RamPercent}%";
        UpdatePluginStatus();
    }

    async Task SetMusicEnabled(bool enabled) {
        NeedDevice();
        musicEnabled=enabled;
        loading=true;musicPluginToggle.Checked=enabled;loading=false;
        if(enabled) {
            if(monitorEnabled)await SetMonitorEnabled(false);
            musicPlugin.Reset();
            lastMusicPayload="";
            await SendMusicFrame();
            musicTimer.Start();
        } else {
            musicTimer.Stop();
            lastMusicPayload="";
            await device.Request("MUSIC|OFF");
            musicInfo.Text="Music Player: OFF";
        }
        UpdatePluginStatus();
    }

    static string CleanMusicField(string value) {
        var chars=value.Take(48).Select(ch=>ch switch {
            '|' => '/',
            >= ' ' and <= '~' => ch,
            _ => '?'
        }).ToArray();
        string text=new(chars);
        return string.IsNullOrWhiteSpace(text)?"--":text;
    }

    async Task SendMusicFrame() {
        var snapshot=await musicPlugin.Read();
        if(snapshot is null) {
            if(lastMusicPayload!="OFF") {
                await device.Request("MUSIC|OFF");
                lastMusicPayload="OFF";
            }
            musicInfo.Text="Music Player: enabled · no active SMTC session";
            UpdatePluginStatus();
            return;
        }

        string title=CleanMusicField(snapshot.Value.Title);
        string artist=CleanMusicField(snapshot.Value.Artist);
        string payload=$"{(snapshot.Value.Playing?1:0)}|{title}|{artist}";
        if(payload!=lastMusicPayload) {
            await device.Request("MUSIC|SET|"+payload);
            lastMusicPayload=payload;
        }
        musicInfo.Text=$"Music Player: {(snapshot.Value.Playing?"Playing":"Paused")} · {title} · {artist}";
        UpdatePluginStatus();
    }

    void UpdatePluginStatus() {
        string pc=monitorEnabled?"PC Monitor ON":"PC Monitor OFF";
        string music=musicEnabled?"Music ON":"Music OFF";
        pluginStatus.Text=$"Plugins: {pc} · {music}";
    }

    async Task RunDeviceStep(Step step) {
        if(step.Type=="ChangeProfile") {
            int p=int.Parse(step.Value)-1;
            await device.Request($"PROFILE|{p}");activeProfile=p;RefreshProfileButtons();return;
        }
        if(step.Type=="DeviceCtrl") {
            string v=step.Value.Trim().ToUpperInvariant();
            if(v=="MONITOR_TOGGLE"){await ToggleMonitor();return;}
            int delta=v=="PROFILE_NEXT"?1:v=="PROFILE_PREV"?-1:0;
            if(delta!=0) {
                int p=(activeProfile+delta+DeviceLimits.Profiles)%DeviceLimits.Profiles;
                await device.Request($"PROFILE|{p}");activeProfile=p;RefreshProfileButtons();
            }
        }
    }

    async void HandleEvent(string text) {
        var t=text.Split('|');
        if(t.Length==4&&t[1]=="HOST"&&armed.Checked&&!busy&&
           int.TryParse(t[2],out int p)&&p is >=0 and <DeviceLimits.Profiles&&
           int.TryParse(t[3],out int k)&&k is >=0 and <DeviceLimits.Keys) {
            var binding=preset.Profiles[p][k];
            if(binding.Type!="H")return;
            try{await runner.Run(binding,shutdown.Token,RunDeviceStep);}
            catch(OperationCanceledException){}
            catch(Exception ex){Log("MACRO: "+ex.Message);}
        } else if(t.Length>=3&&t[1]=="PROFILE"&&int.TryParse(t[2],out int profile)&&profile is >=0 and <DeviceLimits.Profiles) {
            activeProfile=profile;RefreshProfileButtons();
        } else if(t.Length>=2&&t[1]=="CALDONE") {
            touchInfo.Text="Touch calibration: AFFINE saved";
            status.Text="Touch calibration saved · test all corners now";
            Log(text);
        } else if(t.Length>=2&&t[1]=="CALFAIL") {
            touchInfo.Text="Touch calibration failed: "+string.Join(" · ",t.Skip(2));
            status.Text="Touch calibration failed · retry while holding each target";
            Log(text);
        } else if(t.Length>=4&&t[1]=="CALPOINT") {
            touchInfo.Text=$"Calibration point {t[2]}/4 · raw {t[3]},{(t.Length>=5?t[4]:"?")} captured";
            status.Text=int.TryParse(t[2],out int cp)&&cp<4
                ?$"Point {cp} captured · touch point {cp+1}"
                :"Finishing affine calibration…";
            Log(text);
        } else if(t.Length>=3&&t[1]=="CALWAIT") {
            touchInfo.Text=$"Calibration point {t[2]}/4 · giữ lâu hơn rồi nhả";
            Log(text);
        } else if(t.Length>=7&&t[1]=="TOUCHRAW") {
            string state=t[2]=="1"?"DOWN":"idle";
            touchInfo.Text=$"Touch RAW: {state} · raw {t[3]},{t[4]} · q {t[5]} · mapped {t[6]},{(t.Length>=8?t[7]:"-")}";
        } else if(t.Length>=4&&t[1]=="TOUCH") {
            string quality=t.Length>=5?$" · quality {t[4]}":"";
            touchInfo.Text=$"Touch: x {t[2]} · y {t[3]}{quality}";
        } else if(t.Length>=2&&t[1]=="SCRIPTERR") {
            Log(text);
        }
    }

    void LoadRulesGrid() {
        loading=true;
        try {
            autoGrid.Rows.Clear();
            foreach(var r in preset.AutoProfiles)autoGrid.Rows.Add(r.Enabled,r.Process,r.Profile+1);
            autoProfileEnabled.Checked=preset.AutoProfileEnabled;
        } finally {loading=false;}
    }

    void SaveRulesGrid() {
        var rules=new List<AutoProfileRule>();
        foreach(DataGridViewRow row in autoGrid.Rows) {
            string process=Convert.ToString(row.Cells["Process"].Value)?.Trim()??"";
            if(string.IsNullOrWhiteSpace(process))continue;
            if(process.EndsWith(".exe",StringComparison.OrdinalIgnoreCase))process=process[..^4];
            bool enabled=Convert.ToBoolean(row.Cells["Enabled"].Value??true);
            if(!int.TryParse(Convert.ToString(row.Cells["Profile"].Value),out int profile)||profile is <1 or >DeviceLimits.Profiles)
                throw new FormatException($"Auto profile for {process}: profile must be 1..{DeviceLimits.Profiles}.");
            rules.Add(new AutoProfileRule{Process=process,Profile=profile-1,Enabled=enabled});
        }
        preset.AutoProfiles=rules;preset.AutoProfileEnabled=autoProfileEnabled.Checked;
        preset.Validate();
    }

    void AddForegroundRule() {
        string? process=ForegroundApp.ProcessName();
        if(string.IsNullOrWhiteSpace(process))throw new IOException("Cannot read the foreground app.");
        autoGrid.Rows.Add(true,process,currentProfile+1);
    }

    void DeleteRule() {
        if(autoGrid.CurrentRow is { } row&&!row.IsNewRow)autoGrid.Rows.Remove(row);
    }

    async Task CheckAutoProfile() {
        if(autoSwitching||!preset.AutoProfileEnabled||!device.Connected)return;
        string? process=ForegroundApp.ProcessName();
        if(string.IsNullOrWhiteSpace(process))return;
        var rule=preset.AutoProfiles.FirstOrDefault(r=>r.Enabled&&
            string.Equals(r.Process,process,StringComparison.OrdinalIgnoreCase));
        if(rule is null||rule.Profile==activeProfile)return;
        autoSwitching=true;
        try {
            await device.Request($"PROFILE|{rule.Profile}");
            activeProfile=rule.Profile;RefreshProfileButtons();
        } catch(Exception ex){Log("AUTO PROFILE: "+ex.Message);}
        finally{autoSwitching=false;}
    }

    void SaveLocal() {
        Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
        preset.Save(localPath);
    }

    void SaveLocalQuiet() {
        try{SaveEditor();SaveRulesGrid();SaveLocal();}catch{}
    }

    void CaptureLanguageBase(Control parent) {
        foreach(Control control in parent.Controls) {
            if(!languageBase.ContainsKey(control))languageBase[control]=control.Text;
            CaptureLanguageBase(control);
        }
    }

    void ApplyLanguage() {
        CaptureLanguageBase(this);
        foreach(var pair in languageBase.ToArray()) {
            if(pair.Key.IsDisposed)continue;
            if(pair.Key.Tag is ActionDef action) {
                pair.Key.Text=ActionGlyph(action.Type)+"\n"+UiText.ActionName(action.Type,action.Name,languageCode);
                continue;
            }
            string source=pair.Value;
            if(source.StartsWith("Profile & Key Selection",StringComparison.Ordinal))
                pair.Key.Text=UiText.Translate("Profile & Key Selection",languageCode)+$" · {DeviceLimits.Profiles} profiles";
            else pair.Key.Text=UiText.Translate(source,languageCode);
        }

        for(int i=0;i<screenOff.Items.Count;i++) {
            string english=i switch {0=>"Always On",1=>"30 seconds",2=>"5 minutes",_=>"15 minutes"};
            screenOff.Items[i]=UiText.Translate(english,languageCode);
        }
        RefreshTiles();RefreshProfileButtons();
    }

    void ApplyTheme(bool useDark) {
        dark=useDark;
        Color back=dark?Color.FromArgb(31,33,38):Color.FromArgb(245,246,248);
        Color panel=dark?Color.FromArgb(39,42,48):Color.White;
        Color fore=dark?Color.FromArgb(236,238,242):Color.FromArgb(25,28,34);
        BackColor=back;ForeColor=fore;
        ApplyThemeRecursive(this,back,panel,fore);
        RefreshTiles();RefreshProfileButtons();
    }

    static void ApplyThemeRecursive(Control parent,Color back,Color panel,Color fore) {
        foreach(Control c in parent.Controls) {
            if(c is TextBoxBase or ListBox or DataGridView) {
                c.BackColor=panel;c.ForeColor=fore;
            } else if(c is GroupBox or TabPage or TableLayoutPanel or FlowLayoutPanel or Panel or SplitContainer) {
                c.BackColor=back;c.ForeColor=fore;
            } else if(c is Label) {
                if(c.ForeColor!=Color.DimGray)c.ForeColor=fore;
            }
            ApplyThemeRecursive(c,back,panel,fore);
        }
    }
}

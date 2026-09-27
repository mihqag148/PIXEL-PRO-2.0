namespace PixelPro2;

public sealed record BuiltInPreset(string Name,string Category,string Description,ProfilePreset Profile);

public static class PresetGallery {
    public static string PresetsFolder=>Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PixelPro2","presets");

    public static void EnsureFolder()=>Directory.CreateDirectory(PresetsFolder);

    public static IEnumerable<(string Name,string Path)> LocalFiles() {
        EnsureFolder();
        return Directory.EnumerateFiles(PresetsFolder,"*.json",SearchOption.TopDirectoryOnly)
            .Select(path=>(Path.GetFileNameWithoutExtension(path),path))
            .OrderBy(x=>x.Item1,StringComparer.OrdinalIgnoreCase);
    }

    static Binding Key(string label,string shortcut)=>new(){
        Type="K",Code=HidShortcut.TryParse(shortcut,out int usage,out int mods)?usage:4,
        Modifiers=mods,Label=label,Color=1215,
        Steps=[new Step{Type="Shortcut",Value=shortcut}]
    };

    static Binding Media(string label,string action,int code)=>new(){
        Type="C",Code=code,Modifiers=0,Label=label,Color=1215,
        Steps=[new Step{Type="Media",Value=action}]
    };

    static ProfilePreset Make(params Binding[] keys) {
        if(keys.Length!=DeviceLimits.Keys)throw new ArgumentException("Preset needs 8 keys.");
        return new ProfilePreset{Keys=keys};
    }

    public static IReadOnlyList<BuiltInPreset> BuiltIns { get; }=[
        new("Windows Essentials","Windows","Common Windows editing and navigation",
            Make(Key("Copy","CTRL+C"),Key("Paste","CTRL+V"),Key("Cut","CTRL+X"),Key("Undo","CTRL+Z"),
                 Key("Redo","CTRL+SHIFT+Z"),Key("Shot","WIN+SHIFT+S"),Key("Home","HOME"),Key("End","END"))),
        new("Media Control","Media","Playback and volume controls",
            Make(Media("Play","PLAYPAUSE",205),Media("Next","NEXT",181),Media("Prev","PREV",182),Media("Stop","STOP",183),
                 Media("Vol +","VOLUP",233),Media("Vol -","VOLDOWN",234),Media("Mute","MUTE",226),Key("Space","SPACE"))),
        new("Browser","Navigation","Browser navigation and tabs",
            Make(Key("New Tab","CTRL+T"),Key("Close","CTRL+W"),Key("Reopen","CTRL+SHIFT+T"),Key("Find","CTRL+F"),
                 Key("Back","ALT+LEFT"),Key("Forward","ALT+RIGHT"),Key("Top","HOME"),Key("Bottom","END"))),
        new("Office","Office","Word / Excel / PowerPoint common shortcuts",
            Make(Key("Copy","CTRL+C"),Key("Paste","CTRL+V"),Key("Save","CTRL+S"),Key("Print","CTRL+P"),
                 Key("Bold","CTRL+B"),Key("Undo","CTRL+Z"),Key("Redo","CTRL+Y"),Key("Find","CTRL+F"))),
        new("OBS Studio","OBS","Streaming shortcuts ready to remap in OBS",
            Make(Key("Scene 1","CTRL+F1"),Key("Scene 2","CTRL+F2"),Key("Scene 3","CTRL+F3"),Key("Scene 4","CTRL+F4"),
                 Key("Record","CTRL+F5"),Key("Stream","CTRL+F6"),Key("Mute","CTRL+F7"),Key("Replay","CTRL+F8"))),
        new("Fusion 360","Creative","Useful CAD/navigation shortcuts",
            Make(Key("Undo","CTRL+Z"),Key("Redo","CTRL+Y"),Key("Save","CTRL+S"),Key("Search","S"),
                 Key("Home","HOME"),Key("Fit","F"),Key("Copy","CTRL+C"),Key("Paste","CTRL+V"))),
        new("DaVinci Resolve","Creative","Editing transport shortcuts",
            Make(Key("Play","SPACE"),Key("Cut","CTRL+B"),Key("Undo","CTRL+Z"),Key("Redo","CTRL+SHIFT+Z"),
                 Key("In","I"),Key("Out","O"),Key("Left","LEFT"),Key("Right","RIGHT"))),
        new("Photoshop","Creative","Common Photoshop shortcuts",
            Make(Key("Brush","B"),Key("Move","V"),Key("Select","M"),Key("Crop","C"),
                 Key("Undo","CTRL+Z"),Key("Save","CTRL+S"),Key("100%","CTRL+1"),Key("Fit","CTRL+0")))
    ];
}

public sealed class PresetGalleryForm : Form {
    readonly TextBox search=new(){PlaceholderText="Search presets...",Width=260};
    readonly ComboBox category=new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=150};
    readonly CheckBox filterDuplicates=new(){Text="Filter Duplicated",AutoSize=true,Checked=true};
    readonly FlowLayoutPanel cards=new(){Dock=DockStyle.Fill,AutoScroll=true,Padding=new Padding(8)};
    readonly Action<ProfilePreset> apply;

    public PresetGalleryForm(Action<ProfilePreset> applyPreset) {
        apply=applyPreset;
        Text="PIXEL PRO · Presets Gallery";
        Size=new Size(880,650);
        MinimumSize=new Size(720,520);
        StartPosition=FormStartPosition.CenterParent;
        Font=new Font("Segoe UI",10);

        var root=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=2,ColumnCount=1,Padding=new Padding(10)};
        root.RowStyles.Add(new RowStyle(SizeType.Absolute,48));
        root.RowStyles.Add(new RowStyle(SizeType.Percent,100));

        var tools=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false};
        tools.Controls.Add(search);
        category.Items.Add("All");
        category.Items.Add("Local");
        foreach(var c in PresetGallery.BuiltIns.Select(x=>x.Category).Distinct().Order())
            category.Items.Add(c);
        category.SelectedIndex=0;
        tools.Controls.Add(category);
        tools.Controls.Add(filterDuplicates);

        var folder=new Button{Text="Open Presets Folder",AutoSize=true,Height=30};
        folder.Click+=(_,_)=>{
            PresetGallery.EnsureFolder();
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
                PresetGallery.PresetsFolder){UseShellExecute=true});
        };
        tools.Controls.Add(folder);

        var online=new Button{Text="Open Community Gallery",AutoSize=true,Height=30};
        online.Click+=(_,_)=>System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
            "https://www.eezbotfun.com/en/files"){UseShellExecute=true});
        tools.Controls.Add(online);

        var import=new Button{Text="Import Downloaded",AutoSize=true,Height=30};
        import.Click+=(_,_)=>{
            using var dialog=new OpenFileDialog{Filter="PIXEL PRO profile|*.profile.json;*.json"};
            if(dialog.ShowDialog()==DialogResult.OK){
                PresetGallery.EnsureFolder();
                string target=Path.Combine(PresetGallery.PresetsFolder,Path.GetFileName(dialog.FileName));
                if(!string.Equals(Path.GetFullPath(dialog.FileName),Path.GetFullPath(target),StringComparison.OrdinalIgnoreCase))
                    File.Copy(dialog.FileName,target,true);
                apply(ProfilePreset.Load(target));DialogResult=DialogResult.OK;
            }
        };
        tools.Controls.Add(import);

        root.Controls.Add(tools,0,0);
        root.Controls.Add(cards,0,1);
        Controls.Add(root);

        search.TextChanged+=(_,_)=>RefreshCards();
        category.SelectedIndexChanged+=(_,_)=>RefreshCards();
        filterDuplicates.CheckedChanged+=(_,_)=>RefreshCards();
        RefreshCards();
    }

    void RefreshCards() {
        cards.SuspendLayout();
        cards.Controls.Clear();
        string q=search.Text.Trim();
        string cat=category.SelectedItem?.ToString()??"All";
        IEnumerable<BuiltInPreset> items=PresetGallery.BuiltIns;
        if(cat!="All")items=items.Where(x=>x.Category==cat);
        if(q.Length>0)items=items.Where(x=>
            x.Name.Contains(q,StringComparison.OrdinalIgnoreCase)||
            x.Category.Contains(q,StringComparison.OrdinalIgnoreCase)||
            x.Description.Contains(q,StringComparison.OrdinalIgnoreCase));
        if(filterDuplicates.Checked)items=items.GroupBy(x=>x.Name,StringComparer.OrdinalIgnoreCase).Select(x=>x.First());

        foreach(var local in PresetGallery.LocalFiles()) {
            if(q.Length>0&&!local.Name.Contains(q,StringComparison.OrdinalIgnoreCase))continue;
            if(cat!="All"&&cat!="Local")continue;
            var localCard=new Panel{Width=250,Height=128,Margin=new Padding(8),BorderStyle=BorderStyle.FixedSingle,Padding=new Padding(10)};
            localCard.Controls.Add(new Label{Text=local.Name,AutoSize=true,Font=new Font("Segoe UI",11,FontStyle.Bold),Location=new Point(10,10)});
            localCard.Controls.Add(new Label{Text="Local",AutoSize=true,ForeColor=Color.DimGray,Location=new Point(10,36)});
            localCard.Controls.Add(new Label{Text="AppData preset",AutoSize=false,Size=new Size(225,34),Location=new Point(10,58)});
            var useLocal=new Button{Text="Add Preset",Width=100,Height=27,Location=new Point(138,94)};
            string localPath=local.Path;
            useLocal.Click+=(_,_)=>{apply(ProfilePreset.Load(localPath));DialogResult=DialogResult.OK;};
            localCard.Controls.Add(useLocal);
            cards.Controls.Add(localCard);
        }

        foreach(var preset in items) {
            var card=new Panel{Width=250,Height=128,Margin=new Padding(8),BorderStyle=BorderStyle.FixedSingle,Padding=new Padding(10)};
            card.Controls.Add(new Label{Text=preset.Name,AutoSize=true,Font=new Font("Segoe UI",11,FontStyle.Bold),Location=new Point(10,10)});
            card.Controls.Add(new Label{Text=preset.Category,AutoSize=true,ForeColor=Color.DimGray,Location=new Point(10,36)});
            card.Controls.Add(new Label{Text=preset.Description,AutoSize=false,Size=new Size(225,34),Location=new Point(10,58)});
            var use=new Button{Text="Add Preset",Width=100,Height=27,Location=new Point(138,94)};
            use.Click+=(_,_)=>{apply(preset.Profile);DialogResult=DialogResult.OK;};
            card.Controls.Add(use);
            cards.Controls.Add(card);
        }
        cards.ResumeLayout();
    }
}

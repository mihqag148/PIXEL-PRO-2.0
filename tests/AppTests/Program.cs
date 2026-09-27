using PixelPro2;
using Binding = PixelPro2.Binding;
using Shortcut = PixelPro2.Shortcut;

static void Check(bool test){if(!test)throw new Exception("Assertion failed");}
static void Reject(Action action){try{action();}catch(FormatException){return;}throw new Exception("Expected rejection");}

Check(Protocol.CompatibleHello("PIXELPRO2|2.4.0|25|8|HX8357B|HID,CDC,MEDIA,MONITOR,MOUSE,SCRIPT,TOUCHDIAG"));
Check(!Protocol.CompatibleHello("PIXELPRO2|2.3.0|5|8|HX8357B|HID,CDC"));
Check(!Protocol.CompatibleHello("PIXELPRO2|3.0.0|25|8|HX8357B|HID,CDC"));
Check(!Protocol.CompatibleHello("PIXELPRO2|2.4.0|20|8|HX8357B|HID,CDC"));

var b=Binding.Parse("K|6|1|1215|Copy");
Check(b.Wire(0,0)=="SET|0|0|K|6|1|1215|Copy");
Reject(()=>Binding.Parse("K|65535|0|0|Bad"));
Check(Binding.Parse("P|24|0|0|P25").Code==24);
Reject(()=>Binding.Parse("P|25|0|0|Bad"));
Reject(()=>Binding.Parse("H|0|1|0|Bad"));
Reject(()=>Binding.Parse("C|1234|0|0|Bad"));
Reject(()=>Binding.Parse("D|0|0|0|too-long-label"));
Check(Binding.Parse("M|1|0|1215|Click").Type=="M");
Check(Binding.Parse("S|0|0|1215|Macro").Type=="S");
Reject(()=>Binding.Parse("M|7|0|1215|Bad"));
Reject(()=>Binding.Parse("S|1|0|1215|Bad"));

Check(Shortcut.Parse("CTRL+SHIFT+S").SequenceEqual(new ushort[]{17,16,83}));
Check(Shortcut.Parse("HOME").Single()==0x24);
Reject(()=>Shortcut.Parse("CTRL+BOGUS"));
Check(MacroValue.Point("20,-10")==(20,-10));
Check(MacroValue.Wheel("-120")==-120);
Check(MacroValue.Click("doubleleft")=="DOUBLELEFT");
Reject(()=>MacroValue.Point("bad"));
Reject(()=>MacroValue.Wheel("9999"));
Reject(()=>MacroValue.Click("SIDE"));

Check(HidShortcut.TryParse("CTRL+SHIFT+S",out int hidUsage,out int hidMods));
Check(hidUsage==22&&hidMods==3);
Check(HidShortcut.Format(hidUsage,hidMods)=="CTRL+SHIFT+S");
Check(HidShortcut.TryParse("F24",out int f24,out int f24mods)&&f24==115&&f24mods==0);
Check(HidShortcut.TryParse("COMMAND+OPTION+F13",out int f13,out int macMods)&&f13==104&&macMods==(8|4));
Check(HidShortcut.TryParse("PRINTSCREEN",out int printUsage,out _)&&printUsage==70);
Check(HidShortcut.TryParse("KP_9",out int kp9,out _)&&kp9==97);

var eezScript=EezScript.Expand("""
WINDOWS r
DELAY 500
STRING www.example.com
ENTER
MOUSE_MOVE 100 -20
LMOUSE
REPEAT 1
GOTO_PROFILE 25
PREV_PROFILE
NEXT_PROFILE
""",true);
Check(eezScript.Any(x=>x.Type=="NativeChordTimed"));
Check(eezScript.Any(x=>x.Type=="NativeTextTimed"));
Check(eezScript.Any(x=>x.Type=="NativeMouseMove"));
Check(eezScript.Count(x=>x.Type=="MouseClick")>=2);
byte[] scriptBinary=MediaCodec.FromNativeScript([new Step{Type="Script",Value="""
DEFAULTDURATION 35
DEFAULTCHARDELAY 25
DEFAULTDELAY 18
CONTROL SHIFT ESC
STRING hello
MOUSE_MOVE 10 0
F24
KP_9
"""}]);
Check(scriptBinary[0]=='P'&&scriptBinary[3]=='2');
Check(scriptBinary.Contains((byte)9));
Check(scriptBinary.Contains((byte)12));
Check(scriptBinary.Contains((byte)13));
Reject(()=>EezScript.Expand("MOUSE_MOVE 999 0",true));
Reject(()=>EezScript.Expand("GOTO_PROFILE 26",true));

var nativeSteps=Enumerable.Range(0,512)
    .Select(_=>new Step{Type="Delay",Value="1"}).ToList();
byte[] native=MediaCodec.FromNativeScript(nativeSteps);
Check(native[0]=='P'&&native[1]=='X'&&native[2]=='S'&&native[3]=='2');
Check(BitConverter.ToUInt16(native,4)==512);
Check(native.Length<=8192);
Check(MediaCodec.CanEncodeNative([
    new Step{Type="Shortcut",Value="CTRL+C"},
    new Step{Type="Delay",Value="50"},
    new Step{Type="Media",Value="PLAYPAUSE"}
]));
Check(!MediaCodec.CanEncodeNative([new Step{Type="LaunchApp",Value=@"C:\Windows\notepad.exe"}]));
Check(!MediaCodec.CanEncodeNative([new Step{Type="PowerOff",Value=""}]));
var powerBinding=new Binding{Type="H",Code=0,Modifiers=0,Label="Power",Steps=[new Step{Type="PowerOff",Value=""}]};
powerBinding.Validate();

var preset=new Preset();
preset.Profiles[0][0].Type="H";
preset.Profiles[0][0].Code=0;
preset.Profiles[0][0].Steps.AddRange([
    new Step{Type="MouseMove",Value="10,20"},
    new Step{Type="MouseClick",Value="LEFT"},
    new Step{Type="KeyDown",Value="CTRL"},
    new Step{Type="KeyUp",Value="CTRL"},
    new Step{Type="Delay",Value="250"}
]);
preset.Validate();
preset.Profiles[0][0].Steps.Add(new Step{Type="Delay",Value="99999"});
Reject(preset.Validate);

preset=new Preset();
preset.AutoProfileEnabled=true;
preset.AutoProfiles.Add(new AutoProfileRule{Process="Fusion360",Profile=3,Enabled=true});
preset.Validate();
string path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".json");
try{
    preset.Save(path);
    var loaded=Preset.Load(path);
    Check(loaded.Profiles.Length==DeviceLimits.Profiles);
    Check(loaded.Profiles[24][7].Code==11);
    Check(loaded.Schema==4&&loaded.AutoProfiles.Count==1&&loaded.AutoProfiles[0].Profile==3);
}finally{File.Delete(path);}

// Migrate a legacy schema-3 five-profile preset into schema 4 / 25 profiles.
string legacyPath=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".json");
try {
    var legacyProfiles=new Preset().Profiles.Take(5).ToArray();
    legacyProfiles[4][7].Label="Legacy";
    var legacyJson=System.Text.Json.JsonSerializer.Serialize(new {
        Schema=3,
        AutoProfiles=Array.Empty<AutoProfileRule>(),
        AutoProfileEnabled=true,
        Profiles=legacyProfiles
    });
    File.WriteAllText(legacyPath,legacyJson);
    var migrated=Preset.Load(legacyPath);
    Check(migrated.Schema==4&&migrated.Profiles.Length==25);
    Check(migrated.Profiles[4][7].Label=="Legacy");
    Check(migrated.Profiles[24][0].Code==4);
} finally { File.Delete(legacyPath); }

// Single-profile import/export document.
string profilePath=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".profile.json");
try {
    var profileDoc=new ProfilePreset{Keys=new Preset().Profiles[24].ToArray()};
    profileDoc.Keys[0].Label="Imported";
    profileDoc.Save(profilePath);
    var loadedProfile=ProfilePreset.Load(profilePath);
    Check(loadedProfile.Keys.Length==8&&loadedProfile.Keys[0].Label=="Imported");
} finally { File.Delete(profilePath); }

Console.WriteLine("App protocol, 25-profile migration, macro validation and preset round-trip tests passed.");

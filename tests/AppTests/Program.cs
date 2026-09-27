using PixelPro2;
using Binding = PixelPro2.Binding;
using Shortcut = PixelPro2.Shortcut;

static void Check(bool test){if(!test)throw new Exception("Assertion failed");}
static void Reject(Action action){try{action();}catch(FormatException){return;}throw new Exception("Expected rejection");}

Check(Protocol.CompatibleHello("PIXELPRO2|2.3.0|5|8|HX8357B|HID,CDC,MEDIA,MONITOR,MOUSE,SCRIPT"));
Check(Protocol.CompatibleHello("PIXELPRO2|2.0.1|5|8|HX8357B|HID,CDC"));
Check(!Protocol.CompatibleHello("PIXELPRO2|3.0.0|5|8|HX8357B|HID,CDC"));
Check(!Protocol.CompatibleHello("PIXELPRO2|2.1.0|20|8|HX8357B|HID,CDC"));

var b=Binding.Parse("K|6|1|1215|Copy");
Check(b.Wire(0,0)=="SET|0|0|K|6|1|1215|Copy");
Reject(()=>Binding.Parse("K|65535|0|0|Bad"));
Reject(()=>Binding.Parse("P|5|0|0|Bad"));
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
    Check(loaded.Profiles[4][7].Code==11);
    Check(loaded.Schema==3&&loaded.AutoProfiles.Count==1&&loaded.AutoProfiles[0].Profile==3);
}finally{File.Delete(path);}

Console.WriteLine("App protocol, keymap, macro validation and preset round-trip tests passed.");

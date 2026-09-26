using PixelPro2;
static void Check(bool test){if(!test)throw new Exception("Assertion failed");}
static void Reject(Action action){try{action();}catch(FormatException){return;}throw new Exception("Expected rejection");}
Check(Protocol.CompatibleHello("PIXELPRO2|2.0.1|5|8|HX8357B|HID,CDC"));
Check(Protocol.CompatibleHello("PIXELPRO2|2.0.0|5|8|HX8357B|HID,CDC"));
Check(!Protocol.CompatibleHello("PIXELPRO2|3.0.0|5|8|HX8357B|HID,CDC"));
Check(!Protocol.CompatibleHello("PIXELPRO2|2.0.1|20|8|HX8357B|HID,CDC"));
var b=Binding.Parse("K|6|1|1215|Copy");Check(b.Wire(0,0)=="SET|0|0|K|6|1|1215|Copy");
Reject(()=>Binding.Parse("K|65535|0|0|Bad"));Reject(()=>Binding.Parse("P|5|0|0|Bad"));
Reject(()=>Binding.Parse("H|0|1|0|Bad"));Reject(()=>Binding.Parse("C|1234|0|0|Bad"));
Reject(()=>Binding.Parse("D|0|0|0|too-long-label"));
Check(Shortcut.Parse("CTRL+SHIFT+S").SequenceEqual(new ushort[]{17,16,83}));Reject(()=>Shortcut.Parse("CTRL+BOGUS"));
var preset=new Preset();preset.Validate();preset.Profiles[0][0].Steps.Add(new Step{Type="Delay",Value="99999"});Reject(preset.Validate);
preset=new Preset();string path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".json");
try{preset.Save(path);Check(Preset.Load(path).Profiles[4][7].Code==11);}finally{File.Delete(path);}
Console.WriteLine("App model, protocol, macro validation and preset round-trip tests passed.");

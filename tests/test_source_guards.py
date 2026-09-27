from pathlib import Path
import unittest

ROOT=Path(__file__).resolve().parents[1]

class SourceGuardTests(unittest.TestCase):
    def test_custom_cdc_owns_interface_zero(self):
        workflow=(ROOT/'.github/workflows/build-release.yml').read_text()
        firmware=(ROOT/'firmware/PixelPro2/PixelPro2.ino').read_text()
        self.assertIn('CDCOnBoot=default',workflow)
        self.assertIn('USBCDC usbLink;',firmware)
        self.assertIn('usbLink.enableReboot(false);',firmware)
        self.assertNotIn('if(usbLink)usbLink.println(text);',firmware)

    def test_orientation_migration_and_panel_blank(self):
        firmware=(ROOT/'firmware/PixelPro2/PixelPro2.ino').read_text()
        display=(ROOT/'firmware/PixelPro2/DisplayMode.h').read_text()
        panel=(ROOT/'firmware/PixelPro2/Panel.h').read_text()
        self.assertIn('getUChar("orient2",0)',firmware)
        self.assertIn('return 0x68 ^',display)
        self.assertLess(panel.index('reg(0x28);delay(10);'),panel.index('reg(0x01);delay(150)'))
        self.assertIn('reg(0x11);delay(150);',panel)
        self.assertIn('reg(0x29);delay(50);',panel)
        self.assertLess(panel.index('fillScreen(0x0000);'),panel.index('reg(0x29);delay(50)'))
        self.assertIn('panel.begin(displayMode);',firmware)
        self.assertNotIn('initialPanelBackground',firmware)

    def test_touch_and_fast_boot_guards(self):
        firmware=(ROOT/'firmware/PixelPro2/PixelPro2.ino').read_text()
        # Actual MCUFRIEND raw geometry: tp.x is sampled from D13 and tp.y
        # from D14. Screen X follows rawY reversed, screen Y follows rawX.
        self.assertIn('rawX=uint16_t(1023-readTouchAdc10(yp));',firmware)
        self.assertIn('rawY=uint16_t(1023-readTouchAdc10(xm));',firmware)
        self.assertIn('map(long(rawY),942L,139L,0L,479L)',firmware)
        self.assertIn('map(long(rawX),136L,907L,0L,319L)',firmware)
        # Calibration must be pressure-independent and solve a full affine
        # transform from four physical targets.
        self.assertIn('touchcal2',firmware)
        self.assertIn('fabsf(determinant)<1500.0f',firmware)
        self.assertIn('touchMedian7',firmware)
        self.assertIn('TOUCHDIAG',firmware)
        self.assertNotIn('pressure>=45',firmware)
        self.assertIn('constexpr uint32_t pollMs=20',firmware)
        self.assertIn('constexpr uint8_t confirmCount=3',firmware)
        self.assertIn('constexpr uint8_t releaseMissCount=4',firmware)
        self.assertIn('uint16_t stabilityLimit=calibrating?180:100',firmware)
        setup=firmware.split('void setup()',1)[1]
        self.assertIn('flashReady=SPIFFS.begin(false);',setup)
        self.assertNotIn('flashReady=SPIFFS.begin(true);',setup)
        self.assertIn('sdAttempted=false',firmware)

    def test_profile_count_and_migration(self):
        firmware=(ROOT/'firmware/PixelPro2/PixelPro2.ino').read_text()
        model=(ROOT/'firmware/PixelPro2/Model.h').read_text()
        self.assertIn('Profiles=25',model)
        self.assertIn('LegacyConfiguration5',firmware)
        self.assertIn('PIXELPRO2|2.5.0|25|8|HX8357B',firmware)
        self.assertIn('profileBank=(profile/5)*5',firmware)
        self.assertIn('Pixel::number(tokens[2],Pixel::Profiles-1,p)',firmware)

    def test_studio_parity_guards(self):
        studio=(ROOT/'app/PixelPro2/StudioForm.cs').read_text()
        device=(ROOT/'app/PixelPro2/Device.cs').read_text()
        hub=(ROOT/'app/PixelPro2/DeviceHub.cs').read_text()
        models=(ROOT/'app/PixelPro2/Models.cs').read_text()
        self.assertIn('readonly DeviceHub device=new();',studio)
        self.assertIn('BuildPlugins()',studio)
        self.assertIn('PresetGalleryForm',studio)
        self.assertIn('Music Player Plugin (SMTC)',studio)
        self.assertIn('language.SelectedIndexChanged',studio)
        self.assertIn('new("Script","Script"',studio)
        self.assertIn('>=DeviceLimits.Profiles',device)
        self.assertIn('Dictionary<string,Device>',hub)
        self.assertIn('public static class EezScript',models)
        self.assertIn("fn is >=1 and <=24",models)
        self.assertIn('>=104 and <=115 => $"F{usage-91}"',models)
        main_project=(ROOT/'app/PixelPro2/PixelPro2.csproj').read_text()
        pipe_host=(ROOT/'app/PixelPro2/NamedPipePluginHost.cs').read_text()
        pc_project=(ROOT/'app/PixelPro2.PcMonitorPlugin/PixelPro2.PcMonitorPlugin.csproj').read_text()
        music_project=(ROOT/'app/PixelPro2.MusicPlugin/PixelPro2.MusicPlugin.csproj').read_text()
        workflow=(ROOT/'.github/workflows/build-release.yml').read_text()
        self.assertNotIn('LibreHardwareMonitorLib',main_project)
        self.assertIn('Compile Remove="SystemMonitor.cs"',main_project)
        self.assertIn('PIXEL_PRO_2_PLUGINS',pipe_host)
        self.assertIn('LibreHardwareMonitorLib',pc_project)
        self.assertIn('MusicPlugin.cs',music_project)
        self.assertIn('PixelPro2.PcMonitorPlugin.exe',workflow)
        self.assertIn('PixelPro2.MusicPlugin.exe',workflow)

    def test_native_hid_script_engine_present(self):
        firmware=(ROOT/'firmware/PixelPro2/PixelPro2.ino').read_text()
        model=(ROOT/'firmware/PixelPro2/Model.h').read_text()
        self.assertIn("case 'S':",model)
        self.assertIn('validateScriptBytes',firmware)
        self.assertIn('SCRIPT"&&n==7',firmware)
        self.assertIn('runScript(now);',firmware)
        self.assertIn('memcmp(d,"PXS2",4)',firmware)
        self.assertIn('steps>512',firmware)
        self.assertIn('type==9',firmware)
        self.assertIn('type==12',firmware)
        self.assertIn('type==13',firmware)
        self.assertIn('mouse.move(x,y,0)',firmware)
        self.assertIn('screenOffSeconds',firmware)
        self.assertIn('cmd=="SCREENOFF"',firmware)
        self.assertIn('cmd=="MUSIC"',firmware)
        self.assertIn('drawMusicScreen()',firmware)
        self.assertIn('monitorActive||musicActive',firmware)

if __name__=='__main__':
    unittest.main()

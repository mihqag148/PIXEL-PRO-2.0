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
        self.assertLess(panel.index('reg(0x28);delay(5);'),panel.index('reg(0x01);delay(15)'))
        self.assertLess(panel.index('fillScreen(initialColor);'),panel.index('reg(0x29);delay(15)'))
        self.assertIn('panel.begin(displayMode,0x0843);',firmware)
        self.assertIn('initialPanelBackground',firmware)

    def test_touch_and_fast_boot_guards(self):
        firmware=(ROOT/'firmware/PixelPro2/PixelPro2.ino').read_text()
        self.assertIn('pressure>=45',firmware)
        self.assertIn('map(rx,touchCal.left,touchCal.right,24,455)',firmware)
        self.assertIn('map(ry,touchCal.top,touchCal.bottom,24,295)',firmware)
        self.assertIn('uint32_t(now-lastTouch)<8',firmware)
        setup=firmware.split('void setup()',1)[1]
        self.assertIn('flashReady=SPIFFS.begin(false);',setup)
        self.assertNotIn('flashReady=SPIFFS.begin(true);',setup)
        self.assertIn('sdAttempted=false',firmware)

    def test_native_hid_script_engine_present(self):
        firmware=(ROOT/'firmware/PixelPro2/PixelPro2.ino').read_text()
        model=(ROOT/'firmware/PixelPro2/Model.h').read_text()
        self.assertIn("case 'S':",model)
        self.assertIn('validateScriptBytes',firmware)
        self.assertIn('SCRIPT"&&n==7',firmware)
        self.assertIn('runScript(now);',firmware)
        self.assertIn('memcmp(d,"PXS2",4)',firmware)
        self.assertIn('steps>512',firmware)
        self.assertIn('screenOffSeconds',firmware)
        self.assertIn('cmd=="SCREENOFF"',firmware)

if __name__=='__main__':
    unittest.main()

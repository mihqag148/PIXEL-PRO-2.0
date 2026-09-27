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
        self.assertLess(panel.index('fillScreen(0x0000);'),panel.index('reg(0x29);delay(50)'))

if __name__=='__main__':
    unittest.main()

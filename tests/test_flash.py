import hashlib
import importlib.util
from pathlib import Path
import shutil
import tempfile
import unittest
from unittest.mock import patch

class FlashTests(unittest.TestCase):
    def test_full_image_offset_and_checksum_guard(self):
        with tempfile.TemporaryDirectory() as temp:
            folder=Path(temp).resolve()
            path=folder/'flash_firmware.py'
            shutil.copyfile(Path(__file__).resolve().parents[1]/'tools/flash_firmware.py',path)
            image=folder/'PIXEL_PRO_2_merged.bin'
            image.write_bytes(b'test firmware')
            (folder/'SHA256SUMS.txt').write_text(hashlib.sha256(image.read_bytes()).hexdigest()+'  '+image.name+'\n')
            spec=importlib.util.spec_from_file_location('flash_test_target',path)
            module=importlib.util.module_from_spec(spec)
            spec.loader.exec_module(module)
            with patch.object(module.sys,'argv',[str(path),'--port','COM5']),patch.object(module.subprocess,'run') as run:
                module.main()
                command=run.call_args.args[0]
                self.assertEqual(command[-3:],['write-flash','0x0',str(image)])
                self.assertIn('esp32s2',command)
                run.reset_mock()
                image.write_bytes(b'corrupted')
                with self.assertRaises(SystemExit):module.main()
                run.assert_not_called()

if __name__=='__main__':unittest.main()

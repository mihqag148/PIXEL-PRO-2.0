import hashlib
import json
import os
from pathlib import Path
import shutil
import struct
import subprocess
import sys
import zipfile

raw = Path('build/raw')
out = Path('dist/firmware')
out.mkdir(parents=True, exist_ok=True)
def one(pattern):
    files = list(raw.glob(pattern))
    assert len(files) == 1, (pattern, files)
    return files[0]
app = one('*.ino.bin')
boot = one('*.ino.bootloader.bin')
part = one('*.ino.partitions.bin')
entries = {}
data = part.read_bytes()
for pos in range(0, len(data)-31, 32):
    chunk = data[pos:pos+32]
    if struct.unpack_from('<H', chunk)[0] != 0x50AA:
        continue
    name = chunk[12:28].split(b'\0',1)[0].decode()
    entries[name] = struct.unpack_from('<II', chunk,4)
assert entries['factory'] == (0x10000,0x200000), entries
assert entries['spiffs'] == (0x210000,0x1F0000), entries
assert app.stat().st_size <= 0x200000
for source, name in [(app,'app'),(boot,'bootloader'),(part,'partitions')]:
    shutil.copyfile(source,out/f'PIXEL_PRO_2_{name}.bin')
core = Path(sys.argv[1])
shutil.copyfile(core/'tools/partitions/boot_app0.bin',out/'boot_app0.bin')
merged = out/'PIXEL_PRO_2_merged.bin'
subprocess.run([sys.executable,'-m','esptool','--chip','esp32s2','merge-bin','-o',str(merged),
    '0x1000',str(out/'PIXEL_PRO_2_bootloader.bin'),'0x8000',str(out/'PIXEL_PRO_2_partitions.bin'),
    '0xe000',str(out/'boot_app0.bin'),'0x10000',str(out/'PIXEL_PRO_2_app.bin')],check=True)
image=merged.read_bytes()
assert image[0x1000] == 0xE9, 'Missing ESP32-S2 bootloader at 0x1000'
assert image[0x8000:0x8000+len(data)] == data, 'Partition table offset mismatch'
assert image[0x10000:0x10000+app.stat().st_size] == app.read_bytes()
for name in ['FLASH.md','HARDWARE.md','PROTOCOL.md','VALIDATION.md']:
    shutil.copyfile(Path('docs')/name,out/name)
shutil.copyfile('THIRD_PARTY.md',out/'THIRD_PARTY.md')
shutil.copyfile('tools/flash_firmware.py',out/'flash_firmware.py')
(out/'FLASH-OFFSETS.txt').write_text(
    'ESP32-S2 / Flash Download Tool\n'
    'FULL INSTALL: select ONLY PIXEL_PRO_2_merged.bin at 0x000000\n'
    'APP UPDATE: select ONLY PIXEL_PRO_2_app.bin at 0x010000 (existing v2 partition only)\n'
    'Never flash the merged image at 0x10000. Do not select both images.\n'
    'Use SPI Download, DIO, 40 MHz, 4 MB. Reset the board after flashing.\n')
(out/'manifest.json').write_text(json.dumps({'version':Path('VERSION').read_text().strip(),
    'commit':os.environ.get('GITHUB_SHA','local'),'chip':'esp32s2','mergedOffset':'0x0',
    'appOffset':'0x10000','core':'3.3.12','hardwareValidated':False},indent=2))
(out/'SHA256SUMS.txt').write_text(''.join(f'{hashlib.sha256(p.read_bytes()).hexdigest()}  {p.name}\n' for p in sorted(out.glob('*.bin'))))
with zipfile.ZipFile('dist/PIXEL-PRO-2.0-firmware.zip','w',zipfile.ZIP_DEFLATED) as archive:
    for p in sorted(out.iterdir()): archive.write(p,p.name)
print('Firmware image, partition checks and package passed')

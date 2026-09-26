"""Flash only the adjacent, checksum-verified PIXEL PRO 2 merged image at 0x0."""
import argparse
import hashlib
from pathlib import Path
import subprocess
import sys

def main():
    parser=argparse.ArgumentParser(description='PIXEL PRO 2 full firmware flasher (ESP32-S2, offset 0x0)')
    parser.add_argument('--port',required=True,help='ROM download COM port, for example COM5')
    args=parser.parse_args()
    folder=Path(__file__).resolve().parent
    image=folder/'PIXEL_PRO_2_merged.bin'
    sums=folder/'SHA256SUMS.txt'
    expected={line.split()[1]:line.split()[0] for line in sums.read_text().splitlines() if line.strip()}
    if hashlib.sha256(image.read_bytes()).hexdigest()!=expected.get(image.name):
        raise SystemExit('Checksum mismatch. Download and extract the firmware ZIP again.')
    subprocess.run([sys.executable,'-m','esptool','--chip','esp32s2','--port',args.port,
        'write-flash','0x0',str(image)],check=True)

if __name__=='__main__':main()

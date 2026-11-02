import subprocess
import sys

def win():
    print("Starting Windows img-converter Build...")
    command = [
        "nuitka",
        "--standalone",
        "--onefile",
        '--include-data-files=texconv.exe=texconv.exe',
        '--output-dir=..\\published-components\\img-converter\\windows',
        "img-converter.py"
    ]
    subprocess.run(command, check=True)

def linux():
    print("Starting Linux img-converter Build...")
    command = [
        "nuitka",
        "--standalone",
        "--onefile",
        "--include-data-files=texconv=texconv",
        "--output-dir=../published-components/img-converter/linux",
        "img-converter.py"
    ]
    subprocess.run(command, check=True)
import os
import shutil
import subprocess
import sys


def build():
    extra_args = []

    if sys.platform.startswith("win"):
        print("Starting Windows img-converter Build...")
        output_dir = os.path.abspath("../published-components/img-converter/windows")
        binary_name = "img-converter.exe"
        data_file_arg = "--include-data-files=texconv.exe=texconv.exe"
        # Compile with Nuitka's Zig backend instead of MSVC or MinGW64 to produce a clean
        # 64-bit binary without Visual Studio Build Tools or Python 3.13+ MinGW64 issues. 
        # Switching compiler backends/flags prevents prior app crashes tied to mismatched
        # CPU/chipset instruction tuning in earlier builds without fallback glue code.
        # Quirk: `--zig` requires 64-bit Python and may download Zig on first run.
        # 32-bit CPU usage for this tool is effectively negligible (~0%), so dropping 32-bit here is acceptable.
        extra_args.append("--zig")  
    elif sys.platform.startswith("linux"):
        print("Starting Linux img-converter Build...")
        output_dir = os.path.abspath("../published-components/img-converter/linux")
        binary_name = "img-converter"
        data_file_arg = "--include-data-files=texconv=texconv"
    else:
        print(f"Unsupported operating system: {sys.platform}")
        sys.exit(1)

    command = [
        sys.executable,
        "-m",
        "nuitka",
        "--standalone",
        "--onefile",
        *extra_args,
        data_file_arg,
        f"--output-dir={output_dir}",
        f"--output-filename={binary_name}",
        "img-converter.py",
    ]

    subprocess.run(command, check=True)

    print("Cleaning up intermediate build folders inside output directory...")
    artifact_base = os.path.splitext(binary_name)[0]
    artifacts_to_remove = [
        f"{artifact_base}.build",
        f"{artifact_base}.dist",
        f"{artifact_base}.onefile-build",
        "img-converter.bin",  # Cleanup any legacy .bin artifacts if present
    ]

    for item in artifacts_to_remove:
        item_path = os.path.join(output_dir, item)
        if os.path.isdir(item_path):
            shutil.rmtree(item_path)
        elif os.path.isfile(item_path):
            os.remove(item_path)

    final_executable_path = os.path.join(output_dir, binary_name)
    print(f"Done! Clean extensionless standalone binary saved to: {final_executable_path}")


if __name__ == "__main__":
    build()
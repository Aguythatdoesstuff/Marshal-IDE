import os
import shutil
import subprocess
import sys

def build():
    if sys.platform.startswith("win"):
        print("Starting Windows img-converter Build...")
        output_dir = os.path.abspath("../published-components/img-converter/windows")
        binary_name = "img-converter.exe"
        data_file_arg = "--include-data-files=texconv.exe=texconv.exe"
    elif sys.platform.startswith("linux"):
        print("Starting Linux img-converter Build...")
        output_dir = os.path.abspath("../published-components/img-converter/linux")
        binary_name = "img-converter"
        data_file_arg = "--include-data-files=texconv=texconv"
    else:
        print(f"Unsupported operating system: {sys.platform}")
        sys.exit(1)

    command = [
        "nuitka",
        "--standalone",
        "--onefile",
        data_file_arg,
        f"--output-dir={output_dir}",
        f"--output-filename={binary_name}",
        "img-converter.py"
    ]

    # Run the Nuitka build
    subprocess.run(command, check=True)

    # Clean up intermediate build folders inside the output directory
    print("Cleaning up intermediate build folders inside output directory...")
    artifacts_to_remove = [
        f"{binary_name}.build",
        f"{binary_name}.dist",
        f"{binary_name}.onefile-build",
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
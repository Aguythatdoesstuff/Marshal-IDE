import json
import os
import platform
import stat
import subprocess
import sys
from datetime import datetime
from pathlib import Path


class DualLogger:
    def __init__(self, output_dir: Path, filename: str = "process.log"):
        self.log_dir = output_dir / "img-converter"
        self.log_dir.mkdir(parents=True, exist_ok=True)

        self.log_filepath = self.log_dir / filename
        self.terminal = sys.stdout
        self.log_file = open(self.log_filepath, "a", encoding="utf-8")

    def write(self, message: str) -> None:
        self.terminal.write(message)
        if message.strip() and self.log_file and not self.log_file.closed:
            timestamp = datetime.now().strftime("[%Y-%m-%d %H:%M:%S] ")
            self.log_file.write(f"{timestamp}{message}\n")
            self.log_file.flush()

    def log(self, message: str) -> None:
        """Writes directly to the log file without sending output to stdout/console."""
        if message.strip() and self.log_file and not self.log_file.closed:
            timestamp = datetime.now().strftime("[%Y-%m-%d %H:%M:%S] ")
            self.log_file.write(f"{timestamp}{message}\n")
            self.log_file.flush()

    def flush(self) -> None:
        self.terminal.flush()
        if self.log_file and not self.log_file.closed:
            self.log_file.flush()

    def close(self) -> None:
        if self.log_file and not self.log_file.closed:
            self.log_file.close()


def log(message: str) -> None:
    """Helper to write to the log file silently without flooding stdout."""
    if isinstance(sys.stdout, DualLogger):
        sys.stdout.log(message)


def setup_custom_logger(args: list[str]) -> bool:
    """Finds --log-path= argument to initialize logging in [path]/img-converter/.
    
    Returns True if log path was found and initialized, False otherwise.
    """
    log_path: Path | None = None

    for arg in args:
        if arg.startswith("--log-path="):
            raw_path = arg.split("=", 1)[1]
            extracted_path = Path(raw_path)

            if extracted_path.is_absolute():
                log_path = extracted_path
            else:
                print(f"[ERROR] Relative --log-path provided ('{raw_path}'). Absolute path required.")
                return False

    if log_path is None:
        print("[ERROR] Missing required '--log-path=' argument. Logging directory not defined.")
        return False

    sys.stdout = DualLogger(output_dir=log_path, filename="process.log")
    return True


def get_texconv_executable() -> Path:
    """Resolves and validates the texconv binary path sitting next to the executable/script."""
    if getattr(sys, "frozen", False):
        # Path where the compiled img-converter.exe actually resides
        script_dir = Path(sys.executable).resolve().parent
    else:
        # Path when running directly as a .py script
        script_dir = Path(__file__).resolve().parent

    system_name = platform.system()
    if system_name == "Windows":
        exe = script_dir / "texconv.exe"
    elif system_name == "Linux":
        exe = script_dir / "texconv"
    else:
        raise RuntimeError(f"Unsupported Operating System: {system_name}")

    if not exe.exists():
        raise FileNotFoundError(f"Missing texture conversion binary next to executable: {exe}")

    return exe


def run_texconv(exe: Path, args: list[str]) -> subprocess.CompletedProcess:
    """Spawns an execution of texconv for a specific set of arguments."""
    try:
        return subprocess.run(
            [str(exe)] + args,
            check=True,
            capture_output=True,
            text=True
        )
    except subprocess.CalledProcessError as err:
        raise RuntimeError(f"texconv failed (code {err.returncode}):\n{err.stderr}") from err

def process_command(cmd_str: str, texconv_exe: Path) -> None:
    """Parses and executes a single incoming command line string."""
    cmd_str = cmd_str.strip()
    if not cmd_str:
        return

    if cmd_str.startswith("process-image:"):
        payload = cmd_str.split("process-image:", 1)[1]
        cmd_args: list[str] = []

        try:
            parsed_json = json.loads(payload)
            if isinstance(parsed_json, list):
                cmd_args.extend([str(x) for x in parsed_json])
            else:
                cmd_args.append(str(parsed_json))
        except json.JSONDecodeError as e:
            print(f"[ERROR] JSON decode error in payload: {e}")
            cmd_args.append(payload)

        if cmd_args:
            log(f"Spawning texconv with arguments: {cmd_args}")
            try:
                result = run_texconv(texconv_exe, cmd_args)
                log(f"texconv execution completed successfully:\n{result.stdout}")
                print("[READY] Processing finished.")
            except RuntimeError as err:
                print(f"[ERROR] Conversion failed: {err}")


def main(args: list[str]) -> None:
    # 1. Initialize custom dual logging (exits if --log-path= is missing or invalid)
    if not setup_custom_logger(args):
        sys.exit(1)

    log(f"Received arguments: {args}")
    
    # 2. Locate texconv binary
    try:
        texconv_exe = get_texconv_executable()
    except (FileNotFoundError, RuntimeError) as e:
        print(f"[ERROR] Failed to locate texconv executable: {e}")
        return

    # 3. Process initial command line arguments
    for arg in args:
        if arg.lower() in ("exit process", "exit", "quit", "shutdown"):
            log("Received exit command in arguments. Shutting down.")
            return
        process_command(arg, texconv_exe)

    # 4. Continuous stdin loop listening for further commands from host process
    print("[READY] img-converter Worker started. Listening on stdin...")
    sys.stdout.flush()

    try:
        for line in sys.stdin:
            cmd = line.strip()
            if cmd.lower() in ("exit process", "exit", "quit", "shutdown"):
                log("Received shutdown signal from host process. Exiting.")
                break

            process_command(cmd, texconv_exe)
            sys.stdout.flush()
    except (KeyboardInterrupt, SystemExit):
        print("Process received termination signal (SIGINT/SIGTERM). Shutting down cleanly.")


if __name__ == "__main__":
    try:
        main(sys.argv[1:])
    finally:
        # Guarantee log file handles are flushed and closed on exit
        if isinstance(sys.stdout, DualLogger):
            sys.stdout.close()
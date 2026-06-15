import { app, BrowserWindow } from 'electron';
import path from 'path';
import { spawn } from 'child_process';
import fs from 'fs';
import os from 'os';

const getCpuTimes = () => {
  const cpus = os.cpus();
  if (!cpus || cpus.length === 0) return { total: 0, idle: 0 };
  let user = 0, nice = 0, sys = 0, idle = 0, irq = 0;
  for (const cpu of cpus) {
    user += cpu.times.user;
    nice += cpu.times.nice;
    sys += cpu.times.sys;
    idle += cpu.times.idle;
    irq += cpu.times.irq;
  }
  return { total: user + nice + sys + idle + irq, idle };
};

let lastCpuTimes = getCpuTimes();

setInterval(() => {
  const currentCpuTimes = getCpuTimes();
  const idleDiff = currentCpuTimes.idle - lastCpuTimes.idle;
  const totalDiff = currentCpuTimes.total - lastCpuTimes.total;
  
  let cpuPercent = 0;
  if (totalDiff > 0) {
    cpuPercent = 100 - (100 * idleDiff / totalDiff);
  }
  
  lastCpuTimes = currentCpuTimes;

  const totalMem = os.totalmem();
  const freeMem = os.freemem();
  const usedMemMB = Math.round((totalMem - freeMem) / (1024 * 1024));

  const wins = BrowserWindow.getAllWindows();
  const activeWin = wins.length > 0 ? wins[0] : null;
  
  if (activeWin && !activeWin.isDestroyed()) {
    activeWin.webContents.send('importer-telemetry', {
      cpu: Math.max(0, Math.min(100, cpuPercent)).toFixed(1),
      ram: usedMemMB.toString()
    });
  }
}, 1000);

/**
 * Spawns the native C# 'compiler' binary in a persistent state.
 * This is called by the background watcher process to maintain an always-on compilation server.
 * @param {Object} params
 * @param {string} params.output - Path to the output directory.
 * @param {string} params.debug - Path to the active session debug directory.
 * @returns {ChildProcess} The active process instance so stdin/stdout can be controlled by the caller.
 */
function runCompiler({ output, debug }) {
  const platform = process.platform;

  // 1. Resolve safe absolute path using the unified helper
  const binaryPath = resolveBinaryPath('compiler');

  // 2. Format argument parameters matching C# expected structure
  const args = [
    `--output=${output}`,
    `--debug=${debug}`
  ];

  // 3. Dynamic Linux execution permission assignment
  if (platform === 'linux') {
    try {
      if (fs.existsSync(binaryPath)) {
        fs.chmodSync(binaryPath, 0o755);
        console.log(`[Compiler] Granted execution permissions (0755) to Linux binary.`);
      }
    } catch (err) {
      console.warn(`[Compiler] Failed to assign permissions on binary:`, err.message);
    }
  }

  console.log(`[Compiler] Spawning PERSISTENT process: ${binaryPath} with args:`, args);

  // 4. Spawn and return process directly so standard I/O pipes can be attached
  return spawn(binaryPath, args);
}

/**
 * Spawns the native C# 'importer' binary and listens to its Console output.
 * * * Directory Structure:
 * c#/published-components/importer/
 * ├── linux/importer
 * └── windows/importer.exe
 * * @param {Object} paths - The configuration paths for the importer.
 * @param {string} paths.input - Path to the input directory.
 * @param {string} paths.output - Path to the output directory.
 * @param {string} paths.debug - Path to the debug directory.
 */
function runImporter({ input, output, debug }) {
  const isDev = !app.isPackaged;
  const platform = process.platform;

  // 1. Resolve base directory based on environment
  const binaryPath = resolveBinaryPath('importer');
  if (!binaryPath) return;

  // 2. Format arguments to match your C# parser's named argument structure
  const args = [
    `--input=${input}`,
    `--output=${output}`,
    `--debug=${debug}`
  ];

  // Fix Linux permission flags: dynamically grant permission if missing
  if (platform === 'linux') {
    try {
      if (fs.existsSync(binaryPath)) {
        fs.chmodSync(binaryPath, 0o755);
        console.log(`[Importer] Successfully assigned execution rights (0755) to Linux binary.`);
      }
    } catch (permissionError) {
      console.warn(`[Importer] Failed to run chmodSync on binary file:`, permissionError);
    }
  }

  console.log(`[Importer] Spawning process: ${binaryPath} with args:`, args);

  // 4. Spawn the process asynchronously and wrap execution in a Promise
  return new Promise((resolve, reject) => {
    const importerProcess = spawn(binaryPath, args);

    // 5. Attach listeners and pass resolve/reject controllers
    handleProcessOutput(importerProcess, resolve, reject);
  });
}

/**
 * Helper function to listen to data streams (Console.WriteLine) from the spawned process.
 * @param {ChildProcess} processInstance - The spawned child process instance.
 * @param {Function} resolve - Promise resolution handler.
 * @param {Function} reject - Promise rejection handler.
 */
function handleProcessOutput(processInstance, resolve, reject) {
  processInstance.stdout.setEncoding('utf8');
  processInstance.stderr.setEncoding('utf8');

  const getFocusedWindow = () => {
    const wins = BrowserWindow.getAllWindows();
    return wins.length > 0 ? wins[0] : null;
  };

  // Listen to standard Console.WriteLine() outputs
  processInstance.stdout.on('data', (data) => {
    const message = data.trim();
    if (message) {
      console.log(`[Importer STDOUT]: ${message}`);
      const win = getFocusedWindow();
      if (win) {
        win.webContents.send('importer-stdout-line', message);
      }
    }
  });

  // Listen to standard Error streams or unhandled exceptions safely
  processInstance.stderr.on('data', (data) => {
    const errorMsg = data.trim();
    if (errorMsg) {
      console.error(`[Importer STDERR]: ${errorMsg}`);
      const win = getFocusedWindow();
      if (win) {
        win.webContents.send('importer-stderr-line', errorMsg);
      }
    }
  });

  let isFinished = false;

  // Fired when the C# app terminates or exits
  processInstance.on('close', (code) => {
    if (isFinished) return;
    isFinished = true;
    console.log(`[Importer] Process exited with code ${code}`);
    if (code === 0) {
      resolve({ success: true });
    } else {
      reject(new Error(`Importer process exited with non-zero code: ${code}`));
    }
  });

  // Fired if the binary fails to spawn entirely (e.g., missing execution permissions on Linux)
  processInstance.on('error', (err) => {
    if (isFinished) return;
    isFinished = true;
    console.error(`[Importer Spawn Error]: ${err.message}`);
    reject(err);
  });

  // Periodically check if the PID is still alive in the OS (guards against silent antivirus kills)
  const pidCheckInterval = setInterval(() => {
    if (isFinished) {
      clearInterval(pidCheckInterval);
      return;
    }
    try {
      // signal 0 tests if the process exists without actually killing it
      process.kill(processInstance.pid, 0);
    } catch (e) {
      // Process is dead/unreachable
      clearInterval(pidCheckInterval);
      if (!isFinished) {
        isFinished = true;
        try { processInstance.kill('SIGKILL'); } catch(err){}
        reject(new Error("Process closed or killed unexpectedly (Antivirus / OS Termination)"));
      }
    }
  }, 1000);
}

/**
 * Helper function to cleanly resolve binary component paths across platforms and environments.
 * Built to support both Electron Main context and Forked Child Process context under Vite.
 * @param {string} componentName - The name of the target component subdirectory ('compiler' or 'importer').
 * @returns {string} The absolute path to the platform-specific executable binary.
 */
function resolveBinaryPath(componentName) {
  const platform = process.platform;
  const isDev = !app || !app.isPackaged || process.env.NODE_ENV === 'development';

  // 1. Array of potential lookups matching exactly where the files live across architectures
  let baseChoices = [];

  if (app && typeof app.getAppPath === 'function') {
    try {
      baseChoices.push(path.join(app.getAppPath(), 'c#', 'published-components', componentName));
    } catch (e) {}
  }

  // Handle Vite development paths relative to current directory structures
  if (typeof __dirname !== 'undefined') {
    baseChoices.push(path.join(__dirname, '..', 'c#', 'published-components', componentName));
    baseChoices.push(path.join(__dirname, '..', '..', '..', 'c#', 'published-components', componentName));
  }

  // Production ASAR unpack structure fallback
  if (app && typeof app.getPath === 'function') {
    try {
      baseChoices.push(path.join(process.resourcesPath, 'published-components', componentName));
    } catch (e) {}
  }
  
  // Standard execution working directory lookup
  baseChoices.push(path.join(process.cwd(), 'c#', 'published-components', componentName));

  let exeName = componentName; // default to lowercase ('importer')
  
  if (componentName === 'compiler') {
    exeName = 'Compiler'; // force uppercase for compiler
  }

  // Combine with the platform extension if on Windows
  const finalFileName = platform === 'win32' ? `${exeName}.exe` : exeName;
  const binaryName = platform === 'win32' 
    ? path.join('windows', finalFileName) 
    : path.join('linux', finalFileName);

  // 3. Loop through choices and find the one that actually contains the files
  for (const baseDir of baseChoices) {
    const fullPath = path.join(baseDir, binaryName);
    if (fs.existsSync(fullPath)) {
      return fullPath;
    }
  }

  // Ultimate fallback layout if disk indexer is running asynchronously under Vite
  const defaultFallbackBase = isDev
    ? path.join(process.cwd(), 'c#', 'published-components', componentName)
    : path.join(process.resourcesPath, 'published-components', componentName);
    
  return path.join(defaultFallbackBase, binaryName);
}

// --- Example Usage ---
// runImporter({
//   input: '/path/to/input',
//   output: '/path/to/output',
//   debug: '/path/to/debug'
// });

export { runImporter, handleProcessOutput, runCompiler };
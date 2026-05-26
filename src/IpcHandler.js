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
  const baseDir = isDev
    ? path.join(app.getAppPath(), 'c#', 'published-components', 'importer')
    : path.join(process.resourcesPath, 'published-components', 'importer');

  // 2. Select correct binary matching the host OS
  let binaryPath;
  if (platform === 'win32') {
    binaryPath = path.join(baseDir, 'windows', 'importer.exe');
  } else if (platform === 'linux') {
    binaryPath = path.join(baseDir, 'linux', 'importer');
  } else {
    console.error(`[Importer] Unsupported OS platform: ${platform}`);
    return;
  }

  // 3. Format arguments to match your C# parser's named argument structure
  const args = [
    `--input=${input}`,
    `--output=${output}`,
    `--debug=${debug}`
  ];

  // Fix Linux permission flags: dynamically grant permission if missing
  if (platform === 'linux') {
    try {
      // 0o755 gives the owner full Read/Write/Execute access so the binary can spin up
      fs.chmodSync(binaryPath, 0o755);
      console.log(`[Importer] Successfully assigned execution rights (0755) to Linux binary.`);
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

// --- Example Usage ---
// runImporter({
//   input: '/path/to/input',
//   output: '/path/to/output',
//   debug: '/path/to/debug'
// });

export { runImporter, handleProcessOutput };
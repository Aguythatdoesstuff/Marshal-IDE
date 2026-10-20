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

    let stdoutBuffer = '';
    let stderrBuffer = '';

    // Helper to get active window for streaming logs
    const getActiveWindow = () => {
      const wins = BrowserWindow.getAllWindows();
      return wins.length > 0 ? wins[0] : null;
    };

    importerProcess.stdout.on('data', (data) => {
      stdoutBuffer += data.toString();
      const lines = stdoutBuffer.split('\n');
      stdoutBuffer = lines.pop(); // Retain incomplete trailing line

      const activeWin = getActiveWindow();
      for (const line of lines) {
        const trimmed = line.trim();
        if (trimmed) {
          console.log(`[Importer] ${trimmed}`);
          if (activeWin && !activeWin.isDestroyed()) {
            activeWin.webContents.send('importer-stdout-line', trimmed);
          }
        }
      }
    });

  importerProcess.stderr.on('data', (data) => {
    stderrBuffer += data.toString();
    const lines = stderrBuffer.split('\n');
    stderrBuffer = lines.pop();

    const activeWin = getActiveWindow();
    for (const line of lines) {
      const trimmed = line.trim();
      if (trimmed) {
        console.error(`[Importer Error] ${trimmed}`);
        if (activeWin && !activeWin.isDestroyed()) {
          activeWin.webContents.send('importer-stderr-line', trimmed);
        }
      }
    }
    });

    importerProcess.on('error', (error) => {
      console.error(`[Importer] Failed to start process:`, error);
      reject(error);
    });

    importerProcess.on('close', (code) => {
      console.log(`[Importer] Process exited with code ${code}`);
      if (code === 0) {
        resolve({ success: true });
      } else {
        reject(new Error(`Importer exited with code ${code}`));
      }
    });
  });
}

/**
 * Helper function to cleanly resolve binary component paths across platforms and environments.
 * Built to support both Electron Main context and Forked Child Process context under Vite.
 * @param {string} componentName - The name of the target component subdirectory ('compiler' or 'importer').
 * @returns {string} The absolute path to the platform-specific executable binary.
 */
function resolveBinaryPath(componentName) {
  const platform = process.platform;
  const isDev = !app.isPackaged;

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

export { runImporter };
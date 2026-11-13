import { app, BrowserWindow } from 'electron';
import { spawn } from 'child_process';
import os from 'os';
import { resolveBinaryPath } from './binaryUtils.js';

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
 * 
 * @param {Object} paths - The configuration paths for the importer.
 * @param {string} paths.input - Path to the input directory.
 * @param {string} paths.output - Path to the output directory.
 * @param {string} paths.debug - Path to the debug directory.
 */
function runImporter({ input, output, debug }) {
  const binaryPath = resolveBinaryPath('importer', (type, msg) => {
    if (type === 'warn') {
      console.warn(`[Importer] ${msg}`);
    } else {
      console.log(`[Importer] ${msg}`);
    }
  });

  const args = [
    `--input=${input}`,
    `--output=${output}`,
    `--debug=${debug}`
  ];

  console.log(`[Importer] Spawning process: ${binaryPath} with args:`, args);

  return new Promise((resolve, reject) => {
    const importerProcess = spawn(binaryPath, args);

    let stdoutBuffer = '';
    let stderrBuffer = '';

    const getActiveWindow = () => {
      const wins = BrowserWindow.getAllWindows();
      return wins.length > 0 ? wins[0] : null;
    };

    importerProcess.stdout.on('data', (data) => {
      stdoutBuffer += data.toString();
      const lines = stdoutBuffer.split('\n');
      stdoutBuffer = lines.pop();

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

export { runImporter };
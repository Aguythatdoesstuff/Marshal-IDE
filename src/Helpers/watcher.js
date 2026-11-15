import { app, BrowserWindow, ipcMain, dialog, Menu } from 'electron';
import path from 'path';
import fs from 'fs-extra';
import { fork } from 'child_process'; 
import { logToSystem } from '../mainInit.js';
import { spawnCompiler, compileFile, stopCompiler } from '../ipcHandlers/compiler.binary.js';
/**
 * Starts the watcher process and sends a message to compile all files after startup.
 * @param {string} projectName 
 */
export async function startWatcher(projectName, state) {
    if (!state.currentProjectConfig) {
    state.appLogger?.error(`Cannot start watcher: No project configuration loaded.`, { source: 'Main-Watcher' });
    return;
    }
    
    // Kill any existing watcher process before starting a new one
    if (state.watcherProcess) {
    state.appLogger?.warn(`Terminating previous watcher process (PID: ${state.watcherProcess.pid}).`, { source: 'Main-Watcher' });
    state.watcherProcess.kill('SIGTERM'); 
    state.watcherProcess = null;
    }


    const watcherPayload = JSON.stringify({
        input_dir: path.resolve(state.INPUT_DIR), 
        output_dir: path.resolve(state.OUTPUT_DIR), 
        project_root: state.currentProjectConfig.project_root,
        mod_name: projectName,
        log_dir: state.getLogsSessionDir(),
        metadata_dir: state.METADATA_DIR
    });
    
    state.watcherProcess = fork(state.WATCHER_SCRIPT_PATH, [watcherPayload], { 
    stdio: ['inherit', 'inherit', 'inherit', 'ipc'],
    execArgv: ['--enable-source-maps'] 
    });
    state.watcherProcess.on('message', (logData) => {
        if (!logData || typeof logData !== 'object') return;

        // Handle compiler control actions sent from watcher
        if (logData.action === 'spawn-compiler') {
            spawnCompiler(logData.config, logToSystem);
            return;
        }
        if (logData.action === 'compile-file') {
            compileFile(logData.filePath, logData.outputOptions, logToSystem);
            return;
        }
        if (logData.action === 'stop-compiler') {
            console.log(`[Main] Received stop-compiler action from watcher. Stopping compiler...`);
            stopCompiler(logData.source, logToSystem);
            return;
        }

        if (logData.action === 'show-warning-dialog') {
            dialog.showMessageBox(BrowserWindow.getFocusedWindow(), {
                type: 'warning',
                title: logData.title,
                message: logData.title,
                detail: logData.message,
                buttons: ['OK']
            });
        }
        logToSystem(logData);
    });
    
    state.watcherProcess.on('error', (err) => {
    state.appLogger?.error(`Watcher child process failed: ${err.message}`, { source: 'Main-Watcher' });
    });

    state.watcherProcess.on('exit', (code, signal) => {
    if (code !== 0 && signal !== 'SIGTERM' && signal !== null) {
        state.appLogger?.error(`Watcher exited unexpectedly. Code: ${code}, Signal: ${signal}`, { source: 'Main-Watcher' });
    }
    state.watcherProcess = null;
    });

    state.appLogger?.info(`Child process for compilation started. PID: ${state.watcherProcess.pid}`, { source: 'Main-Watcher' });
}
import { app, dialog, BrowserWindow } from 'electron';
import path from 'path';
import fs from 'fs-extra';
import AdmZip from 'adm-zip';

export function registerLoggerHandlers(ipcMain, state) {

    ipcMain.handle('get-log-directory', async () => {
        const logPath = state.getLogsRootDir(); 

        if (logPath) { 
        return { success: true, path: logPath };
        }
        
        const errorMessage = "Logger path not initialized.";
        console.error(`[IPC-Handler] - ${errorMessage}`); 
        return { success: false, message: errorMessage };
    });

    
    ipcMain.handle('get-log-broadcaster-methods', () => {
        return state.LogBroadcaster ? { success: true } : { success: false };
    });

    ipcMain.on('log-broadcaster-broadcast', (event, message) => {
        state.LogBroadcaster?.emit('log', message);
    });

    ipcMain.on('renderer-log', (event, data) => {
        state.appLogger?.info(data);
    });
}
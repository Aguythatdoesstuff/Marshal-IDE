import { app, dialog, BrowserWindow } from 'electron';
import path from 'path';
import fs from 'fs-extra';
import AdmZip from 'adm-zip';

export function registerNavigationHandlers(ipcMain, state, logToSystem) {
    ipcMain.on('switch-page', (e, pageName) => {
        if (state.mainWindow) {
            const targetScreen = pageName === 'index' ? 'WORKSPACE' : pageName.toUpperCase();
            state.mainWindow.webContents.send('navigate-to', targetScreen);
        }
    });
}
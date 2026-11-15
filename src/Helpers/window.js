import { app, BrowserWindow, ipcMain, dialog, Menu } from 'electron';
import path from 'path';
import fs from 'fs-extra';
import { setupEulaHandlers } from '../eula_manager.js';

export async function createWindow(state) {
    if (app.isPackaged) {
        Menu.setApplicationMenu(null);
    }
    
    state.mainWindow = new BrowserWindow({
        width: 1200, 
        height: 800,
        backgroundColor: '#1e1e1e',
        titleBarStyle: 'hidden',
        ...(process.platform !== 'darwin' ? {
            titleBarOverlay: {
                color: '#1e1e1e',      
                symbolColor: '#858585', 
                height: 32              
            }
        } : {}),
        show: false, // Keeping it false to prevent flicker
        
        icon: path.join(state.__dirname, '..', '..', '..', 'build', 'icon.png'), 
        
        webPreferences: {
            preload: path.join(state.__dirname, '..', 'preload', 'index.cjs'), 
            nodeIntegration: false, 
            contextIsolation: true,
            devTools: !app.isPackaged 
        }
    });


    state.mainWindow.webContents.on('did-finish-load', () => {
        if (state.pendingUpdateInfo) {
            state.appLogger?.info("Page reloaded/switched. Re-sending pending update UI.");
            state.mainWindow.webContents.send('show-update-ui', state.pendingUpdateInfo);
        }
    });

    state.mainWindow.once('ready-to-show', () => {
        state.mainWindow.show();
        state.appLogger?.info("Main window shell is now visible showing Splash state.");
    });

    // Register handlers immediately so they are alive the instant Vue mounts
    setupEulaHandlers(state.mainWindow, async () => {
        state.mainWindow.webContents.send('eula-accepted-success');
    }, state.appLogger);

    await state.mainWindow.loadFile(path.join(state.__dirname, '..', 'renderer', 'index.html'));
}
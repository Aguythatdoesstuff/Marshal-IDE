import { app,dialog, BrowserWindow } from 'electron';
import path from 'path';
import fs from 'fs-extra';
import pkg from 'electron-updater';
const { autoUpdater } = pkg;

export function setupAutoUpdater(ipcMain, context) {
    const { getLogger } = context;
    const appLogger = getLogger();
    if (typeof appLogger !== 'undefined') {
        autoUpdater.logger = appLogger;
    }

    autoUpdater.autoDownload = true; 
    autoUpdater.autoInstallOnAppQuit = true;

    autoUpdater.on('update-available', async (info) => {
        appLogger?.info(`Update found: v${info.version}. Starting silent download...`);
    });

    autoUpdater.on('download-progress', (progressObj) => {
        appLogger?.info(`Download progress: ${Math.round(progressObj.percent)}%`);
    });

    autoUpdater.on('update-downloaded', (info) => {
        appLogger?.info(`Update v${info.version} downloaded silently.`);
        
        if (pendingUpdateInfo?.forced) {
            appLogger?.warn("Forcing installation of critical update...");
            autoUpdater.quitAndInstall(false, true); 
        } else {
            appLogger?.info("Standard update ready. Presenting user interactive install choices.");
            // Notify frontend that the download is finished and ready to install
            mainWindow?.webContents.send('update-ready-interactive', { version: info.version });
        }
    });

    autoUpdater.on('error', (err) => {
        appLogger?.error(`Updater Error: ${err.message}`);
        handleUpdaterError(err);
    });

    autoUpdater.checkForUpdates().catch(err => {
        appLogger?.error(`Update Check Failed: ${err.message}`);
        handleUpdaterError(err);
    });

    // Keep IPC handlers for manual overrides if needed
    ipcMain.on('start-update-download', () => {
        autoUpdater.downloadUpdate().catch(err => {
            appLogger?.error(`Manual download trigger failed: ${err.message}`);
            handleUpdaterError(err);
        });
    });

    // Execute instant install request from user interface click choice
    ipcMain.handle('execute-immediate-install', () => {
        appLogger?.info("User requested immediate update execution. Restarting application...");
        autoUpdater.quitAndInstall(false, true);
    });
}

function handleUpdaterError(err) {
    const errMsg = err.message || '';
    const errCode = err.code || '';

    const isNetworkIssue = 
        errCode === 'ENOTFOUND' ||
        errCode === 'ETIMEDOUT' ||
        errCode === 'ECONNREFUSED' ||
        errCode === 'ENETUNREACH' ||
        errCode === 'EAI_AGAIN' ||
        errMsg.includes('net::ERR_') ||
        errMsg.toLowerCase().includes('offline') ||
        errMsg.toLowerCase().includes('internet') ||
        errMsg.toLowerCase().includes('network');

    if (isNetworkIssue) {
        appLogger?.info(`Auto-update skipped due to network connectivity issue: ${errMsg}`);
        return;
    }

    appLogger?.error(`Critical updater bug encountered: ${errMsg}`);
    
    dialog.showMessageBox(BrowserWindow.getFocusedWindow(), {
        type: 'error',
        title: 'Auto-Updater Error',
        message: 'Automatic update failed due to a download or configuration error.',
        detail: `Error: ${errMsg}\n\nSince automatic downloading is currently failing, please manually download and run the latest installer from GitHub Releases and report this bug if the issue persists.`,
        buttons: ['Open GitHub Releases', 'OK']
    }).then(({ response }) => {
        if (response === 0) {
            const openFunc = open.default || open;
            openFunc('https://github.com/Aguythatdoesstuff/Marshal-IDE/releases');
        }
    });
}
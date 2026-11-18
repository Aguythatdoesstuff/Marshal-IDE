// main.js - The Electron Main Process
import { app, BrowserWindow, ipcMain, dialog, Menu } from 'electron';
import { registerImageImporterHandlers } from './src/ipcHandlers/imageImporter.ipcMain.js';
import { registerProjectHandlers } from './src/ipcHandlers/project.ipcMain.js';
import { registerSettingsHandlers } from './src/ipcHandlers/settings.ipcMain.js';
import { registerFilesystemHandlers } from './src/ipcHandlers/filesystem.ipcMain.js';
import { registerCompilerHandlers } from './src/ipcHandlers/compiler.ipcMain.js';
import { registerImporterHandlers } from './src/ipcHandlers/importer.ipcMain.js';
import { registerLoggerHandlers } from './src/ipcHandlers/logger.ipcMain.js';
import { registerDocsHandlers } from './src/ipcHandlers/docs.ipcMain.js';
import { registerNavigationHandlers } from './src/ipcHandlers/navigation.ipcMain.js';
import { createWindow } from './src/Helpers/window.js';
import { setupAutoUpdater } from './src/ipcHandlers/updater.ipcMain.js';
import { initializeDirectories } from './src/Helpers/filesystem.js';

import { 
    state
} from './src/mainInit.js';
import path from 'path';
import fs from 'fs-extra';
import { spawnImgConverter, stopImgConverter } from './src/ipcHandlers/img-converter.binary.js';

const gotTheLock = app.requestSingleInstanceLock();

if (!gotTheLock) {
    // If we didn't get the lock, another instance is running. 
    // We quit this one immediately to avoid conflicts.
    app.quit();
} else {
	// We ARE the first instance. 
	// Listen for when someone tries to open a second instance:
	app.on('second-instance', (event, commandLine, workingDirectory) => {
		// Someone tried to run a second instance, we should focus our main window.
		if (state.mainWindow.isMinimized()) state.mainWindow.restore();
		state.mainWindow.focus();
	});
	
	registerSettingsHandlers(ipcMain, state);

	registerFilesystemHandlers(ipcMain, state);

	registerCompilerHandlers(ipcMain, state);

	registerImporterHandlers(ipcMain, state);

	registerLoggerHandlers(ipcMain, state);

	registerDocsHandlers(ipcMain, state);

	registerNavigationHandlers(ipcMain, state);


	// todo: update registerImageImporterHandlers to use the new ipcMain and state instead of getters
	registerImageImporterHandlers(ipcMain, state);

	registerProjectHandlers(ipcMain, state);
	// ========================================================
	// --- HELPER FUNCTIONS ---
	// ========================================================




	

	app.whenReady().then(async () => {

		createWindow(state);

	    const systemInfo = { 
			platform: process.platform, 
			arch: process.arch, 
			version: app.getVersion() 
	    };

	    const LOGS_ROOT_PATH = path.join(state.USER_DATA_PATH, 'logs');
	    
	    let maxStorageMB = 20; // Default
	    try {
		const settingsExists = await fs.stat(state.LOGGER_SETTINGS_PATH).catch(() => false);
		if (settingsExists) {
		    const logSettingsRaw = await fs.readFile(state.LOGGER_SETTINGS_PATH, 'utf8');
		    const logSettings = JSON.parse(logSettingsRaw);
		    
		    const configValue = logSettings.max_archived_mb || logSettings.max_storage_size;

		    if (configValue && !isNaN(configValue)) {
		        maxStorageMB = parseInt(configValue, 10);
		    }
		}
	    } catch (e) {
			console.error("Failed to read logger settings during bootstrap:", e.message);
	    }

		state.appLogger = await state.initializeLogger(LOGS_ROOT_PATH, systemInfo, state.mainWindow, maxStorageMB, state);

	    
	    await initializeDirectories(state);

		if (app.isPackaged) { 
			setupAutoUpdater(ipcMain, {
				getLogger: () => state.appLogger
			});
		} else {
			state.appLogger?.info(`Skipping setupAutoUpdater, Running in DEV.`, { source: 'Main-Init' })
		}
		
		spawnImgConverter({ log_path: state.getLogsSessionDir() }, state.handleIpcLog);
		


		
	});

	let isQuitting = false;

	app.on('before-quit', async (event) => {
		// If we've already run the cleanup, let the quit proceed normally
		if (isQuitting) {
			return;
		}

		// Prevent the default quit behavior so we can perform async cleanup
		event.preventDefault();
		isQuitting = true;
		if (state.watcherProcess) {
			try {
				if (state.watcherProcess.connected) {
					state.watcherProcess.send({ action: 'shutdown' });
					
					// Disconnect the IPC channel safely so the watcher's 'disconnect' event fires
					state.watcherProcess.disconnect();

				}
			} catch (e) {
				state.applogger?.error(`Error while terminating watcher process: ${e.message}`, { source: 'Main-Quit' });
			}
			state.watcherProcess = null;
		}
		await stopImgConverter();
		if (!state.isArchivingAndQuitting) {
			state.isArchivingAndQuitting = true;
			await state.archiveCurrentSession();
		}
		app.quit();
	});
	
}

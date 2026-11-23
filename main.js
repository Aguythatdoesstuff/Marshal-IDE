// main.js - The Electron Main Process
import { app, ipcMain } from 'electron';
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
import { spawnBinary, spawnBinaries, stopProcesses } from './src/Helpers/binary.js';

import { 
    state
} from './src/mainInit.js';

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

	registerImageImporterHandlers(ipcMain, state);

	registerProjectHandlers(ipcMain, state);

	app.whenReady().then(async () => {
		state.appLogger = await state.initializeLogger(state);

		createWindow(state);
	    
	    
	    await initializeDirectories(state);

		if (app.isPackaged) { 
			setupAutoUpdater(ipcMain, {
				getLogger: () => state.appLogger
			});
		} else {
			state.appLogger?.info(`Skipping setupAutoUpdater, Running in DEV.`, { source: 'Main-Init' })
		}
		
		// spawnImgConverter({ log_path: state.getLogsSessionDir() }, state.handleIpcLog);
		spawnBinary("img-converter", state);
		
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
		const processesToShutdown = [
			state.imgConverterProcess
		];
		await stopProcesses(processesToShutdown, state.handleIpcLog, "Main-shutdown");
		if (state.watcherProcess) {
			try {
				if (state.watcherProcess.connected) {
					state.watcherProcess.send({ action: 'shutdown' });
					
					// Disconnect the IPC channel safely so the watcher's 'disconnect' event fires
					state.watcherProcess.disconnect();

				}
			} catch (e) {
				state.appLogger?.error(`Error while terminating watcher process: ${e.message}`, { source: 'Main-Quit' });
			}
			state.watcherProcess = null;
		}
		if (!state.isArchivingAndQuitting) {
			state.isArchivingAndQuitting = true;
			await state.archiveCurrentSession(state);
		}
		app.quit();
	});
	
}

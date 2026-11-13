// main.js - The Electron Main Process
import { app, BrowserWindow, ipcMain, dialog, Menu } from 'electron';
import { registerImageImporterHandlers } from './src/ipcHandlers/imageImporter.ipcMain.js';
import { registerProjectHandlers } from './src/ipcHandlers/project.ipcMain.js';
import { setupAutoUpdater } from './src/ipcHandlers/updater.ipcMain.js';
import { 
    logToSystem, 
    initializeDirectories, 
    readSettingsFile,
	executeProcessImage,
    state
} from './mainInit.js';
import path from 'path';
import fs from 'fs-extra';
import { fileURLToPath} from 'url'; 
import { fork } from 'child_process'; 
import open from 'open';
import { Transform } from 'stream'; 
import { runImporter } from './src/ipcHandlers/importer.binary.js';
import { spawnCompiler, compileFile, stopCompiler } from './src/ipcHandlers/compiler.binary.js';
import { spawnImgConverter, stopImgConverter } from './src/ipcHandlers/img-converter.binary.js';
import { getEditorCache, saveEditorCache } from './src/cacheHandler.js';

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

	// --- Settings IPC ---
	ipcMain.handle('load-all-global-settings', async () => {
	    try {
		const loggerSettings = await readSettingsFile(state.LOGGER_SETTINGS_PATH);
		return { success: true, settings: { logger: loggerSettings } };
	    } catch (error) {
		return { success: false, message: error.message };
	    }
	});

	ipcMain.handle('open-path', async (e, { path: p }) => {
	    try {
		const absolutePath = path.resolve(p);
		await fs.access(absolutePath);

		const openFunc = open.default || open;
		
		await openFunc(absolutePath); 
		
		state.appLogger?.info(`Successfully opened path: ${p}`, { source: 'Main-OpenPath' });
		return { success: true };
	    } catch (error) {
		const errorMessage = `Failed to open path ${p} with system handler: ${error.message}`;
		state.appLogger?.error(errorMessage, { source: 'Main-OpenPath' });
		return { success: false, message: errorMessage };
	    }
	});

	ipcMain.handle('is-dev', () => !app.isPackaged);


	ipcMain.handle('get-simplified-editor-cache', async (event, { filePath }) => {
		const projectRoot = state.currentProjectConfig?.project_root || null;
		state.appLogger?.info(`[Main-Cache] Fetching cache for file: ${filePath}`, { source: 'Main-Cache' });
		
		return await getEditorCache(filePath, projectRoot, async ({ filePath: targetPath, workspaceCachePath }) => {
			try {
				state.appLogger?.info(`[Main-Cache] Cache miss for ${targetPath}. Requesting Rete.js-formatted AST directly from compiler...`, { source: 'Main-Cache' });
				
				const absoluteFilePath = state.INPUT_DIR ? path.join(state.INPUT_DIR, targetPath) : targetPath;
				
				const outputOptions = {
					outputAst: true,
					outputCode: false,
					astOutputPath: workspaceCachePath, // Compiler outputs directly in Rete format here
					codeOutputPath: null
				};

				const result = await handleAstRequest(null, { filePath: absoluteFilePath, outputOptions });
				if (!result.success) return false;

				// Poll for completion of the compiler output in the workspace cache path
				for (let i = 0; i < 30; i++) {
					if (await fs.pathExists(workspaceCachePath)) {
						return true;
					}
					await new Promise(resolve => setTimeout(resolve, 100));
				}

				return await fs.pathExists(workspaceCachePath);
			} catch (e) {
				state.appLogger?.error(`AST fallback execution error: ${e.message}`, { source: 'Main-Cache' });
				return false;
			}
		});
	});

	ipcMain.handle('save-simplified-editor-cache', async (event, { filePath, cacheData }) => {
		const projectRoot = state.currentProjectConfig?.project_root || null;
		state.appLogger?.info(`[Main-Cache] Saving cache for file: ${filePath}`, { source: 'Main-Cache' });
		return await saveEditorCache(filePath, cacheData, projectRoot);
	});

	ipcMain.handle('run-importer', async (event, { input, workspaceName }) => {
		try {
			// 1. Calculate and fetch target directory paths matching Electron's workspace structure
			// Target: electron userdata folder/workspaces/hoi4/ <workspaceName>
			const projectWorkspaceRoot = path.join(state.USER_WORKSPACES_DIR, workspaceName);
			const debugDirectory = state.getSessionDir(); // Fetches active SESSION_DIR absolute path

			console.log(`[Main IPC] Starting Importer Task.`);
			console.log(` - Input Source:   ${input}`);
			console.log(` - Workspace Root: ${projectWorkspaceRoot}`);
			console.log(` - Debug Path:     ${debugDirectory}`);

			// 2. Enforce absolute path validations
			if (!path.isAbsolute(input) || !path.isAbsolute(projectWorkspaceRoot) || !path.isAbsolute(debugDirectory)) {
			const errorMsg = `Non-absolute path detected! Execution aborted.\n\nInput: ${input}\nOutput: ${projectWorkspaceRoot}\nDebug: ${debugDirectory}`;
			
			state.logger.handleIpcLog({
				type: 'error',
				source: 'Main-IPC-Importer',
				message: errorMsg
			});

			dialog.showErrorBox('Path Validation Error', 'The application attempted to pass a relative path to the native components.');
			return { success: false, message: 'Path targets must be absolute.' };
			}

			// 3. Execute binary safely with the dynamic safe workspace path mapped as destination
			const result = await runImporter({
			input: input,
			output: projectWorkspaceRoot,
			debug: debugDirectory
			});

			return result; // Returns { success: true }
		} catch (error) {
			console.error('[Main IPC] Importer registration failure:', error);
			
			state.logger.handleIpcLog({
			type: 'error',
			source: 'Main-IPC-Importer',
			message: `Fatal operational failure: ${error.message}`
			});

			return {
			success: false,
			message: error.message
			};
		}
	});

	ipcMain.handle('run-compiler', async (event, { input, workspaceName }) => {
		try {
			if (!state.watcherProcess || !state.watcherProcess.connected) {
				const targetName = workspaceName || (state.currentProjectConfig ? path.basename(state.currentProjectConfig.project_root) : null);
				if (targetName) {
					await startWatcher(targetName);
				} else {
					throw new Error("No active workspace watcher found. Compilation engine offline.");
				}
			}

			// Forward compile message payload smoothly to the online watcher child process via standard IPC packet channel
			state.watcherProcess.send({
				action: 'manual-compile',
				filePath: input
			});

			state.appLogger?.info(`[Main IPC] Forwarded file compilation request to watcher for: ${path.basename(input)}`, { source: 'Main-IPC-Compiler' });
			return { success: true };
		} catch (error) {
			state.appLogger?.error(`Manual compilation trigger failed: ${error.message}`, { source: 'Main-IPC-Compiler' });
			return { success: false, message: error.message };
		}
	});

	ipcMain.handle('save-global-settings', async (event, settingsData) => {
	    let hasWatcherRestarted = false;
	    try {
		if (settingsData.logger) await fs.writeFile(state.LOGGER_SETTINGS_PATH, JSON.stringify(settingsData.logger, null, 4), 'utf8');

		// Handle Project Output Updates (Sent from index_renderer.js for per-project setting)
		if (settingsData.workspaceUpdates && Object.keys(settingsData.workspaceUpdates).length > 0) {
		    
		    for (const [projectName, newOutputDir] of Object.entries(settingsData.workspaceUpdates)) {
		        
		        // Construct the expected filename from the project name
		        const safeName = projectName.replace(/[^a-z0-9]/gi, '_').toLowerCase();
		        const targetFileName = `${safeName}_project.json`;
		        const targetPath = path.join(state.USER_PROJECTS_DIR, targetFileName);

		        try {
		            await fs.access(targetPath);
		            const pData = await fs.readFile(targetPath, 'utf8');
		            const pConfig = JSON.parse(pData);

		            pConfig.state.output_dir = newOutputDir;
		            
		            await fs.writeFile(targetPath, JSON.stringify(pConfig, null, 4), 'utf8');

		            if (state.currentProjectConfig && state.currentProjectConfig.project_root === pConfig.project_root) {
		                state.currentProjectConfig.state.output_dir = newOutputDir;
		                state.OUTPUT_DIR = newOutputDir;
		                hasWatcherRestarted = true; // Flag for restart
		            }
		        } catch (err) {
		            state.appLogger?.warn(`Could not update project ${projectName} config: ${err.message}`, { source: 'Main-Settings' });
		        }
		    }

		    if (hasWatcherRestarted && state.currentProjectConfig) {
		         const activeName = path.basename(state.currentProjectConfig.project_root);
		         startWatcher(activeName); // Restart watcher to pick up new state.OUTPUT_DIR
		    }
		}
		return { success: true, message: 'Settings saved.' };
	    } catch (error) {
		return { success: false, message: error.message };
	    }
	});


	// ========================================================
	// --- HELPER FUNCTIONS ---
	// ========================================================

	async function copyDir(src, dest, includeFiles = true) {
	    await fs.mkdir(dest, { recursive: true }); 
	    const entries = await fs.readdir(src, { withFileTypes: true });
	    for (const entry of entries) {
		const srcPath = path.join(src, entry.name);
		const destPath = path.join(dest, entry.name);
		if (entry.isDirectory()) {
		    await copyDir(srcPath, destPath, includeFiles);
		} else if (includeFiles) {
		    await fs.copyFile(srcPath, destPath);
		}
	    }
	}

	// Add this new IPC handler function
	async function handleAstRequest(event, { filePath, outputOptions }) {
		try {
			const absoluteFilePath = path.resolve(filePath);
			const { outputAst, outputCode, astOutputPath, codeOutputPath } = outputOptions;

			if (!outputAst && !outputCode) {
				throw new Error("At least one output option (outputAst or outputCode) must be specified");
			}

			if ((outputAst && !astOutputPath) || (outputCode && !codeOutputPath)) {
				throw new Error("Output path required for specified output type");
			}

			const request = {
				action: 'process-file',
				filePath: absoluteFilePath,
				outputOptions: outputOptions
			};

			if (!state.watcherProcess || !state.watcherProcess.connected) {
				const targetName = state.currentProjectConfig ? path.basename(state.currentProjectConfig.project_root) : null;
				if (!targetName) {
					throw new Error("No active workspace found. Please load a project first.");
				}
				await startWatcher(targetName);
			}

			state.watcherProcess.send(request);
			return { success: true };
		} catch (error) {
			state.appLogger?.error(`AST request failed: ${error.message}`, { source: 'Main-IPC-Compiler' });
			return { success: false, message: error.message };
		}
	}

	/**
	 * Starts the watcher process and sends a message to compile all files after startup.
	 * @param {string} projectName 
	 */
	async function startWatcher(projectName) {
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


	/**
	 * Handles saving the file content and manually triggering compilation in the watcher process.
	 */
	async function saveAndTriggerCompilation(filePath, content) {
	    if (!state.INPUT_DIR) return { success: false, message: "No project loaded. Cannot save." };
	    
	    const fullPath = path.join(state.INPUT_DIR, filePath);
	    const dirName = path.dirname(fullPath); 
	    
	    try {
		await fs.mkdir(dirName, { recursive: true });
		await fs.writeFile(fullPath, content, 'utf8');
		
		state.appLogger?.info(`File saved: ${filePath}. Watcher is monitoring for changes.`, { source: 'Main-Filesystem' });
		
		return { success: true, message: `File saved successfully to disk. Watcher monitoring.` };
	    } catch (error) {
		state.appLogger?.error(`Error saving file ${filePath}: ${error.message}`, { source: 'Main-Filesystem' });
		return { success: false, message: `Error saving file: ${error.message}` };
	    }
	}

	// --- Standard File System Helpers (No logic changes, just context) ---
	async function walkDir(currentAbsPath, tree, relativePath = '') {
	    try {
		const dirents = await fs.readdir(currentAbsPath, { withFileTypes: true });
		for (const dirent of dirents) {
		    const fullPath = path.join(currentAbsPath, dirent.name);
		    const newRelativePath = path.join(relativePath, dirent.name);
		    if (dirent.name.startsWith('.')) continue;
		    
		    const fileItem = {
		        name: dirent.name,
		        path: newRelativePath, 
		        isDir: dirent.isDirectory(),
		    };
		    if (dirent.isDirectory()) {
		        fileItem.children = [];
		        await walkDir(fullPath, fileItem.children, newRelativePath);
		    }
		    tree.push(fileItem);
		}
	    } catch (error) {
		state.appLogger?.warn(`Failed to read directory ${currentAbsPath}: ${error.message}`);
	    }
	}

	async function getFilesFromWorkspace() {
	    if (!state.INPUT_DIR) return { success: false, contents: [], message: "No project loaded." };
	    const fileTree = [];
	    await walkDir(state.INPUT_DIR, fileTree); 
	    return { success: true, contents: fileTree };
	}

	async function getFileContent(filePath) {
	    if (!state.INPUT_DIR) return { success: false, message: "No project loaded." };
	    try {
		const content = await fs.readFile(path.join(state.INPUT_DIR, filePath), 'utf8');
		return { success: true, content };
	    } catch (error) {
		return { success: false, message: error.message };
	    }
	}

	ipcMain.handle('request-ast', handleAstRequest);
	ipcMain.handle('get-file-content', async (e, { filePath }) => getFileContent(filePath));
	ipcMain.handle('save-file', async (e, { filePath, content }) => saveAndTriggerCompilation(filePath, content));

	ipcMain.handle('open-directory-dialog', async () => {
		const { canceled, filePaths } = await dialog.showOpenDialog(BrowserWindow.getFocusedWindow(), { 
			defaultPath: require('os').homedir(),
			properties: [
				'openDirectory', 
				'createDirectory', 
				'showHiddenFiles'
			] 
		});
		
		return canceled ? { success: false } : { success: true, path: filePaths[0] };
	});

	/**
	 * LIVE STRUCTURE (In-App)
	 * Scans your /docs folder and includes content for live searching.
	 */
	ipcMain.handle('get-docs-structure', async () => {
	    const docsPath = path.join(state.__dirname, 'docs');
	    async function scan(dir) {
		try {
		    const entries = await fs.readdir(dir, { withFileTypes: true });
		    const parts = await Promise.all(entries.map(async (entry) => {
		        const fullPath = path.join(dir, entry.name);
		        const relativePath = path.relative(docsPath, fullPath);
		        if (entry.isDirectory()) {
		            return { name: entry.name, type: 'folder', children: await scan(fullPath) };
		        } else if (entry.name.endsWith('.md')) {
		            const content = await fs.readFile(fullPath, 'utf-8');
		            return { 
		                name: entry.name.replace('.md', ''), 
		                type: 'file', 
		                path: relativePath,
		                content: content 
		            };
		        }
		        return null;
		    }));
		    return parts.filter(Boolean);
		} catch (e) { return []; }
	    }
	    return await scan(docsPath);
	});

	/**
	 * READ FILE (In-App)
	 */
	ipcMain.handle('read-doc-file', async (event, relativePath) => {
	    try {
		const fullPath = path.join(state.__dirname, 'docs', relativePath);
		return await fs.readFile(fullPath, 'utf-8');
	    } catch (err) {
		console.error("Read error:", err);
		throw err;
	    }
	});
	class MarshalEncryptionStream extends Transform {
	    _transform(chunk, encoding, callback) {
		// Simple XOR masking: fast and requires no extra padding/libs
		for (let i = 0; i < chunk.length; i++) {
		    chunk[i] = chunk[i] ^ SECRET_KEY[i % SECRET_KEY.length];
		}
		this.push(chunk);
		callback();
	    }
	}
	class MarshalDecryptionStream extends Transform {
	    _transform(chunk, encoding, callback) {
		for (let i = 0; i < chunk.length; i++) {
		    chunk[i] = chunk[i] ^ SECRET_KEY[i % SECRET_KEY.length];
		}
		this.push(chunk);
		callback();
	    }
	}


	ipcMain.handle('get-log-directory', async () => {
	    const logPath = state.getLogsRootDir(); 

	    if (logPath) { 
		return { success: true, path: logPath };
	    }
	    
	    const errorMessage = "Logger path not initialized.";
	    console.error(`[IPC-Handler] - ${errorMessage}`); 
	    return { success: false, message: errorMessage };
	});

	ipcMain.handle('list-directory-contents', async (event, args) => {
		// Guard Clause: If no project is active yet, return cleanly instead of crashing path.join!
		if (!state.INPUT_DIR) {
			return { success: false, contents: [], error: "No active project loaded." };
		}

        // 1. Safely extract dirPath, ensuring it defaults to an empty string if null/undefined is passed
        const dirPath = args && typeof args.dirPath === 'string' ? args.dirPath : '';
        
        // 2. Prevent path collisions if state.INPUT_DIR is inadvertently re-sent as an absolute target
        const relativePath = dirPath === state.INPUT_DIR ? '' : dirPath;
        const absolutePath = path.join(state.INPUT_DIR, relativePath);
        const contents = [];
	    try {
		const dirents = await fs.readdir(absolutePath, { withFileTypes: true });
		for (const dirent of dirents) {
		    if (dirent.name.startsWith('.')) continue; 
		    const newRelativePath = path.join(relativePath, dirent.name);
		    contents.push({ name: dirent.name, path: newRelativePath, isDir: dirent.isDirectory() });
		}
		return { success: true, contents: contents };
	    } catch (error) {
		return { success: false, contents: [], error: error.message };
	    }
	});
	async function updateManifest(action, relPath, oldRelPath = null) {
	    if (!state.currentProjectConfig) return;
	    const manifestPath = path.join(state.currentProjectConfig.project_root, 'metadata', 'manifest.json');
	    
	    try {
		await fs.ensureDir(path.dirname(manifestPath));
		
		let manifest = { files: [] };
		if (await fs.pathExists(manifestPath)) {
		    manifest = await fs.readJson(manifestPath);
		}

		if (action === 'add') {
		    if (!manifest.files.includes(relPath)) manifest.files.push(relPath);
		} else if (action === 'remove') {
		    manifest.files = manifest.files.filter(f => f !== relPath);
		} else if (action === 'rename') {
		    manifest.files = manifest.files.map(f => f === oldRelPath ? relPath : f);
		}

		await fs.writeJson(manifestPath, manifest, { spaces: 4 });
	    } catch (e) {
		state.appLogger?.error(`Manifest sync failed: ${e.message}`);
	    }
	}

	// IPC Handlers using fs-extra for better safety
	ipcMain.handle('create-file', async (event, { filePath }) => {
	    try {
		const fullPath = path.join(state.INPUT_DIR, filePath);
		await fs.ensureDir(path.dirname(fullPath)); // Now works perfectly!
		await fs.writeFile(fullPath, '', 'utf8');
		
		await updateManifest('add', filePath);
		return { success: true };
	    } catch (e) { return { success: false, message: e.message }; }
	});

	ipcMain.handle('rename-file', async (event, { oldFilePath, newFilePath }) => {
	    try {
	       const fullOld = path.join(state.INPUT_DIR, oldFilePath);
	       const fullNew = path.join(state.INPUT_DIR, newFilePath);
	       await fs.move(fullOld, fullNew, { overwrite: true }); // fs-extra move is more robust than rename
	       
	       await updateManifest('rename', newFilePath, oldFilePath);
	       return { success: true };
	    } catch (e) { return { success: false, message: e.message }; }
	});

	ipcMain.handle('delete-file-or-dir', async (event, { path: p }) => {
	    try {
	       await fs.remove(path.join(state.INPUT_DIR, p)); // fs-extra remove is recursive by default
	       
	       await updateManifest('remove', p);
	       return { success: true };
	    } catch (e) { return { success: false, message: e.message }; }
	});
	ipcMain.handle('get-absolute-path', async (event, relPath) => {
        if (!state.INPUT_DIR) {
            return { success: false, message: "No project loaded." };
        }
        try {
            const absolutePath = path.resolve(state.INPUT_DIR, relPath);
            return { success: true, absolutePath };
        } catch (error) {
            state.appLogger?.error(`[Main-Import] Error getting absolute path: ${error.message}`);
            return { success: false, message: error.message };
        }
    });
	ipcMain.handle('compile-file', async (event, { filePath, outputOptions }) => {
	    try {
	       await compileFile(filePath, outputOptions, logToSystem);
	       return { success: true };
	    } catch (e) { return { success: false, message: e.message }; }
	});

	ipcMain.handle('get-log-broadcaster-methods', () => {
		return state.LogBroadcaster ? { success: true } : { success: false };
	});

	ipcMain.on('log-broadcaster-broadcast', (event, message) => {
		state.LogBroadcaster?.emit('log', message);
	});

	ipcMain.on('renderer-log', (event, data) => {
		logToSystem(data);
	});


	async function createWindow() {
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

		const managerPath = path.join(state.__dirname, 'eula_manager.js');
		const { checkEulaStatus, setupEulaHandlers } = require(managerPath);
		const eulaPromise = checkEulaStatus(state.appLogger);

		ipcMain.handle('get-boot-state', async () => {
			const eula = await eulaPromise;
			
			let showWhatsNew = false;
			const changelogPath = path.join(state.USER_DATA_PATH, 'metadata', 'changelog.json');
			const currentVersion = app.getVersion();
			
			try {
				await fs.ensureDir(path.dirname(changelogPath));
				let lastSeenVersion = null;
				if (await fs.pathExists(changelogPath)) {
					const data = await fs.readJson(changelogPath);
					lastSeenVersion = data.version;
				}
				
				// Trigger the overlay notice if no previous state tracking exists or versions differ
				if (!lastSeenVersion || lastSeenVersion !== currentVersion) {
					showWhatsNew = true;
				}
			} catch (err) {
				state.appLogger?.error(`Failed to verify changelog version state: ${err.message}`);
			}

			return {
				enforceEula: !eula.valid,
				showWhatsNew,
				currentVersion
			};
		});

		ipcMain.handle('recompile-all', async (event, workspaceName) => {
			try {
				state.appLogger?.info(`[Main IPC] Recompile-all requested for workspace: ${workspaceName || 'Active Context'}`, { source: 'Main-IPC-Compiler' });

				// If no current project configuration or watcher is bound, dynamically spin it up first
				if (!state.watcherProcess || !state.watcherProcess.connected) {
					const targetName = workspaceName || (state.currentProjectConfig ? path.basename(state.currentProjectConfig.project_root) : null);
					if (!targetName) {
						throw new Error("No active workspace selected or available to recompile.");
					}
					state.appLogger?.info(`[Main IPC] Watcher unavailable for recompile-all. Bootstrapping workspace environment: ${targetName}`, { source: 'Main-IPC-Compiler' });
					await startWatcher(targetName);
				}

				// Forward the master full-recompile trigger command down to the active watcher process
				state.watcherProcess.send({
					action: 'manual-recompile-all'
				});

				state.appLogger?.info(`[Main IPC] Master compilation directive broadcasted successfully down to workspace watcher.`, { source: 'Main-IPC-Compiler' });
				return { success: true };
			} catch (error) {
				state.appLogger?.error(`Failed to trigger full workspace recompilation: ${error.message}`, { source: 'Main-IPC-Compiler' });
				return { success: false, message: error.message };
			}
		});

		ipcMain.handle('dismiss-whats-new', async () => {
			try {
				const changelogPath = path.join(state.USER_DATA_PATH, 'metadata', 'changelog.json');
				await fs.writeJson(changelogPath, { version: app.getVersion() }, { spaces: 4 });
				return { success: true };
			} catch (err) {
				state.appLogger?.error(`Failed to save dismissed changelog state: ${err.message}`);
				return { success: false, message: err.message };
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

	app.whenReady().then(async () => {
	// 1. Load LogBroadcaster early to prevent preload script timing bugs
    try {
        const broadcasterPath = path.join(state.__dirname, 'LogBroadcaster.cjs');
        state.LogBroadcaster = require(broadcasterPath);
        
        // 2. Set up a permanent data pipe directly to the browser window instance.
        // This makes your UI logs immune to Vite frontend dev-server Hot Module Reloads!
        state.LogBroadcaster.on('log', (msg) => {
            if (state.mainWindow && !state.mainWindow.webContents.isDestroyed()) {
                state.mainWindow.webContents.send('log-broadcaster-update', msg);
            }
        });
    } catch (e) {
        console.error(`Failed to load LogBroadcaster module early: ${e.message}`);
    }
		createWindow();

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

		state.appLogger = await state.initializeLogger(LOGS_ROOT_PATH, systemInfo, state.mainWindow, maxStorageMB);

		state.isLoggerReady = true; 

		while (state.logBuffer.length > 0) {
		    state.logger.handleIpcLog(state.logBuffer.shift());
		}
	    
	    await initializeDirectories();
	    

	    

		ipcMain.on('switch-page', (e, pageName) => {
			if (state.mainWindow) {
				const targetScreen = pageName === 'index' ? 'WORKSPACE' : pageName.toUpperCase();
				state.mainWindow.webContents.send('navigate-to', targetScreen);
			}
		});

		if (app.isPackaged) { 
			setupAutoUpdater(ipcMain, {
				getLogger: () => state.appLogger
			});
		} else {
			state.appLogger?.info(`Skipping setupAutoUpdater, Running in DEV.`, { source: 'Main-Init' })
		}
		
		spawnImgConverter({ log_path: state.getLogsSessionDir() }, logToSystem);
		


		registerImageImporterHandlers(ipcMain, {
			getInputDir: () => state.INPUT_DIR,
			getMetadataDir: () => state.METADATA_DIR,
			getOutputDir: () => state.OUTPUT_DIR,
			getLogger: () => state.appLogger,
			getConfig: () => state.currentProjectConfig,
			processImage: executeProcessImage,
			updateManifest
		});
		registerProjectHandlers(ipcMain, state, startWatcher, copyDir);
		
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

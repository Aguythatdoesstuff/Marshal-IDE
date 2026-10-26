// main.js - The Electron Main Process
import pkg from 'electron-updater';
const { autoUpdater } = pkg;
import { app, BrowserWindow, ipcMain, dialog, Menu } from 'electron';
import path from 'path';
import fs from 'fs-extra';
import { fileURLToPath} from 'url'; 
import { fork } from 'child_process'; 
import open from 'open';
import logger from './src/logger.cjs';
import { Transform } from 'stream'; 
import AdmZip from 'adm-zip';
import { runImporter } from './src/ipcHandlers/importer.js';
import { spawnCompiler, compileFile, stopCompiler } from './src/ipcHandlers/compiler.js';
import { spawnImgConverter, processImage, stopImgConverter } from './src/ipcHandlers/img-converter.js';
import { registerImageImporterHandlers } from './src/ipcHandlers/imageImporter.js';
import { getEditorCache, saveEditorCache } from './src/cacheHandler.js';

const { handleIpcLog, initializeLogger } = logger;
const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

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
		if (mainWindow.isMinimized()) mainWindow.restore();
		mainWindow.focus();
    });

	// SYSTEM PATHS (Persistent User Data - OS safe storage)
	const USER_DATA_PATH = app.getPath('userData');
	const USER_SETTINGS_DIR = path.join(USER_DATA_PATH, 'settings');
	const USER_PROJECTS_DIR = path.join(USER_DATA_PATH, 'projects', 'hoi4');
	const USER_WORKSPACES_DIR = path.join(USER_DATA_PATH, 'workspaces', 'hoi4');

	// APP PATHS (Static/Code - Read-only, updated via app installer)
	const TEMPLATE_DIR = path.join(__dirname, 'templates', 'workspace template'); 
	const WATCHER_SCRIPT_PATH = path.join(__dirname, 'src/watcher_workspace.js'); 

	// SETTINGS FILE PATHS
	const LOGGER_SETTINGS_PATH = path.join(USER_SETTINGS_DIR, 'logger_settings.json');

	// --- Global State ---
	let currentProjectConfig = null; 
	let INPUT_DIR = null;
	let OUTPUT_DIR = null;
	let METADATA_DIR = null;
	let watcherProcess = null; 
	let mainWindow = null; 
	let appLogger = null; 
	let isArchivingAndQuitting = false;
	let LogBroadcaster = null; 
	let isLoggerReady = false;
	const logBuffer = [];
	let pendingUpdateInfo = null; 
	/**
	 * Enhanced log wrapper that buffers messages until the logger is ready
	 */
	 
	// todo: move LogBroadcaster code to the logger.cjs file so it broadcasts the logs, 
	// and make all files to not use logToSystem but a internal logger.cjs helper function
	function logToSystem(logData) {
	    if (!isLoggerReady) {
		logBuffer.push(logData);
		return;
	    }
	    logger.handleIpcLog(logData);
	    if (LogBroadcaster) {
		// ensure we dont pass undefined or null messages to the LogBroadcaster, as the normal logger can handle it but the log broadcaster cannot currently cant (once we move this to logger.cjs itll be cleaner)
	    if (!logData || typeof logData !== 'object') {
	        logData = { type: 'info', message: String(logData), source: 'System' };
	    }
		LogBroadcaster.emit('log', {
		    level: logData.type.toLowerCase(),
		    message: logData.message,
		    source: logData.source,
		    timestamp: new Date().toISOString()
		});
	    }
	}

	// ========================================================
	// --- INITIALIZATION & HELPERS ---
	// ========================================================

	/**
	 * Ensures all necessary directories exist in UserData on startup.
	 */
	async function initializeDirectories() {
	    await fs.mkdir(USER_SETTINGS_DIR, { recursive: true });
	    await fs.mkdir(USER_PROJECTS_DIR, { recursive: true });
	    await fs.mkdir(USER_WORKSPACES_DIR, { recursive: true });
	    appLogger?.info(`Initialized directories at: ${USER_DATA_PATH}`, { source: 'Main-Init' });
	}

	/**
	 * Reads a user setting file, falling back to a default if it doesn't exist.
	 */
	async function readSettingsFile(userPath, defaultPath) {
	    try {
		const data = await fs.readFile(userPath, 'utf8');
		return JSON.parse(data);
	    } catch (error) {
		if (error.code === 'ENOENT') {
		    appLogger?.warn(`Settings missing: ${path.basename(userPath)}. Creating from default.`, { source: 'Main-Settings' });
		    try {
		        const defaultData = await fs.readFile(defaultPath, 'utf8');
		        const settings = JSON.parse(defaultData);
		        await fs.writeFile(userPath, JSON.stringify(settings, null, 4), 'utf8');
		        return settings;
		    } catch (defaultError) {
		        appLogger?.error(`FATAL: Default settings missing at ${defaultPath}`, { source: 'Main-Settings' });
		        throw defaultError;
		    }
		}
		throw error;
	    }
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

function setupAutoUpdater() {
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

    if (app.isPackaged) {
        autoUpdater.checkForUpdates().catch(err => {
            appLogger?.error(`Update Check Failed: ${err.message}`);
            handleUpdaterError(err);
        });
    }

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

	// --- Settings IPC ---
	ipcMain.handle('load-all-global-settings', async () => {
	    try {
		const loggerSettings = await readSettingsFile(LOGGER_SETTINGS_PATH);
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
		
		appLogger?.info(`Successfully opened path: ${p}`, { source: 'Main-OpenPath' });
		return { success: true };
	    } catch (error) {
		const errorMessage = `Failed to open path ${p} with system handler: ${error.message}`;
		appLogger?.error(errorMessage, { source: 'Main-OpenPath' });
		return { success: false, message: errorMessage };
	    }
	});

	ipcMain.handle('is-dev', () => !app.isPackaged);


	ipcMain.handle('get-simplified-editor-cache', async (event, { filePath }) => {
		const projectRoot = currentProjectConfig?.project_root || null;
		appLogger?.info(`[Main-Cache] Fetching cache for file: ${filePath}`, { source: 'Main-Cache' });
		
		return await getEditorCache(filePath, projectRoot, async ({ filePath: targetPath, workspaceCachePath }) => {
			try {
				appLogger?.info(`[Main-Cache] Cache miss for ${targetPath}. Requesting Rete.js-formatted AST directly from compiler...`, { source: 'Main-Cache' });
				
				const absoluteFilePath = INPUT_DIR ? path.join(INPUT_DIR, targetPath) : targetPath;
				
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
				appLogger?.error(`AST fallback execution error: ${e.message}`, { source: 'Main-Cache' });
				return false;
			}
		});
	});

	ipcMain.handle('save-simplified-editor-cache', async (event, { filePath, cacheData }) => {
		const projectRoot = currentProjectConfig?.project_root || null;
		appLogger?.info(`[Main-Cache] Saving cache for file: ${filePath}`, { source: 'Main-Cache' });
		return await saveEditorCache(filePath, cacheData, projectRoot);
	});

	ipcMain.handle('run-importer', async (event, { input, workspaceName }) => {
		try {
			// 1. Calculate and fetch target directory paths matching Electron's workspace structure
			// Target: electron userdata folder/workspaces/hoi4/ <workspaceName>
			const projectWorkspaceRoot = path.join(USER_WORKSPACES_DIR, workspaceName);
			const debugDirectory = logger.getSessionDir(); // Fetches active SESSION_DIR absolute path

			console.log(`[Main IPC] Starting Importer Task.`);
			console.log(` - Input Source:   ${input}`);
			console.log(` - Workspace Root: ${projectWorkspaceRoot}`);
			console.log(` - Debug Path:     ${debugDirectory}`);

			// 2. Enforce absolute path validations
			if (!path.isAbsolute(input) || !path.isAbsolute(projectWorkspaceRoot) || !path.isAbsolute(debugDirectory)) {
			const errorMsg = `Non-absolute path detected! Execution aborted.\n\nInput: ${input}\nOutput: ${projectWorkspaceRoot}\nDebug: ${debugDirectory}`;
			
			logger.handleIpcLog({
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
			
			logger.handleIpcLog({
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
			if (!watcherProcess || !watcherProcess.connected) {
				const targetName = workspaceName || (currentProjectConfig ? path.basename(currentProjectConfig.project_root) : null);
				if (targetName) {
					await startWatcher(targetName);
				} else {
					throw new Error("No active workspace watcher found. Compilation engine offline.");
				}
			}

			// Forward compile message payload smoothly to the online watcher child process via standard IPC packet channel
			watcherProcess.send({
				action: 'manual-compile',
				filePath: input
			});

			appLogger?.info(`[Main IPC] Forwarded file compilation request to watcher for: ${path.basename(input)}`, { source: 'Main-IPC-Compiler' });
			return { success: true };
		} catch (error) {
			appLogger?.error(`Manual compilation trigger failed: ${error.message}`, { source: 'Main-IPC-Compiler' });
			return { success: false, message: error.message };
		}
	});

	ipcMain.handle('save-global-settings', async (event, settingsData) => {
	    let hasWatcherRestarted = false;
	    try {
		if (settingsData.logger) await fs.writeFile(LOGGER_SETTINGS_PATH, JSON.stringify(settingsData.logger, null, 4), 'utf8');

		// Handle Project Output Updates (Sent from index_renderer.js for per-project setting)
		if (settingsData.workspaceUpdates && Object.keys(settingsData.workspaceUpdates).length > 0) {
		    
		    for (const [projectName, newOutputDir] of Object.entries(settingsData.workspaceUpdates)) {
		        
		        // Construct the expected filename from the project name
		        const safeName = projectName.replace(/[^a-z0-9]/gi, '_').toLowerCase();
		        const targetFileName = `${safeName}_project.json`;
		        const targetPath = path.join(USER_PROJECTS_DIR, targetFileName);

		        try {
		            await fs.access(targetPath);
		            const pData = await fs.readFile(targetPath, 'utf8');
		            const pConfig = JSON.parse(pData);

		            pConfig.output_dir = newOutputDir;
		            
		            await fs.writeFile(targetPath, JSON.stringify(pConfig, null, 4), 'utf8');

		            if (currentProjectConfig && currentProjectConfig.project_root === pConfig.project_root) {
		                currentProjectConfig.output_dir = newOutputDir;
		                OUTPUT_DIR = newOutputDir;
		                hasWatcherRestarted = true; // Flag for restart
		            }
		        } catch (err) {
		            appLogger?.warn(`Could not update project ${projectName} config: ${err.message}`, { source: 'Main-Settings' });
		        }
		    }

		    if (hasWatcherRestarted && currentProjectConfig) {
		         const activeName = path.basename(currentProjectConfig.project_root);
		         startWatcher(activeName); // Restart watcher to pick up new OUTPUT_DIR
		    }
		}
		return { success: true, message: 'Settings saved.' };
	    } catch (error) {
		return { success: false, message: error.message };
	    }
	});


	// --- Project Management IPC ---

	ipcMain.handle('unload-project', async () => {
		try {
			appLogger?.info('Initiating project unmount sequence...', { source: 'Main-Config' });
			
			// Kill background watcher process. The watcher process will internally run 
			// process.on('exit') and terminate the persistent C# compiler instantly.
			if (typeof watcherProcess !== 'undefined' && watcherProcess) {
				// Send specific cleanup IPC action or signal
				if (watcherProcess.connected) {
					watcherProcess.send({ action: 'shutdown' });
				}
				
				// Enforce definitive termination after brief grace period
				setTimeout(() => {
					if (watcherProcess) {
						watcherProcess.kill('SIGTERM');
						watcherProcess = null;
						appLogger?.info('Enforced fallback SIGTERM cleanup for watcherProcess.', { source: 'Main-Config' });
					}
				}, 300);
				
				appLogger?.info('Sent shutdown sequence to active workspace watcher environment.', { source: 'Main-Config' });
			} else {
				appLogger?.warn('Watcher is already unexpectedly inactive!', { source: 'Main-Config' });
			}

			// Clear memory state bindings safely
			currentProjectConfig = null;
			INPUT_DIR = null;
			OUTPUT_DIR = null;

			// Navigate frontend smoothly back to view context
			if (mainWindow && !mainWindow.isDestroyed()) {
				appLogger?.info('Navigating frontend back to workspace selection view context.', { source: 'Main-Config' });
				mainWindow.webContents.send('navigate-to', 'WORKSPACE');
			}

			return { success: true };
		} catch (error) {
			appLogger?.error(`Failed during project unload sequence: ${error.message}`, { source: 'Main-Config' });
			return { success: false, message: error.message };
		}
	});


	ipcMain.handle('list-projects', async () => {
	    try {
		const files = await fs.readdir(USER_PROJECTS_DIR);
		const projects = {};

		for (const file of files) {
		    if (file.endsWith('_project.json')) {
		        try {
		            const content = await fs.readFile(path.join(USER_PROJECTS_DIR, file), 'utf8');
		            const config = JSON.parse(content);
		            const name = path.basename(config.project_root); 
		            projects[name] = config;
		        } catch (e) {
		            appLogger?.warn(`Skipping corrupt project file ${file}: ${e.message}`);
		        }
		    }
		}
		return { success: true, projects: projects };
	    } catch (error) {
		if (error.code === 'ENOENT') return { success: true, projects: {} };
		return { success: false, projects: {}, message: error.message };
	    }
	});

	ipcMain.handle('load-project', async (event, projectName) => {
		try {
			const safeName = projectName.replace(/[^a-z0-9]/gi, '_').toLowerCase();
			const configPath = path.join(USER_PROJECTS_DIR, `${safeName}_project.json`);

			const data = await fs.readFile(configPath, 'utf8');
			currentProjectConfig = JSON.parse(data);
			
			METADATA_DIR = path.join(currentProjectConfig.project_root, 'metadata');

			const metaFilePath = path.join(METADATA_DIR, 'project_info.json');
			try {
				await fs.mkdir(METADATA_DIR, { recursive: true });
				let metaData = {};
				try {
					const existingMeta = await fs.readFile(metaFilePath, 'utf8');
					metaData = JSON.parse(existingMeta);
				} catch (e) {
					metaData.created_at = new Date().toISOString();
					metaData.version_created_with = "Unknown (Legacy)";
				}

				// Sync/Update Project Name and Timestamps
				metaData.project_name = projectName; 
				metaData.last_opened = new Date().toISOString();
				metaData.last_version_used = app.getVersion();

				await fs.writeFile(metaFilePath, JSON.stringify(metaData, null, 4), 'utf8');
			} catch (metaErr) {
				appLogger?.warn(`Could not update metadata for ${projectName}: ${metaErr.message}`, { source: 'Main-Config' });
			}

			INPUT_DIR = currentProjectConfig.input_dir;
			OUTPUT_DIR = currentProjectConfig.output_dir;
			
			await fs.mkdir(INPUT_DIR, { recursive: true });

			// Compare template folder with the workspace root folder
			const templateFiles = await fs.readdir(TEMPLATE_DIR, { withFileTypes: true });
			for (const templateEntry of templateFiles) {
				const targetPath = path.join(INPUT_DIR, templateEntry.name);
				if (!await fs.pathExists(targetPath)) {
					if (templateEntry.isDirectory()) {
						await fs.mkdir(targetPath, { recursive: true });
						appLogger?.info(`Created directory ${templateEntry.name} in workspace root.`, { source: 'Main-Config' });
					} else if (templateEntry.isFile()) {
						await fs.copyFile(path.join(TEMPLATE_DIR, templateEntry.name), targetPath);
						appLogger?.info(`Created file ${templateEntry.name} in workspace root.`, { source: 'Main-Config' });
					}
				}
			}

			appLogger?.info(`Project loaded: ${projectName}`, { source: 'Main-Config' });
			
			startWatcher(projectName); 
			return { success: true, projectName, config: currentProjectConfig };

		} catch (error) {
			appLogger?.error(`Failed to load project ${projectName}: ${error.message}`, { source: 'Main-Config' });
			return { success: false, message: error.message };
		}
	});

	ipcMain.handle('create-project', async (event, data) => {
	    const name = data.projectName.trim();
	    const safeName = name.replace(/[^a-z0-9]/gi, '_').toLowerCase();
	    const configPath = path.join(USER_PROJECTS_DIR, `${safeName}_project.json`);
	    
	    try {
			try {
				await fs.access(configPath);
				return { success: false, message: `Project "${name}" already exists.` };
			} catch (e) { /* Proceed */ }

		const projectRoot = path.join(USER_WORKSPACES_DIR, name);
		const inputPath = path.join(projectRoot, 'mod');
		const metaPath = path.join(projectRoot, 'metadata'); 
		
		await fs.mkdir(inputPath, { recursive: true });
		await fs.mkdir(metaPath, { recursive: true }); 
		await copyDir(TEMPLATE_DIR, inputPath, data.includeTemplates);

		// Created metadata with Project Name
		const metaInfo = {
		    project_name: name,
		    game: 'hoi4',
		    created_at: new Date().toISOString(),
		    updated_at: new Date().toISOString(),
		    last_opened: new Date().toISOString(),
		    version_created_with: app.getVersion(),
		    last_version_used: app.getVersion()
		};
		await fs.writeFile(path.join(metaPath, 'project_info.json'), JSON.stringify(metaInfo, null, 4), 'utf8');

		const newConfig = {
		    project_root: projectRoot,
		    input_dir: inputPath,
		    output_dir: data.outputDir.trim()
		};

		await fs.writeFile(configPath, JSON.stringify(newConfig, null, 4), 'utf8');
		appLogger?.info(`Created new project: ${name} with metadata folder`, { source: 'Main-Config' });

		return { success: true, projectName: name };

	    } catch (error) {
		return { success: false, message: error.message };
	    }
	});

	ipcMain.handle('delete-project', async (event, { projectName }) => {
	    const safeName = projectName.replace(/[^a-z0-9]/gi, '_').toLowerCase();
	    const configPath = path.join(USER_PROJECTS_DIR, `${safeName}_project.json`);

	    try {
		// Safety: If deleting the active project, kill the watcher first
		if (currentProjectConfig && path.basename(currentProjectConfig.project_root) === projectName) {
		    if (watcherProcess) {
		        appLogger?.info(`Killing active watcher for ${projectName} before deletion.`, { source: 'Main-Config' });
		        watcherProcess.kill('SIGTERM');
		        watcherProcess = null;
		    }
		    currentProjectConfig = null;
		    INPUT_DIR = null;
		    OUTPUT_DIR = null;
		}

		// Read the config to get the folder path
		const data = await fs.readFile(configPath, 'utf8');
		const config = JSON.parse(data);
		
		// Delete the physical workspace folder (removes /mod and /metadata)
		if (config.project_root && config.project_root.startsWith(USER_WORKSPACES_DIR)) {
		    await fs.rm(config.project_root, { recursive: true, force: true });
		}

		// Delete the app's local JSON configuration
		await fs.rm(configPath, { force: true });
		
		appLogger?.info(`Permanently deleted project and metadata: ${projectName}`, { source: 'Main-Config' });
		return { success: true, message: 'Project deleted.' };

	    } catch (error) {
		appLogger?.error(`Delete failed for ${projectName}: ${error.message}`, { source: 'Main-Config' });
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

			if (!watcherProcess || !watcherProcess.connected) {
				const targetName = currentProjectConfig ? path.basename(currentProjectConfig.project_root) : null;
				if (!targetName) {
					throw new Error("No active workspace found. Please load a project first.");
				}
				await startWatcher(targetName);
			}

			watcherProcess.send(request);
			return { success: true };
		} catch (error) {
			appLogger?.error(`AST request failed: ${error.message}`, { source: 'Main-IPC-Compiler' });
			return { success: false, message: error.message };
		}
	}

	/**
	 * Starts the watcher process and sends a message to compile all files after startup.
	 * @param {string} projectName 
	 */
	async function startWatcher(projectName) {
	    if (!currentProjectConfig) {
		appLogger?.error(`Cannot start watcher: No project configuration loaded.`, { source: 'Main-Watcher' });
		return;
	    }
	    
	    // Kill any existing watcher process before starting a new one
	    if (watcherProcess) {
		appLogger?.warn(`Terminating previous watcher process (PID: ${watcherProcess.pid}).`, { source: 'Main-Watcher' });
		watcherProcess.kill('SIGTERM'); 
		watcherProcess = null;
	    }


	    const watcherPayload = JSON.stringify({
			input_dir: path.resolve(INPUT_DIR), 
			output_dir: path.resolve(OUTPUT_DIR), 
			project_root: currentProjectConfig.project_root,
			mod_name: projectName,
    		log_dir: logger.getLogsSessionDir()
	    });
	    
	    watcherProcess = fork(WATCHER_SCRIPT_PATH, [watcherPayload], { 
		stdio: ['inherit', 'inherit', 'inherit', 'ipc'],
		execArgv: ['--enable-source-maps'] 
	    });
	    watcherProcess.on('message', (logData) => {
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
	    
	    watcherProcess.on('error', (err) => {
		appLogger?.error(`Watcher child process failed: ${err.message}`, { source: 'Main-Watcher' });
	    });

	    watcherProcess.on('exit', (code, signal) => {
		if (code !== 0 && signal !== 'SIGTERM' && signal !== null) {
		    appLogger?.error(`Watcher exited unexpectedly. Code: ${code}, Signal: ${signal}`, { source: 'Main-Watcher' });
		}
		watcherProcess = null;
	    });

	    appLogger?.info(`Child process for compilation started. PID: ${watcherProcess.pid}`, { source: 'Main-Watcher' });
	}


	/**
	 * Handles saving the file content and manually triggering compilation in the watcher process.
	 */
	async function saveAndTriggerCompilation(filePath, content) {
	    if (!INPUT_DIR) return { success: false, message: "No project loaded. Cannot save." };
	    
	    const fullPath = path.join(INPUT_DIR, filePath);
	    const dirName = path.dirname(fullPath); 
	    
	    try {
		await fs.mkdir(dirName, { recursive: true });
		await fs.writeFile(fullPath, content, 'utf8');
		
		appLogger?.info(`File saved: ${filePath}. Watcher is monitoring for changes.`, { source: 'Main-Filesystem' });
		
		return { success: true, message: `File saved successfully to disk. Watcher monitoring.` };
	    } catch (error) {
		appLogger?.error(`Error saving file ${filePath}: ${error.message}`, { source: 'Main-Filesystem' });
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
		appLogger?.warn(`Failed to read directory ${currentAbsPath}: ${error.message}`);
	    }
	}

	async function getFilesFromWorkspace() {
	    if (!INPUT_DIR) return { success: false, contents: [], message: "No project loaded." };
	    const fileTree = [];
	    await walkDir(INPUT_DIR, fileTree); 
	    return { success: true, contents: fileTree };
	}

	async function getFileContent(filePath) {
	    if (!INPUT_DIR) return { success: false, message: "No project loaded." };
	    try {
		const content = await fs.readFile(path.join(INPUT_DIR, filePath), 'utf8');
		return { success: true, content };
	    } catch (error) {
		return { success: false, message: error.message };
	    }
	}

	ipcMain.handle('request-ast', handleAstRequest);
	ipcMain.handle('get-file-content', async (e, { filePath }) => getFileContent(filePath));
	ipcMain.handle('save-file', async (e, { filePath, content }) => saveAndTriggerCompilation(filePath, content));
	// Define the core helper function
    async function executeProcessImage({ filePath, imageType, mainOutputDir, cacheOutputDir }) {
        if (!INPUT_DIR) {
            return { success: false, message: "No project loaded." };
        }

        const targetMainOutput = mainOutputDir || OUTPUT_DIR;
        const targetCacheOutput = cacheOutputDir || path.join(currentProjectConfig.project_root, 'cache', 'images');

        return await processImage(filePath, imageType, targetMainOutput, targetCacheOutput, logToSystem);
    }

    // Update the 'process-image' IPC handler to call the helper
    ipcMain.handle('process-image', async (event, args) => {
        try {
            return await executeProcessImage(args);
        } catch (error) {
            appLogger?.error(`Image processing IPC failure: ${error.message}`, { source: 'Main-IPC-ImgConverter' });
            return { success: false, message: error.message };
        }
    });
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
	    const docsPath = path.join(__dirname, 'docs');
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
		const fullPath = path.join(__dirname, 'docs', relativePath);
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
	    const logPath = logger.getLogsRootDir(); 

	    if (logPath) { 
		return { success: true, path: logPath };
	    }
	    
	    const errorMessage = "Logger path not initialized.";
	    console.error(`[IPC-Handler] - ${errorMessage}`); 
	    return { success: false, message: errorMessage };
	});

	ipcMain.handle('list-directory-contents', async (event, args) => {
		// Guard Clause: If no project is active yet, return cleanly instead of crashing path.join!
		if (!INPUT_DIR) {
			return { success: false, contents: [], error: "No active project loaded." };
		}

        // 1. Safely extract dirPath, ensuring it defaults to an empty string if null/undefined is passed
        const dirPath = args && typeof args.dirPath === 'string' ? args.dirPath : '';
        
        // 2. Prevent path collisions if INPUT_DIR is inadvertently re-sent as an absolute target
        const relativePath = dirPath === INPUT_DIR ? '' : dirPath;
        const absolutePath = path.join(INPUT_DIR, relativePath);
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
	    if (!currentProjectConfig) return;
	    const manifestPath = path.join(currentProjectConfig.project_root, 'metadata', 'manifest.json');
	    
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
		appLogger?.error(`Manifest sync failed: ${e.message}`);
	    }
	}

	// IPC Handlers using fs-extra for better safety
	ipcMain.handle('create-file', async (event, { filePath }) => {
	    try {
		const fullPath = path.join(INPUT_DIR, filePath);
		await fs.ensureDir(path.dirname(fullPath)); // Now works perfectly!
		await fs.writeFile(fullPath, '', 'utf8');
		
		await updateManifest('add', filePath);
		return { success: true };
	    } catch (e) { return { success: false, message: e.message }; }
	});

	ipcMain.handle('rename-file', async (event, { oldFilePath, newFilePath }) => {
	    try {
	       const fullOld = path.join(INPUT_DIR, oldFilePath);
	       const fullNew = path.join(INPUT_DIR, newFilePath);
	       await fs.move(fullOld, fullNew, { overwrite: true }); // fs-extra move is more robust than rename
	       
	       await updateManifest('rename', newFilePath, oldFilePath);
	       return { success: true };
	    } catch (e) { return { success: false, message: e.message }; }
	});

	ipcMain.handle('delete-file-or-dir', async (event, { path: p }) => {
	    try {
	       await fs.remove(path.join(INPUT_DIR, p)); // fs-extra remove is recursive by default
	       
	       await updateManifest('remove', p);
	       return { success: true };
	    } catch (e) { return { success: false, message: e.message }; }
	});
	
	ipcMain.handle('get-log-broadcaster-methods', () => {
		return LogBroadcaster ? { success: true } : { success: false };
	});

	ipcMain.on('log-broadcaster-broadcast', (event, message) => {
		LogBroadcaster?.emit('log', message);
	});

	ipcMain.on('renderer-log', (event, data) => {
		logToSystem(data);
	});


	async function createWindow() {
		if (app.isPackaged) {
			Menu.setApplicationMenu(null);
		}
		
		mainWindow = new BrowserWindow({
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
			
			icon: path.join(__dirname, '..', '..', '..', 'build', 'icon.png'), 
			
			webPreferences: {
				preload: path.join(__dirname, '..', 'preload', 'index.cjs'), 
				nodeIntegration: false, 
				contextIsolation: true,
				devTools: !app.isPackaged 
			}
		});

		const managerPath = path.join(__dirname, 'eula_manager.js');
		const { checkEulaStatus, setupEulaHandlers } = require(managerPath);
		const eulaPromise = checkEulaStatus(appLogger);

		ipcMain.handle('get-boot-state', async () => {
			const eula = await eulaPromise;
			
			let showWhatsNew = false;
			const changelogPath = path.join(USER_DATA_PATH, 'metadata', 'changelog.json');
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
				appLogger?.error(`Failed to verify changelog version state: ${err.message}`);
			}

			return {
				enforceEula: !eula.valid,
				showWhatsNew,
				currentVersion
			};
		});

		ipcMain.handle('recompile-all', async (event, workspaceName) => {
			try {
				appLogger?.info(`[Main IPC] Recompile-all requested for workspace: ${workspaceName || 'Active Context'}`, { source: 'Main-IPC-Compiler' });

				// If no current project configuration or watcher is bound, dynamically spin it up first
				if (!watcherProcess || !watcherProcess.connected) {
					const targetName = workspaceName || (currentProjectConfig ? path.basename(currentProjectConfig.project_root) : null);
					if (!targetName) {
						throw new Error("No active workspace selected or available to recompile.");
					}
					appLogger?.info(`[Main IPC] Watcher unavailable for recompile-all. Bootstrapping workspace environment: ${targetName}`, { source: 'Main-IPC-Compiler' });
					await startWatcher(targetName);
				}

				// Forward the master full-recompile trigger command down to the active watcher process
				watcherProcess.send({
					action: 'manual-recompile-all'
				});

				appLogger?.info(`[Main IPC] Master compilation directive broadcasted successfully down to workspace watcher.`, { source: 'Main-IPC-Compiler' });
				return { success: true };
			} catch (error) {
				appLogger?.error(`Failed to trigger full workspace recompilation: ${error.message}`, { source: 'Main-IPC-Compiler' });
				return { success: false, message: error.message };
			}
		});

		ipcMain.handle('dismiss-whats-new', async () => {
			try {
				const changelogPath = path.join(USER_DATA_PATH, 'metadata', 'changelog.json');
				await fs.writeJson(changelogPath, { version: app.getVersion() }, { spaces: 4 });
				return { success: true };
			} catch (err) {
				appLogger?.error(`Failed to save dismissed changelog state: ${err.message}`);
				return { success: false, message: err.message };
			}
		});

		mainWindow.webContents.on('did-finish-load', () => {
			if (pendingUpdateInfo) {
				appLogger?.info("Page reloaded/switched. Re-sending pending update UI.");
				mainWindow.webContents.send('show-update-ui', pendingUpdateInfo);
			}
		});

		mainWindow.once('ready-to-show', () => {
			mainWindow.show();
			appLogger?.info("Main window shell is now visible showing Splash state.");
		});

		// Register handlers immediately so they are alive the instant Vue mounts
		setupEulaHandlers(mainWindow, async () => {
			mainWindow.webContents.send('eula-accepted-success');
		}, appLogger);

		await mainWindow.loadFile(path.join(__dirname, '..', 'renderer', 'index.html'));
	}

	app.whenReady().then(async () => {
	// 1. Load LogBroadcaster early to prevent preload script timing bugs
    try {
        const broadcasterPath = path.join(__dirname, 'LogBroadcaster.cjs');
        LogBroadcaster = require(broadcasterPath);
        
        // 2. Set up a permanent data pipe directly to the browser window instance.
        // This makes your UI logs immune to Vite frontend dev-server Hot Module Reloads!
        LogBroadcaster.on('log', (msg) => {
            if (mainWindow && !mainWindow.webContents.isDestroyed()) {
                mainWindow.webContents.send('log-broadcaster-update', msg);
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

	    const LOGS_ROOT_PATH = path.join(USER_DATA_PATH, 'logs');
	    
	    let maxStorageMB = 20; // Default
	    try {
		const settingsExists = await fs.stat(LOGGER_SETTINGS_PATH).catch(() => false);
		if (settingsExists) {
		    const logSettingsRaw = await fs.readFile(LOGGER_SETTINGS_PATH, 'utf8');
		    const logSettings = JSON.parse(logSettingsRaw);
		    
		    const configValue = logSettings.max_archived_mb || logSettings.max_storage_size;

		    if (configValue && !isNaN(configValue)) {
		        maxStorageMB = parseInt(configValue, 10);
		    }
		}
	    } catch (e) {
			console.error("Failed to read logger settings during bootstrap:", e.message);
	    }

		appLogger = await logger.initializeLogger(LOGS_ROOT_PATH, systemInfo, mainWindow, maxStorageMB);

		isLoggerReady = true; 

		while (logBuffer.length > 0) {
		    logger.handleIpcLog(logBuffer.shift());
		}
	    
	    await initializeDirectories();
	    

	    

		ipcMain.on('switch-page', (e, pageName) => {
			if (mainWindow) {
				const targetScreen = pageName === 'index' ? 'WORKSPACE' : pageName.toUpperCase();
				mainWindow.webContents.send('navigate-to', targetScreen);
			}
		});
		if (app.isPackaged) { 
	    	setupAutoUpdater();
		}else {
			appLogger?.info(`Skipping setupAutoUpdater, Running in DEV.`, { source: 'Main-Init' })
		}
		
		spawnImgConverter({ log_path: logger.getLogsSessionDir() }, logToSystem);
		
		ipcMain.handle('export-project', async (event, projectName) => {
			try {
				const safeName = projectName.replace(/[^a-z0-9]/gi, '_').toLowerCase();
				const configPath = path.join(USER_PROJECTS_DIR, `${safeName}_project.json`);
				const config = await fs.readJson(configPath);

				const { filePath } = await dialog.showSaveDialog({
					title: 'Export Marshal Project',
					defaultPath: path.join(app.getPath('downloads'), `${projectName}.MarshalIDE`),
					filters: [{ name: 'Marshal Project', extensions: ['MarshalIDE'] }]
				});

				if (!filePath) return { success: false };

				const zip = new AdmZip();
				zip.addLocalFolder(config.project_root);
				zip.writeZip(filePath);

				return { success: true };
			} catch (error) {
				appLogger?.error(`Export Error: ${error.message}`);
				return { success: false, message: error.message };
			}
		});

		registerImageImporterHandlers(ipcMain, {
			getInputDir: () => INPUT_DIR,
			getMetadataDir: () => METADATA_DIR,
			getOutputDir: () => OUTPUT_DIR,
			getLogger: () => appLogger,
			getConfig: () => currentProjectConfig,
			processImage: executeProcessImage,
			updateManifest
		});
		
		ipcMain.handle('import-project', async () => {
			try {
				const { filePaths } = await dialog.showOpenDialog({
					filters: [{ name: 'Marshal Project', extensions: ['MarshalIDE'] }],
					properties: ['openFile']
				});

				if (!filePaths || filePaths.length === 0) return { success: false };
				const selectedFile = filePaths[0];

				const tempPath = path.join(app.getPath('temp'), `import_${Date.now()}`);
				const zip = new AdmZip(selectedFile);
				zip.extractAllTo(tempPath, true);

				const metaPath = path.join(tempPath, 'metadata', 'project_info.json');
				if (!await fs.pathExists(metaPath)) {
					throw new Error("Invalid MarshalIDE project: Missing metadata.");
				}
				const metadata = await fs.readJson(metaPath);
				const projectName = metadata.project_name || path.basename(selectedFile, '.MarshalIDE');

				const { filePaths: outPaths } = await dialog.showOpenDialog({
					title: `Select Output Directory for ${projectName}`,
					properties: ['openDirectory', 'createDirectory']
				});

				if (!outPaths || outPaths.length === 0) throw new Error("Output directory is required.");
				const selectedOutputDir = outPaths[0];

				const finalWorkspacePath = path.join(USER_WORKSPACES_DIR, projectName);
				if (await fs.pathExists(finalWorkspacePath)) {
					throw new Error(`A project named "${projectName}" already exists.`);
				}

				await fs.move(tempPath, finalWorkspacePath);

				const safeName = projectName.replace(/[^a-z0-9]/gi, '_').toLowerCase();
				const configPath = path.join(USER_PROJECTS_DIR, `${safeName}_project.json`);
				const configData = {
					project_root: finalWorkspacePath,
					input_dir: path.join(finalWorkspacePath, 'mod'),
					output_dir: selectedOutputDir
				};

				await fs.writeJson(configPath, configData, { spaces: 4 });
				return { success: true, projectName };

			} catch (error) {
				appLogger?.error(`Import Error: ${error.message}`);
				return { success: false, message: error.message };
			}
		});
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
		if (watcherProcess) {
			try {
				if (watcherProcess.connected) {
					watcherProcess.send({ action: 'shutdown' });
					
					// Disconnect the IPC channel safely so the watcher's 'disconnect' event fires
					watcherProcess.disconnect();

				}
			} catch (e) {
				applogger?.error(`Error while terminating watcher process: ${e.message}`, { source: 'Main-Quit' });
			}
			watcherProcess = null;
		}
		await stopImgConverter();
		if (!isArchivingAndQuitting) {
			isArchivingAndQuitting = true;
			await logger.archiveCurrentSession();
		}
		app.quit();
	});
	
}

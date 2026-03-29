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
        if (mainWindow) {
            if (mainWindow.isMinimized()) mainWindow.restore();
            mainWindow.focus();
        } else if (splashWindow) {
            // If the app is still on the splash screen, focus that instead
            if (splashWindow.isMinimized()) splashWindow.restore();
            splashWindow.focus();
        }
    });

	// SYSTEM PATHS (Persistent User Data - OS safe storage)
	const USER_DATA_PATH = app.getPath('userData');
	const USER_SETTINGS_DIR = path.join(USER_DATA_PATH, 'settings');
	const USER_PROJECTS_DIR = path.join(USER_DATA_PATH, 'projects', 'hoi4');
	const USER_WORKSPACES_DIR = path.join(USER_DATA_PATH, 'workspaces', 'hoi4');

	// APP PATHS (Static/Code - Read-only, updated via app installer)
	const APP_JSONS_DIR = path.join(__dirname, 'jsons');
	const GLOBAL_COMPILERS_PATH = path.join(APP_JSONS_DIR, 'global_compilers.json'); 
	const TEMPLATE_DIR = path.join(__dirname, 'templates', 'workspace template'); 
	const WATCHER_SCRIPT_PATH = path.join(__dirname, 'src/watcher_workspace.js'); 

	// SETTINGS FILE PATHS
	const COMPILER_SETTINGS_PATH = path.join(USER_SETTINGS_DIR, 'compiler_settings.json');
	const LOGGER_SETTINGS_PATH = path.join(USER_SETTINGS_DIR, 'logger_settings.json');

	// --- Global State ---
	let currentProjectConfig = null;
	let globalCompilers = null; 
	let INPUT_DIR = null;
	let OUTPUT_DIR = null;
	let watcherProcess = null; 
	let mainWindow = null; 
	let splashWindow = null;
	let appLogger = null; 
	let isArchivingAndQuitting = false;
	let LogBroadcaster = null; 
	let isLoggerReady = false;
	const logBuffer = [];
	let pendingUpdateInfo = null; 
	/**
	 * Enhanced log wrapper that buffers messages until the logger is ready
	 */
	 
	function logToSystem(logData) {
	    if (!isLoggerReady) {
		logBuffer.push(logData);
		return;
	    }
	    logger.handleIpcLog(logData);
	    if (LogBroadcaster) {
		LogBroadcaster.emit('log', {
		    level: logData.type.toLowerCase(),
		    message: logData.message,
		    source: logData.source,
		    timestamp: new Date().toISOString()
		});
	    }
	}

	// ========================================================
	// --- CONSOLE INTERCEPTOR (RECURSION SAFE) ---
	// ========================================================
	const originalConsole = { 
	    log: console.log, 
	    info: console.info, 
	    warn: console.warn, 
	    error: console.error 
	};

	['log', 'info', 'warn', 'error'].forEach((level) => {
	    console[level] = (...args) => {
		// Terminal output (Immediate)
		originalConsole[level](...args);

		const message = args.map(arg => 
		    arg instanceof Error ? arg.stack : (typeof arg === 'object' ? JSON.stringify(arg, null, 2) : String(arg))
		).join(' ');

		// SMART FILTER:
		// Only skip if the message starts with a Year (like 2026-...) 
		// This prevents re-logging things Winston already wrote, 
		// but ALLOWS "raw" logs like [Archive-Cleanup] to be captured!
		const isAlreadyFormatted = /^\d{4}-\d{2}-\d{2}/.test(message);
		
		if (isAlreadyFormatted) return;

		logToSystem({
		    type: level === 'log' ? 'info' : level,
		    message: message,
		    source: 'Console-Intercept' 
		});
	    };
	});

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

	/**
	 * Loads the global compilers from the app root (static JSON).
	 */
	async function loadGlobalCompilers() {
	    try {
		const data = await fs.readFile(GLOBAL_COMPILERS_PATH, 'utf8');
		return JSON.parse(data);
	    } catch (error) {
		appLogger?.error(`Failed to load global_compilers.json: ${error.message}`, { source: 'Main-Config' });
		return {}; 
	    }
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
            // false: do not run after install (immediately), true: force run after install
            autoUpdater.quitAndInstall(false, true); 
        } else {
            appLogger?.info("Standard update ready. It will install automatically on next quit.");
            mainWindow?.webContents.send('update-ready-silent', { version: info.version });
        }
    });

    autoUpdater.on('error', (err) => {
        appLogger?.error(`Updater Error: ${err.message}`);
    });

    if (app.isPackaged) {
        autoUpdater.checkForUpdates().catch(err => {
            appLogger?.error(`Update Check Failed: ${err.message}`);
        });
    }

    // Keep IPC handlers for manual overrides if needed
    ipcMain.on('start-update-download', () => {
        autoUpdater.downloadUpdate();
    });
}

	// --- Settings IPC ---
	ipcMain.handle('load-all-global-settings', async () => {
	    try {
		const loggerSettings = await readSettingsFile(LOGGER_SETTINGS_PATH, DEFAULT_LOGGER_SETTINGS_PATH);
		const compilerSettings = await readSettingsFile(COMPILER_SETTINGS_PATH, DEFAULT_COMPILER_SETTINGS_PATH);
		return { success: true, settings: { logger: loggerSettings, compiler: compilerSettings } };
	    } catch (error) {
		return { success: false, message: error.message };
	    }
	});

	ipcMain.handle('save-global-settings', async (event, settingsData) => {
	    let hasWatcherRestarted = false;
	    try {
		if (settingsData.logger) await fs.writeFile(LOGGER_SETTINGS_PATH, JSON.stringify(settingsData.logger, null, 4), 'utf8');
		if (settingsData.compiler) await fs.writeFile(COMPILER_SETTINGS_PATH, JSON.stringify(settingsData.compiler, null, 4), 'utf8');

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
		
		const metaDirPath = path.join(currentProjectConfig.project_root, 'metadata');
		const metaFilePath = path.join(metaDirPath, 'project_info.json');

		try {
		    await fs.mkdir(metaDirPath, { recursive: true });
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

		globalCompilers = await loadGlobalCompilers();
		INPUT_DIR = currentProjectConfig.input_dir;
		OUTPUT_DIR = currentProjectConfig.output_dir;

		await fs.mkdir(INPUT_DIR, { recursive: true });
		appLogger?.info(`Project loaded: ${projectName}`, { source: 'Main-Config' });
		
		startWatcher(projectName); 
		return { success: true, projectName, config: currentProjectConfig, globalCompilers };

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
		await copyDir(TEMPLATE_DIR, inputPath);

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
		        watcherProcess.kill('SIGKILL');
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

	async function copyDir(src, dest) {
	    await fs.mkdir(dest, { recursive: true });
	    const entries = await fs.readdir(src, { withFileTypes: true });
	    for (const entry of entries) {
		const srcPath = path.join(src, entry.name);
		const destPath = path.join(dest, entry.name);
		if (entry.isDirectory()) await copyDir(srcPath, destPath);
		else await fs.copyFile(srcPath, destPath);
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
		watcherProcess.kill('SIGKILL'); 
		watcherProcess = null;
	    }

	    // --- NEW: Load Compiler Settings for Marker ---
	    let compilerSettings = {};
	    try {
		// Try reading user settings, fallback to default if needed (logic similar to readSettingsFile but inline or reused)
		const settingsRaw = await fs.readFile(COMPILER_SETTINGS_PATH, 'utf8').catch(async () => {
		    // If user settings don't exist, try defaults or empty
		    return await fs.readFile(DEFAULT_COMPILER_SETTINGS_PATH, 'utf8').catch(() => "{}");
		});
		compilerSettings = JSON.parse(settingsRaw);
	    } catch (e) {
		appLogger?.warn(`Could not load compiler settings for watcher: ${e.message}`, { source: 'Main-Watcher' });
	    }

	    const watcherPayload = JSON.stringify({
			input_dir: path.resolve(INPUT_DIR), 
			output_dir: path.resolve(OUTPUT_DIR), 
			project_root: currentProjectConfig.project_root,
			compilers: globalCompilers,
			mod_name: projectName
	    });
	    
	    watcherProcess = fork(WATCHER_SCRIPT_PATH, [watcherPayload], { 
		stdio: ['inherit', 'inherit', 'inherit', 'ipc'],
		execArgv: ['--enable-source-maps'] 
	    });
	    watcherProcess.on('message', (logData) => {
		logToSystem(logData);
	    });
	    
	    watcherProcess.on('error', (err) => {
		appLogger?.error(`Watcher child process failed: ${err.message}`, { source: 'Main-Watcher' });
	    });

	    watcherProcess.on('exit', (code, signal) => {
		if (code !== 0 && signal !== 'SIGKILL' && signal !== null) {
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
	 * Scans your /docs folder.
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
		            return { name: entry.name.replace('.md', ''), type: 'file', path: relativePath };
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
	 * Reads the markdown from your app's internal /docs folder.
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
	/**
	 * BROWSER EXPORT (In-Browser)
	 * Creates the snapshot in userData/wiki_metadata to avoid read-only errors.
	 */
	ipcMain.handle('open-wiki-external', async () => {
	    // Correct paths for the bundled environment
	    const docsPath = path.join(__dirname, 'docs');
	    
	    const templatePath = path.join(__dirname, '..', 'renderer', 'wiki.html'); 
	    
	    // Set up the writeable path in UserData
	    const userDataPath = app.getPath('userData');
	    const metadataDir = path.join(userDataPath, 'wiki_metadata');
	    const outputPath = path.join(metadataDir, 'wiki_browser.html');

	    // Deep scan to package content into the JSON
	    async function packageDocs(dir) {
		const entries = await fs.readdir(dir, { withFileTypes: true });
		const parts = await Promise.all(entries.map(async (entry) => {
		    const fullPath = path.join(dir, entry.name);
		    const relativePath = path.relative(docsPath, fullPath);
		    if (entry.isDirectory()) {
		        return { name: entry.name, type: 'folder', children: await packageDocs(fullPath) };
		    } else if (entry.name.endsWith('.md')) {
		        const content = await fs.readFile(fullPath, 'utf-8');
		        return { name: entry.name.replace('.md', ''), type: 'file', content: content };
		    }
		    return null;
		}));
		return parts.filter(Boolean);
	    }

	    try {
		await fs.mkdir(metadataDir, { recursive: true });
		const fullData = await packageDocs(docsPath);
		let htmlContent = await fs.readFile(templatePath, 'utf-8');
		const dataInjection = `<script>window.BROWSER_WIKI_DATA = ${JSON.stringify(fullData)};</script>`;
		htmlContent = htmlContent.replace('<head>', `<head>\n    ${dataInjection}`);
		await fs.writeFile(outputPath, htmlContent);

		const openFunc = open.default || open;
		await openFunc(`file://${outputPath}`);
		
		return { success: true };
	    } catch (err) {
		console.error("Failed to generate wiki in userData:", err);
		return { success: false, message: err.message };
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



	ipcMain.handle('get-log-directory', async () => {
	    const logPath = logger.getLogsRootDir(); 

	    if (logPath) { 
		return { success: true, path: logPath };
	    }
	    
	    const errorMessage = "Logger path not initialized.";
	    console.error(`[IPC-Handler] - ${errorMessage}`); 
	    return { success: false, message: errorMessage };
	});

	ipcMain.handle('list-directory-contents', async (event, { dirPath }) => {
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
	ipcMain.handle('get-log-broadcaster-methods', () => LogBroadcaster ? { success: true } : { success: false });
	ipcMain.on('log-broadcaster-renderer-subscribe', (event) => {
	    if (LogBroadcaster && !LogBroadcaster._isRendererSubscribed) {
		LogBroadcaster.addListener('log', (msg) => event.sender.send('log-broadcaster-update', msg));
		LogBroadcaster._isRendererSubscribed = true;
	    }
	});
	ipcMain.on('log-broadcaster-broadcast', (e, msg) => LogBroadcaster?.emit('log', msg));
	ipcMain.on('renderer-log', (e, data) => logToSystem(data));



	async function loadingWindow() {
	    if (app.isPackaged) {
		Menu.setApplicationMenu(null);
	    }
	    splashWindow = new BrowserWindow({
		width: 1200, 
		height: 800,
		titleBarStyle: 'hidden',
		icon: path.join(__dirname, 'build', 'icon.png'), 
		webPreferences: {
		    contextIsolation: true,
		    devTools: !app.isPackaged 
		}

	    });
	    // Force show when the window is ready, regardless of what page is loaded
	    splashWindow.once('ready-to-show', () => {
		splashWindow.show();
		console.log("loading screen loaded.");
	    });
	    await splashWindow.loadFile(path.join(__dirname, '..', 'renderer', 'loading.html'));
	}


	async function createWindow() {
	    if (app.isPackaged) {
		Menu.setApplicationMenu(null);
	    }
	    mainWindow = new BrowserWindow({
		width: 1200, 
		height: 800,
		titleBarStyle: 'hidden',
	...(process.platform !== 'darwin' ? {
		titleBarOverlay: {
		    color: '#1e1e1e',       // Background of the button area (Matches your new titlebar)
		    symbolColor: '#858585', // Color of the X, _, and [] symbols
		    height: 32              // Match the height of your .titlebar in CSS
		}
	    } : {}),
		show: false, // IMPORTANT: Keep this false to prevent flickering!!!
		icon: path.join(__dirname, 'build', 'icon.png'), 
		webPreferences: {
		    preload: path.join(__dirname, '..', 'preload', 'index.cjs'), 
		    nodeIntegration: false, 
		    contextIsolation: true,
		    devTools: !app.isPackaged 
		}
	    });

	    const managerPath = path.join(__dirname, 'eula_manager.js');
	    const { checkEulaStatus, setupEulaHandlers } = require(managerPath);
	    
	    const eula = await checkEulaStatus(appLogger);

	    // Force show when the window is ready, regardless of what page is loaded
	    mainWindow.once('ready-to-show', () => {
		mainWindow.show();
		appLogger?.info("Main window is now visible.");
		// Kill the loading window
		if (splashWindow && !splashWindow.isDestroyed()) {
		    splashWindow.close(); 
		    splashWindow = null; // Clean up memory
		}
	    });
	    mainWindow.webContents.on('did-finish-load', () => {
		if (pendingUpdateInfo) {
		    appLogger?.info("Page reloaded/switched. Re-sending pending update UI.");
		    mainWindow.webContents.send('show-update-ui', pendingUpdateInfo);

		}
	    });
	    if (eula.valid) {
		appLogger?.info("EULA valid, loading index.");
		await mainWindow.loadFile(path.join(__dirname, '..', 'renderer', 'index.html'));
	    } else {
		appLogger?.info("EULA required, loading eula page.");
		setupEulaHandlers(mainWindow, async () => {
		    // This callback runs after the user clicks "Accept" in the EULA window
		    await mainWindow.loadFile(path.join(__dirname, '..', 'renderer', 'index.html'));
		}, appLogger);
		
		await mainWindow.loadFile(path.join(__dirname, '..', 'renderer', 'eula.html'));
	    }
	}

	app.whenReady().then(async () => {
	loadingWindow();
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
	    
	try {
		const broadcasterPath = path.join(__dirname, 'LogBroadcaster.cjs');
		LogBroadcaster = require(broadcasterPath);
	    appLogger?.info("LogBroadcaster loaded successfully.");
	} catch (e) { 
	    appLogger?.error(`Failed to load LogBroadcaster module: ${e.message}`, { source: 'Main-Init' });
	}
	    
	    createWindow(); 

		ipcMain.on('switch-page', (e, pageName) => {
			const targetPath = path.join(__dirname, '..', 'renderer', `${pageName}.html`);
			
			// Use the focused window to load the new file
			const win = BrowserWindow.getFocusedWindow();
			if (win) {
			win.loadFile(targetPath).catch(err => {
				appLogger?.error(`Failed to switch to page ${pageName}: ${err.message}`);
			});
			}
		});

	    setupAutoUpdater();
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

		ipcMain.handle('select-image-file', async () => {
			const { canceled, filePaths } = await dialog.showOpenDialog(BrowserWindow.getFocusedWindow(), {
				title: 'Select Image to Import',
				filters: [{ name: 'DirectDraw Surface', extensions: ['dds'] }],
				properties: ['openFile']
			});

			if (canceled || filePaths.length === 0) {
				return { success: true, canceled: true };
			}

			return { success: true, canceled: false, filePath: filePaths[0] };
		});

		ipcMain.handle('import-image', async (event, args) => {
			if (!INPUT_DIR) return { success: false, message: "No project loaded." };

			try {
				let sourcePath = args?.sourcePath;
				let newFileName = args?.newFileName;
				if (!sourcePath) {
					const result = await dialog.showOpenDialog({
						title: 'Select Image to Import',
						properties: ['openFile'], // Required for Linux/GTK
						filters: [
							{ name: 'Images', extensions: ['dds', 'png', 'jpg', 'tga'] }
						]
					});

					if (result.canceled || result.filePaths.length === 0) {
						return { success: false, message: "Import cancelled by user." };
					}
					sourcePath = result.filePaths[0];
					
					// If newFileName wasn't passed from the modal yet, 
					// generate a safe one from the selected file
					if (!newFileName) {
						newFileName = path.basename(sourcePath)
							.replace(/\s+/g, '_') // Strip spaces
							.replace(/\.[^/.]+$/, "") + ".dds"; // Force .dds
					}
				}

				const gfxDir = path.join(INPUT_DIR, 'GFX');
				const destPath = path.join(gfxDir, newFileName);

				await fs.ensureDir(gfxDir);

				await fs.copy(sourcePath, destPath);

				const relativePath = path.join('GFX', newFileName).replace(/\\/g, '/');
				await updateManifest('add', relativePath);

				appLogger?.info(`Imported image: ${newFileName} to GFX folder`, { source: 'Main-Import' });
				
				return { 
					success: true, 
					path: destPath, 
					fileName: newFileName 
				};

			} catch (error) {
				appLogger?.error(`Failed to import image: ${error.message}`, { source: 'Main-Import' });
				return { success: false, message: error.message };
			}
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

	app.on('will-quit', async (event) => {
		if (watcherProcess) watcherProcess.kill('SIGKILL');
		if (!isArchivingAndQuitting) {
		isArchivingAndQuitting = true;
		event.preventDefault();
		await logger.archiveCurrentSession();
		app.quit();
		}
	});
	
}

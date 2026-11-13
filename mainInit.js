// main.js - The Electron Main Process
import { app, BrowserWindow, ipcMain, dialog, Menu } from 'electron';
import path from 'path';
import fs from 'fs-extra';
import { fileURLToPath} from 'url'; 
import logger from './src/logger.cjs';
import { processImage } from './src/ipcHandlers/img-converter.binary.js';

const { handleIpcLog, initializeLogger, archiveCurrentSession, getSessionDir, getLogsRootDir, getLogsSessionDir, MAX_LOG_STORAGE_MB } = logger;
const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// SYSTEM PATHS (Persistent User Data - OS safe storage)
const USER_DATA_PATH = app.getPath('userData');
const USER_SETTINGS_DIR = path.join(USER_DATA_PATH, 'settings');
const USER_PROJECTS_DIR = path.join(USER_DATA_PATH, 'projects', 'hoi4');
const USER_WORKSPACES_DIR = path.join(USER_DATA_PATH, 'workspaces', 'hoi4');

// APP PATHS (Static/Code - Read-only, updated via app installer)
const TEMPLATE_DIR = path.join(__dirname, 'templates', 'workspace template'); 
const TEMPLATE_CACHE_DIR = path.join(__dirname, 'templates', 'workspace template cache'); 
const WATCHER_SCRIPT_PATH = path.join(__dirname, 'src/watcher_workspace.js'); 

// SETTINGS FILE PATHS
const LOGGER_SETTINGS_PATH = path.join(USER_SETTINGS_DIR, 'logger_settings.json');

export const state = {
    handleIpcLog, 
    initializeLogger,
    archiveCurrentSession,
    getSessionDir,
    getLogsRootDir,
    getLogsSessionDir,
    MAX_LOG_STORAGE_MB,
    __filename,
    __dirname,




    // SYSTEM PATHS (Persistent User Data - OS safe storage)
    USER_DATA_PATH,
    USER_SETTINGS_DIR,
    USER_PROJECTS_DIR,
    USER_WORKSPACES_DIR,

    // APP PATHS (Static/Code - Read-only, updated via app installer)
    TEMPLATE_DIR,
    TEMPLATE_CACHE_DIR,
    WATCHER_SCRIPT_PATH,

    // SETTINGS FILE PATHS
    LOGGER_SETTINGS_PATH,

    // --- Global State ---
    currentProjectConfig: null, 
    INPUT_DIR: null,
    OUTPUT_DIR: null,
    METADATA_DIR: null,
    watcherProcess: null, 
    mainWindow: null, 
    appLogger: null, 
    isArchivingAndQuitting: false,
    LogBroadcaster: null, 
    isLoggerReady: false,
    logBuffer: [],
    pendingUpdateInfo: null, 
}

// Define the core helper function
export async function executeProcessImage({ filePath, imageType, mainOutputDir, cacheOutputDir }) {
    if (!INPUT_DIR) {
        return { success: false, message: "No project loaded." };
    }

    const targetMainOutput = mainOutputDir || OUTPUT_DIR;
    const targetCacheOutput = cacheOutputDir || path.join(currentProjectConfig.project_root, 'cache', 'images');

    return await processImage(filePath, imageType, targetMainOutput, targetCacheOutput, logToSystem);
}

/**
 * Enhanced log wrapper that buffers messages until the logger is ready
 */
    
// todo: move LogBroadcaster code to the logger.cjs file so it broadcasts the logs, 
// and make all files to not use logToSystem but a internal logger.cjs helper function
export function logToSystem(logData) {
    if (!state.isLoggerReady) {
    state.logBuffer.push(logData);
    return;
    }
    state.handleIpcLog(logData);
    if (state.LogBroadcaster) {
    // ensure we dont pass undefined or null messages to the LogBroadcaster, as the normal logger can handle it but the log broadcaster cannot currently cant (once we move this to logger.cjs itll be cleaner)
    if (!logData || typeof logData !== 'object') {
        logData = { type: 'info', message: String(logData), source: 'System' };
    }
    state.LogBroadcaster.emit('log', {
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
export async function initializeDirectories() {
    await fs.mkdir(USER_SETTINGS_DIR, { recursive: true });
    await fs.mkdir(USER_PROJECTS_DIR, { recursive: true });
    await fs.mkdir(USER_WORKSPACES_DIR, { recursive: true });
    state.appLogger?.info(`Initialized directories at: ${USER_DATA_PATH}`, { source: 'Main-Init' });
}

/**
 * Reads a user setting file, falling back to a default if it doesn't exist.
 */
export async function readSettingsFile(userPath, defaultPath) {
    try {
    const data = await fs.readFile(userPath, 'utf8');
    return JSON.parse(data);
    } catch (error) {
    if (error.code === 'ENOENT') {
        state.appLogger?.warn(`Settings missing: ${path.basename(userPath)}. Creating from default.`, { source: 'Main-Settings' });
        try {
            const defaultData = await fs.readFile(defaultPath, 'utf8');
            const settings = JSON.parse(defaultData);
            await fs.writeFile(userPath, JSON.stringify(settings, null, 4), 'utf8');
            return settings;
        } catch (defaultError) {
            state.appLogger?.error(`FATAL: Default settings missing at ${defaultPath}`, { source: 'Main-Settings' });
            throw defaultError;
        }
    }
    throw error;
    }
}
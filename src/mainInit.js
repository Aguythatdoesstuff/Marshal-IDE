// main.js - The Electron Main Process
import { app, BrowserWindow, ipcMain, dialog, Menu } from 'electron';
import path from 'path';
import fs from 'fs-extra';
import { fileURLToPath} from 'url'; 
import logger from './logger.cjs';

const { handleIpcLog, initializeLogger, archiveCurrentSession, getSessionDir, getLogsRootDir, getLogsSessionDir, MAX_LOG_STORAGE_MB } = logger;
const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// SYSTEM PATHS (Persistent User Data - OS safe storage)
const USER_DATA_PATH = app.getPath('userData');
const USER_SETTINGS_DIR = path.join(USER_DATA_PATH, 'settings');
const USER_PROJECTS_DIR = path.join(USER_DATA_PATH, 'projects', 'hoi4');
const USER_WORKSPACES_DIR = path.join(USER_DATA_PATH, 'workspaces', 'hoi4');
const LOGS_ROOT_PATH = path.join(USER_DATA_PATH, 'logs');

// APP PATHS (Static/Code - Read-only, updated via app installer)
const TEMPLATE_DIR = path.join(__dirname, 'templates', 'workspace template'); 
const TEMPLATE_CACHE_DIR = path.join(__dirname, 'templates', 'workspace template cache'); 
const WATCHER_SCRIPT_PATH = path.join(__dirname, 'src/watcher_workspace.js'); 

// SETTINGS FILE PATHS
const LOGGER_SETTINGS_PATH = path.join(USER_SETTINGS_DIR, 'logger_settings.json');

const systemInfo = { 
    platform: process.platform, 
    arch: process.arch, 
    version: app.getVersion() 
};

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
    LOGS_ROOT_PATH,
    systemInfo,

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
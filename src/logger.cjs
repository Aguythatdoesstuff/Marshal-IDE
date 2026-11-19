const EventEmitter = require('events');
const winston = require('winston');
const path = require('path');
const fs = require('fs/promises');
const archiver = require('archiver');
const { createWriteStream, existsSync } = require('fs');
const { dialog } = require('electron');
const { performance } = require('perf_hooks');

// --- Log Broadcaster ---
// migrated directly here to avoid dynamic require bundling errors with Vite/Rollup.
const logBroadcaster = new EventEmitter();
logBroadcaster.setMaxListeners(20);

// --- Configuration ---
let LOGS_ROOT_DIR = null; 
let SESSION_DIR = null; 

let MAX_LOG_STORAGE_MB = 20;
let MAX_LOG_STORAGE_BYTES = MAX_LOG_STORAGE_MB * 1024 * 1024;

let lastTimestamp = performance.now(); // Initialize global tracker

const SESSION_TIMESTAMP = new Date().toISOString().replace(/:/g, '-').replace(/\..+/, '');

function getDelta() {
    const now = performance.now();
    const diff = now - lastTimestamp;
    lastTimestamp = now; 
    return diff.toFixed(2); // Returns ms as a string, e.g., "10.45"
}

const formatLogString = ({ level, message, timestamp, source, exactTimestamp, exactDelta }) => {
    // Prefer exact timings if passed (from buffer or interceptor), otherwise calculate fresh
    const finalTimestamp = exactTimestamp || timestamp || new Date().toISOString().replace(/\..+/, '');
    const finalDelta = exactDelta || getDelta();
    return `${finalTimestamp} (+${finalDelta}ms) [${level.toUpperCase()}] [${source || 'Main'}] - ${message}`;
};

const winstonLogFormat = winston.format.printf(formatLogString);

let logger = null;
let mainWindow = null;
const preInitBuffer = [];

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

        // SMART FILTER: Prevents recursive loops by ignoring Winston's own outputs
        const isAlreadyFormatted = /^\d{4}-\d{2}-\d{2}/.test(message);
        
        if (isAlreadyFormatted) return;

        // Route to the IPC handler which will automatically buffer if !logger
        handleIpcLog({
            type: level === 'log' ? 'info' : level,
            message: message,
            source: 'Console-Intercept' 
        });
    };
});

// --- CRITICAL FALLBACK ---

function handleCriticalLoggerError(message, error) {
    const errorDetails = (error && (error.stack || error.message)) || String(error);
    console.error(`[FATAL LOGGER ERROR] ${message}: ${errorDetails}`);
    if (dialog) {
        dialog.showErrorBox("Marshal IDE Critical Logger Error", `${message}\n\nDetails: ${errorDetails}`);
    }
}

// --- STORAGE UTILITIES ---

async function getDirectorySize(itemPath) {
    try {
        const stats = await fs.lstat(itemPath);
        if (stats.isFile()) return stats.size;
        if (stats.isDirectory()) {
            let totalSize = stats.size;
            const entries = await fs.readdir(itemPath);
            for (const entry of entries) {
                totalSize += await getDirectorySize(path.join(itemPath, entry));
            }
            return totalSize;
        }
        return 0;
    } catch (e) { return 0; }
}

/**
 * Detailed Storage Maintenance
 */
async function performStorageMaintenance() {
    if (!LOGS_ROOT_DIR || !existsSync(LOGS_ROOT_DIR)) return;
    if (!logger) return;

    const entries = await fs.readdir(LOGS_ROOT_DIR, { withFileTypes: true });
    const logItems = [];
    let initialSize = 0;

    for (const entry of entries) {
        // We only care about session directories or archived zip files
        if (!entry.isDirectory() && (!entry.isFile() || !entry.name.endsWith('.zip'))) continue;
        
        const fullPath = path.join(LOGS_ROOT_DIR, entry.name);
        const size = await getDirectorySize(fullPath);
        initialSize += size;
        logItems.push({ name: entry.name, path: fullPath, size });
    }

    // Sort by name (timestamp) so oldest items are first
    logItems.sort((a, b) => a.name.localeCompare(b.name));

    const initialMB = (initialSize / (1024 * 1024)).toFixed(2);
    logger.info(`--- Logger Storage Audit: ${initialMB} MB currently used. ---`, { source: 'Logger-Maintenance' });

    let currentSize = initialSize;
    let deletedCount = 0;

    while (currentSize > MAX_LOG_STORAGE_BYTES && logItems.length > 0) {
        const oldestItem = logItems.shift();
        
        // Safety: Never delete the active session directory
        if (oldestItem.name === SESSION_TIMESTAMP) continue;

        logger.warn(`Deleting old log data: ${oldestItem.name} (${(oldestItem.size / 1024).toFixed(1)} KB)`, { source: 'Logger-Maintenance' });
        
        try {
            await fs.rm(oldestItem.path, { recursive: true, force: true });
            currentSize -= oldestItem.size;
            deletedCount++;
        } catch (e) {
            logger.error(`Failed to delete ${oldestItem.name}: ${e.message}`, { source: 'Logger-Maintenance' });
        }
    }

    const finalMB = (currentSize / (1024 * 1024)).toFixed(2);
    if (deletedCount > 0) {
        logger.info(`Storage maintenance complete. Deleted ${deletedCount} item(s). New total: ${finalMB} MB.`, { source: 'Logger-Maintenance' });
    } else {
        logger.info(`Storage maintenance complete. No deletions required. Total usage: ${finalMB} MB.`, { source: 'Logger-Maintenance' });
    }
}

function attemptArchiveOldSession(sessionDir, sessionTimestamp) {
    return new Promise(async (resolve, reject) => {
        const outputZipPath = path.join(LOGS_ROOT_DIR, `${sessionTimestamp}.zip`);
        const output = createWriteStream(outputZipPath);
        const archive = archiver('zip', { zlib: { level: 9 } });

        output.on('close', async () => {
            if (logger) logger.info(`Successfully archived old session: ${sessionTimestamp}`, { source: 'Archive-Cleanup' });
            await fs.rm(sessionDir, { recursive: true, force: true });
            resolve();
        });
        archive.on('error', (err) => reject(err));
        archive.pipe(output);
        try {
            archive.directory(sessionDir, false);
            archive.finalize();
        } catch (e) { reject(e); }
    });
}

/**
 * Asynchronously initializes the logging system, setting up session storage paths, 
 * Winston log transports, UI log piping via logBroadcaster, and buffer flushing.
 * 
 * @param {string} logsRootPath - Absolute path to the root logs storage directory.
 * @param {Object} systemInfo - Operating system and architecture diagnostic metadata.
 * @param {Electron.BrowserWindow} mainWin - Primary application window instance.
 * @param {number} maxStorageMB - Maximum allowable log storage size in megabytes.
 * @param {Object} state - The single global application state reference.
 * @returns {Promise<winston.Logger>} The configured Winston logger instance.
 */
async function initializeLogger(logsRootPath, systemInfo, mainWin, maxStorageMB, state) {
    if (logger) return logger;
    mainWindow = mainWin;

    // Attach references to the global state container
    if (state) {
        state.mainWindow = mainWin;
        state.LogBroadcaster = logBroadcaster;
    }

    // Configure storage byte thresholds if a valid numeric value is provided
    if (maxStorageMB && !isNaN(maxStorageMB)) {
        MAX_LOG_STORAGE_MB = maxStorageMB;
        MAX_LOG_STORAGE_BYTES = MAX_LOG_STORAGE_MB * 1024 * 1024;
    }

    LOGS_ROOT_DIR = logsRootPath;
    SESSION_DIR = path.join(LOGS_ROOT_DIR, SESSION_TIMESTAMP);

    // Ensure the unique session log directory exists on disk
    try {
        await fs.mkdir(SESSION_DIR, { recursive: true });
    } catch (e) {
        handleCriticalLoggerError("Failed to create log directory", e);
        return console;
    }

    // Set up a permanent data pipe directly to the browser window instance.
    // This makes your UI logs immune to Vite frontend dev-server Hot Module Reloads!
    logBroadcaster.on('log', (msg) => {
        if (mainWindow && !mainWindow.webContents.isDestroyed()) {
            mainWindow.webContents.send('log-broadcaster-update', msg);
        }
    });

    // Define Winston output transports (Console for dev output, File for persistence)
    const transports = [
        new winston.transports.Console({
            level: 'debug',
            format: winston.format.combine(winston.format.colorize(), winston.format.timestamp({ format: 'YYYY-MM-DD HH:mm:ss' }), winstonLogFormat)
        }),
        new winston.transports.File({ 
            filename: path.join(SESSION_DIR, 'Master.log'), 
            level: 'debug',
            format: winston.format.combine(winston.format.timestamp({ format: 'YYYY-MM-DD HH:mm:ss' }), winstonLogFormat)
        })
    ];

    logger = winston.createLogger({ level: 'debug', transports, exitOnError: false });

    const originalLog = logger.log.bind(logger);
    logger.log = (levelOrInfo, ...args) => {
        const result = originalLog(levelOrInfo, ...args);
        let level;
        let message;
        let source = 'Main';

        if (typeof levelOrInfo === 'object' && levelOrInfo !== null) {
            level = levelOrInfo.level;
            message = levelOrInfo.message;
            source = levelOrInfo.source || source;
        } else {
            level = levelOrInfo;
            message = args[0];
            if (args[1] && typeof args[1] === 'object' && args[1].source) {
                source = args[1].source;
            }
        }

        const resolvedMessage = typeof message === 'string'
            ? message
            : (message instanceof Error ? message.stack : JSON.stringify(message));

        logBroadcaster.emit('log', {
            level,
            message: resolvedMessage,
            source,
            timestamp: new Date().toISOString()
        });

        return result;
    };

    state.isLoggerReady = true; 
    
    // Flush any logs accumulated in the pre-init buffer before logger readiness
    while (preInitBuffer.length > 0) {
        const bufferedLog = preInitBuffer.shift();
        handleIpcLog(bufferedLog);
    }

    // Record system diagnostic startup metadata
    logger.info(`--- System Info ---`, { source: 'Logger-System' });
    logger.info(`Platform: ${systemInfo.platform}, Arch: ${systemInfo.arch}, Version: ${systemInfo.version}`, { source: 'Logger-System' });
    logger.info(`Session started in ${SESSION_DIR}`, { source: 'Logger-System' });

    // Execute background storage maintenance and session archiving asynchronously
    (async () => {
        try {
            if (existsSync(LOGS_ROOT_DIR)) {
                const entries = await fs.readdir(LOGS_ROOT_DIR, { withFileTypes: true });
                const oldDirs = entries.filter(d => d.isDirectory() && d.name !== SESSION_TIMESTAMP);
                for (const dir of oldDirs) {
                    await attemptArchiveOldSession(path.join(LOGS_ROOT_DIR, dir.name), dir.name).catch(() => {});
                }
            }
            await performStorageMaintenance();
        } catch (maintenanceError) {
            if (logger) logger.error(`Background maintenance error: ${maintenanceError.message}`, { source: 'Logger-Maintenance' });
        }
    })();

    logger.info('Logger initialized and ready.', { source: 'Logger-System' });
    return logger;
}

/**
 * Handles incoming log data, normalizes payloads, buffers them if uninitialized, 
 * and writes them to Winston transports and specific log files.
 * 
 * @param {Object|string} data - The log payload or message string to process.
 */
function handleIpcLog(data) {
    const sanitized = {
        level: (data && data.type) ? data.type.toLowerCase() : 'warn',
        source: (data && data.source) ? data.source : 'Unknown-Process',
        message: ''
    };

    if (typeof data === 'string') {
        sanitized.message = data;
    } else if (data && data.message) {
        sanitized.message = data.message;
    } else {
        sanitized.message = JSON.stringify(data) || 'Empty/Null Log';
    }

    // Capture exact timing the moment the function is called
    const exactTimestamp = new Date().toISOString().replace(/\..+/, '');
    const exactDelta = getDelta();

    // If logger isn't ready yet, buffer it with precise timings
    if (!logger) {
        preInitBuffer.push({
            ...sanitized,
            timestamp: exactTimestamp,
            delta: exactDelta
        });
        return;
    }

    logger.log({ 
        level: sanitized.level, 
        message: sanitized.message, 
        source: sanitized.source,
        exactTimestamp,
        exactDelta
    });

    let specificLogFile;
    if (sanitized.source.includes('Watcher')) specificLogFile = 'Watcher.log';
    else if (sanitized.source.includes('Compiler')) specificLogFile = 'Compilers.log';

    if (specificLogFile) {
        const logEntry = formatLogString({ 
            level: sanitized.level, 
            message: sanitized.message, 
            source: sanitized.source, 
            exactTimestamp, 
            exactDelta 
        });
        fs.appendFile(path.join(SESSION_DIR, specificLogFile), `${logEntry}\n`).catch(() => {});
    }
}

function archiveCurrentSession() {
    return new Promise(async (resolve, reject) => {
        try {
            if (logger) {
                await new Promise((resolveLogger) => {
                    logger.end(() => {
                        resolveLogger();
                    });
                });
                logger = null;
            }
        } catch (e) {
        }

        const outputZipPath = path.join(LOGS_ROOT_DIR, `${SESSION_TIMESTAMP}.zip`);

        const output = createWriteStream(outputZipPath);
        const archive = archiver('zip', { zlib: { level: 9 } });

        output.on('close', async () => {
            try {
                await fs.rm(SESSION_DIR, { recursive: true, force: true });
                resolve(`Archived: ${path.basename(outputZipPath)}`);
            } catch (e) { 
                reject(e); 
            }
        });

        output.on('error', (err) => {
            reject(err);
        });

        archive.on('error', (err) => {
            reject(err);
        });

        archive.pipe(output);

        try {
            archive.directory(SESSION_DIR, false);
            await archive.finalize();
        } catch (e) { 
            reject(e); 
        }
    });
}


function getSessionDir() {
    return SESSION_DIR;
}

module.exports = {
    initializeLogger,
    handleIpcLog,
    archiveCurrentSession,
    getSessionDir,
    getLogsRootDir: () => LOGS_ROOT_DIR, 
    getLogsSessionDir: () => SESSION_DIR, 
    MAX_LOG_STORAGE_MB 
};

const logBroadcaster = require('../modules/LogBroadcaster.cjs');
const winston = require('winston');
const path = require('path');
const fs = require('fs/promises');
const archiver = require('archiver');
const { createWriteStream, existsSync } = require('fs');
const { dialog } = require('electron');
const { performance } = require('perf_hooks');

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
const formatLogString = ({ level, message, timestamp, source }) => {
	const delta = getDelta();
	return `${timestamp} (+${delta}ms) [${level.toUpperCase()}] [${source || 'Main'}] - ${message}`; [cite_start]// [cite: 4, 5]
};

const winstonLogFormat = winston.format.printf(formatLogString);

let logger = null;
let mainWindow = null;

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
            const files = await fs.readdir(sessionDir);
            files.forEach(file => archive.file(path.join(sessionDir, file), { name: file }));
            archive.finalize();
        } catch (e) { reject(e); }
    });
}

async function initializeLogger(logsRootPath, systemInfo, mainWin, maxStorageMB) {
    if (logger) return logger;
    mainWindow = mainWin;

    if (maxStorageMB && !isNaN(maxStorageMB)) {
        MAX_LOG_STORAGE_MB = maxStorageMB;
        MAX_LOG_STORAGE_BYTES = MAX_LOG_STORAGE_MB * 1024 * 1024;
    }

    LOGS_ROOT_DIR = logsRootPath;
    SESSION_DIR = path.join(LOGS_ROOT_DIR, SESSION_TIMESTAMP);

    try {
        await fs.mkdir(SESSION_DIR, { recursive: true });
    } catch (e) {
        handleCriticalLoggerError("Failed to create log directory", e);
        return console;
    }

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

    logger.info(`--- System Info ---`, { source: 'Logger-System' });
    logger.info(`Platform: ${systemInfo.platform}, Arch: ${systemInfo.arch}, Version: ${systemInfo.version}`, { source: 'Logger-System' });
    logger.info(`Session started in ${SESSION_DIR}`, { source: 'Logger-System' });

    // BACKGROUND MAINTENANCE
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

function archiveCurrentSession() {
    return new Promise(async (resolve, reject) => {
        const outputZipPath = path.join(LOGS_ROOT_DIR, `${SESSION_TIMESTAMP}.zip`);
        const output = createWriteStream(outputZipPath);
        const archive = archiver('zip', { zlib: { level: 9 } });

        output.on('close', async () => {
            try {
                await fs.rm(SESSION_DIR, { recursive: true, force: true });
                resolve(`Archived: ${path.basename(outputZipPath)}`);
            } catch (e) { resolve(`Archived, but dir deletion failed.`); }
        });
        archive.on('error', (err) => reject(err));
        archive.pipe(output);
        try {
            const files = await fs.readdir(SESSION_DIR);
            files.forEach(f => archive.file(path.join(SESSION_DIR, f), { name: f }));
            archive.finalize();
        } catch (e) { reject(e); }
    });
}

function handleIpcLog(data) {
    if (!logger) return;

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

    logger.log({ level: sanitized.level, message: sanitized.message, source: sanitized.source });

    let specificLogFile;
    if (sanitized.source.includes('Watcher')) specificLogFile = 'Watcher.log';
    else if (sanitized.source.includes('Compiler')) specificLogFile = 'Compilers.log';

    if (specificLogFile) {
        const timestamp = new Date().toISOString().replace(/\..+/, '');
        const logEntry = formatLogString({ ...sanitized, timestamp });
        fs.appendFile(path.join(SESSION_DIR, specificLogFile), `${logEntry}\n`).catch(() => {});
    }
}

module.exports = {
    initializeLogger,
    handleIpcLog,
    archiveCurrentSession,
    getLogsRootDir: () => LOGS_ROOT_DIR, 
    MAX_LOG_STORAGE_MB 
};

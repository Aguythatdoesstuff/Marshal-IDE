const EventEmitter = require('events');
const winston = require('winston');
const path = require('path');
const fs = require('fs/promises');
const archiver = require('archiver');
const { createWriteStream, existsSync } = require('fs');
const { dialog } = require('electron');
const { performance } = require('perf_hooks');
const { execFile } = require('node:child_process');
const { promisify } = require('node:util');
const koffi = require('koffi');

const execFileAsync = promisify(execFile);


const CCH_RM_SESSION_KEY = 32;
const CCH_RM_MAX_APP_NAME = 255;
const CCH_RM_MAX_SVC_NAME = 63;
const ERROR_SUCCESS = 0;
const ERROR_MORE_DATA = 234;

let restartManagerApi = null;


// --- Log Broadcaster ---
// migrated directly here to avoid dynamic require bundling errors with Vite/Rollup.
const logBroadcaster = new EventEmitter();
logBroadcaster.setMaxListeners(20);

// --- Configuration ---
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
async function performStorageMaintenance(state) {
    if (!state.LOGS_ROOT_PATH || !existsSync(state.LOGS_ROOT_PATH)) return;
    if (!logger) return;

    const entries = await fs.readdir(state.LOGS_ROOT_PATH, { withFileTypes: true });
    const logItems = [];
    let initialSize = 0;

    for (const entry of entries) {
        // We only care about session directories or archived zip files
        if (!entry.isDirectory() && (!entry.isFile() || !entry.name.endsWith('.zip'))) continue;
        
        const fullPath = path.join(state.LOGS_ROOT_PATH, entry.name);
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
        logger.info(`Storage maintenance complete. Deleted ${deletedCount} item(s). New total: ${finalMB} MB. Max log storage ${MAX_LOG_STORAGE_MB} MB.`, { source: 'Logger-Maintenance' });
    } else {
        logger.info(`Storage maintenance complete. No deletions required. Total usage: ${finalMB} MB. Max log storage ${MAX_LOG_STORAGE_MB} MB.`, { source: 'Logger-Maintenance' });
    }
}

function attemptArchiveOldSession(sessionDir, sessionTimestamp) {
    return new Promise(async (resolve, reject) => {
        const outputZipPath = path.join(sessionDir, `${sessionTimestamp}.zip`);
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
 * @param {Electron.BrowserWindow} mainWin - Primary application window instance..
 * @param {Object} state - The single global application state reference.
 * @returns {Promise<winston.Logger>} The configured Winston logger instance.
 */
async function initializeLogger(state) {
    if (logger) return logger;

    // Attach references to the global state container
    if (state) {
        state.LogBroadcaster = logBroadcaster;
    }

    try {
        const settingsExists = await fs.stat(state.LOGGER_SETTINGS_PATH).catch(() => false);
        if (settingsExists) {
            const logSettingsRaw = await fs.readFile(state.LOGGER_SETTINGS_PATH, 'utf8');
            const logSettings = JSON.parse(logSettingsRaw);
            
            const configValue = logSettings.max_archived_mb || logSettings.max_storage_size;

            if (configValue && !isNaN(configValue)) {
                MAX_LOG_STORAGE_MB = parseInt(configValue, 10);
            }
        }
    } catch (e) {
        console.error("Failed to read logger settings during bootstrap:", e.message);
    }




    // Configure storage byte thresholds if a valid numeric value is provided
    if (MAX_LOG_STORAGE_MB && !isNaN(MAX_LOG_STORAGE_MB)) {
        MAX_LOG_STORAGE_BYTES = MAX_LOG_STORAGE_MB * 1024 * 1024;
    }

    SESSION_DIR = path.join(state.LOGS_ROOT_PATH, SESSION_TIMESTAMP);

    // Ensure the unique session log directory exists on disk
    try {
        await fs.mkdir(SESSION_DIR, { recursive: true });
    } catch (e) {
        handleCriticalLoggerError("Failed to create log directory", e);
        return console;
    }

    // Set up a permanent data pipe directly to the browser window instance.
    // This makes UI logs immune to Vite frontend dev-server Hot Module Reloads!
    logBroadcaster.on('log', (msg) => {
        // Guard mainWindow and webContents with isDestroyed() so shutdown logs never throw "Object has been destroyed".
        if (state.mainWindow && !state.mainWindow.isDestroyed() && state.mainWindow.webContents && !state.mainWindow.webContents.isDestroyed()) {
            state.mainWindow.webContents.send('log-broadcaster-update', msg);
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
            
        // Guard mainWindow and webContents with isDestroyed() so shutdown logs never throw "Object has been destroyed".
        if (state.mainWindow && !state.mainWindow.isDestroyed() && state.mainWindow.webContents && !state.mainWindow.webContents.isDestroyed()) {
            logBroadcaster.emit('log', {
                level,
                message: resolvedMessage,
                source,
                timestamp: new Date().toISOString()
            });
        }

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
    logger.info(`Platform: ${state.systemInfo.platform}, Arch: ${state.systemInfo.arch}, Version: ${state.systemInfo.version}`, { source: 'Logger-System' });
    logger.info(`Session started in ${SESSION_DIR}`, { source: 'Logger-System' });

    // Execute background storage maintenance and session archiving asynchronously
    (async () => {
        try {
            if (existsSync(state.LOGS_ROOT_PATH)) {
                const entries = await fs.readdir(state.LOGS_ROOT_PATH, { withFileTypes: true });
                const oldDirs = entries.filter(d => d.isDirectory() && d.name !== SESSION_TIMESTAMP);
                for (const dir of oldDirs) {
                    await attemptArchiveOldSession(path.join(state.LOGS_ROOT_PATH, dir.name), dir.name).catch(() => {});
                }
            }
            await performStorageMaintenance(state);
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

function sleep(ms) {
    return new Promise((resolve) => setTimeout(resolve, ms));
}

function isProcessAlive(pid) {
    if (!Number.isInteger(pid) || pid <= 0) return false;
    try {
        process.kill(pid, 0);
        return true;
    } catch (err) {
        return err.code === 'EPERM';
    }
}

async function waitForPidsToExit(pids, timeoutMs = 3000) {
    const deadline = Date.now() + timeoutMs;
    while (Date.now() < deadline) {
        const remaining = pids.filter((pid) => isProcessAlive(pid));
        if (remaining.length === 0) return;
        await sleep(100);
    }
}

/**
 * Returns PIDs of Linux or Windows processes holding a handle/lock on `filePath`.
 * @param {string} filePath
 * @param {{ handleExePath?: string }} [options]
 * @returns {Promise<number[]>}
 */
async function getLockingPids(filePath) {
    if (!filePath || typeof filePath !== 'string') {
        throw new TypeError('filePath must be a non-empty string');
    }

    if (process.platform === 'win32') {
        return getWindowsLockingPids(filePath);
    }

    if (process.platform === 'linux') {
        try {
            const { stdout } = await execFileAsync('lsof', ['-t', filePath]);
            return [
                ...new Set(
                    stdout
                        .split(/\r?\n/)
                        .map((line) => Number.parseInt(line.trim(), 10))
                        .filter((pid) => Number.isInteger(pid) && pid > 0)
                ),
            ];
        } catch (err) {
            if (err.code === 1) {
                return [];
            }
            throw err;
        }
    }

    throw new Error(`Unsupported platform: ${process.platform}`);
}

function getRestartManagerApi() {
    if (restartManagerApi) return restartManagerApi;

    const rstrtmgr = koffi.load('Rstrtmgr.dll');

    const FILETIME = koffi.struct('FILETIME', {
        dwLowDateTime: 'uint32',
        dwHighDateTime: 'uint32',
    });

    const RM_UNIQUE_PROCESS = koffi.struct('RM_UNIQUE_PROCESS', {
        dwProcessId: 'uint32',
        ProcessStartTime: FILETIME,
    });

    const RM_PROCESS_INFO = koffi.struct('RM_PROCESS_INFO', {
        Process: RM_UNIQUE_PROCESS,
        strAppName: koffi.array('char16', CCH_RM_MAX_APP_NAME + 1),
        strServiceShortName: koffi.array('char16', CCH_RM_MAX_SVC_NAME + 1),
        ApplicationType: 'int',
        AppStatus: 'uint32',
        TSSessionId: 'uint32',
        bRestartable: 'int',
    });

    restartManagerApi = {
        RmStartSession: rstrtmgr.func('__stdcall', 'RmStartSession', 'uint32', [
            koffi.out('uint32 *'),
            'uint32',
            koffi.out('uint16 *'),
        ]),
        RmRegisterResources: rstrtmgr.func('__stdcall', 'RmRegisterResources', 'uint32', [
            'uint32',
            'uint32',
            'str16 *',
            'uint32',
            'void *',
            'uint32',
            'void *',
        ]),
        RmGetList: rstrtmgr.func('__stdcall', 'RmGetList', 'uint32', [
            'uint32',
            koffi.out('uint32 *'),
            koffi.inout('uint32 *'),
            koffi.out(koffi.pointer(RM_PROCESS_INFO)),
            koffi.out('uint32 *'),
        ]),
        RmEndSession: rstrtmgr.func('__stdcall', 'RmEndSession', 'uint32', ['uint32']),
    };

    return restartManagerApi;
}

async function collectTargetFiles(targetPath) {
    const resolvedPath = path.resolve(targetPath);
    const stats = await fs.stat(resolvedPath);
    if (!stats.isDirectory()) {
        return [resolvedPath];
    }

    const files = [];
    const entries = await fs.readdir(resolvedPath, { withFileTypes: true });
    for (const entry of entries) {
        const entryPath = path.join(resolvedPath, entry.name);
        if (entry.isDirectory()) {
            const nestedFiles = await collectTargetFiles(entryPath);
            files.push(...nestedFiles);
        } else if (entry.isFile()) {
            files.push(entryPath);
        }
    }
    return files;
}

async function getWindowsLockingPids(filePath) {
    const { RmStartSession, RmRegisterResources, RmGetList, RmEndSession } = getRestartManagerApi();
    const targetFiles = await collectTargetFiles(filePath);
    if (targetFiles.length === 0) {
        return [];
    }

    const sessionHandle = [0];
    // Restart Manager expects a wide-char (UTF-16, 2 bytes per char) buffer for strSessionKey.
    const sessionKey = Buffer.alloc((CCH_RM_SESSION_KEY + 1) * 2);

    const startCode = RmStartSession(sessionHandle, 0, sessionKey);
    if (startCode !== ERROR_SUCCESS) {
        throw new Error(`RmStartSession failed with code: ${startCode}`);
    }

    const handle = sessionHandle[0];

    try {
        const registerCode = RmRegisterResources(handle, targetFiles.length, targetFiles, 0, null, 0, null);
        if (registerCode !== ERROR_SUCCESS) {
            throw new Error(`RmRegisterResources failed with code: ${registerCode}`);
        }

        const nProcInfoNeeded = [0];
        const nProcInfo = [0];
        const rebootReasons = [0];

        const initialListCode = RmGetList(handle, nProcInfoNeeded, nProcInfo, null, rebootReasons);
        if (initialListCode === ERROR_SUCCESS) {
            return [];
        }
        if (initialListCode !== ERROR_MORE_DATA) {
            throw new Error(`RmGetList failed with code: ${initialListCode}`);
        }

        const count = nProcInfoNeeded[0];
        nProcInfo[0] = count;
        const processList = Array.from({ length: count }, () => ({}));

        const fullListCode = RmGetList(handle, nProcInfoNeeded, nProcInfo, processList, rebootReasons);
        if (fullListCode !== ERROR_SUCCESS) {
            throw new Error(`RmGetList (second call) failed with code: ${fullListCode}`);
        }

        const pids = new Set();
        for (const info of processList.slice(0, nProcInfo[0])) {
            const pid = info?.Process?.dwProcessId;
            if (Number.isInteger(pid) && pid > 0) {
                pids.add(pid);
                const appName = typeof info?.strAppName === 'string' && info.strAppName.length > 0
                    ? info.strAppName
                    : 'unknown';
                originalConsole.log(`[Archive-Cleanup] Locking process detected: PID ${pid} (${appName}) on "${filePath}".`);
            }
        }
        return [...pids];
    } finally {
        // Always end the Restart Manager session so Windows does not hit its concurrent session limit.
        RmEndSession(handle);
    }
}

async function escalateAndKillLockers(targetPath) {
    const pids = await getLockingPids(targetPath);
    const filteredPids = pids.filter((pid) => pid !== process.pid);

    if (filteredPids.length === 0) {
        originalConsole.log(`[Archive-Cleanup] No external locking PIDs found for "${targetPath}".`);
        return [];
    }

    originalConsole.log(`[Archive-Cleanup] Found locking PIDs for "${targetPath}": ${filteredPids.join(', ')}`);

    const signals = ['SIGTERM', 'SIGINT', 'SIGKILL'];
    for (const signal of signals) {
        const aliveBeforeSignal = filteredPids.filter((pid) => isProcessAlive(pid));
        if (aliveBeforeSignal.length === 0) break;

        for (const pid of aliveBeforeSignal) {
            try {
                originalConsole.log(`[Archive-Cleanup] Sending ${signal} to PID ${pid} holding lock on "${targetPath}".`);
                process.kill(pid, signal);
            } catch (killErr) {
                originalConsole.error(`[Archive-Cleanup] Failed to send ${signal} to PID ${pid}: ${killErr.message}`);
            }
        }

        await waitForPidsToExit(aliveBeforeSignal, 1500);
    }

    return filteredPids;
}

async function runWithEbusyRecovery(operation, fallbackPath) {
    try {
        return await operation();
    } catch (err) {
        if (!err || err.code !== 'EBUSY') {
            throw err;
        }

        const lockedPath = err.path || fallbackPath;
        originalConsole.warn(`[Archive-Cleanup] EBUSY on "${lockedPath}". Pass 1: waiting 1000ms before retry.`);
        await sleep(1000);

        try {
            return await operation();
        } catch (pass1RetryErr) {
            if (!pass1RetryErr || pass1RetryErr.code !== 'EBUSY') {
                throw pass1RetryErr;
            }

            const pass1Target = pass1RetryErr.path || lockedPath;
            originalConsole.warn(`[Archive-Cleanup] Still EBUSY on "${pass1Target}" after 1000ms wait. Pass 1: escalating signals on locking PIDs.`);
            await escalateAndKillLockers(pass1Target);

            try {
                return await operation();
            } catch (pass2StartErr) {
                if (!pass2StartErr || pass2StartErr.code !== 'EBUSY') {
                    throw pass2StartErr;
                }

                const pass2Target = pass2StartErr.path || pass1Target;
                originalConsole.warn(`[Archive-Cleanup] Still EBUSY on "${pass2Target}" after Pass 1. Pass 2: immediately escalating signals on locking PIDs.`);
                await escalateAndKillLockers(pass2Target);

                return await operation();
            }
        }
    }
}

function createZipArchive(sourceDir, outputZipPath) {
    return new Promise((resolve, reject) => {
        const output = createWriteStream(outputZipPath);
        const archive = archiver('zip', { zlib: { level: 9 } });

        let settled = false;
        const finishResolve = () => {
            if (settled) return;
            settled = true;
            resolve();
        };
        const finishReject = (err) => {
            if (settled) return;
            settled = true;
            reject(err);
        };

        output.on('close', finishResolve);
        output.on('error', finishReject);
        archive.on('error', finishReject);

        archive.pipe(output);

        try {
            archive.directory(sourceDir, false);
            archive.finalize();
        } catch (e) {
            finishReject(e);
        }
    });
}

function archiveCurrentSession(state) {
    return new Promise(async (resolve) => {
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
            originalConsole.error(`[Archive-Cleanup] Error closing logger stream: ${e.message}`);
        }

        const logsRootPath = (state && state.LOGS_ROOT_PATH) || (SESSION_DIR ? path.dirname(SESSION_DIR) : null);
        if (!logsRootPath || !SESSION_DIR) {
            originalConsole.error('[Archive-Cleanup] Missing logsRootPath or SESSION_DIR; skipping archive and resolving.');
            resolve();
            return;
        }

        const outputZipPath = path.join(logsRootPath, `${SESSION_TIMESTAMP}.zip`);

        try {
            await runWithEbusyRecovery(
                () => createZipArchive(SESSION_DIR, outputZipPath),
                SESSION_DIR
            );
        } catch (archiveErr) {
            originalConsole.error(`[Archive-Cleanup] Failed to create archive "${outputZipPath}" after EBUSY recovery: ${archiveErr.message}`);
            resolve();
            return;
        }

        try {
            await runWithEbusyRecovery(
                () => fs.rm(SESSION_DIR, { recursive: true, force: true }),
                SESSION_DIR
            );
            originalConsole.log(`[Archive-Cleanup] Archived and cleaned up session: ${path.basename(outputZipPath)}`);
        } catch (removeErr) {
            originalConsole.error(`[Archive-Cleanup] Failed to remove session directory "${SESSION_DIR}" after EBUSY recovery: ${removeErr.message}`);
        }

        resolve();
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
    getLogsRootDir: () => state.LOGS_ROOT_PATH, 
    getLogsSessionDir: () => SESSION_DIR, 
    MAX_LOG_STORAGE_MB 
};

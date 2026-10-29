// watcher_workspace.js (Child Process)
import fs from 'fs-extra';
import * as path from 'path';
import chokidar from 'chokidar';
import { fileURLToPath } from 'url';
import { handleDeletion, handleRename } from './deletion_handler.js'; 
import { SyncEngine } from './sync_engine.js'; 

const __filename = fileURLToPath(import.meta.url);

let config; 
let outputBaseDir; 

const ALLOWED_EXTENSIONS = new Set(['.event', '.decision', '.scriptedgui', '.script', '.idea', '.focus', '.dds', '.equipment', '.unit', '.oob', '.history']);

// --- Process Safety ---
const checkParentAndExit = () => {
    if (!process.connected) {
        console.log('warn', 'Parent process disconnected. Watcher terminating compiler process...', 'Watcher-Lifecycle');
        stopCompilerProcess();
        process.exit(0)
    };
};
process.on('disconnect', () => checkParentAndExit());
setInterval(checkParentAndExit, 2000);

export function logToMain(type, message, source) {
    const normalizedType = type.toLowerCase();
    process.send({ type: normalizedType, message, source });
}

async function setupWorkspace() {
    const SOURCE = 'Watcher-Setup';
    try {
        const payloadString = process.argv[2]; 
        if (!payloadString) throw new Error(`No config payload.`);
        config = JSON.parse(payloadString);
        outputBaseDir = config.output_dir;
        const cache_dir = path.join(config.metadata_dir, 'fileOutCache');

        logToMain('info', `Requesting main process to spawn persistent compiler process...`, SOURCE);
        // Request main.js to spawn the compiler via compiler.js
        process.send({
            action: 'spawn-compiler',
            config: {
                output_dir: config.output_dir,
                log_dir: config.log_dir,    
                cache_dir: cache_dir
            }
        });

        // Safe auto-cleanup when the parent script exits
        process.on('exit', () => {
            stopCompilerProcess();
        });

    } catch (error) {
        logToMain('error', `Setup failed: ${error.message}`, SOURCE);
        process.exit(1); 
    }
}

function triggerCompilation(filePath) {
    const ext = path.extname(filePath).toLowerCase();

    // Whitelist check
    if (!ALLOWED_EXTENSIONS.has(ext)) return;

    try {
        const absolutePath = path.resolve(filePath);
        // Request main.js to compile the file via compiler.js
        process.send({
            action: 'compile-file',
            filePath: absolutePath
        });
    } catch (error) {
        logToMain('error', `Compile Error: ${error.message}`, 'Watcher-Compile');
    }
}

let lastUnlinkedPath = null;
const RENAME_THRESHOLD_MS = 1000; 

async function startWatcher() {
    const SOURCE = 'Watcher-Chokidar';
    await setupWorkspace();

    const syncEngine = new SyncEngine(config.project_root, config.input_dir, (f) => handleDeletion(f, outputBaseDir));
    const syncStats = await syncEngine.performInitialSync();

    // Compile files identified by the sync engine as new, modified, or part of an initial scan
    if (syncStats && Array.isArray(syncStats.changedFiles) && syncStats.changedFiles.length > 0) {
        logToMain('info', `Sync engine identified ${syncStats.changedFiles.length} files requiring compilation on startup. Processing...`, SOURCE);
        for (const filePath of syncStats.changedFiles) {
            triggerCompilation(filePath);
        }
    }

    const watcher = chokidar.watch(config.input_dir, {
        persistent: true,
        ignoreInitial: true, 
        ignored: (p) => {
            const fileName = path.basename(p);
            return fileName.startsWith('.') && fileName !== '.' && fileName !== '..';
        },
    });

    watcher
        .on('add', (filePath) => {
            const relPath = path.relative(config.input_dir, filePath);
            syncEngine.addFile(relPath).catch(() => {});
            
            if (lastUnlinkedPath) {
                handleRename(lastUnlinkedPath, filePath, outputBaseDir);
                lastUnlinkedPath = null; 
            }
            triggerCompilation(filePath);
        })
        .on('change', (filePath) => {
            const relPath = path.relative(config.input_dir, filePath);
            syncEngine.addFile(relPath).catch(() => {});
            logToMain('info', `File changed: ${path.basename(filePath)}`, SOURCE);
            triggerCompilation(filePath);
        })
        .on('unlink', (filePath) => {
            const relPath = path.relative(config.input_dir, filePath);
            syncEngine.removeFile(relPath).catch(() => {});
            lastUnlinkedPath = filePath;

            setTimeout(() => {
                if (lastUnlinkedPath === filePath) {
                    handleDeletion(filePath, outputBaseDir);
                    lastUnlinkedPath = null;
                }
            }, RENAME_THRESHOLD_MS);
        });

    logToMain('info', "Watcher Active and Monitoring.", SOURCE);
}

function walkAndCompile(dirPath) {
    const files = fs.readdirSync(dirPath, { withFileTypes: true });
    for (const file of files) {
        const fullPath = path.join(dirPath, file.name);
        if (file.isDirectory()) {
            walkAndCompile(fullPath); 
        } else {
            triggerCompilation(fullPath); 
        }
    }
}

function stopCompilerProcess(source = 'Watcher-Lifecycle') {
    process.send({
        action: 'stop-compiler',
        source
    });
}

// --- IPC Directive Router for Main Process Directives ---
process.on('message', (packet) => {
    if (!packet || typeof packet !== 'object') return;

    switch (packet.action) {
        case 'manual-recompile-all':
            logToMain('info', 'Received master workspace-wide rebuild instruction. Initiating complete compiler pass...', 'Watcher-IPC');
            
            if (config && config.input_dir) {
                walkAndCompile(config.input_dir); 
            }
            break;
            
        case 'shutdown':
            logToMain('info', 'Watcher process received lifecycle shutdown signal from parent app. Terminating persistent compilation process...', 'Watcher-IPC');
            stopCompilerProcess();
            process.exit(0);
            break;

        default:
            break;
    }
});


startWatcher();
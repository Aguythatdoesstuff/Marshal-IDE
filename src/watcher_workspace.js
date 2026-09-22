// watcher_workspace.js (Child Process)
import fs from 'fs-extra';
import * as path from 'path';
import chokidar from 'chokidar';
import { fileURLToPath, pathToFileURL } from 'url';
import { spawn } from 'child_process';
import { handleDeletion, handleRename } from './deletion_handler.js'; 
import { SyncEngine } from './sync_engine.js'; 

const __filename = fileURLToPath(import.meta.url);

let config; 
let compilerProcess = null; 
let outputBaseDir; 

const ALLOWED_EXTENSIONS = new Set(['.event', '.decision', '.scriptedgui', '.script', '.idea', '.focus', '.dds', '.equipment', '.unit']);

// --- Process Safety ---
const checkParentAndExit = () => {
    if (!process.connected) process.exit(0);
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

        const platform = process.platform;
        const isDev = !process.resourcesPath || !fs.existsSync(path.join(process.resourcesPath, 'published-components'));

        // Use process.cwd() in dev to anchor directly to the project root, bypassing the Vite build folder maze entirely
        const baseDir = isDev
            ? path.join(process.cwd(), 'c#', 'published-components', 'compiler')
            : path.join(process.resourcesPath, 'published-components', 'compiler');

        let binaryPath;
        if (platform === 'win32') {
            binaryPath = path.join(baseDir, 'windows', 'Compiler.exe'); 
        } else if (platform === 'linux') {
            binaryPath = path.join(baseDir, 'linux', 'Compiler');
        } else {
            throw new Error(`Unsupported OS platform: ${platform}`);
        }

        if (platform === 'linux') {
            try {
                fs.chmodSync(binaryPath, 0o755);
                logToMain('info', `Successfully assigned execution rights (0755) to Linux binary.`, SOURCE);
            } catch (permissionError) {
                logToMain('warn', `Failed to run chmodSync on binary file: ${permissionError.message}`, SOURCE);
            }
        }

        const args = [
            `--output=${config.output_dir}`,
            `--debug=${config.log_dir}`
        ];

        logToMain('info', `Spawning persistent compiler process: ${binaryPath}`, SOURCE);
        compilerProcess = spawn(binaryPath, args);

        compilerProcess.stdout.on('data', (data) => {
            logToMain('info', `[Compiler Output]: ${data.toString().trim()}`, 'Compiler-Stdout');
        });

        compilerProcess.stderr.on('data', (data) => {
            logToMain('error', `[Compiler Error]: ${data.toString().trim()}`, 'Compiler-Stderr');
        });

        compilerProcess.on('close', (code) => {
            logToMain('error', `Compiler process exited with code ${code}`, SOURCE);
        });

        compilerProcess.on('error', (err) => {
            logToMain('error', `Compiler process error: ${err.message}`, SOURCE);
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
    const SOURCE = 'Watcher-Compile';
    const ext = path.extname(filePath).toLowerCase();

    // Whitelist check
    if (!ALLOWED_EXTENSIONS.has(ext)) return;

    try {
                if (compilerProcess && compilerProcess.stdin && compilerProcess.stdin.writable) {
            const absolutePath = path.resolve(filePath);
            const safeJsonPath = JSON.stringify(absolutePath);
            
            compilerProcess.stdin.write(`${safeJsonPath}\n`); 
            logToMain('info', `Sent absolute path to persistent compiler: ${absolutePath}`, SOURCE);
        } else {
            logToMain('error', `Compiler process is not running or stdin is unavailable.`, SOURCE);
        }
    } catch (error) {
        logToMain('error', `Compile Error: ${error.message}`, SOURCE);
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
            walkAndCompile(fullPath); // Go deeper into subfolders
        } else {
            triggerCompilation(fullPath); // Send the actual file!
        }
    }
}
function stopCompilerProcess(source = 'Watcher-Lifecycle') {
    if (!compilerProcess) return;

    try {
        compilerProcess.kill('SIGTERM');
    } catch (e) {
        logToMain('error', `Watcher process failed to SIGTERM compiler process (${e.message}), sending SIGKILL command.`, source);
        try { 
            compilerProcess.kill('SIGKILL'); 
        } catch (err) {
            logToMain('error', `Failed to SIGKILL compiler process (exiting anyway): ${err.message}`, source);
        }
    }
}
// --- IPC Directive Router for Main Process Directives ---
process.on('message', (packet) => {
    if (!packet || typeof packet !== 'object') return;

    switch (packet.action) {
        case 'manual-recompile-all':
            logToMain('info', 'Received master workspace-wide rebuild instruction. Initiating complete compiler pass...', 'Watcher-IPC');
            
            if (config && config.input_dir) {
                // Walk the directory and push every single valid file into the C# stdin stream!
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

// --- Dynamic POSIX OS Termination Interceptors ---
process.on('SIGTERM', () => {
    logToMain('warn', 'Received SIGTERM from unknown source. Watcher terminating compiler process...', 'Watcher-Lifecycle');
    stopCompilerProcess();
    process.exit(0);
});

startWatcher();
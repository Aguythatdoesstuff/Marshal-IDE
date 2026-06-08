// watcher_workspace.js (Child Process)
// watcher_workspace.js (Child Process)
import fs from 'fs-extra';
import * as path from 'path';
import chokidar from 'chokidar';
import { fileURLToPath, pathToFileURL } from 'url';
import { spawn } from 'child_process';
import { handleDeletion, handleRename } from './deletion_handler.js'; 
import { SyncEngine } from './sync_engine.js'; 

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

let config; 
let compilerProcess = null; 
let outputBaseDir; 
let trackedGfxFiles = new Set();

// Allowed extensions extracted from previous runtime compilers configuration
const ALLOWED_EXTENSIONS = new Set(['.event', '.decision', '.scriptedgui', '.script', '.idea', '.focus', '.dds']);
// --- Process Safety ---
const checkParentAndExit = () => {
    if (!process.connected) process.exit(0);
};
process.on('disconnect', () => checkParentAndExit());
setInterval(checkParentAndExit, 2000);

export function logToMain(type, message, source) {
    const normalizedType = type.toLowerCase();
    const finalSource = source || 'Watcher-Process';
    if (process.send) {
        process.send({ type: normalizedType, message, source });
    } else {
        const consoleMethod = console[normalizedType] || console.log;
        consoleMethod(`[${source.toUpperCase()}] - ${message}`);
    }
}

// --- GFX Helpers ---
function cleanEmptyGfxDefinition() {
    const GFX_DEF_PATH = "interface/marshalIDE_definitions.gfx"; 
    const finalPath = path.join(config.output_dir, GFX_DEF_PATH);
    if (fs.existsSync(finalPath)) {
        fs.writeFileSync(finalPath, "spriteTypes = {\n}", 'utf8');
        logToMain('info', `🧹 Cleared GFX definitions.`, 'Watcher-Clean');
    }
}

function ensureDirectoryExistence(dirPath) {
    if (!fs.existsSync(dirPath)) fs.mkdirSync(dirPath, { recursive: true });
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
            logToMain('warn', `Compiler process exited with code ${code}`, SOURCE);
        });

        compilerProcess.on('error', (err) => {
            logToMain('error', `Compiler process error: ${err.message}`, SOURCE);
        });

        // Safe auto-cleanup when the parent script exits
        process.on('exit', () => {
            if (compilerProcess) compilerProcess.kill();
        });

    } catch (error) {
        logToMain('error', `Setup failed: ${error.message}`, SOURCE);
        process.exit(1); 
    }
}

function writeFileWithBomLogic(filePath, content) {
    const isBOMNeeded = filePath.endsWith('.yml');
    let contentToWrite = content;
    if (isBOMNeeded) {
        contentToWrite = contentToWrite.replace(/^\ufeff+/, '').trimStart().replace(/\u00A0/g, ' ');
        contentToWrite = '\ufeff' + contentToWrite;
    }
    fs.writeFileSync(filePath, contentToWrite, 'utf8');
}

function triggerCompilation(filePath) {
    const SOURCE = 'Watcher-Compile';
    const ext = path.extname(filePath).toLowerCase();

    // Whitelist and accept only matching valid extension paths 
    const allowedExts = ['.event', '.decision', '.scriptedgui', '.script', '.idea', '.focus', '.dds'];
    if (!allowedExts.includes(ext)) return;

    const normalizedFilePath = filePath.split(path.sep).join(path.posix.sep);
    if (ext === '.dds') trackedGfxFiles.add(normalizedFilePath);
    
    try {
        if (compilerProcess && compilerProcess.stdin && compilerProcess.stdin.writable) {
            const absolutePath = path.resolve(filePath);
            if (compilerProcess && compilerProcess.stdin && compilerProcess.stdin.writable) {
                const safeJsonPath = JSON.stringify(filePath);
                
                compilerProcess.stdin.write(`${safeJsonPath}\n`); 
            }
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

    // IPC listener to handle the frontend recompile command
    process.on('message', async (packet) => {
        if (packet && packet.action === 'recompile-all') {
            logToMain('info', 'Recompile all requested by front-end. Wiping manifest...', SOURCE);
            try {
                if (await fs.pathExists(syncEngine.manifestPath)) {
                    await fs.remove(syncEngine.manifestPath);
                }
                const reindexStats = await syncEngine.performInitialSync();
                if (reindexStats && Array.isArray(reindexStats.changedFiles)) {
                    logToMain('info', `Forcing full recompilation of all ${reindexStats.changedFiles.length} files...`, SOURCE);
                    for (const filePath of reindexStats.changedFiles) {
                        triggerCompilation(filePath);
                    }
                }
            } catch (err) {
                logToMain('error', `Failed to execute recompile-all: ${err.message}`, SOURCE);
            }
        }
    });

    const watcher = chokidar.watch(config.input_dir, {
        persistent: true,
        ignoreInitial: true, // Prevents chokidar from emitting 'add' events for existing files during discovery
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
                const oldNorm = lastUnlinkedPath.split(path.sep).join(path.posix.sep);
                trackedGfxFiles.delete(oldNorm);
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
                    const norm = filePath.split(path.sep).join(path.posix.sep);
                    if (trackedGfxFiles.has(norm)) {
                        trackedGfxFiles.delete(norm);
                        if (trackedGfxFiles.size > 0) {
                            triggerCompilation(Array.from(trackedGfxFiles)[0]);
                        } else {
                            cleanEmptyGfxDefinition();
                        }
                    }
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

// --- IPC Directive Router for Main Process Directives ---
process.on('message', (packet) => {
    if (!packet || typeof packet !== 'object') return;

    switch (packet.action) {
        case 'manual-compile':
            if (packet.filePath) {
                logToMain('info', `Received explicit manual compile demand for single-file context: ${path.basename(packet.filePath)}`, 'Watcher-IPC');
                triggerCompilation(packet.filePath);
            }
            break;

        case 'manual-recompile-all':
            logToMain('info', 'Received master workspace-wide rebuild instruction. Initiating complete compiler pass...', 'Watcher-IPC');
            
            if (config && config.input_dir) {
                // Walk the directory and push every single valid file into the C# stdin stream!
                walkAndCompile(config.input_dir); 
            }
            break;
            
        case 'shutdown':
            logToMain('warn', 'Watcher process received lifecycle shutdown signal from parent app. Terminating persistent compilation sub-server...', 'Watcher-IPC');
            if (compilerProcess) {
                try {
                    compilerProcess.kill('SIGTERM');
                } catch (e) {
                    try { compilerProcess.kill('SIGKILL'); } catch(err) {}
                }
            }
            process.exit(0);
            break;

        default:
            break;
    }
});

// --- Dynamic POSIX OS Termination Interceptors ---
process.on('SIGTERM', () => {
    logToMain('warn', 'Watcher Terminating compiler process...', 'Watcher-Lifecycle');
    if (compilerProcess) {
        try {
            compilerProcess.kill('SIGTERM');
        } catch(e) {}
    }
    process.exit(0);
});

startWatcher();

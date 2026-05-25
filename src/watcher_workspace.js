// watcher_workspace.js (Child Process)
import fs from 'fs-extra';
import * as path from 'path';
import chokidar from 'chokidar';
import { fileURLToPath, pathToFileURL } from 'url';
import { handleDeletion, handleRename } from './deletion_handler.js'; 
import { SyncEngine } from './sync_engine.js'; 

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

let config; 
let compilerMap = {}; 
let outputBaseDir; 
let trackedGfxFiles = new Set();

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

        for (const key in config.compilers) {
            const compilerInfo = config.compilers[key];
            const modulePath = path.resolve(__dirname, compilerInfo.processor); 
            const moduleUrl = pathToFileURL(modulePath).href;
            const compilerModule = await import(moduleUrl);
            if (!compilerMap[compilerInfo.ext]) compilerMap[compilerInfo.ext] = [];
            compilerMap[compilerInfo.ext].push({ key, module: compilerModule, config: compilerInfo });
        }
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
    const normalizedFilePath = filePath.split(path.sep).join(path.posix.sep);
    const ext = path.extname(normalizedFilePath).toLowerCase();

    // Registry Update
    if (ext === '.dds') trackedGfxFiles.add(normalizedFilePath);

    const potentialCompilers = compilerMap[ext];
    if (!potentialCompilers) return;

    let compilerToUse = potentialCompilers[0]; 
    if (potentialCompilers.length > 1) {
        const bestMatch = potentialCompilers.find(c => normalizedFilePath.toLowerCase().includes(c.key.toLowerCase()));
        if (bestMatch) compilerToUse = bestMatch;
    }
    
    try {
        const isBinary = ext === '.dds';
        // CRITICAL: Don't read binary as utf8
        const content = fs.readFileSync(filePath, isBinary ? null : 'utf8');
        if (!isBinary && content.trim().length === 0) return;

        const result = compilerToUse.module.compile(
            content, 
            compilerToUse.config, 
            normalizedFilePath, 
            Array.from(trackedGfxFiles)
        );

        if (result.success) {
            if (result.outputs && Array.isArray(result.outputs)) {
                for (const out of result.outputs) {
                    const finalPath = path.join(config.output_dir, out.path);
                    if (out.action === 'delete') {
                        fs.removeSync(finalPath);
                    } else {
                        ensureDirectoryExistence(path.dirname(finalPath)); 
                        if (isBinary || !out.path.endsWith('.yml')) {
                            fs.writeFileSync(finalPath, out.content);
                        } else {
                            writeFileWithBomLogic(finalPath, out.content);
                        }
                    }
                }
            } else {
            	// legacy fallback should now be uneccessery as all compilers should be now on the newer way however
            	// we will let this in here fornow just incase
                if (result.hoi4OutputPath && result.hoi4Code) {
                    const outPath = path.join(config.output_dir, result.hoi4OutputPath);
                    ensureDirectoryExistence(path.dirname(outPath));
                    writeFileWithBomLogic(outPath, result.hoi4Code);
                }
            }
            logToMain('info', `✅ Compiled: ${path.basename(normalizedFilePath)}`, SOURCE);
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

startWatcher();

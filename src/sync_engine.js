import fs from 'fs-extra';
import path from 'path';
import crypto from 'crypto';
import { logToMain } from './watcher_workspace.js';

const SOURCE = 'Sync-Engine';

export class SyncEngine {
    constructor(projectRoot, inputDir, handleDeletionCallback) {
        if (!projectRoot) throw new Error("SyncEngine Error: projectRoot is undefined.");
        
        this.manifestPath = path.join(projectRoot, 'metadata', 'manifest.json');
        this.inputDir = inputDir;
        this.handleDeletion = handleDeletionCallback;
        this.manifest = { files: [] };
        
        // Lock and batching controls to prevent concurrent write race conditions
        this.isSaving = false;
        this.saveRequested = false;
        this.pendingResolves = [];
    }

    /**
     * Fast-Hash: Reads first/last 8KB of a file + size to generate a quick MD5 string.
     */
    async getFastHash(filePath, size) {
        const chunkSize = 8192;
        const fd = await fs.open(filePath, 'r').catch(() => null);
        if (!fd) return null;
        try {
            const hash = crypto.createHash('md5');
            hash.update(size.toString());
            
            if (size <= chunkSize * 2) {
                const buf = await fs.readFile(filePath);
                hash.update(buf);
            } else {
                const head = Buffer.alloc(chunkSize);
                await fs.read(fd, head, 0, chunkSize, 0);
                hash.update(head);
                
                const tail = Buffer.alloc(chunkSize);
                await fs.read(fd, tail, 0, chunkSize, size - chunkSize);
                hash.update(tail);
            }
            return hash.digest('hex');
        } finally {
            await fs.close(fd);
        }
    }

    /**
     * Startup Sync: Checks disk against manifest and self-heals
     * @returns {Object} Statistics about the sync process
     */
    async performInitialSync() {
        logToMain('info', `Initializing sync check for: ${path.basename(this.inputDir)}`, SOURCE);
        
        await fs.ensureDir(path.dirname(this.manifestPath));

        // If no manifest exists, do a full scan and initialize
        if (!(await fs.pathExists(this.manifestPath))) {
            logToMain('warn', 'No manifest found. Performing first-time scan...', SOURCE);
            const diskFiles = await this.scanDir(this.inputDir);
            
            // Compute baseline hashes for new setup
            for (const [relPath, stat] of Object.entries(diskFiles)) {
                diskFiles[relPath].hash = await this.getFastHash(path.join(this.inputDir, relPath), stat.size);
            }

            this.manifest = { files: diskFiles };
            await this.save();
            const total = Object.keys(diskFiles).length;
            return { total, new: total, deleted: 0, changedFiles: Object.keys(diskFiles).map(p => path.join(this.inputDir, p)), isNew: true };
        }

        // 1. Load the "JSON Truth" from last session
        let oldManifest = { files: {} };
        try {
            const loaded = await fs.readJson(this.manifestPath);
            
            if (loaded && Array.isArray(loaded.files)) {
                // Backward compatibility layout migration
                loaded.files.forEach(f => oldManifest.files[f] = { size: 0, mtimeMs: 0, hash: null });
                logToMain('info', `Migrating legacy manifest format array layout.`, SOURCE);
            } else if (loaded && loaded.files && typeof loaded.files === 'object') {
                oldManifest = loaded;
                logToMain('info', `Manifest loaded. Previous session had ${Object.keys(oldManifest.files).length} files.`, SOURCE);
            } else {
                throw new Error("Manifest structure missing standard 'files' field mapping.");
            }
        } catch (error) {
            logToMain('error', `CRITICAL: Manifest JSON is corrupted or unreadable. Error: ${error.message}`, SOURCE);
            if (process.send) {
                process.send({
                    action: 'show-warning-dialog',
                    title: 'Manifest Corruption Detected',
                    message: 'The output may contain ghost files due to the manifest being broken. We cannot validate if there were any file deletions in the input after the last time the project was open.'
                });
            }
        }
        
        // 2. Perform a single directory scan
        const diskFiles = await this.scanDir(this.inputDir);
        
        let changedFiles = [];
        let deletedCount = 0;
        let newCount = 0;

        // 3. Find Ghost Deletions (In manifest but gone from disk)
        for (const relPath of Object.keys(oldManifest.files)) {
            if (!diskFiles[relPath]) {
                logToMain('warn', `Ghost deletion detected: ${relPath}`, SOURCE);
                await this.handleDeletion(path.join(this.inputDir, relPath));
                deletedCount++;
            }
        }

        // 4. Find Additions and Modifications
        for (const [relPath, stat] of Object.entries(diskFiles)) {
            const fullPath = path.join(this.inputDir, relPath);
            const oldData = oldManifest.files[relPath];

            let isModified = false;
            let currentHash = null;

            if (!oldData) {
                isModified = true;
                newCount++;
                currentHash = await this.getFastHash(fullPath, stat.size);
                logToMain('info', `Offline addition detected: ${relPath}`, SOURCE);
            } else if (oldData.size !== stat.size) {
                isModified = true;
                currentHash = await this.getFastHash(fullPath, stat.size);
                logToMain('info', `Offline size modification detected: ${relPath}`, SOURCE);
            } else if (oldData.mtimeMs !== stat.mtimeMs) {
                // Trigger Option 3: mtime changed but size matches (like a git pull). Evaluate fast-hash content signature.
                currentHash = await this.getFastHash(fullPath, stat.size);
                if (!oldData.hash || currentHash !== oldData.hash) {
                    isModified = true;
                    logToMain('info', `Offline content modification detected: ${relPath}`, SOURCE);
                }
            } else {
                currentHash = oldData.hash;
            }

            diskFiles[relPath].hash = currentHash;

            if (isModified) {
                changedFiles.push(fullPath);
            }
        }

        this.manifest.files = diskFiles;
        await this.save();

        return {
            total: Object.keys(diskFiles).length,
            new: newCount,
            deleted: deletedCount,
            changedFiles,
            isNew: false
        };
    }

    async addFile(relPath) {
        const fullPath = path.join(this.inputDir, relPath);
        try {
            const stat = await fs.stat(fullPath);
            const hash = await this.getFastHash(fullPath, stat.size);
            this.manifest.files[relPath] = { size: stat.size, mtimeMs: stat.mtimeMs, hash };
            await this.save();
            logToMain('info', `Manifest updated: added/changed ${relPath}`, SOURCE);
        } catch (e) {
            logToMain('error', `Failed to stat added file ${relPath}: ${e.message}`, SOURCE);
        }
    }

    async removeFile(relPath) {
        if (this.manifest.files[relPath]) {
            delete this.manifest.files[relPath];
            await this.save();
            logToMain('info', `Manifest updated: removed ${relPath}`, SOURCE);
        }
    }

    async save() {
        return new Promise((resolve) => {
            this.pendingResolves.push(resolve);
            this.triggerSave();
        });
    }

    async triggerSave() {
        if (this.isSaving) {
            this.saveRequested = true;
            return;
        }

        this.isSaving = true;
        this.saveRequested = false;

        // Yield to the event loop to allow synchronous operations in the same tick to batch together
        await new Promise(resolve => setImmediate(resolve));

        const resolves = this.pendingResolves;
        this.pendingResolves = [];

        try {
            const tempPath = `${this.manifestPath}.tmp`;
            await fs.ensureDir(path.dirname(this.manifestPath));
            await fs.writeJson(tempPath, this.manifest, { spaces: 4 });
            await fs.move(tempPath, this.manifestPath, { overwrite: true });
        } catch (error) {
            logToMain('error', `Failed to save manifest atomically: ${error.message}`, SOURCE);
        } finally {
            this.isSaving = false;
            
            // Resolve all promises waiting on this batch
            for (const resolve of resolves) resolve();

            // If another save request arrived while writing, process the next batch immediately
            if (this.saveRequested) {
                this.triggerSave();
            }
        }
    }

    async scanDir(dir, base = dir) {
        let results = {};
        try {
            const list = await fs.readdir(dir);
            for (const file of list) {
                const fullPath = path.join(dir, file);
                const stat = await fs.stat(fullPath);
                if (stat && stat.isDirectory()) {
                    Object.assign(results, await this.scanDir(fullPath, base));
                } else {
                    if (!file.startsWith('.')) {
                        const relPath = path.relative(base, fullPath);
                        results[relPath] = { size: stat.size, mtimeMs: stat.mtimeMs };
                    }
                }
            }
        } catch (error) {
            logToMain('error', `Directory scan failed at ${dir}: ${error.message}`, SOURCE);
        }
        return results;
    }
}
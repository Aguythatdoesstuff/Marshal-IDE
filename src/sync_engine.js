import fs from 'fs-extra';
import path from 'path';
import { logToMain } from './watcher_workspace.js';

const SOURCE = 'Sync-Engine';

export class SyncEngine {
    constructor(projectRoot, inputDir, handleDeletionCallback) {
        if (!projectRoot) throw new Error("SyncEngine Error: projectRoot is undefined.");
        
        this.manifestPath = path.join(projectRoot, 'metadata', 'manifest.json');
        this.inputDir = inputDir;
        this.handleDeletion = handleDeletionCallback;
        this.manifest = { files: [] };
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
            const files = await this.scanDir(this.inputDir);
            this.manifest = { files };
            await this.save();
            return { total: files.length, new: files.length, deleted: 0, isNew: true };
        }

        // 1. Load the "JSON Truth" from last session
        const oldManifest = await fs.readJson(this.manifestPath);
        logToMain('info', `Manifest loaded. Previous session had ${oldManifest.files.length} files.`, SOURCE);
        
        // 2. Perform a single directory scan
        const diskFiles = await this.scanDir(this.inputDir);
        
        const oldSet = new Set(oldManifest.files);
        const diskSet = new Set(diskFiles);

        let deletedCount = 0;
        let newCount = 0;

        // 3. Find Ghost Deletions (In manifest but gone from disk)
        for (const relPath of oldManifest.files) {
            if (!diskSet.has(relPath)) {
                logToMain('warn', `Ghost deletion detected: ${relPath}`, SOURCE);
                await this.handleDeletion(path.join(this.inputDir, relPath));
                deletedCount++;
            }
        }

        // 4. Find New Files (On disk but not in manifest)
        for (const relPath of diskFiles) {
            if (!oldSet.has(relPath)) {
                logToMain('info', `Offline addition detected: ${relPath}`, SOURCE);
                newCount++;
            }
        }

        this.manifest.files = diskFiles;
        await this.save();

        return {
            total: diskFiles.length,
            new: newCount,
            deleted: deletedCount,
            isNew: false
        };
    }

    async addFile(relPath) {
        if (!this.manifest.files.includes(relPath)) {
            this.manifest.files.push(relPath);
            await this.save();
            logToMain('debug', `Manifest updated: added ${relPath}`, SOURCE);
        }
    }

    async removeFile(relPath) {
        const originalCount = this.manifest.files.length;
        this.manifest.files = this.manifest.files.filter(f => f !== relPath);
        
        if (this.manifest.files.length !== originalCount) {
            await this.save();
            logToMain('debug', `Manifest updated: removed ${relPath}`, SOURCE);
        }
    }

    async save() {
        try {
            await fs.writeJson(this.manifestPath, this.manifest, { spaces: 4 });
        } catch (error) {
            logToMain('error', `Failed to save manifest: ${error.message}`, SOURCE);
        }
    }

    async scanDir(dir, base = dir) {
        let results = [];
        try {
            const list = await fs.readdir(dir);
            for (const file of list) {
                const fullPath = path.join(dir, file);
                const stat = await fs.stat(fullPath);
                if (stat && stat.isDirectory()) {
                    results = results.concat(await this.scanDir(fullPath, base));
                } else {
                    if (!file.startsWith('.')) {
                        results.push(path.relative(base, fullPath));
                    }
                }
            }
        } catch (error) {
            logToMain('error', `Directory scan failed at ${dir}: ${error.message}`, SOURCE);
        }
        return results;
    }
}

import fs from 'fs-extra';
import path from 'path';
import { app } from 'electron';

const USER_DATA_PATH = app.getPath('userData');
const PRIMARY_CACHE_DIR = path.join(USER_DATA_PATH, 'metadata', 'node_cache');

/**
 * Normalizes and validates the file path to prevent directory traversal.
 */
function getRelativePath(filePath) {
    if (typeof filePath !== 'string') {
        throw new TypeError(`The "filePath" argument must be of type string. Received an instance of ${typeof filePath}`);
    }
    return path.normalize(filePath).replace(/^(\.\.[/\\])+/, '');
}

/**
 * 3-Way Cache Retrieval System:
 * 1. Primary Electron User Data Cache (<userData>/metadata/node_cache/<relPath>.json)
 * 2. Workspace Backup Cache (<projectRoot>/metadata/cache/<relPath>.json)
 * 3. AST Fallback (Generates AST output into workspace cache as last resort)
 */
export async function getEditorCache(filePath, projectRoot, astFallbackFn) {
    try {
        // Handle object payload if passed directly
        if (typeof filePath === 'object' && filePath !== null) {
            projectRoot = filePath.projectRoot || projectRoot;
            astFallbackFn = filePath.astFallbackFn || astFallbackFn;
            filePath = filePath.filePath;
        }

        const relPath = getRelativePath(filePath);
        const primaryCachePath = path.join(PRIMARY_CACHE_DIR, `${relPath}.json`);

        // 1. Check Primary Electron Cache
        if (await fs.pathExists(primaryCachePath)) {
            const cache = await fs.readJson(primaryCachePath);
            return { success: true, cache };
        }

        // 2. Check Workspace Backup Cache
        if (projectRoot) {
            const workspaceCachePath = path.join(projectRoot, 'metadata', 'cache', `${relPath}.json`);
            if (await fs.pathExists(workspaceCachePath)) {
                const cache = await fs.readJson(workspaceCachePath);
                // Sync back to primary cache for future speed
                try {
                    await fs.ensureDir(path.dirname(primaryCachePath));
                    await fs.writeJson(primaryCachePath, cache, { spaces: 2 });
                } catch (syncErr) {
                    console.warn('Failed to sync workspace cache to primary cache:', syncErr);
                }
                return { success: true, cache };
            }
        }

        // 3. AST Fallback (Very Last Backup)
        if (projectRoot && typeof astFallbackFn === 'function') {
            const workspaceCachePath = path.join(projectRoot, 'metadata', 'cache', `${relPath}.json`);
            try {
                const fallbackSuccess = await astFallbackFn({
                    filePath,
                    workspaceCachePath,
                    relPath
                });

                if (fallbackSuccess && await fs.pathExists(workspaceCachePath)) {
                    const cache = await fs.readJson(workspaceCachePath);
                    // Also save to primary cache
                    try {
                        await fs.ensureDir(path.dirname(primaryCachePath));
                        await fs.writeJson(primaryCachePath, cache, { spaces: 2 });
                    } catch (syncErr) {
                        console.warn('Failed to sync fallback AST cache to primary cache:', syncErr);
                    }
                    return { success: true, cache };
                }
            } catch (fallbackErr) {
                console.error('AST fallback generation failed:', fallbackErr);
            }
        }

        // Return null if all 3 tiers miss
        return { success: true, cache: null };
    } catch (err) {
        console.error('Failed to get editor cache:', err);
        return { success: false, message: err.message };
    }
}

/**
 * Saves editor cache to both the primary Electron cache and the workspace backup cache.
 */
export async function saveEditorCache(filePath, cacheData, projectRoot) {
    try {
        // Handle object payload if passed directly (e.g. { filePath, cacheData, projectRoot })
        if (typeof filePath === 'object' && filePath !== null) {
            cacheData = filePath.cacheData || cacheData;
            projectRoot = filePath.projectRoot || projectRoot;
            filePath = filePath.filePath;
        }

        const relPath = getRelativePath(filePath);
        const primaryCachePath = path.join(PRIMARY_CACHE_DIR, `${relPath}.json`);

        // Save to Primary Electron Cache
        await fs.ensureDir(path.dirname(primaryCachePath));
        await fs.writeJson(primaryCachePath, cacheData, { spaces: 2 });

        // Save to Workspace Backup Cache (for workspace portability / exports)
        if (projectRoot) {
            const workspaceCachePath = path.join(projectRoot, 'metadata', 'cache', `${relPath}.json`);
            await fs.ensureDir(path.dirname(workspaceCachePath));
            await fs.writeJson(workspaceCachePath, cacheData, { spaces: 2 });
        }

        return { success: true };
    } catch (err) {
        console.error('Failed to save editor cache:', err);
        return { success: false, message: err.message };
    }
}
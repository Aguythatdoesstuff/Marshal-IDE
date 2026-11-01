// deletion_handler.js
import fs from 'fs-extra';
import path from 'path';
import { logToMain } from './watcher_workspace.js';

const SOURCE = 'Deletion-Handler';

/**
 * Handles the deletion of a file by reading its cache config, purging all output files,
 * and removing its specific folder from both fileOutCache and the gfx cache.
 * @param {string} filePath Path of the deleted input file.
 * @param {Object} config Workspace configuration object.
 */
export async function handleDeletion(filePath, config) {
    if (!config || !config.metadata_dir) {
        logToMain('warn', `Skipped deletion handling: Missing metadata_dir in config.`, SOURCE);
        return;
    }

    const fileOutCacheDir = path.join(config.metadata_dir, 'fileOutCache');
    const gfxCacheDir = path.join(config.metadata_dir, 'gfx');
    
    const fileName = path.basename(filePath);
    const parsed = path.parse(fileName);
    
    // 1. fileOutCache: Uniform naming for ALL files (e.g. image.dds -> imagedds)
    const fileOutCacheFolderName = parsed.name + parsed.ext.replace('.', '');
    
    // 2. gfx cache: Strips extension entirely for all files (e.g. image.dds -> image)
    const gfxCacheFolderName = parsed.name;
    
    const fileCacheDir = path.join(fileOutCacheDir, fileOutCacheFolderName);
    const gfxFileCacheDir = path.join(gfxCacheDir, gfxCacheFolderName);
    
    const configJsonPath = path.join(fileCacheDir, 'config.json');

    logToMain('info', `Processing deletion for input file: "${filePath}"`, SOURCE);
    logToMain('info', `-> fileOutCache folder name: "${fileOutCacheFolderName}"`, SOURCE);
    logToMain('info', `-> gfx cache folder name: "${gfxCacheFolderName}"`, SOURCE);
    logToMain('info', `-> Expected cache config path: "${configJsonPath}"`, SOURCE);

    // 1. Read config.json safely with its own localized error handling
    let cacheConfig = null;
    try {
        if (await fs.pathExists(configJsonPath)) {
            logToMain('info', `Found config.json. Reading output references...`, SOURCE);
            cacheConfig = await fs.readJson(configJsonPath);
        } else {
            logToMain('info', `No config.json found at expected path: "${configJsonPath}"`, SOURCE);
        }
    } catch (error) {
        logToMain('error', `Failed to read config.json at "${configJsonPath}": ${error.message}`, SOURCE);
    }

    // 2. Purge referenced outputs - loop is cleanly isolated with individual try/catch per item
    if (cacheConfig && Array.isArray(cacheConfig.outputs)) {
        logToMain('info', `Purging ${cacheConfig.outputs.length} referenced output file(s)...`, SOURCE);
        for (const outputPath of cacheConfig.outputs) {
            try {
                if (await fs.pathExists(outputPath)) {
                    logToMain('info', `  - Removing output file: "${outputPath}"`, SOURCE);
                    await fs.remove(outputPath);
                } else {
                    logToMain('info', `  - Output file already missing on disk: "${outputPath}"`, SOURCE);
                }
            } catch (err) {
                logToMain('error', `Failed to purge output file "${outputPath}": ${err.message}`, SOURCE);
            }
        }
    } else if (cacheConfig) {
        logToMain('info', `No valid 'outputs' array found in config.json.`, SOURCE);
    }

    // 3. Remove the file's cache folder inside fileOutCache
    try {
        if (await fs.pathExists(fileCacheDir)) {
            logToMain('info', `Deleting fileOutCache directory: "${fileCacheDir}"`, SOURCE);
            await fs.remove(fileCacheDir);
        } else {
            logToMain('info', `fileOutCache folder already doesn't exist: "${fileCacheDir}"`, SOURCE);
        }
    } catch (error) {
        logToMain('error', `Failed to delete fileOutCache directory "${fileCacheDir}": ${error.message}`, SOURCE);
    }

    // 4. Remove the file's folder inside the gfx cache (extension-free parsed.name)
    try {
        if (await fs.pathExists(gfxFileCacheDir)) {
            logToMain('info', `Deleting gfx cache directory: "${gfxFileCacheDir}"`, SOURCE);
            await fs.remove(gfxFileCacheDir);
        } else {
            logToMain('info', `gfx cache folder already doesn't exist: "${gfxFileCacheDir}"`, SOURCE);
        }
    } catch (error) {
        logToMain('error', `Failed to delete gfx cache directory "${gfxFileCacheDir}": ${error.message}`, SOURCE);
    }

    logToMain('info', `Successfully completed cleanup sequence for deleted file: "${filePath}"`, SOURCE);
}
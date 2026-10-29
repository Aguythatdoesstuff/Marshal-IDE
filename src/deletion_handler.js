// deletion_handler.js
import fs from 'fs-extra';
import path from 'path';
import { logToMain } from './watcher_workspace.js';

const SOURCE = 'Deletion-Handler';

/**
 * Handles the deletion of a file by reading its cache config, purging all output files,
 * and then removing the file's specific cache folder.
 * @param {string} filePath Path of the deleted input file.
 * @param {Object} config Workspace configuration object.
 */
export async function handleDeletion(filePath, config) {
    if (!config || !config.metadata_dir) {
        logToMain('warn', `Skipped deletion handling: Missing metadata_dir in config.`, SOURCE);
        return;
    }

    const cache_dir = path.join(config.metadata_dir, 'fileOutCache');
    const fileName = path.basename(filePath);
    const parsed = path.parse(fileName);
    
    // Turn filename into cache folder name (e.g., china.event -> chinaevent)
    const cacheFolderName = parsed.name + parsed.ext.replace('.', '');
    const fileCacheDir = path.join(cache_dir, cacheFolderName);
    const configJsonPath = path.join(fileCacheDir, 'config.json');

    logToMain('info', `Processing deletion for input file: "${filePath}"`, SOURCE);
    logToMain('info', `-> Derived cache folder name: "${cacheFolderName}"`, SOURCE);
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

    // 3. Remove the file's cache folder itself with its own localized error handling
    try {
        if (await fs.pathExists(fileCacheDir)) {
            logToMain('info', `Deleting file cache directory: "${fileCacheDir}"`, SOURCE);
            await fs.remove(fileCacheDir);
        } else {
            logToMain('info', `Cache folder already doesn't exist: "${fileCacheDir}"`, SOURCE);
        }
    } catch (error) {
        logToMain('error', `Failed to delete cache directory "${fileCacheDir}": ${error.message}`, SOURCE);
    }

    logToMain('info', `Successfully completed cleanup sequence for deleted file: "${filePath}"`, SOURCE);
}
import { dialog, BrowserWindow } from 'electron';
import path from 'path';
import fs from 'fs-extra';

export function registerImageImporterHandlers(ipcMain, context) {
    const { getInputDir, getLogger, updateManifest, getMetadataDir, processImage } = context;

    /**
     * Helper to consistently extract the exact base name (without extension) from a string path,
     * filename, or payload object, strictly preserving spaces, hyphens, and original casing,
     * deliberately bypassing any upstream slugified/sanitized safeName properties.
     */
    function getSafeName(input) {
        if (!input) return '';
        if (typeof input === 'object') {
            const p = input.path || input.filePath || input.imagePath || input.filename || input.name;
            if (p) {
                const ext = path.extname(p);
                return path.basename(p, ext);
            }
            if (input.safeName) return input.safeName;
            return '';
        }
        if (typeof input === 'string') {
            const ext = path.extname(input);
            return path.basename(input, ext);
        }
        return '';
    }

    ipcMain.handle('prepare-img-import', async (event, args) => {
        const appLogger = getLogger();
        const INPUT_DIR = getInputDir();
        
        appLogger?.info(`[Main-Import-Prep] Handler called with args: ${JSON.stringify(args)}`, { source: 'Main-Import' });

        if (!INPUT_DIR) {
            appLogger?.error('[Main-Import-Prep] Failed: INPUT_DIR is not defined/loaded', { source: 'Main-Import' });
            return { success: false, message: "No project loaded." };
        }

        try {
            let filePaths = [];
            
            const directSource = args?.sourcePath || (typeof args === 'string' ? args : null) || (Array.isArray(args) ? args : null);

            if (directSource) {
                filePaths = Array.isArray(directSource) ? directSource : [directSource];
                appLogger?.info(`[Main-Import-Prep] Received direct source paths: ${JSON.stringify(filePaths)}`, { source: 'Main-Import' });
            } else {
                appLogger?.info('[Main-Import-Prep] No sourcePath provided. Triggering openDialog...', { source: 'Main-Import' });
                
                const result = await dialog.showOpenDialog({
                    title: 'Select Images to Import',
                    properties: ['openFile', 'multiSelections'], 
                    filters: [
                        { name: 'All Supported Images', extensions: ['dds', 'png', 'jpg', 'jpeg', 'bmp', 'tga', 'webp', 'tiff', 'tif', 'hdr'] },
                        { name: 'DirectDraw Surface', extensions: ['dds'] },
                        { name: 'Standard Raster Images', extensions: ['png', 'jpg', 'jpeg', 'webp', 'bmp', 'tga'] }
                    ]
                });

                if (result.canceled || result.filePaths.length === 0) {
                    appLogger?.info('[Main-Import-Prep] Dialog cancelled or no files selected.', { source: 'Main-Import' });
                    return { success: false, message: "Import cancelled by user." };
                }

                filePaths = result.filePaths;
                appLogger?.info(`[Main-Import-Prep] Dialog selected files: ${JSON.stringify(filePaths)}`, { source: 'Main-Import' });
            }

            appLogger?.info(`[Main-Import-Prep] Preparation successful. Returning ${filePaths.length} path(s).`, { source: 'Main-Import' });
            
            return { 
                success: true, 
                filePaths: filePaths 
            };

        } catch (error) {
            appLogger?.error(`[Main-Import-Prep] Error during preparation: ${error.stack || error.message}`, { source: 'Main-Import' });
            return { success: false, message: error.message };
        }
    });

    ipcMain.handle('update-img-cache', async (event, payload) => {
        const appLogger = getLogger();
        const metaDirPath = getMetadataDir();

        appLogger?.info(`[Main-Import-Cache] Handler called with payload length: ${Array.isArray(payload) ? payload.length : 'non-array'}`, { source: 'Main-Import' });

        if (!metaDirPath) {
            appLogger?.error('[Main-Import-Cache] Failed: metaDirPath is not defined/loaded', { source: 'Main-Import' });
            return { success: false, message: "No project loaded." };
        }

        try {
            const files = Array.isArray(payload) ? payload : [payload];

            for (const file of files) {
                const { path: importedFrom, imageType, ...extraFields } = file;
                const safeName = getSafeName(file);
                if (!safeName) continue;

                const configDir = path.join(metaDirPath, 'gfx', safeName);
                const configPath = path.join(configDir, 'config.json');

                await fs.ensureDir(configDir);

                let existingConfig = {};
                if (await fs.pathExists(configPath)) {
                    try {
                        existingConfig = await fs.readJson(configPath);
                    } catch (readErr) {
                        appLogger?.error(`[Main-Import-Cache] Failed to parse existing config at ${configPath}, overwriting: ${readErr.message}`, { source: 'Main-Import' });
                    }
                }

                delete extraFields.path;
                delete extraFields.safeName;

                const updatedConfig = {
                    ...existingConfig,
                    ...extraFields,
                    safeName,
                    imageType,
                    importedFrom,
                    updatedAt: new Date().toISOString()
                };

                const tempPath = `${configPath}.${Date.now()}.${Math.random().toString(36).substring(2, 8)}.tmp`;
                await fs.outputJson(tempPath, updatedConfig, { spaces: 2 });
                await fs.move(tempPath, configPath, { overwrite: true });

                appLogger?.info(`[Main-Import-Cache] Successfully updated cache config for ${safeName} at ${configPath}`, { source: 'Main-Import' });
            }

            return { success: true };
        } catch (error) {
            appLogger?.error(`[Main-Import-Cache] Error updating image cache: ${error.stack || error.message}`, { source: 'Main-Import' });
            return { success: false, message: error.message };
        }
    });
    
    ipcMain.handle('finalize-img-import', async (event, payload) => {
        const appLogger = getLogger();
        const metaDirPath = getMetadataDir();
        const inputDir = getInputDir();

        appLogger?.info(`[Main-Finalize-Import] called with payload length: ${Array.isArray(payload) ? payload.length : 'non-array'}`, { source: 'Main-Import' });

        if (!metaDirPath || !inputDir) {
            appLogger?.error('[Main-Finalize-Import] Failed: metaDirPath or inputDir is not defined/loaded', { source: 'Main-Import' });
            return { success: false, message: "No project loaded." };
        }

        try {
            const files = Array.isArray(payload) ? payload : [payload];
            const importedResults = [];

            for (const file of files) {
                const { path: filePath, imageType } = file;
                const safeName = getSafeName(file);

                if (!filePath || !safeName || !imageType) {
                    throw new Error(`Missing required import parameters: path=${filePath}, safeName=${safeName}, imageType=${imageType}`);
                }

                const mainOutputDir = path.join(inputDir, 'gfx');
                const cacheOutputDir = path.join(metaDirPath, 'gfx', safeName);

                appLogger?.info(`[Main-Finalize-Import] Processing image: ${filePath} as type: ${imageType}`, { source: 'Main-Import' });

                const result = await processImage({
                    filePath,
                    imageType,
                    mainOutputDir,
                    cacheOutputDir
                });

                if (!result || !result.success) {
                    throw new Error(result?.message || `Failed to process image: ${safeName}`);
                }

                importedResults.push({
                    safeName,
                    originalPath: filePath,
                    outputPath: result.outputPath || mainOutputDir
                });
            }

            appLogger?.info(`[Main-Finalize-Import] Successfully finalized ${importedResults.length} image(s).`, { source: 'Main-Import' });
            return { success: true, imported: importedResults };

        } catch (error) {
            appLogger?.error(`[Main-Finalize-Import] Error finalizing image import: ${error.stack || error.message}`, { source: 'Main-Import' });
            return { success: false, message: error.message };
        }
    });

    async function updateSingleImageConfig(file, metaDirPath, appLogger) {
        const safeName = getSafeName(file);
        
        if (!safeName) {
            throw new Error("Missing target path or safeName in payload item.");
        }

        const configDir = path.join(metaDirPath, 'gfx', safeName);
        const configPath = path.join(configDir, 'config.json');

        await fs.ensureDir(configDir);

        let existingConfig = {};
        const configExists = await fs.pathExists(configPath);

        if (!configExists) {
            appLogger?.error(`[Main-Img-Config] Error: config.json does not exist for ${safeName} at ${configPath}. Something may have gone wrong upstream. Proceeding to create/update.`, { source: 'Main-Config' });
        } else {
            try {
                existingConfig = await fs.readJson(configPath);
            } catch (readErr) {
                appLogger?.error(`[Main-Img-Config] Failed to parse existing config at ${configPath}, overwriting: ${readErr.message}`, { source: 'Main-Config' });
            }
        }

        const customFields = typeof file === 'object' ? { ...file } : {};
        delete customFields.path;
        delete customFields.filePath;
        delete customFields.imagePath;
        delete customFields.filename;
        delete customFields.safeName;

        const updatedConfig = {
            ...existingConfig,
            ...customFields,
            safeName,
            updatedAt: new Date().toISOString()
        };

        const tempPath = `${configPath}.${Date.now()}.${Math.random().toString(36).substring(2, 8)}.tmp`;
        await fs.outputJson(tempPath, updatedConfig, { spaces: 2 });
        await fs.move(tempPath, configPath, { overwrite: true });

        appLogger?.info(`[Main-Img-Config] Successfully updated config for ${safeName} at ${configPath}`, { source: 'Main-Config' });
        return { success: true, safeName };
    }

    ipcMain.handle('get-img-config', async (event, filePath) => {
        const appLogger = getLogger();
        const metaDirPath = getMetadataDir();
        if (!metaDirPath || !filePath) {
            return { success: false, config: { safeName: '', imageType: '' } };
        }

        const safeName = getSafeName(filePath);
        const gfxMetaDir = path.join(metaDirPath, 'gfx');

        if (safeName) {
            const directConfigPath = path.join(gfxMetaDir, safeName, 'config.json');
            try {
                if (await fs.pathExists(directConfigPath)) {
                    const cfg = await fs.readJson(directConfigPath);
                    return { success: true, config: cfg };
                }
            } catch (readErr) {
                appLogger?.warn(`[Main-Img-Config] Failed to read direct config at ${directConfigPath}: ${readErr.message}`);
            }
        }

        let gfxExists = false;
        try {
            gfxExists = await fs.pathExists(gfxMetaDir);
        } catch (err) {
            appLogger?.error(`[Main-Img-Config] Failed to check gfx metadata directory existence: ${err.message}`);
        }

        if (gfxExists) {
            let subDirs = [];
            try {
                subDirs = await fs.readdir(gfxMetaDir);
            } catch (err) {
                appLogger?.error(`[Main-Img-Config] Failed to read gfx metadata directory: ${err.message}`);
            }

            for (const subDir of subDirs) {
                const configPath = path.join(gfxMetaDir, subDir, 'config.json');
                
                let configExists = false;
                try {
                    configExists = await fs.pathExists(configPath);
                } catch (_) {}

                if (configExists) {
                    try {
                        const cfg = await fs.readJson(configPath);
                        if (
                            cfg.safeName === safeName ||
                            cfg.importedFrom === filePath || 
                            path.basename(cfg.importedFrom || '') === path.basename(filePath)
                        ) {
                            return { success: true, config: cfg };
                        }
                    } catch (readErr) {
                        appLogger?.warn(`[Main-Img-Config] Failed to read config at ${configPath}: ${readErr.message}`);
                    }
                }
            }
        }

        return { success: true, config: { safeName: safeName || path.basename(filePath, path.extname(filePath)), imageType: '' } };
    });

    ipcMain.handle('update-img-config', async (event, payload) => {
        const appLogger = getLogger();
        const metaDirPath = getMetadataDir();

        appLogger?.info(`[Main-Img-Config] Handler called with payload length: ${Array.isArray(payload) ? payload.length : 'non-array'}`, { source: 'Main-Config' });

        if (!metaDirPath) {
            appLogger?.error('[Main-Img-Config] Failed: metaDirPath is not defined/loaded', { source: 'Main-Config' });
            return { success: false, message: "No project loaded." };
        }

        const items = Array.isArray(payload) ? payload : [payload];
        const results = [];

        for (const file of items) {
            try {
                const result = await updateSingleImageConfig(file, metaDirPath, appLogger);
                results.push(result);
            } catch (error) {
                appLogger?.error(`[Main-Img-Config] Error updating individual item config: ${error.stack || error.message}`, { source: 'Main-Config' });
                results.push({ success: false, message: error.message });
            }
        }

        const hasFailures = results.some(r => !r.success);
        const allFailed = results.every(r => !r.success);

        if (allFailed) {
            return { success: false, message: "All image config updates failed.", results };
        }

        return { success: !hasFailures, results };
    });

    ipcMain.handle('get-img-preview', async (event, payload) => {
        const appLogger = getLogger();
        const metaDirPath = getMetadataDir();

        appLogger?.info(`[Main-Img-Config] Handler called with payload: ${JSON.stringify(payload)}`, { source: 'Main-Config' });

        if (!metaDirPath) {
            appLogger?.error('[Main-Img-Config] Failed: metaDirPath is not defined/loaded', { source: 'Main-Config' });
            return { success: false, message: "No project loaded." };
        }

        const rawItems = payload?.files || payload;
        const requestedSize = payload?.size || 'original';
        const items = Array.isArray(rawItems) ? rawItems : [rawItems];
        const results = [];

        for (const file of items) {
            let result;
            
            try {
                result = await resolveImagePreview(file, requestedSize, metaDirPath, appLogger);
            } catch (error) {
                appLogger?.error(`[Main-Img-Config] Error getting preview for item: ${error.stack || error.message}`, { source: 'Main-Config' });
                result = { success: false, message: error.message, file };
            }
            
            results.push(result);
        }

        const hasFailures = results.some(r => !r.success);
        const allFailed = results.every(r => !r.success);

        if (allFailed) {
            return { success: false, message: "All image preview requests failed.", results };
        }

        return { success: !hasFailures, results };
    });

    async function findFileRecursive(dir, targetName, imageType = 'fallback', appLogger = null) {
        if (!(await fs.pathExists(dir))) {
            appLogger?.info(`[Main-Img-Scan] Directory does not exist or is inaccessible: ${dir}`, { source: 'Main-Scan' });
            return null;
        }
        
        appLogger?.info(`[Main-Img-Scan] Scanning directory: "${dir}" looking for base name: "${targetName}" (Image Type: ${imageType})`, { source: 'Main-Scan' });
        
        let entries;
        try {
            entries = await fs.readdir(dir, { withFileTypes: true });
        } catch (err) {
            appLogger?.error(`[Main-Img-Scan] Failed to read directory entries at ${dir}: ${err.message}`, { source: 'Main-Scan' });
            return null;
        }
        
        const targetLower = targetName.toLowerCase();
        const expectedExt = imageType === 'flag' ? '.tga' : '.dds';

        let primaryMatch = null;
        let fallbackMatch = null;

        for (const entry of entries) {
            const fullPath = path.join(dir, entry.name);
            if (entry.isDirectory()) {
                appLogger?.info(`[Main-Img-Scan] Entering subdirectory: ${fullPath}`, { source: 'Main-Scan' });
                const found = await findFileRecursive(fullPath, targetName, imageType, appLogger);
                if (found) {
                    const parsedFound = path.parse(found);
                    appLogger?.info(`[Main-Img-Scan] Candidate found in subdirectory: ${found} (Extension: ${parsedFound.ext}, Expected: ${expectedExt})`, { source: 'Main-Scan' });
                    if (parsedFound.ext.toLowerCase() === expectedExt) {
                        appLogger?.info(`[Main-Img-Scan] Primary match confirmed via recursion: ${found}`, { source: 'Main-Scan' });
                        return found;
                    }
                    if (!fallbackMatch) {
                        fallbackMatch = found;
                        appLogger?.info(`[Main-Img-Scan] Fallback match registered via recursion: ${found}`, { source: 'Main-Scan' });
                    }
                }
            } else if (entry.isFile()) {
                const parsed = path.parse(entry.name);
                const ext = parsed.ext.toLowerCase();
                
                // Only check .dds and .tga files, matching base name case-insensitively
                if (ext === '.dds' || ext === '.tga') {
                    const baseNameMatches = parsed.name.toLowerCase() === targetLower;
                    appLogger?.info(`[Main-Img-Scan] Inspecting file: "${entry.name}" | Base Match (${parsed.name.toLowerCase()} === ${targetLower}): ${baseNameMatches} | Ext: ${ext} (Expected: ${expectedExt})`, { source: 'Main-Scan' });
                    
                    if (baseNameMatches) {
                        if (ext === expectedExt) {
                            primaryMatch = fullPath;
                            appLogger?.info(`[Main-Img-Scan] SUCCESS: Found exact primary match: ${fullPath}`, { source: 'Main-Scan' });
                            return primaryMatch;
                        } else if (!fallbackMatch) {
                            fallbackMatch = fullPath;
                            appLogger?.warn(`[Main-Img-Scan] WARNING: Found base match with mismatched extension (${ext} instead of ${expectedExt}): ${fullPath}`, { source: 'Main-Scan' });
                        }
                    }
                }
            }
        }
        
        const finalResult = primaryMatch || fallbackMatch || null;
        if (finalResult) {
            appLogger?.info(`[Main-Img-Scan] Finished scanning "${dir}". Selected match: ${finalResult}`, { source: 'Main-Scan' });
        } else {
            appLogger?.info(`[Main-Img-Scan] Finished scanning "${dir}". No matching .dds or .tga file found for target "${targetName}".`, { source: 'Main-Scan' });
        }
        return finalResult;
    }

    async function resolveImagePreview(file, size, metaDirPath, appLogger) {
        const safeName = getSafeName(file);
        appLogger?.info(`[Main-Img-Config] Starting preview resolution for safeName: "${safeName}" with requested size: "${size}"`, { source: 'Main-Config' });

        if (!safeName) {
            appLogger?.error(`[Main-Img-Config] Failed: Could not determine safeName from input payload: ${JSON.stringify(file)}`, { source: 'Main-Config' });
            throw new Error("Could not determine safeName for preview resolution.");
        }
        
        const isThumbnail = size === '32x32';
        const targetFileName = isThumbnail ? `${safeName}_32x32.png` : `${safeName}.png`;
        const configDir = path.join(metaDirPath, 'gfx', safeName);
        const configPath = path.join(configDir, 'config.json');
        let config = {};

        appLogger?.info(`[Main-Img-Config] Checking config path at: ${configPath}`, { source: 'Main-Config' });
        try {
            if (await fs.pathExists(configPath)) {
                const fileContent = await fs.readFile(configPath, 'utf8');
                config = JSON.parse(fileContent);
                appLogger?.info(`[Main-Img-Config] Successfully loaded config.json for "${safeName}". ImageType configured as: "${config.imageType || 'none'}"`, { source: 'Main-Config' });
            } else {
                appLogger?.info(`[Main-Img-Config] config.json does not exist yet at ${configPath}`, { source: 'Main-Config' });
            }
        } catch (readErr) {
            appLogger?.warn(`[Main-Img-Config] Failed to read or parse config.json at ${configPath}: ${readErr.message}`, { source: 'Main-Config' });
            config = {};
        }

        if (isThumbnail && config?.thumbnail) {
            appLogger?.info(`[Main-Img-Config] Cache hit: Found embedded thumbnail in config.json for "${safeName}"`, { source: 'Main-Config' });
            return { 
                success: true, 
                size: '32x32', 
                fileName: targetFileName, 
                base64: config.thumbnail,
                imageType: config.imageType || null
            };
        }

        if (config?.imagePreviews?.[targetFileName]) {
            appLogger?.info(`[Main-Img-Config] Cache hit: Found preview "${targetFileName}" in config.json imagePreviews mapping`, { source: 'Main-Config' });
            return { 
                success: true, 
                size, 
                fileName: targetFileName, 
                base64: config.imagePreviews[targetFileName],
                imageType: config.imageType || null
            };
        }

        const targetImagePath = path.join(configDir, targetFileName);
        appLogger?.info(`[Main-Img-Config] Checking physical preview file on disk at: ${targetImagePath}`, { source: 'Main-Config' });
        if (await fs.pathExists(targetImagePath)) {
            appLogger?.info(`[Main-Img-Config] Cache hit: Found physical preview file on disk at ${targetImagePath}`, { source: 'Main-Config' });
            const buffer = await fs.readFile(targetImagePath);
            const base64Data = `data:image/png;base64,${buffer.toString('base64')}`;
            return {
                success: true,
                size,
                fileName: targetFileName,
                base64: base64Data,
                imageType: config.imageType || null
            };
        }

        appLogger?.warn(`[Main-Img-Config] Warning: Preview (${targetFileName}) for "${safeName}" not found in cache or disk. Initiating source search and generation via processImage.`, { source: 'Main-Config' });

        const inputDir = getInputDir();
        let filePath = null;
        if (!inputDir) {
            appLogger?.error(`[Main-Img-Config] Error: inputDir is not defined or loaded. Cannot search for source image for "${safeName}".`, { source: 'Main-Config' });
        } else {
            let searchDir = inputDir;
            
            appLogger?.info(`[Main-Img-Config] Scanning inputDir root for 'gfx'/'GFX' folder case-insensitively at: ${inputDir}`, { source: 'Main-Config' });
            
            try {
                const rootEntries = await fs.readdir(inputDir, { withFileTypes: true });
                // Filter for any directory matching 'gfx' case-insensitively
                const gfxEntries = rootEntries.filter(e => e.isDirectory() && e.name.toLowerCase() === 'gfx');
                
                if (gfxEntries.length > 0) {
                    // Prioritize uppercase 'GFX' if it exists in the array, otherwise take the first match
                    gfxEntries.sort((a, b) => (b.name === 'GFX' ? 1 : 0) - (a.name === 'GFX' ? 1 : 0));
                    searchDir = path.join(inputDir, gfxEntries[0].name);
                    appLogger?.info(`[Main-Img-Config] Successfully located asset directory: "${searchDir}"`, { source: 'Main-Config' });
                } else {
                    appLogger?.warn(`[Main-Img-Config] Warning: No 'gfx' or 'GFX' directory found in inputDir. Falling back to scanning entire inputDir root: "${inputDir}"`, { source: 'Main-Config' });
                }
            } catch (err) {
                appLogger?.error(`[Main-Img-Config] Failed to read inputDir root entries: ${err.message}. Falling back to: "${inputDir}"`, { source: 'Main-Config' });
                searchDir = inputDir;
            }

            const imageType = config.imageType || 'fallback';
            appLogger?.info(`[Main-Img-Config] Delegating to findFileRecursive. Target Name: "${safeName}", ImageType: "${imageType}", Search Root: "${searchDir}"`, { source: 'Main-Config' });
            
            filePath = await findFileRecursive(searchDir, safeName, imageType, appLogger);
        }

        if (!filePath || !(await fs.pathExists(filePath))) {
            appLogger?.error(`[Main-Img-Config] ERROR: Source image search failed completely. No valid .dds or .tga source found for safeName "${safeName}" inside inputDir ("${inputDir}").`, { source: 'Main-Config' });
            throw new Error(`Source image not found for preview generation: ${safeName}`);
        }

        appLogger?.info(`[Main-Img-Config] SUCCESS: Source image located at: "${filePath}". Proceeding to trigger processImage daemon.`, { source: 'Main-Config' });

        const mainOutputDir = path.dirname(filePath);
        const cacheOutputDir = configDir;
        const imageTypeToUse = config.imageType || 'fallback';

        const result = await processImage({
            filePath,
            imageType: imageTypeToUse,
            mainOutputDir,
            cacheOutputDir
        });

        if (!result || !result.success) {
            appLogger?.error(`[Main-Img-Config] ERROR: processImage daemon failed for "${safeName}". Message: ${result?.message || 'Unknown error'}`, { source: 'Main-Config' });
            throw new Error(result?.message || `Failed to process image for preview: ${safeName}`);
        }

        appLogger?.info(`[Main-Img-Config] processImage completed successfully. Verifying generated preview output at: ${targetImagePath}`, { source: 'Main-Config' });

        if (!(await fs.pathExists(targetImagePath))) {
            appLogger?.error(`[Main-Img-Config] ERROR: Expected generated preview image is missing from disk at: ${targetImagePath}`, { source: 'Main-Config' });
            throw new Error(`Generated preview image not found at: ${targetImagePath}`);
        }

        const buffer = await fs.readFile(targetImagePath);
        const base64Data = `data:image/png;base64,${buffer.toString('base64')}`;

        if (isThumbnail) {
            appLogger?.info(`[Main-Img-Config] Embedding generated 32x32 thumbnail into config.json for "${safeName}"`, { source: 'Main-Config' });
            config.thumbnail = base64Data;
            delete config.imagePreviews;

            const tempPath = `${configPath}.${Date.now()}.${Math.random().toString(36).substring(2, 8)}.tmp`;
            await fs.outputJson(tempPath, config, { spaces: 2 });
            await fs.move(tempPath, configPath, { overwrite: true });
            appLogger?.info(`[Main-Img-Config] Successfully updated config.json with new thumbnail.`, { source: 'Main-Config' });
        }

        return { 
            success: true, 
            size, 
            fileName: targetFileName, 
            base64: base64Data,
            warning: 'Preview was missing and generated via processImage.',
            imageType: config.imageType || null
        };
    }
}
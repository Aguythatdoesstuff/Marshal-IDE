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
                const { path: importedFrom, imageType } = file;
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

                const updatedConfig = {
                    ...existingConfig,
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

    async function findFileRecursive(dir, targetName) {
        if (!(await fs.pathExists(dir))) return null;
        const entries = await fs.readdir(dir, { withFileTypes: true });
        for (const entry of entries) {
            const fullPath = path.join(dir, entry.name);
            if (entry.isDirectory()) {
                const found = await findFileRecursive(fullPath, targetName);
                if (found) return found;
            } else if (entry.isFile()) {
                const parsed = path.parse(entry.name);
                if (parsed.name === targetName) {
                    return fullPath;
                }
            }
        }
        return null;
    }

    async function resolveImagePreview(file, size, metaDirPath, appLogger) {
        const safeName = getSafeName(file);
        if (!safeName) {
            throw new Error("Could not determine safeName for preview resolution.");
        }
        
        const isThumbnail = size === '32x32';
        const targetFileName = isThumbnail ? `${safeName}_32x32.png` : `${safeName}.png`;
        const configDir = path.join(metaDirPath, 'gfx', safeName);
        const configPath = path.join(configDir, 'config.json');
        let config = {};

        try {
            if (await fs.pathExists(configPath)) {
                const fileContent = await fs.readFile(configPath, 'utf8');
                config = JSON.parse(fileContent);
            }
        } catch (readErr) {
            config = {};
        }

        if (isThumbnail && config?.thumbnail) {
            return { 
                success: true, 
                size: '32x32', 
                fileName: targetFileName, 
                base64: config.thumbnail 
            };
        }

        if (config?.imagePreviews?.[targetFileName]) {
            return { 
                success: true, 
                size, 
                fileName: targetFileName, 
                base64: config.imagePreviews[targetFileName] 
            };
        }

        const targetImagePath = path.join(configDir, targetFileName);
        if (await fs.pathExists(targetImagePath)) {
            const buffer = await fs.readFile(targetImagePath);
            const base64Data = `data:image/png;base64,${buffer.toString('base64')}`;
            return {
                success: true,
                size,
                fileName: targetFileName,
                base64: base64Data
            };
        }

        appLogger?.warn(`[Main-Img-Config] Warning: Preview (${targetFileName}) for "${safeName}" not found. Generating via processImage.`, { source: 'Main-Config' });

        const inputDir = getInputDir();
        let filePath = null;
        if (config.importedFrom && await fs.pathExists(config.importedFrom)) {
            filePath = config.importedFrom;
        } else if (inputDir) {
            const gfxInputRoot = path.join(inputDir, 'gfx');
            filePath = await findFileRecursive(gfxInputRoot, safeName);
        }

        if (!filePath || !(await fs.pathExists(filePath))) {
            appLogger?.error(`[Main-Img-Config] Error: Source image not found for ${safeName}`, { source: 'Main-Config' });
            throw new Error(`Source image not found for preview generation: ${safeName}`);
        }

        const mainOutputDir = path.dirname(filePath);
        const cacheOutputDir = configDir;
        const imageType = config.imageType || 'fallback';

        const result = await processImage({
            filePath,
            imageType,
            mainOutputDir,
            cacheOutputDir
        });

        if (!result || !result.success) {
            throw new Error(result?.message || `Failed to process image for preview: ${safeName}`);
        }

        if (!(await fs.pathExists(targetImagePath))) {
            throw new Error(`Generated preview image not found at: ${targetImagePath}`);
        }

        const buffer = await fs.readFile(targetImagePath);
        const base64Data = `data:image/png;base64,${buffer.toString('base64')}`;

        if (isThumbnail) {
            config.thumbnail = base64Data;
            delete config.imagePreviews;

            const tempPath = `${configPath}.${Date.now()}.${Math.random().toString(36).substring(2, 8)}.tmp`;
            await fs.outputJson(tempPath, config, { spaces: 2 });
            await fs.move(tempPath, configPath, { overwrite: true });
        }

        return { 
            success: true, 
            size, 
            fileName: targetFileName, 
            base64: base64Data,
            warning: 'Preview was missing and generated via processImage.'
        };
    }
}
import { dialog, BrowserWindow } from 'electron';
import path from 'path';
import fs from 'fs-extra';
import { executeProcessImage, getSafeName, resolveImagePreview, findFileRecursive, updateSingleImageConfig } from '../Helpers/image.js';

export function registerImageImporterHandlers(ipcMain, state) {
    
    
    // Update the 'process-image' IPC handler to call the helper
    ipcMain.handle('process-image', async (event, args) => {
        try {
            return await executeProcessImage(args);
        } catch (error) {
            state.appLogger?.error(`Image processing IPC failure: ${error.message}`, { source: 'Main-IPC-ImgConverter' });
            return { success: false, message: error.message };
        }
    });
    ipcMain.handle('prepare-img-import', async (event, args) => {
        
        state.appLogger?.info(`[Main-Import-Prep] Handler called with args: ${JSON.stringify(args)}`, { source: 'Main-Import' });

        if (!state.INPUT_DIR) {
            state.appLogger?.error('[Main-Import-Prep] Failed: state.INPUT_DIR is not defined/loaded', { source: 'Main-Import' });
            return { success: false, message: "No project loaded." };
        }

        try {
            let filePaths = [];
            
            const directSource = args?.sourcePath || (typeof args === 'string' ? args : null) || (Array.isArray(args) ? args : null);

            if (directSource) {
                filePaths = Array.isArray(directSource) ? directSource : [directSource];
                state.appLogger?.info(`[Main-Import-Prep] Received direct source paths: ${JSON.stringify(filePaths)}`, { source: 'Main-Import' });
            } else {
                state.appLogger?.info('[Main-Import-Prep] No sourcePath provided. Triggering openDialog...', { source: 'Main-Import' });
                
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
                    state.appLogger?.info('[Main-Import-Prep] Dialog cancelled or no files selected.', { source: 'Main-Import' });
                    return { success: false, message: "Import cancelled by user." };
                }

                filePaths = result.filePaths;
                state.appLogger?.info(`[Main-Import-Prep] Dialog selected files: ${JSON.stringify(filePaths)}`, { source: 'Main-Import' });
            }

            state.appLogger?.info(`[Main-Import-Prep] Preparation successful. Returning ${filePaths.length} path(s).`, { source: 'Main-Import' });
            
            return { 
                success: true, 
                filePaths: filePaths 
            };

        } catch (error) {
            state.appLogger?.error(`[Main-Import-Prep] Error during preparation: ${error.stack || error.message}`, { source: 'Main-Import' });
            return { success: false, message: error.message };
        }
    });

    ipcMain.handle('update-img-cache', async (event, payload) => {

        state.appLogger?.info(`[Main-Import-Cache] Handler called with payload length: ${Array.isArray(payload) ? payload.length : 'non-array'}`, { source: 'Main-Import' });

        if (!state.METADATA_DIR) {
            state.appLogger?.error('[Main-Import-Cache] Failed: state.METADATA_DIR is not defined/loaded', { source: 'Main-Import' });
            return { success: false, message: "No project loaded." };
        }

        try {
            const files = Array.isArray(payload) ? payload : [payload];

            for (const file of files) {
                const { path: importedFrom, imageType, ...extraFields } = file;
                const safeName = getSafeName(file);
                if (!safeName) continue;

                const configDir = path.join(state.METADATA_DIR, 'gfx', safeName);
                const configPath = path.join(configDir, 'config.json');

                await fs.ensureDir(configDir);

                let existingConfig = {};
                if (await fs.pathExists(configPath)) {
                    try {
                        existingConfig = await fs.readJson(configPath);
                    } catch (readErr) {
                        state.appLogger?.error(`[Main-Import-Cache] Failed to parse existing config at ${configPath}, overwriting: ${readErr.message}`, { source: 'Main-Import' });
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

                state.appLogger?.info(`[Main-Import-Cache] Successfully updated cache config for ${safeName} at ${configPath}`, { source: 'Main-Import' });
            }

            return { success: true };
        } catch (error) {
            state.appLogger?.error(`[Main-Import-Cache] Error updating image cache: ${error.stack || error.message}`, { source: 'Main-Import' });
            return { success: false, message: error.message };
        }
    });
    
    ipcMain.handle('finalize-img-import', async (event, payload) => {

        state.appLogger?.info(`[Main-Finalize-Import] called with payload length: ${Array.isArray(payload) ? payload.length : 'non-array'}`, { source: 'Main-Import' });

        if (!state.METADATA_DIR || !state.INPUT_DIR) {
            state.appLogger?.error('[Main-Finalize-Import] Failed: state.METADATA_DIR or state.INPUT_DIR is not defined/loaded', { source: 'Main-Import' });
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

                const mainOutputDir = path.join(state.INPUT_DIR, 'GFX');
                const cacheOutputDir = path.join(state.METADATA_DIR, 'gfx', safeName);

                state.appLogger?.info(`[Main-Finalize-Import] Processing image: ${filePath} as type: ${imageType}`, { source: 'Main-Import' });

                const result = await executeProcessImage({
                    filePath,
                    imageType,
                    mainOutputDir,
                    cacheOutputDir,
                    state
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

            state.appLogger?.info(`[Main-Finalize-Import] Successfully finalized ${importedResults.length} image(s).`, { source: 'Main-Import' });
            return { success: true, imported: importedResults };

        } catch (error) {
            state.appLogger?.error(`[Main-Finalize-Import] Error finalizing image import: ${error.stack || error.message}`, { source: 'Main-Import' });
            return { success: false, message: error.message };
        }
    });


    ipcMain.handle('get-img-config', async (event, filePath) => {
        if (!state.METADATA_DIR || !filePath) {
            return { success: false, config: { safeName: '', imageType: '' } };
        }

        const safeName = getSafeName(filePath);
        const gfxMetaDir = path.join(state.METADATA_DIR, 'gfx');

        if (safeName) {
            const directConfigPath = path.join(gfxMetaDir, safeName, 'config.json');
            try {
                if (await fs.pathExists(directConfigPath)) {
                    const cfg = await fs.readJson(directConfigPath);
                    return { success: true, config: cfg };
                }
            } catch (readErr) {
                state.appLogger?.warn(`[Main-Img-Config] Failed to read direct config at ${directConfigPath}: ${readErr.message}`);
            }
        }

        let gfxExists = false;
        try {
            gfxExists = await fs.pathExists(gfxMetaDir);
        } catch (err) {
            state.appLogger?.error(`[Main-Img-Config] Failed to check gfx metadata directory existence: ${err.message}`);
        }

        if (gfxExists) {
            let subDirs = [];
            try {
                subDirs = await fs.readdir(gfxMetaDir);
            } catch (err) {
                state.appLogger?.error(`[Main-Img-Config] Failed to read gfx metadata directory: ${err.message}`);
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
                        state.appLogger?.warn(`[Main-Img-Config] Failed to read config at ${configPath}: ${readErr.message}`);
                    }
                }
            }
        }

        return { success: true, config: { safeName: safeName || path.basename(filePath, path.extname(filePath)), imageType: '' } };
    });

    ipcMain.handle('update-img-config', async (event, payload) => {

        state.appLogger?.info(`[Main-Img-Config] Handler called with payload length: ${Array.isArray(payload) ? payload.length : 'non-array'}`, { source: 'Main-Config' });

        if (!state.METADATA_DIR) {
            state.appLogger?.error('[Main-Img-Config] Failed: state.METADATA_DIR is not defined/loaded', { source: 'Main-Config' });
            return { success: false, message: "No project loaded." };
        }

        const items = Array.isArray(payload) ? payload : [payload];
        const results = [];

        for (const file of items) {
            try {
                const result = await updateSingleImageConfig(file, state);
                results.push(result);
            } catch (error) {
                state.appLogger?.error(`[Main-Img-Config] Error updating individual item config: ${error.stack || error.message}`, { source: 'Main-Config' });
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

        state.appLogger?.info(`[Main-Img-Config] Handler called with payload: ${JSON.stringify(payload)}`, { source: 'Main-Config' });

        if (!state.METADATA_DIR) {
            state.appLogger?.error('[Main-Img-Config] Failed: state.METADATA_DIR is not defined/loaded', { source: 'Main-Config' });
            return { success: false, message: "No project loaded." };
        }

        const rawItems = payload?.files || payload;
        const requestedSize = payload?.size || 'original';
        const items = Array.isArray(rawItems) ? rawItems : [rawItems];
        const results = [];

        for (const file of items) {
            let result;
            
            try {
                result = await resolveImagePreview(file, requestedSize, state);
            } catch (error) {
                state.appLogger?.error(`[Main-Img-Config] Error getting preview for item: ${error.stack || error.message}`, { source: 'Main-Config' });
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

}
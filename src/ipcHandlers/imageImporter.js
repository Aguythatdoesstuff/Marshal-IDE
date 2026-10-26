import { dialog, BrowserWindow } from 'electron';
import path from 'path';
import fs from 'fs-extra';

export function registerImageImporterHandlers(ipcMain, context) {
    const { getInputDir, getLogger, updateManifest, getMetadataDir, processImage } = context;

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
            
            // Extract paths if passed via args object, direct string, or array
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
            
            // Return the clean file paths for your modal to consume
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
                const { path: importedFrom, safeName, imageType } = file;
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
                const { path: filePath, safeName, imageType } = file;
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
}
import { dialog, BrowserWindow } from 'electron';
import path from 'path';
import fs from 'fs-extra';

export function registerImageImporterHandlers(ipcMain, context) {
    const { getInputDir, getLogger, updateManifest } = context;

    ipcMain.handle('import-image', async (event, args) => {
        const appLogger = getLogger();
        const INPUT_DIR = getInputDir();
        
        appLogger?.info(`[Main-Import] Handler called with args: ${JSON.stringify(args)}`, { source: 'Main-Import' });

        if (!INPUT_DIR) {
            appLogger?.error('[Main-Import] Failed: INPUT_DIR is not defined/loaded', { source: 'Main-Import' });
            return { success: false, message: "No project loaded." };
        }

        try {
            let filePaths = [];
            
            // Extract paths if passed via args object, direct string, or array
            const directSource = args?.sourcePath || (typeof args === 'string' ? args : null) || (Array.isArray(args) ? args : null);

            if (directSource) {
                filePaths = Array.isArray(directSource) ? directSource : [directSource];
                appLogger?.info(`[Main-Import] Received direct source paths: ${JSON.stringify(filePaths)}`, { source: 'Main-Import' });
            } else {
                appLogger?.info('[Main-Import] No sourcePath provided. Triggering openDialog...', { source: 'Main-Import' });
                
                const result = await dialog.showOpenDialog({
                    title: 'Select Images to Import',
                    properties: ['openFile', 'multiSelections'], 
                    filters: [
                        { name: 'DirectDraw Surface Images', extensions: ['dds'] }
                    ]
                });

                if (result.canceled || result.filePaths.length === 0) {
                    appLogger?.info('[Main-Import] Dialog cancelled or no files selected.', { source: 'Main-Import' });
                    return { success: false, message: "Import cancelled by user." };
                }

                filePaths = result.filePaths;
                appLogger?.info(`[Main-Import] Dialog selected files: ${JSON.stringify(filePaths)}`, { source: 'Main-Import' });
            }

            const gfxDir = path.join(INPUT_DIR, 'GFX');
            appLogger?.info(`[Main-Import] Target GFX Directory: ${gfxDir}`, { source: 'Main-Import' });
            
            await fs.ensureDir(gfxDir);

            const importedFiles = [];

            for (const sourcePath of filePaths) {
                appLogger?.info(`[Main-Import] Processing sourcePath: "${sourcePath}" (Type: ${typeof sourcePath})`, { source: 'Main-Import' });

                if (!sourcePath || typeof sourcePath !== 'string') {
                    appLogger?.warn(`[Main-Import] Skipping invalid source path entry: ${sourcePath}`, { source: 'Main-Import' });
                    continue;
                }

                let newFileName = (filePaths.length === 1 && args?.newFileName) ? args.newFileName : null;
                
                if (!newFileName) {
                    const baseName = path.basename(sourcePath);
                    newFileName = baseName
                        .replace(/\s+/g, '_') 
                        .replace(/\.[^/.]+$/, "") + ".dds"; 
                }

                const destPath = path.join(gfxDir, newFileName);
                appLogger?.info(`[Main-Import] Copying from "${sourcePath}" to "${destPath}"`, { source: 'Main-Import' });

                await fs.copy(sourcePath, destPath);

                const relativePath = path.join('GFX', newFileName).replace(/\\/g, '/');
                appLogger?.info(`[Main-Import] Updating manifest for: ${relativePath}`, { source: 'Main-Import' });
                
                await updateManifest('add', relativePath);

                appLogger?.info(`[Main-Import] Successfully imported asset: ${newFileName}`, { source: 'Main-Import' });
                importedFiles.push({ path: destPath, fileName: newFileName });
            }

            appLogger?.info(`[Main-Import] Batch sequence finished. Imported count: ${importedFiles.length}`, { source: 'Main-Import' });
            
            return { 
                success: true, 
                imported: importedFiles 
            };

        } catch (error) {
            appLogger?.error(`[Main-Import] Error during import execution: ${error.stack || error.message}`, { source: 'Main-Import' });
            return { success: false, message: error.message };
        }
    });
}
import { dialog, BrowserWindow } from 'electron';
import path from 'path';
import fs from 'fs-extra';

export function registerImageImporterHandlers(ipcMain, context) {
    const { getInputDir, getLogger, updateManifest } = context;
// prepare-img-import
// update-img-cache
// finalize-img-import
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
                    { name: 'DirectDraw Surface Images', extensions: ['dds'] }
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
}
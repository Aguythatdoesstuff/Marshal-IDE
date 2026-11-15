// These handle reading directories, manipulating files/folders, getting absolute paths, and handling file content/dialogs
import { app, dialog, BrowserWindow } from 'electron';
import { updateManifest } from '../Helpers/project.js';
import { getFileContent, saveAndTriggerCompilation } from '../Helpers/filesystem.js';
import path from 'path';
import fs from 'fs-extra';
import AdmZip from 'adm-zip';

export function registerFilesystemHandlers(ipcMain, state) {



    ipcMain.handle('list-directory-contents', async (event, args) => {
        // Guard Clause: If no project is active yet, return cleanly instead of crashing path.join!
        if (!state.INPUT_DIR) {
            return { success: false, contents: [], error: "No active project loaded." };
        }

        // 1. Safely extract dirPath, ensuring it defaults to an empty string if null/undefined is passed
        const dirPath = args && typeof args.dirPath === 'string' ? args.dirPath : '';
        
        // 2. Prevent path collisions if state.INPUT_DIR is inadvertently re-sent as an absolute target
        const relativePath = dirPath === state.INPUT_DIR ? '' : dirPath;
        const absolutePath = path.join(state.INPUT_DIR, relativePath);
        const contents = [];
        try {
        const dirents = await fs.readdir(absolutePath, { withFileTypes: true });
        for (const dirent of dirents) {
            if (dirent.name.startsWith('.')) continue; 
            const newRelativePath = path.join(relativePath, dirent.name);
            contents.push({ name: dirent.name, path: newRelativePath, isDir: dirent.isDirectory() });
        }
        return { success: true, contents: contents };
        } catch (error) {
        return { success: false, contents: [], error: error.message };
        }
    });

    ipcMain.handle('get-file-content', async (e, { filePath }) => getFileContent(filePath, state));
    ipcMain.handle('save-file', async (e, { filePath, content }) => saveAndTriggerCompilation(filePath, content, state));
    // IPC Handlers using fs-extra for better safety
    ipcMain.handle('create-file', async (event, { filePath }) => {
        try {
        const fullPath = path.join(state.INPUT_DIR, filePath);
        await fs.ensureDir(path.dirname(fullPath)); // Now works perfectly!
        await fs.writeFile(fullPath, '', 'utf8');
        
        await updateManifest('add', filePath, null, state);
        return { success: true };
        } catch (e) { return { success: false, message: e.message }; }
    });

    ipcMain.handle('rename-file', async (event, { oldFilePath, newFilePath }) => {
        try {
            const fullOld = path.join(state.INPUT_DIR, oldFilePath);
            const fullNew = path.join(state.INPUT_DIR, newFilePath);
            await fs.move(fullOld, fullNew, { overwrite: true }); // fs-extra move is more robust than rename
            
            await updateManifest('rename', newFilePath, oldFilePath, state);
            return { success: true };
        } catch (e) { return { success: false, message: e.message }; }
    });


    ipcMain.handle('delete-file-or-dir', async (event, { path: p }) => {
        try {
            await fs.remove(path.join(state.INPUT_DIR, p)); // fs-extra remove is recursive by default
            
            await updateManifest('remove', p, null, state);
            return { success: true };
        } catch (e) { return { success: false, message: e.message }; }
    });

    ipcMain.handle('get-absolute-path', async (event, relPath) => {
        if (!state.INPUT_DIR) {
            return { success: false, message: "No project loaded." };
        }
        try {
            const absolutePath = path.resolve(state.INPUT_DIR, relPath);
            return { success: true, absolutePath };
        } catch (error) {
            state.appLogger?.error(`[Main-Import] Error getting absolute path: ${error.message}`);
            return { success: false, message: error.message };
        }
    });

    ipcMain.handle('open-path', async (e, { path: p }) => {
        try {
        const absolutePath = path.resolve(p);
        await fs.access(absolutePath);

        const openFunc = open.default || open;
        
        await openFunc(absolutePath); 
        
        state.appLogger?.info(`Successfully opened path: ${p}`, { source: 'Main-OpenPath' });
        return { success: true };
        } catch (error) {
        const errorMessage = `Failed to open path ${p} with system handler: ${error.message}`;
        state.appLogger?.error(errorMessage, { source: 'Main-OpenPath' });
        return { success: false, message: errorMessage };
        }
    });


    ipcMain.handle('open-directory-dialog', async () => {
        const { canceled, filePaths } = await dialog.showOpenDialog(BrowserWindow.getFocusedWindow(), { 
            defaultPath: require('os').homedir(),
            properties: [
                'openDirectory', 
                'createDirectory', 
                'showHiddenFiles'
            ] 
        });
        
        return canceled ? { success: false } : { success: true, path: filePaths[0] };
    });


}
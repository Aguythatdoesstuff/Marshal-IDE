import { app, BrowserWindow, ipcMain, dialog, Menu } from 'electron';
import path from 'path';
import fs from 'fs-extra';
import { fork } from 'child_process'; 
export async function getFileContent(filePath, state) {
    if (!state.INPUT_DIR) return { success: false, message: "No project loaded." };
    try {
    const content = await fs.readFile(path.join(state.INPUT_DIR, filePath), 'utf8');
    return { success: true, content };
    } catch (error) {
    return { success: false, message: error.message };
    }
}

/**
 * Ensures all necessary directories exist in UserData on startup.
 */
export async function initializeDirectories(state) {
    await fs.mkdir(state.USER_SETTINGS_DIR, { recursive: true });
    await fs.mkdir(state.USER_PROJECTS_DIR, { recursive: true });
    await fs.mkdir(state.USER_WORKSPACES_DIR, { recursive: true });
    state.appLogger?.info(`Initialized directories at: ${state.USER_DATA_PATH}`, { source: 'Main-Init' });
}


export async function copyDir(src, dest, includeFiles = true) {
    await fs.mkdir(dest, { recursive: true }); 
    const entries = await fs.readdir(src, { withFileTypes: true });
    for (const entry of entries) {
    const srcPath = path.join(src, entry.name);
    const destPath = path.join(dest, entry.name);
    if (entry.isDirectory()) {
        await copyDir(srcPath, destPath, includeFiles);
    } else if (includeFiles) {
        await fs.copyFile(srcPath, destPath);
    }
    }
}


/**
 * Handles saving the file content and manually triggering compilation in the watcher process.
 */
export async function saveAndTriggerCompilation(filePath, content, state) {
    if (!state.INPUT_DIR) return { success: false, message: "No project loaded. Cannot save." };
    
    const fullPath = path.join(state.INPUT_DIR, filePath);
    const dirName = path.dirname(fullPath); 
    
    try {
    await fs.mkdir(dirName, { recursive: true });
    await fs.writeFile(fullPath, content, 'utf8');
    
    state.appLogger?.info(`File saved: ${filePath}. Watcher is monitoring for changes.`, { source: 'Main-Filesystem' });
    
    return { success: true, message: `File saved successfully to disk. Watcher monitoring.` };
    } catch (error) {
    state.appLogger?.error(`Error saving file ${filePath}: ${error.message}`, { source: 'Main-Filesystem' });
    return { success: false, message: `Error saving file: ${error.message}` };
    }
}
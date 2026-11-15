import { app, BrowserWindow, ipcMain, dialog, Menu } from 'electron';
import path from 'path';
import fs from 'fs-extra';
import { fork } from 'child_process'; 
export async function updateManifest(action, relPath, oldRelPath = null, state) {
    if (!state.currentProjectConfig) return;
    const manifestPath = path.join(state.currentProjectConfig.project_root, 'metadata', 'manifest.json');
    
    try {
    await fs.ensureDir(path.dirname(manifestPath));
    
    let manifest = { files: [] };
    if (await fs.pathExists(manifestPath)) {
        manifest = await fs.readJson(manifestPath);
    }

    if (action === 'add') {
        if (!manifest.files.includes(relPath)) manifest.files.push(relPath);
    } else if (action === 'remove') {
        manifest.files = manifest.files.filter(f => f !== relPath);
    } else if (action === 'rename') {
        manifest.files = manifest.files.map(f => f === oldRelPath ? relPath : f);
    }

    await fs.writeJson(manifestPath, manifest, { spaces: 4 });
    } catch (e) {
    state.appLogger?.error(`Manifest sync failed: ${e.message}`);
    }
}
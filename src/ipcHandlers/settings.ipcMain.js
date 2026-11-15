// These handle reading/writing global application and logger settings, boot state, and changelogs:
import { app, dialog, BrowserWindow } from 'electron';
import { startWatcher } from '../Helpers/watcher.js';
import { checkEulaStatus } from '../eula_manager.js';
import path from 'path';
import fs from 'fs-extra';
import AdmZip from 'adm-zip';

export function registerSettingsHandlers(ipcMain, state, readSettingsFile) {
    const eulaPromise = checkEulaStatus(state.appLogger);
    // --- Settings IPC ---
    ipcMain.handle('load-all-global-settings', async () => {
        try {
            const loggerSettings = await readSettingsFile(state.LOGGER_SETTINGS_PATH);
            return { success: true, settings: { logger: loggerSettings } };
        } catch (error) {
            return { success: false, message: error.message };
        }
    });

        
    ipcMain.handle('save-global-settings', async (event, settingsData) => {
        let hasWatcherRestarted = false;
        try {
        if (settingsData.logger) await fs.writeFile(state.LOGGER_SETTINGS_PATH, JSON.stringify(settingsData.logger, null, 4), 'utf8');

        // Handle Project Output Updates (Sent from index_renderer.js for per-project setting)
        if (settingsData.workspaceUpdates && Object.keys(settingsData.workspaceUpdates).length > 0) {
            
            for (const [projectName, newOutputDir] of Object.entries(settingsData.workspaceUpdates)) {
                
                // Construct the expected filename from the project name
                const safeName = projectName.replace(/[^a-z0-9]/gi, '_').toLowerCase();
                const targetFileName = `${safeName}_project.json`;
                const targetPath = path.join(state.USER_PROJECTS_DIR, targetFileName);

                try {
                    await fs.access(targetPath);
                    const pData = await fs.readFile(targetPath, 'utf8');
                    const pConfig = JSON.parse(pData);

                    pConfig.state.output_dir = newOutputDir;
                    
                    await fs.writeFile(targetPath, JSON.stringify(pConfig, null, 4), 'utf8');

                    if (state.currentProjectConfig && state.currentProjectConfig.project_root === pConfig.project_root) {
                        state.currentProjectConfig.state.output_dir = newOutputDir;
                        state.OUTPUT_DIR = newOutputDir;
                        hasWatcherRestarted = true; // Flag for restart
                    }
                } catch (err) {
                    state.appLogger?.warn(`Could not update project ${projectName} config: ${err.message}`, { source: 'Main-Settings' });
                }
            }

            if (hasWatcherRestarted && state.currentProjectConfig) {
                const activeName = path.basename(state.currentProjectConfig.project_root);
                startWatcher(activeName, state); // Restart watcher to pick up new state.OUTPUT_DIR
            }
        }
        return { success: true, message: 'Settings saved.' };
        } catch (error) {
        return { success: false, message: error.message };
        }
    });
        

            
    ipcMain.handle('get-boot-state', async () => {
        const eula = await eulaPromise;
        
        let showWhatsNew = false;
        const changelogPath = path.join(state.USER_DATA_PATH, 'metadata', 'changelog.json');
        const currentVersion = app.getVersion();
        
        try {
            await fs.ensureDir(path.dirname(changelogPath));
            let lastSeenVersion = null;
            if (await fs.pathExists(changelogPath)) {
                const data = await fs.readJson(changelogPath);
                lastSeenVersion = data.version;
            }
            
            // Trigger the overlay notice if no previous state tracking exists or versions differ
            if (!lastSeenVersion || lastSeenVersion !== currentVersion) {
                showWhatsNew = true;
            }
        } catch (err) {
            state.appLogger?.error(`Failed to verify changelog version state: ${err.message}`);
        }

        return {
            enforceEula: !eula.valid,
            showWhatsNew,
            currentVersion
        };
    });
            

        
    ipcMain.handle('dismiss-whats-new', async () => {
        try {
            const changelogPath = path.join(state.USER_DATA_PATH, 'metadata', 'changelog.json');
            await fs.writeJson(changelogPath, { version: app.getVersion() }, { spaces: 4 });
            return { success: true };
        } catch (err) {
            state.appLogger?.error(`Failed to save dismissed changelog state: ${err.message}`);
            return { success: false, message: err.message };
        }
    });

    ipcMain.handle('is-dev', () => !app.isPackaged);

}
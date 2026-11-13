import { app, dialog, BrowserWindow } from 'electron';
import path from 'path';
import fs from 'fs-extra';
import AdmZip from 'adm-zip';

export function registerProjectHandlers(ipcMain, state, startWatcher, copyDir) {
    // --- Project Management IPC ---

    ipcMain.handle('unload-project', async () => {
        try {
            state.appLogger?.info('Initiating project unmount sequence...', { source: 'Main-Config' });
            
            // Kill background watcher process. The watcher process will internally run 
            // process.on('exit') and terminate the persistent C# compiler instantly.
            if (typeof state.watcherProcess !== 'undefined' && state.watcherProcess) {
                // Send specific cleanup IPC action or signal
                if (state.watcherProcess.connected) {
                    state.watcherProcess.send({ action: 'shutdown' });
                }
                
                // Enforce definitive termination after brief grace period
                setTimeout(() => {
                    if (state.watcherProcess) {
                        state.watcherProcess.kill('SIGTERM');
                        state.watcherProcess = null;
                        state.appLogger?.info('Enforced fallback SIGTERM cleanup for state.watcherProcess.', { source: 'Main-Config' });
                    }
                }, 300);
                
                state.appLogger?.info('Sent shutdown sequence to active workspace watcher environment.', { source: 'Main-Config' });
            } else {
                state.appLogger?.warn('Watcher is already unexpectedly inactive!', { source: 'Main-Config' });
            }

            // Clear memory state bindings safely
            state.currentProjectConfig = null;
            state.INPUT_DIR = null;
            state.OUTPUT_DIR = null;

            // Navigate frontend smoothly back to view context
            if (state.mainWindow && !state.mainWindow.isDestroyed()) {
                state.appLogger?.info('Navigating frontend back to workspace selection view context.', { source: 'Main-Config' });
                state.mainWindow.webContents.send('navigate-to', 'WORKSPACE');
            }

            return { success: true };
        } catch (error) {
            state.appLogger?.error(`Failed during project unload sequence: ${error.message}`, { source: 'Main-Config' });
            return { success: false, message: error.message };
        }
    });



    ipcMain.handle('list-projects', async () => {
        try {
        const files = await fs.readdir(state.USER_PROJECTS_DIR);
        const projects = {};

        for (const file of files) {
            if (file.endsWith('_project.json')) {
                try {
                    const content = await fs.readFile(path.join(state.USER_PROJECTS_DIR, file), 'utf8');
                    const config = JSON.parse(content);
                    const name = path.basename(config.project_root); 
                    projects[name] = config;
                } catch (e) {
                    state.appLogger?.warn(`Skipping corrupt project file ${file}: ${e.message}`);
                }
            }
        }
        return { success: true, projects: projects };
        } catch (error) {
        if (error.code === 'ENOENT') return { success: true, projects: {} };
        return { success: false, projects: {}, message: error.message };
        }
    });

    ipcMain.handle('load-project', async (event, projectName) => {
        try {
            const safeName = projectName.replace(/[^a-z0-9]/gi, '_').toLowerCase();
            const configPath = path.join(state.USER_PROJECTS_DIR, `${safeName}_project.json`);

            const data = await fs.readFile(configPath, 'utf8');
            state.currentProjectConfig = JSON.parse(data);
            
            state.METADATA_DIR = path.join(state.currentProjectConfig.project_root, 'metadata');

            const metaFilePath = path.join(state.METADATA_DIR, 'project_info.json');
            try {
                await fs.mkdir(state.METADATA_DIR, { recursive: true });
                let metaData = {};
                try {
                    const existingMeta = await fs.readFile(metaFilePath, 'utf8');
                    metaData = JSON.parse(existingMeta);
                } catch (e) {
                    metaData.created_at = new Date().toISOString();
                    metaData.version_created_with = "Unknown (Legacy)";
                }

                // Sync/Update Project Name and Timestamps
                metaData.project_name = projectName; 
                metaData.last_opened = new Date().toISOString();
                metaData.last_version_used = app.getVersion();

                await fs.writeFile(metaFilePath, JSON.stringify(metaData, null, 4), 'utf8');
            } catch (metaErr) {
                state.appLogger?.warn(`Could not update metadata for ${projectName}: ${metaErr.message}`, { source: 'Main-Config' });
            }

            state.INPUT_DIR = state.currentProjectConfig.input_dir;
            state.OUTPUT_DIR = state.currentProjectConfig.output_dir;
            
            await fs.mkdir(state.INPUT_DIR, { recursive: true });

            // Compare template folder with the workspace root folder
            const templateFiles = await fs.readdir(state.TEMPLATE_DIR, { withFileTypes: true });
            for (const templateEntry of templateFiles) {
                const targetPath = path.join(state.INPUT_DIR, templateEntry.name);
                if (!await fs.pathExists(targetPath)) {
                    if (templateEntry.isDirectory()) {
                        await fs.mkdir(targetPath, { recursive: true });
                        state.appLogger?.info(`Created directory ${templateEntry.name} in workspace root.`, { source: 'Main-Config' });
                    } else if (templateEntry.isFile()) {
                        await fs.copyFile(path.join(state.TEMPLATE_DIR, templateEntry.name), targetPath);
                        state.appLogger?.info(`Created file ${templateEntry.name} in workspace root.`, { source: 'Main-Config' });
                    }
                }
            }

            state.appLogger?.info(`Project loaded: ${projectName}`, { source: 'Main-Config' });
            
            startWatcher(projectName); 
            return { success: true, projectName, config: state.currentProjectConfig };

        } catch (error) {
            state.appLogger?.error(`Failed to load project ${projectName}: ${error.message}`, { source: 'Main-Config' });
            return { success: false, message: error.message };
        }
    });

    ipcMain.handle('create-project', async (event, data) => {
        const name = data.projectName.trim();
        const safeName = name.replace(/[^a-z0-9]/gi, '_').toLowerCase();
        const configPath = path.join(state.USER_PROJECTS_DIR, `${safeName}_project.json`);
        
        try {
            try {
                await fs.access(configPath);
                return { success: false, message: `Project "${name}" already exists.` };
            } catch (e) { /* Proceed */ }

        const projectRoot = path.join(state.USER_WORKSPACES_DIR, name);
        const inputPath = path.join(projectRoot, 'mod');
        const metaPath = path.join(projectRoot, 'metadata'); 
        
        await fs.mkdir(inputPath, { recursive: true });
        await fs.mkdir(metaPath, { recursive: true }); 
        await copyDir(state.TEMPLATE_DIR, inputPath, data.includeTemplates);
        await copyDir(state.TEMPLATE_CACHE_DIR, metaPath, data.includeTemplates);
        // Created metadata with Project Name
        const metaInfo = {
            project_name: name,
            game: 'hoi4',
            created_at: new Date().toISOString(),
            updated_at: new Date().toISOString(),
            last_opened: new Date().toISOString(),
            version_created_with: app.getVersion(),
            last_version_used: app.getVersion()
        };
        await fs.writeFile(path.join(metaPath, 'project_info.json'), JSON.stringify(metaInfo, null, 4), 'utf8');

        const newConfig = {
            project_root: projectRoot,
            input_dir: inputPath,
            output_dir: data.outputDir.trim()
        };

        await fs.writeFile(configPath, JSON.stringify(newConfig, null, 4), 'utf8');
        state.appLogger?.info(`Created new project: ${name} with metadata folder`, { source: 'Main-Config' });

        return { success: true, projectName: name };

        } catch (error) {
        return { success: false, message: error.message };
        }
    });

    ipcMain.handle('delete-project', async (event, { projectName }) => {
        const safeName = projectName.replace(/[^a-z0-9]/gi, '_').toLowerCase();
        const configPath = path.join(state.USER_PROJECTS_DIR, `${safeName}_project.json`);

        try {
        // Safety: If deleting the active project, kill the watcher first
        if (state.currentProjectConfig && path.basename(state.currentProjectConfig.project_root) === projectName) {
            if (state.watcherProcess) {
                state.appLogger?.info(`Killing active watcher for ${projectName} before deletion.`, { source: 'Main-Config' });
                state.watcherProcess.kill('SIGTERM');
                state.watcherProcess = null;
            }
            state.currentProjectConfig = null;
            state.INPUT_DIR = null;
            state.OUTPUT_DIR = null;
        }

        // Read the config to get the folder path
        const data = await fs.readFile(configPath, 'utf8');
        const config = JSON.parse(data);
        
        // Delete the physical workspace folder (removes /mod and /metadata)
        if (config.project_root && config.project_root.startsWith(state.USER_WORKSPACES_DIR)) {
            await fs.rm(config.project_root, { recursive: true, force: true });
        }

        // Delete the app's local JSON configuration
        await fs.rm(configPath, { force: true });
        
        state.appLogger?.info(`Permanently deleted project and metadata: ${projectName}`, { source: 'Main-Config' });
        return { success: true, message: 'Project deleted.' };

        } catch (error) {
        state.appLogger?.error(`Delete failed for ${projectName}: ${error.message}`, { source: 'Main-Config' });
        return { success: false, message: error.message };
        }
    });

    ipcMain.handle('export-project', async (event, projectName) => {
        try {
            const safeName = projectName.replace(/[^a-z0-9]/gi, '_').toLowerCase();
            const configPath = path.join(state.USER_PROJECTS_DIR, `${safeName}_project.json`);
            const config = await fs.readJson(configPath);

            const { filePath } = await dialog.showSaveDialog({
                title: 'Export Marshal Project',
                defaultPath: path.join(app.getPath('downloads'), `${projectName}.MarshalIDE`),
                filters: [{ name: 'Marshal Project', extensions: ['MarshalIDE'] }]
            });

            if (!filePath) return { success: false };

            const zip = new AdmZip();
            zip.addLocalFolder(config.project_root);
            zip.writeZip(filePath);

            return { success: true };
        } catch (error) {
            state.appLogger?.error(`Export Error: ${error.message}`);
            return { success: false, message: error.message };
        }
    });

    ipcMain.handle('import-project', async () => {
        try {
            const { filePaths } = await dialog.showOpenDialog({
                filters: [{ name: 'Marshal Project', extensions: ['MarshalIDE'] }],
                properties: ['openFile']
            });

            if (!filePaths || filePaths.length === 0) return { success: false };
            const selectedFile = filePaths[0];

            const tempPath = path.join(app.getPath('temp'), `import_${Date.now()}`);
            const zip = new AdmZip(selectedFile);
            zip.extractAllTo(tempPath, true);

            const metaPath = path.join(tempPath, 'metadata', 'project_info.json');
            if (!await fs.pathExists(metaPath)) {
                throw new Error("Invalid MarshalIDE project: Missing metadata.");
            }
            const metadata = await fs.readJson(metaPath);
            const projectName = metadata.project_name || path.basename(selectedFile, '.MarshalIDE');

            const { filePaths: outPaths } = await dialog.showOpenDialog({
                title: `Select Output Directory for ${projectName}`,
                properties: ['openDirectory', 'createDirectory']
            });

            if (!outPaths || outPaths.length === 0) throw new Error("Output directory is required.");
            const selectedOutputDir = outPaths[0];

            const finalWorkspacePath = path.join(state.USER_WORKSPACES_DIR, projectName);
            if (await fs.pathExists(finalWorkspacePath)) {
                throw new Error(`A project named "${projectName}" already exists.`);
            }

            await fs.move(tempPath, finalWorkspacePath);

            const safeName = projectName.replace(/[^a-z0-9]/gi, '_').toLowerCase();
            const configPath = path.join(state.USER_PROJECTS_DIR, `${safeName}_project.json`);
            const configData = {
                project_root: finalWorkspacePath,
                input_dir: path.join(finalWorkspacePath, 'mod'),
                output_dir: selectedOutputDir
            };

            await fs.writeJson(configPath, configData, { spaces: 4 });
            return { success: true, projectName };

        } catch (error) {
            state.appLogger?.error(`Import Error: ${error.message}`);
            return { success: false, message: error.message };
        }
    });
}
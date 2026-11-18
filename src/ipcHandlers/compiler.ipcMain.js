// These handle running the compiler, handling AST requests, recompiling, and cache management
import { app, dialog, BrowserWindow } from 'electron';
import { startWatcher } from '../Helpers/watcher.js';
import { handleAstRequest } from '../Helpers/compiler.js';
import  { getEditorCache, saveEditorCache } from '../Helpers/cache.js';
import path from 'path';
import fs from 'fs-extra';
import AdmZip from 'adm-zip';

export function registerCompilerHandlers(ipcMain, state) {

    ipcMain.handle('run-compiler', async (event, { input, workspaceName }) => {
        try {
            if (!state.watcherProcess || !state.watcherProcess.connected) {
                const targetName = workspaceName || (state.currentProjectConfig ? path.basename(state.currentProjectConfig.project_root) : null);
                if (targetName) {
                    await startWatcher(targetName, state);
                } else {
                    throw new Error("No active workspace watcher found. Compilation engine offline.");
                }
            }

            // Forward compile message payload smoothly to the online watcher child process via standard IPC packet channel
            state.watcherProcess.send({
                action: 'manual-compile',
                filePath: input
            });

            state.appLogger?.info(`[Main IPC] Forwarded file compilation request to watcher for: ${path.basename(input)}`, { source: 'Main-IPC-Compiler' });
            return { success: true };
        } catch (error) {
            state.appLogger?.error(`Manual compilation trigger failed: ${error.message}`, { source: 'Main-IPC-Compiler' });
            return { success: false, message: error.message };
        }
    });
	ipcMain.handle('request-ast', handleAstRequest);


    
	ipcMain.handle('get-simplified-editor-cache', async (event, { filePath }) => {
		const projectRoot = state.currentProjectConfig?.project_root || null;
		state.appLogger?.info(`[Main-Cache] Fetching cache for file: ${filePath}`, { source: 'Main-Cache' });
		
		return await getEditorCache(filePath, projectRoot, async ({ filePath: targetPath, workspaceCachePath }) => {
			try {
				state.appLogger?.info(`[Main-Cache] Cache miss for ${targetPath}. Requesting Rete.js-formatted AST directly from compiler...`, { source: 'Main-Cache' });
				
				const absoluteFilePath = state.INPUT_DIR ? path.join(state.INPUT_DIR, targetPath) : targetPath;
				
				const outputOptions = {
					outputAst: true,
					outputCode: false,
					astOutputPath: workspaceCachePath, // Compiler outputs directly in Rete format here
					codeOutputPath: null
				};

				const result = await handleAstRequest(state, { filePath: absoluteFilePath, outputOptions });
				if (!result.success) return false;

				// Poll for completion of the compiler output in the workspace cache path
				for (let i = 0; i < 30; i++) {
					if (await fs.pathExists(workspaceCachePath)) {
						return true;
					}
					await new Promise(resolve => setTimeout(resolve, 100));
				}

				return await fs.pathExists(workspaceCachePath);
			} catch (e) {
				state.appLogger?.error(`AST fallback execution error: ${e.message}`, { source: 'Main-Cache' });
				return false;
			}
		});
	});

    ipcMain.handle('save-simplified-editor-cache', async (event, { filePath, cacheData }) => {
        const projectRoot = state.currentProjectConfig?.project_root || null;
        state.appLogger?.info(`[Main-Cache] Saving cache for file: ${filePath}`, { source: 'Main-Cache' });
        return await saveEditorCache(filePath, cacheData, projectRoot);
    });
    ipcMain.handle('compile-file', async (event, { filePath, outputOptions }) => {
        try {
            await compileFile(filePath, outputOptions, logToSystem);
            return { success: true };
        } catch (e) { return { success: false, message: e.message }; }
    });
    
    ipcMain.handle('recompile-all', async (event, workspaceName) => {
        try {
            state.appLogger?.info(`[Main IPC] Recompile-all requested for workspace: ${workspaceName || 'Active Context'}`, { source: 'Main-IPC-Compiler' });

            // If no current project configuration or watcher is bound, dynamically spin it up first
            if (!state.watcherProcess || !state.watcherProcess.connected) {
                const targetName = workspaceName || (state.currentProjectConfig ? path.basename(state.currentProjectConfig.project_root) : null);
                if (!targetName) {
                    throw new Error("No active workspace selected or available to recompile.");
                }
                state.appLogger?.info(`[Main IPC] Watcher unavailable for recompile-all. Bootstrapping workspace environment: ${targetName}`, { source: 'Main-IPC-Compiler' });
                await startWatcher(targetName, state);
            }

            // Forward the master full-recompile trigger command down to the active watcher process
            state.watcherProcess.send({
                action: 'manual-recompile-all'
            });

            state.appLogger?.info(`[Main IPC] Master compilation directive broadcasted successfully down to workspace watcher.`, { source: 'Main-IPC-Compiler' });
            return { success: true };
        } catch (error) {
            state.appLogger?.error(`Failed to trigger full workspace recompilation: ${error.message}`, { source: 'Main-IPC-Compiler' });
            return { success: false, message: error.message };
        }
    });

}
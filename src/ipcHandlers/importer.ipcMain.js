import { app, dialog, BrowserWindow } from 'electron';
import path from 'path';
import fs from 'fs-extra';
import AdmZip from 'adm-zip';
import { runImporter } from './importer.binary.js';

export function registerImporterHandlers(ipcMain, state) {

    ipcMain.handle('run-importer', async (event, { input, workspaceName }) => {
        try {
            // 1. Calculate and fetch target directory paths matching Electron's workspace structure
            // Target: electron userdata folder/workspaces/hoi4/ <workspaceName>
            const projectWorkspaceRoot = path.join(state.USER_WORKSPACES_DIR, workspaceName);
            const debugDirectory = state.getSessionDir(); // Fetches active SESSION_DIR absolute path

            console.log(`[Main IPC] Starting Importer Task.`);
            console.log(` - Input Source:   ${input}`);
            console.log(` - Workspace Root: ${projectWorkspaceRoot}`);
            console.log(` - Debug Path:     ${debugDirectory}`);

            // 2. Enforce absolute path validations
            if (!path.isAbsolute(input) || !path.isAbsolute(projectWorkspaceRoot) || !path.isAbsolute(debugDirectory)) {
            const errorMsg = `Non-absolute path detected! Execution aborted.\n\nInput: ${input}\nOutput: ${projectWorkspaceRoot}\nDebug: ${debugDirectory}`;
            
            state.logger.handleIpcLog({
                type: 'error',
                source: 'Main-IPC-Importer',
                message: errorMsg
            });

            dialog.showErrorBox('Path Validation Error', 'The application attempted to pass a relative path to the native components.');
            return { success: false, message: 'Path targets must be absolute.' };
            }

            // 3. Execute binary safely with the dynamic safe workspace path mapped as destination
            const result = await runImporter({
            input: input,
            output: projectWorkspaceRoot,
            debug: debugDirectory
            });

            return result; // Returns { success: true }
        } catch (error) {
            console.error('[Main IPC] Importer registration failure:', error);
            
            state.logger.handleIpcLog({
            type: 'error',
            source: 'Main-IPC-Importer',
            message: `Fatal operational failure: ${error.message}`
            });

            return {
            success: false,
            message: error.message
            };
        }
    });
}
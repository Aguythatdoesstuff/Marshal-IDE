import { app, dialog, BrowserWindow } from 'electron';
import path from 'path';
import fs from 'fs-extra';
import AdmZip from 'adm-zip';
import { runImporter } from './importer.binary.js';
import { spawnBinary, spawnBinaries, stopProcesses } from '../Helpers/binary.js';

export function registerImporterHandlers(ipcMain, state) {

    ipcMain.handle('run-importer', async (event, { input, workspaceName }) => {
        try {
            // Target: electron userdata folder/workspaces/hoi4/<workspaceName>
            const projectWorkspaceRoot = path.join(state.USER_WORKSPACES_DIR, workspaceName);

            console.log(`[Main IPC] Starting Importer Task.`);
            console.log(` - Input Source:   ${input}`);
            console.log(` - Workspace Root: ${projectWorkspaceRoot}`);

            if (!path.isAbsolute(input) || !path.isAbsolute(projectWorkspaceRoot)) {
                const errorMsg = `Non-absolute path detected! Execution aborted.\n\nInput: ${input}\nOutput: ${projectWorkspaceRoot}`;

                state.handleIpcLog({
                    type: 'error',
                    source: 'Main-IPC-Importer',
                    message: errorMsg
                });

                dialog.showErrorBox('Path Validation Error', 'The application attempted to pass a relative path to the native components.');
                return { success: false, message: 'Path targets must be absolute.' };
            }

            return await spawnBinary('importer', state, {
                input,
                output: projectWorkspaceRoot
            });
        } catch (error) {
            console.error('[Main IPC] Importer registration failure:', error);

            state.handleIpcLog({
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
import { app, BrowserWindow, ipcMain, dialog, Menu } from 'electron';
import path from 'path';
import fs from 'fs-extra';
import { fork } from 'child_process'; 
export async function handleAstRequest(state, { filePath, outputOptions }) {
    try {
        const absoluteFilePath = path.resolve(filePath);
        const { outputAst, outputCode, astOutputPath, codeOutputPath } = outputOptions;

        if (!outputAst && !outputCode) {
            throw new Error("At least one output option (outputAst or outputCode) must be specified");
        }

        if ((outputAst && !astOutputPath) || (outputCode && !codeOutputPath)) {
            throw new Error("Output path required for specified output type");
        }

        const request = {
            action: 'process-file',
            filePath: absoluteFilePath,
            outputOptions: outputOptions
        };

        if (!state.watcherProcess || !state.watcherProcess.connected) {
            const targetName = state.currentProjectConfig ? path.basename(state.currentProjectConfig.project_root) : null;
            if (!targetName) {
                throw new Error("No active workspace found. Please load a project first.");
            }
            await startWatcher(targetName, state);
        }

        state.watcherProcess.send(request);
        return { success: true };
    } catch (error) {
        state.appLogger?.error(`AST request failed: ${error.message}`, { source: 'Main-IPC-Compiler' });
        return { success: false, message: error.message };
    }
}


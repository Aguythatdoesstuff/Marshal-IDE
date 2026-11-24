import { app, BrowserWindow, ipcMain, dialog, Menu } from 'electron';
import path from 'path';
import fs from 'fs-extra';
import { fork } from 'child_process'; 
// src/ipcHandlers/compiler.js

export function compileFile(filePath, outputOptions, loggerFn, state) {
    const SOURCE = 'Compiler-AST';
    const log = (type, message, source = SOURCE) => {
        if (typeof loggerFn === 'function') {
            loggerFn({ type, message, source });
        }
    };
    if(!outputOptions) outputOptions = {};
    try {
        if (state.compilerProcess && state.compilerProcess.stdin && state.compilerProcess.stdin.writable) {
            const absolutePath = path.resolve(filePath);
            
            const args = [];
            args.push(absolutePath);
            
            if (outputOptions.outputAst) {
                args.push(`&& out_ast=true`);
                if (outputOptions.astOutputPath) {
                    args.push(`&& o=${path.resolve(outputOptions.astOutputPath)}`);
                }
            }
            
            if (outputOptions.outputCode) {
                args.push(`&& out_code=true`);
                if (outputOptions.codeOutputPath) {
                    args.push(`&& c=${path.resolve(outputOptions.codeOutputPath)}`);
                }
            }

            log('info', `Sending compile request with parameters: ${args.join(' ')}`, SOURCE);
            
            state.compilerProcess.stdin.write(args.join(' ') + '\n');
            return { success: true };
        } else {
            log('error', `Compiler process is not running or stdin is unavailable.`, SOURCE);
            return { success: false };
        }
    } catch (error) {
        log('error', `Compile Error: ${error.message}`, SOURCE);
        return { success: false, message: error.message };
    }
}


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


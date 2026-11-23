// src/ipcHandlers/compiler.js
import { spawn } from 'child_process';
import path from 'path';
import { resolveBinaryPath } from './binaryUtils.js';

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
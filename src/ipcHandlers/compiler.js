// src/ipcHandlers/compiler.js
import { spawn } from 'child_process';
import path from 'path';
import { resolveBinaryPath } from './binaryUtils.js';

let compilerProcess = null;

export function spawnCompiler(config, loggerFn) {
    const SOURCE = 'Compiler-Handler';
    const log = (type, message, source = SOURCE) => {
        if (typeof loggerFn === 'function') {
            loggerFn({ type, message, source });
        }
    };

    if (compilerProcess) {
        stopCompiler('Compiler-Handler', loggerFn);
    }

    try {
        const binaryPath = resolveBinaryPath('compiler', log);

        const args = [
            `--output=${config.output_dir}`,
            `--debug=${config.log_dir}`
        ];

        log('info', `Spawning persistent compiler process: ${binaryPath}`);
        compilerProcess = spawn(binaryPath, args);

        compilerProcess.stdout.on('data', (data) => {
            log('info', `[Compiler Output]: ${data.toString().trim()}`, 'Compiler-Stdout');
        });

        compilerProcess.stderr.on('data', (data) => {
            log('error', `[Compiler Error]: ${data.toString().trim()}`, 'Compiler-Stderr');
        });

        compilerProcess.on('close', (code) => {
            log('error', `Compiler process exited with code ${code}`);
            compilerProcess = null;
        });

        compilerProcess.on('error', (err) => {
            log('error', `Compiler process error: ${err.message}`);
            compilerProcess = null;
        });

        return compilerProcess;
    } catch (error) {
        log('error', `Compiler setup failed: ${error.message}`);
        throw error;
    }
}

export function compileFile(filePath, outputOptions, loggerFn) {
    const SOURCE = 'Compiler-AST';
    const log = (type, message, source = SOURCE) => {
        if (typeof loggerFn === 'function') {
            loggerFn({ type, message, source });
        }
    };
    if(!outputOptions) outputOptions = {};
    try {
        if (compilerProcess && compilerProcess.stdin && compilerProcess.stdin.writable) {
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
            
            compilerProcess.stdin.write(args.join(' ') + '\n');
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

export function stopCompiler(source = 'Compiler-Lifecycle', loggerFn = () => {}) {
    const log = (type, message, src = source) => {
        if (typeof loggerFn === 'function') {
            loggerFn({ type, message, source: src });
        }
    };

    if (!compilerProcess) return Promise.resolve();

    return new Promise((resolve) => {
        const proc = compilerProcess;

        const cleanup = () => {
            proc.removeAllListeners('close');
            proc.removeAllListeners('exit');
            if (compilerProcess === proc) {
                compilerProcess = null;
            }
            resolve();
        };

        proc.once('close', cleanup);
        proc.once('exit', cleanup);

        try {
            log('info', `Terminating compiler process via SIGTERM.`, source);
            proc.kill('SIGTERM');
        } catch (e) {
            log('error', `Failed to SIGTERM compiler process (${e.message}), sending SIGKILL command.`);
            try {
                log('info', `Terminating compiler process via SIGKILL.`, source);
                proc.kill('SIGKILL');
            } catch (err) {
                log('error', `Failed to SIGKILL compiler process: ${err.message}`);
                cleanup();
            }
        }

        // Fallback timeout in case process hangs on exit
        setTimeout(() => {
            cleanup();
        }, 3000);
    });
}
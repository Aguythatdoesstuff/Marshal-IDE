// src/ipcHandlers/compiler.js
import { spawn } from 'child_process';
import path from 'path';
import fs from 'fs-extra';

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
        const platform = process.platform;
        const isDev = !process.resourcesPath || !fs.existsSync(path.join(process.resourcesPath, 'published-components'));

        const baseDir = isDev
            ? path.join(process.cwd(), 'c#', 'published-components', 'compiler')
            : path.join(process.resourcesPath, 'published-components', 'compiler');

        let binaryPath;
        if (platform === 'win32') {
            binaryPath = path.join(baseDir, 'windows', 'Compiler.exe');
        } else if (platform === 'linux') {
            binaryPath = path.join(baseDir, 'linux', 'Compiler');
        } else {
            throw new Error(`Unsupported OS platform: ${platform}`);
        }

        if (platform === 'linux') {
            try {
                fs.chmodSync(binaryPath, 0o755);
                log('info', `Successfully assigned execution rights (0755) to Linux binary.`);
            } catch (permissionError) {
                log('warn', `Failed to run chmodSync on binary file: ${permissionError.message}`);
            }
        }

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
            
            // Construct command line arguments
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

    if (!compilerProcess) return;

    try {
        log('info', `Terminating compiler process via SIGTERM.`, source);
        compilerProcess.kill('SIGTERM');
    } catch (e) {
        log('error', `Failed to SIGTERM compiler process (${e.message}), sending SIGKILL command.`);
        try {
            log('info', `Terminating compiler process via SIGKILL.`, source);
            compilerProcess.kill('SIGKILL');
        } catch (err) {
            log('error', `Failed to SIGKILL compiler process: ${err.message}`);
        }
    }
    compilerProcess = null;
}
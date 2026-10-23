// src/ipcHandlers/img-converter.js
import { spawn } from 'child_process';
import path from 'path';
import fs from 'fs-extra';
import { resolveBinaryPath } from './binaryUtils.js';

let imgConverterProcess = null;

const ALLOWED_IMAGE_TYPES = {
    // focus tree specific configuration: Used for rendering focus tree icons in the UI. The format is uncompressed because thats what the game engine preffers.
    'focus': {
        fileType: 'dds',
        format: 'R8G8B8A8_UNORM',
        description: '8.8.8.8 ARGB 32-bit Unsigned (Uncompressed)'
    },
    // Diffuse map: The primary color texture defining the base visual patterns and painting of the model.
    'diffuse': {
        fileType: 'dds',
        format: 'BC3_UNORM',
        description: 'BC3/DXT5 Compressed Texture'
    },
    // Normal map: A surface vector map that simulates high-detail bumps and lighting angles without extra geometry.
    'normal': {
        fileType: 'dds',
        format: 'BC1_UNORM',
        description: 'BC1/DXT1 Compressed Texture'
    }
};

/**
 * Spawns the persistent Python img-converter daemon process.
 * 
 * @param {Object} config - Configuration object (e.g. { log_path })
 * @param {Function} loggerFn - Logging callback function
 * @returns {ChildProcess} The spawned process instance
 */
export function spawnImgConverter(config, loggerFn) {
    const SOURCE = 'ImgConverter-Handler';
    const log = (type, message, source = SOURCE) => {
        if (typeof loggerFn === 'function') {
            loggerFn({ type, message, source });
        }
    };

    if (imgConverterProcess) {
        stopImgConverter();
    }

    try {
        const binaryPath = resolveBinaryPath('img-converter', log);

        const args = [];
        if (config && config.log_path) {
            args.push(`--log-path=${config.log_path}`);
        }

        log('info', `Spawning persistent img-converter process: ${binaryPath}`);
        imgConverterProcess = spawn(binaryPath, args);

        imgConverterProcess.stdout.on('data', (data) => {
            log('info', `[ImgConverter Output]: ${data.toString().trim()}`, 'ImgConverter-Stdout');
        });

        imgConverterProcess.stderr.on('data', (data) => {
            log('error', `[ImgConverter Error]: ${data.toString().trim()}`, 'ImgConverter-Stderr');
        });

        imgConverterProcess.on('close', (code) => {
            if (log == 'function') {
                log('warn', `ImgConverter process closed with code ${code}`);
            }
            else if (console && console.warn)
            {
                console.warn(`ImgConverter process closed with code ${code}`);
            }
            imgConverterProcess = null;
        });

        imgConverterProcess.on('error', (err) => {
            log('error', `ImgConverter process error: ${err.message}`);
            imgConverterProcess = null;
        });

        return imgConverterProcess;
    } catch (error) {
        log('error', `ImgConverter setup failed: ${error.message}`);
        throw error;
    }
}

/**
 * Processes an image file by sending conversion commands to the background daemon.
 * Generates main output based on allowed image types and cache outputs (full PNG + 32x32 PNG).
 * 
 * @param {string} filePath - Absolute path to the source image.
 * @param {string} imageType - The target use-case type (e.g. 'focus').
 * @param {string} mainOutputDir - Absolute path for the primary converted output.
 * @param {string} cacheOutputDir - Absolute path for cache outputs.
 * @param {Function} loggerFn - Logging callback function.
 * @returns {Object} Result object indicating success or failure.
 */
export async function processImage(filePath, imageType, mainOutputDir, cacheOutputDir, loggerFn) {
    const SOURCE = 'ImgConverter-Process';
    const log = (type, message, source = SOURCE) => {
        if (typeof loggerFn === 'function') {
            loggerFn({ type, message, source });
        }
    };

    if (!ALLOWED_IMAGE_TYPES[imageType]) {
        const errorMsg = `Invalid or unallowed image type: "${imageType}". Allowed types: ${Object.keys(ALLOWED_IMAGE_TYPES).join(', ')}`;
        log('error', errorMsg, SOURCE);
        throw new Error(errorMsg);
    }

    if (!imgConverterProcess || !imgConverterProcess.stdin || !imgConverterProcess.writable) {
        const errorMsg = 'ImgConverter daemon process is not running or stdin is unavailable.';
        log('error', errorMsg, SOURCE);
        return { success: false, message: errorMsg };
    }

    try {
        const absolutePath = path.resolve(filePath);
        const targetConfig = ALLOWED_IMAGE_TYPES[imageType];

        await fs.ensureDir(mainOutputDir);
        await fs.ensureDir(cacheOutputDir);

        // 1. Main output command
        const mainArgs = [
            '-ft', targetConfig.fileType,
            '-f', targetConfig.format,
            '-y',
            '-o', path.resolve(mainOutputDir),
            '--',
            absolutePath
        ];

        // 2. Cache full PNG command
        const cacheFullArgs = [
            '-ft', 'png',
            '-y',
            '-o', path.resolve(cacheOutputDir),
            '--',
            absolutePath
        ];

        // 3. Cache 32x32 PNG command (using a temporary copy to avoid filename collisions in cacheOutputDir)
        const parsedPath = path.parse(absolutePath);
        const temp32Name = `${parsedPath.name}_32x32${parsedPath.ext}`;
        const temp32Path = path.join(cacheOutputDir, temp32Name);
        await fs.copy(absolutePath, temp32Path);

        const cache32Args = [
            '-ft', 'png',
            '-w', '32',
            '-h', '32',
            '-y',
            '-o', path.resolve(cacheOutputDir),
            '--',
            temp32Path
        ];

        log('info', `Sending main conversion request for type '${imageType}'`, SOURCE);
        imgConverterProcess.stdin.write(`process-image:${JSON.stringify(mainArgs)}\n`);

        log('info', `Sending cache full-size PNG request`, SOURCE);
        imgConverterProcess.stdin.write(`process-image:${JSON.stringify(cacheFullArgs)}\n`);

        log('info', `Sending cache 32x32 PNG request`, SOURCE);
        imgConverterProcess.stdin.write(`process-image:${JSON.stringify(cache32Args)}\n`);

        return { success: true };
    } catch (error) {
        log('error', `Image Processing Error: ${error.message}`, SOURCE);
        return { success: false, message: error.message };
    }
}

/*
 * Gracefully terminates the img-converter daemon process and waits until its PID exits.
 */
export function stopImgConverter() {
    if (!imgConverterProcess) return Promise.resolve();
    const proc = imgConverterProcess;
    imgConverterProcess = null;

    return new Promise((resolve) => {
        // Resolve immediately once the process fires the 'exit' event
        console.log(`Stopping img-converter process with PID ${proc.pid}...`);
        proc.once('exit', () => {
            console.log(`img-converter process with PID ${proc.pid} exited successfully.`);
            resolve()}
        );

        try {
            proc.kill('SIGTERM');
        } catch (e) {
            console.error(`Failed to terminate img-converter process with SIGTERM, trying SIGKILL. exit msg: ${e.message}`);
            try {
                proc.kill('SIGKILL');
            } catch (err) {
                console.error(`Failed to terminate img-converter process with SIGKILL. exit msg: ${err.message}`);
            }
        }

        // Safety fallback: force SIGKILL and resolve after 2s if it hangs
        setTimeout(() => {
            try {
                console.log(`Forcefully killing img-converter process with PID ${proc.pid} after 2 second timeout.`);
                proc.kill('SIGKILL');
            } catch (e) {
                console.error(`Failed to terminate img-converter process with SIGKILL after timeout. exit msg: ${e.message}`);
            }
            resolve();
        }, 2000);
    });
}
import path from 'path';
import fs from 'fs';
import { app } from 'electron';

/**
 * Resolves the absolute path to a binary component, handles platform-specific
 * executable naming, applies Linux permissions, and throws fatal errors if unknown or missing.
 * 
 * @param {string} componentName - The name of the component ('compiler', 'importer', 'img-converter').
 * @param {Function} [loggerFn] - Optional logger function matching the app's standard logger pattern.
 * @returns {string} The absolute path to the executable binary.
 */
export function resolveBinaryPath(componentName, loggerFn) {
    const SOURCE = 'Binary-Utils';
    const log = (type, message, source = SOURCE) => {
        if (typeof loggerFn === 'function') {
            loggerFn({ type, message, source });
        }
    };

    const platform = process.platform;
    const isDev = typeof app !== 'undefined'
        ? !app.isPackaged
        : (!process.resourcesPath || !fs.existsSync(path.join(process.resourcesPath, 'published-components')));

    let baseDir;
    let exeName;

    if (componentName === 'compiler') {
        baseDir = isDev
            ? path.join(process.cwd(), 'c#', 'published-components', 'compiler')
            : path.join(process.resourcesPath, 'published-components', 'compiler');
        exeName = 'Compiler';
    } else if (componentName === 'importer') {
        baseDir = isDev
            ? path.join(process.cwd(), 'c#', 'published-components', 'importer')
            : path.join(process.resourcesPath, 'published-components', 'importer');
        exeName = 'importer';
    } else if (componentName === 'img-converter') {
        baseDir = isDev
            ? path.join(process.cwd(), 'python', 'published-components', 'img-converter')
            : path.join(process.resourcesPath, 'published-components', 'img-converter');
        exeName = 'img-converter';
    } else {
        throw new Error(`Fatal Error: Unknown component/binary to spawn: "${componentName}"`);
    }

    let binaryPath;
    if (platform === 'win32') {
        binaryPath = path.join(baseDir, 'windows', `${exeName}.exe`);
    } else if (platform === 'linux') {
        binaryPath = path.join(baseDir, 'linux', exeName);
    } else {
        throw new Error(`Unsupported OS platform: ${platform}`);
    }

    if (!fs.existsSync(binaryPath)) {
        throw new Error(`Fatal Error: Binary file not found for "${componentName}" at path: ${binaryPath}`);
    }

    if (platform === 'linux') {
        try {
            fs.chmodSync(binaryPath, 0o755);
            log('info', `Successfully assigned execution rights (0755) to Linux binary.`);
        } catch (permissionError) {
            log('warn', `Failed to run chmodSync on binary file: ${permissionError.message}`);
        }
    }

    return binaryPath;
}
// compiler_helpers.js
import { readFileSync } from 'fs';
import * as path from 'path';
import { fileURLToPath } from 'url';
import { exec } from 'child_process';

// Get the directory name of the current module to locate path_utils.json
const __dirname = path.dirname(fileURLToPath(import.meta.url));

/**
 * Reads path_utils.json from the compiler root with OS-level error reporting
 */
export function getPathConfig(extension) {
    const configPath = path.join(__dirname, 'path_utils.json');
    
    try {
        const configRaw = readFileSync(configPath, 'utf8');
        const config = JSON.parse(configRaw);
        
        // Return config for extension
        const result = config[extension];
        if (!result) throw new Error(`Extension "${extension}" not found in config.`);
        
        return result;
    } catch (e) {
        const errorMsg = `Critical: Could not load ${configPath}. Error: ${e.message}`;
        console.error(errorMsg);
        
        // OS Error Popups
        const platform = process.platform;
        if (platform === 'win32') {
            exec(`msg * "${errorMsg}"`);
        } else if (platform === 'darwin') {
            exec(`osascript -e 'display alert "Compiler Error" message "${errorMsg}" as critical'`);
        } else {
            exec(`notify-send "Compiler Error" "${errorMsg}"`);
        }
        
        throw e; // Stop execution
    }
}

export function getOutputFilePath(inputPath, targetExtension, baseNameOverride, subFolder) {
    const baseName = baseNameOverride || path.basename(inputPath, path.extname(inputPath));
    return path.join(subFolder, baseName + targetExtension);
}

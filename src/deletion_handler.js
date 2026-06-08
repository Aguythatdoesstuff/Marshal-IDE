// deletion_handler.js
import * as path from 'path';
import * as fs from 'fs';
import { logToMain } from './watcher_workspace.js';
import { getPathConfig } from '../compilers/compiler_helpers.js';

/**
 * Derives the list of expected output files based on directory heuristics.
 */
function deriveOutputFileList(inputPath) {
    const FILE_EXTENSION = path.extname(inputPath);
    
    try {
        const PATH_MAP = getPathConfig(FILE_EXTENSION);

        if (!PATH_MAP) {
            return [];
        }

        const inputFilename = path.basename(inputPath);
        const baseName = inputFilename.substring(0, inputFilename.lastIndexOf('.'));
        
        const outputFiles = [];

        for (const [key, targetDir] of Object.entries(PATH_MAP)) {
            let extension = '.txt'; 
            let suffix = '';

            const checkDir = targetDir.replace(/\\/g, '/');

            // --- SPECIAL GFX RULES ---
            if (FILE_EXTENSION === '.dds') {
                // DEFINITION FILE (.gfx) -> goes to interface/handledByMarshalIDE/
                const definitionDir = path.join('interface', 'handledByMarshalIDE');
                outputFiles.push(path.join(definitionDir, `${baseName}.gfx`));

                // IMAGE FILE (.dds) -> goes to gfx/interface/handledByMarshalIDE/
                const imageDir = path.join('gfx', 'interface', 'handledByMarshalIDE');
                outputFiles.push(path.join(imageDir, `${baseName}.dds`));

                continue; // Skip standard heuristics for .dds files completely
            }
            // --- EXISTING HEURISTICS ---
            else if (checkDir.includes('localisation')) {
                extension = '.yml';
                suffix = '_l_english'; 
            } else if (checkDir.includes('interface')) {
		    // Push both .gui and .gfx versions
		    const guiFile = `${baseName}.gui`;
		    const gfxFile = `${baseName}.gfx`;
		    
		    outputFiles.push(path.join(targetDir, guiFile));
		    outputFiles.push(path.join(targetDir, gfxFile));
		    
		    continue; // Skip the default push at the bottom of the loop for this special case
            }

            const finalFilename = `${baseName}${suffix}${extension}`;
            outputFiles.push(path.join(targetDir, finalFilename));
        }

        return outputFiles;

    } catch (error) {
        logToMain('error', `Cleanup error: Could not resolve paths for ${FILE_EXTENSION}`);
        return [];
    }
}

/**
 * Deletes the compiled output files associated with a given IDE source file.
 */
function cleanupFiles(sourcePath, outputBaseDir) {
    const relativePathsToDelete = deriveOutputFileList(sourcePath);

    if (relativePathsToDelete.length === 0) {
        return; 
    }

    relativePathsToDelete.forEach(relativePath => {
        const fullPath = path.join(outputBaseDir, relativePath);

        logToMain('info', `Attempting cleanup: ${relativePath}`, 'Watcher-Cleanup');

        if (fs.existsSync(fullPath)) {
            try {
                fs.unlinkSync(fullPath);
                logToMain('info', `Deleted output file: ${path.basename(fullPath)}`, 'Watcher-Cleanup');
            } catch (err) {
                logToMain('error', `Failed to delete file: ${err.message}`);
            }
        }
    });
}

export function handleDeletion(deletedFilePath, outputBaseDir) {
    logToMain('info', `Handling deletion of: ${path.basename(deletedFilePath)}`);
    cleanupFiles(deletedFilePath, outputBaseDir);
}

export function handleRename(oldFilePath, newFilePath, outputBaseDir) {
    logToMain('info', `Handling rename: ${path.basename(oldFilePath)} -> ${path.basename(newFilePath)}`);
    cleanupFiles(oldFilePath, outputBaseDir);
}

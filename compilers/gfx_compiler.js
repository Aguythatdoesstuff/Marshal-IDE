import * as path from 'path';
import { getPathConfig } from './compiler_helpers.js';

/**
 * Image/GFX Compiler
 * Handles .dds files by moving them to the gfx directory and 
 * generating interface definitions.
 */
export function compile(ideContent, config, inputPath, allProjectFiles = []) {
    try {
        const FILE_EXTENSION = path.extname(inputPath);
        const PATH_MAP = getPathConfig(FILE_EXTENSION);
        
        const imageOutputDir = PATH_MAP.images;
        const definitionFile = PATH_MAP.definition;

        // 1. Generate blocks for ALL tracked files, not just the current one
        // allProjectFiles is now an array of file paths sent from the watcher
        const allSpriteBlocks = allProjectFiles.map(fullPath => {
            const fName = path.basename(fullPath);
            const bName = path.basename(fullPath, FILE_EXTENSION);
            const gName = `GFX_${bName}`;
            
            return [
                `    spriteType = {`,
                `        name = "${gName}"`,
                `        texturefile = "${imageOutputDir}/${fName}"`,
                `    }`
            ].join('\n');
        });

        const finalGfxContent = [
            `spriteTypes = {`,
            allSpriteBlocks.join('\n'),
            `}`
        ].join('\n');

        return {
            success: true,
            outputs: [
                {
                    path: path.join(imageOutputDir, path.basename(inputPath)),
                    content: ideContent, 
                    isBinary: true 
                },
                {
                    path: definitionFile,
                    content: finalGfxContent
                }
            ]
        };
    } catch (e) {
        return { success: false, message: `GFX Compiler Error: ${e.message}` };
    }
}

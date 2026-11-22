import { dialog, BrowserWindow } from 'electron';
import path from 'path';
import fs from 'fs-extra';
import { processImage } from '../ipcHandlers/img-converter.binary.js';


export async function executeProcessImage({ filePath, imageType, mainOutputDir, cacheOutputDir, state }) {
    if (!state.INPUT_DIR) {
        return { success: false, message: "No project loaded." };
    }

    const targetMainOutput = mainOutputDir || state.OUTPUT_DIR;
    const targetCacheOutput = cacheOutputDir || path.join(state.currentProjectConfig.project_root, 'cache', 'images');

    return await processImage(filePath, imageType, targetMainOutput, targetCacheOutput, state.handleIpcLog, state);
}

/**
 * Helper to consistently extract the exact base name (without extension) from a string path,
 * filename, or payload object, strictly preserving spaces, hyphens, and original casing,
 * deliberately bypassing any upstream slugified/sanitized safeName properties.
 */
export function getSafeName(input) {
    if (!input) return '';
    if (typeof input === 'object') {
        const p = input.path || input.filePath || input.imagePath || input.filename || input.name;
        if (p) {
            const ext = path.extname(p);
            return path.basename(p, ext);
        }
        if (input.safeName) return input.safeName;
        return '';
    }
    if (typeof input === 'string') {
        const ext = path.extname(input);
        return path.basename(input, ext);
    }
    return '';
}








export async function findFileRecursive(dir, targetName, imageType = 'fallback', state) {
    if (!(await fs.pathExists(dir))) {
        state.appLogger?.info(`[Main-Img-Scan] Directory does not exist or is inaccessible: ${dir}`, { source: 'Main-Scan' });
        return null;
    }
    
    state.appLogger?.info(`[Main-Img-Scan] Scanning directory: "${dir}" looking for base name: "${targetName}" (Image Type: ${imageType})`, { source: 'Main-Scan' });
    
    let entries;
    try {
        entries = await fs.readdir(dir, { withFileTypes: true });
    } catch (err) {
        state.appLogger?.error(`[Main-Img-Scan] Failed to read directory entries at ${dir}: ${err.message}`, { source: 'Main-Scan' });
        return null;
    }
    
    const targetLower = targetName.toLowerCase();
    const expectedExt = imageType === 'flag' ? '.tga' : '.dds';

    let primaryMatch = null;
    let fallbackMatch = null;

    for (const entry of entries) {
        const fullPath = path.join(dir, entry.name);
        if (entry.isDirectory()) {
            state.appLogger?.info(`[Main-Img-Scan] Entering subdirectory: ${fullPath}`, { source: 'Main-Scan' });
            const found = await findFileRecursive(fullPath, targetName, imageType, state);
            if (found) {
                const parsedFound = path.parse(found);
                state.appLogger?.info(`[Main-Img-Scan] Candidate found in subdirectory: ${found} (Extension: ${parsedFound.ext}, Expected: ${expectedExt})`, { source: 'Main-Scan' });
                if (parsedFound.ext.toLowerCase() === expectedExt) {
                    state.appLogger?.info(`[Main-Img-Scan] Primary match confirmed via recursion: ${found}`, { source: 'Main-Scan' });
                    return found;
                }
                if (!fallbackMatch) {
                    fallbackMatch = found;
                    state.appLogger?.info(`[Main-Img-Scan] Fallback match registered via recursion: ${found}`, { source: 'Main-Scan' });
                }
            }
        } else if (entry.isFile()) {
            const parsed = path.parse(entry.name);
            const ext = parsed.ext.toLowerCase();
            
            // Only check .dds and .tga files, matching base name case-insensitively
            if (ext === '.dds' || ext === '.tga') {
                const baseNameMatches = parsed.name.toLowerCase() === targetLower;
                state.appLogger?.info(`[Main-Img-Scan] Inspecting file: "${entry.name}" | Base Match (${parsed.name.toLowerCase()} === ${targetLower}): ${baseNameMatches} | Ext: ${ext} (Expected: ${expectedExt})`, { source: 'Main-Scan' });
                
                if (baseNameMatches) {
                    if (ext === expectedExt) {
                        primaryMatch = fullPath;
                        state.appLogger?.info(`[Main-Img-Scan] SUCCESS: Found exact primary match: ${fullPath}`, { source: 'Main-Scan' });
                        return primaryMatch;
                    } else if (!fallbackMatch) {
                        fallbackMatch = fullPath;
                        state.appLogger?.warn(`[Main-Img-Scan] WARNING: Found base match with mismatched extension (${ext} instead of ${expectedExt}): ${fullPath}`, { source: 'Main-Scan' });
                    }
                }
            }
        }
    }
    
    const finalResult = primaryMatch || fallbackMatch || null;
    if (finalResult) {
        state.appLogger?.info(`[Main-Img-Scan] Finished scanning "${dir}". Selected match: ${finalResult}`, { source: 'Main-Scan' });
    } else {
        state.appLogger?.info(`[Main-Img-Scan] Finished scanning "${dir}". No matching .dds or .tga file found for target "${targetName}".`, { source: 'Main-Scan' });
    }
    return finalResult;
}

export async function resolveImagePreview(file, size, state) {
    const safeName = getSafeName(file);
    state.appLogger?.info(`[Main-Img-Config] Starting preview resolution for safeName: "${safeName}" with requested size: "${size}"`, { source: 'Main-Config' });

    if (!safeName) {
        state.appLogger?.error(`[Main-Img-Config] Failed: Could not determine safeName from input payload: ${JSON.stringify(file)}`, { source: 'Main-Config' });
        throw new Error("Could not determine safeName for preview resolution.");
    }
    
    const isThumbnail = size === '32x32';
    const targetFileName = isThumbnail ? `${safeName}_32x32.png` : `${safeName}.png`;
    const configDir = path.join(state.METADATA_DIR, 'gfx', safeName);
    const configPath = path.join(configDir, 'config.json');
    let config = {};

    state.appLogger?.info(`[Main-Img-Config] Checking config path at: ${configPath}`, { source: 'Main-Config' });
    try {
        if (await fs.pathExists(configPath)) {
            const fileContent = await fs.readFile(configPath, 'utf8');
            config = JSON.parse(fileContent);
            state.appLogger?.info(`[Main-Img-Config] Successfully loaded config.json for "${safeName}". ImageType configured as: "${config.imageType || 'none'}"`, { source: 'Main-Config' });
        } else {
            state.appLogger?.info(`[Main-Img-Config] config.json does not exist yet at ${configPath}`, { source: 'Main-Config' });
        }
    } catch (readErr) {
        state.appLogger?.warn(`[Main-Img-Config] Failed to read or parse config.json at ${configPath}: ${readErr.message}`, { source: 'Main-Config' });
        config = {};
    }

    if (isThumbnail && config?.thumbnail) {
        state.appLogger?.info(`[Main-Img-Config] Cache hit: Found embedded thumbnail in config.json for "${safeName}"`, { source: 'Main-Config' });
        return { 
            success: true, 
            size: '32x32', 
            fileName: targetFileName, 
            base64: config.thumbnail,
            imageType: config.imageType || null
        };
    }

    if (config?.imagePreviews?.[targetFileName]) {
        state.appLogger?.info(`[Main-Img-Config] Cache hit: Found preview "${targetFileName}" in config.json imagePreviews mapping`, { source: 'Main-Config' });
        return { 
            success: true, 
            size, 
            fileName: targetFileName, 
            base64: config.imagePreviews[targetFileName],
            imageType: config.imageType || null
        };
    }

    const targetImagePath = path.join(configDir, targetFileName);
    state.appLogger?.info(`[Main-Img-Config] Checking physical preview file on disk at: ${targetImagePath}`, { source: 'Main-Config' });
    if (await fs.pathExists(targetImagePath)) {
        state.appLogger?.info(`[Main-Img-Config] Cache hit: Found physical preview file on disk at ${targetImagePath}`, { source: 'Main-Config' });
        const buffer = await fs.readFile(targetImagePath);
        const base64Data = `data:image/png;base64,${buffer.toString('base64')}`;
        return {
            success: true,
            size,
            fileName: targetFileName,
            base64: base64Data,
            imageType: config.imageType || null
        };
    }

    state.appLogger?.warn(`[Main-Img-Config] Warning: Preview (${targetFileName}) for "${safeName}" not found in cache or disk. Initiating source search and generation via executeProcessImage.`, { source: 'Main-Config' });

    const inputDir = getInputDir();
    let filePath = null;
    if (!inputDir) {
        state.appLogger?.error(`[Main-Img-Config] Error: inputDir is not defined or loaded. Cannot search for source image for "${safeName}".`, { source: 'Main-Config' });
    } else {
        let searchDir = inputDir;
        
        state.appLogger?.info(`[Main-Img-Config] Scanning inputDir root for 'gfx'/'GFX' folder case-insensitively at: ${inputDir}`, { source: 'Main-Config' });
        
        try {
            const rootEntries = await fs.readdir(inputDir, { withFileTypes: true });
            // Filter for any directory matching 'gfx' case-insensitively
            const gfxEntries = rootEntries.filter(e => e.isDirectory() && e.name.toLowerCase() === 'gfx');
            
            if (gfxEntries.length > 0) {
                // Prioritize uppercase 'GFX' if it exists in the array, otherwise take the first match
                gfxEntries.sort((a, b) => (b.name === 'GFX' ? 1 : 0) - (a.name === 'GFX' ? 1 : 0));
                searchDir = path.join(inputDir, gfxEntries[0].name);
                state.appLogger?.info(`[Main-Img-Config] Successfully located asset directory: "${searchDir}"`, { source: 'Main-Config' });
            } else {
                state.appLogger?.warn(`[Main-Img-Config] Warning: No 'gfx' or 'GFX' directory found in inputDir. Falling back to scanning entire inputDir root: "${inputDir}"`, { source: 'Main-Config' });
            }
        } catch (err) {
            state.appLogger?.error(`[Main-Img-Config] Failed to read inputDir root entries: ${err.message}. Falling back to: "${inputDir}"`, { source: 'Main-Config' });
            searchDir = inputDir;
        }

        const imageType = config.imageType || 'fallback';
        state.appLogger?.info(`[Main-Img-Config] Delegating to findFileRecursive. Target Name: "${safeName}", ImageType: "${imageType}", Search Root: "${searchDir}"`, { source: 'Main-Config' });
        
        filePath = await findFileRecursive(searchDir, safeName, imageType, state);
    }

    if (!filePath || !(await fs.pathExists(filePath))) {
        state.appLogger?.error(`[Main-Img-Config] ERROR: Source image search failed completely. No valid .dds or .tga source found for safeName "${safeName}" inside inputDir ("${inputDir}").`, { source: 'Main-Config' });
        throw new Error(`Source image not found for preview generation: ${safeName}`);
    }

    state.appLogger?.info(`[Main-Img-Config] SUCCESS: Source image located at: "${filePath}". Proceeding to trigger executeProcessImage daemon.`, { source: 'Main-Config' });

    const mainOutputDir = path.dirname(filePath);
    const cacheOutputDir = configDir;
    const imageTypeToUse = config.imageType || 'fallback';

    const result = await executeProcessImage({
        filePath,
        imageType: imageTypeToUse,
        mainOutputDir,
        cacheOutputDir
    });

    if (!result || !result.success) {
        state.appLogger?.error(`[Main-Img-Config] ERROR: executeProcessImage daemon failed for "${safeName}". Message: ${result?.message || 'Unknown error'}`, { source: 'Main-Config' });
        throw new Error(result?.message || `Failed to process image for preview: ${safeName}`);
    }

    state.appLogger?.info(`[Main-Img-Config] executeProcessImage completed successfully. Verifying generated preview output at: ${targetImagePath}`, { source: 'Main-Config' });

    if (!(await fs.pathExists(targetImagePath))) {
        state.appLogger?.error(`[Main-Img-Config] ERROR: Expected generated preview image is missing from disk at: ${targetImagePath}`, { source: 'Main-Config' });
        throw new Error(`Generated preview image not found at: ${targetImagePath}`);
    }

    const buffer = await fs.readFile(targetImagePath);
    const base64Data = `data:image/png;base64,${buffer.toString('base64')}`;

    if (isThumbnail) {
        state.appLogger?.info(`[Main-Img-Config] Embedding generated 32x32 thumbnail into config.json for "${safeName}"`, { source: 'Main-Config' });
        config.thumbnail = base64Data;
        delete config.imagePreviews;

        const tempPath = `${configPath}.${Date.now()}.${Math.random().toString(36).substring(2, 8)}.tmp`;
        await fs.outputJson(tempPath, config, { spaces: 2 });
        await fs.move(tempPath, configPath, { overwrite: true });
        state.appLogger?.info(`[Main-Img-Config] Successfully updated config.json with new thumbnail.`, { source: 'Main-Config' });
    }

    return { 
        success: true, 
        size, 
        fileName: targetFileName, 
        base64: base64Data,
        warning: 'Preview was missing and generated via executeProcessImage.',
        imageType: config.imageType || null
    };
}



export async function updateSingleImageConfig(file, state) {
    const safeName = getSafeName(file);
    
    if (!safeName) {
        throw new Error("Missing target path or safeName in payload item.");
    }

    const configDir = path.join(state.METADATA_DIR, 'gfx', safeName);
    const configPath = path.join(configDir, 'config.json');

    await fs.ensureDir(configDir);

    let existingConfig = {};
    const configExists = await fs.pathExists(configPath);

    if (!configExists) {
        state.appLogger?.error(`[Main-Img-Config] Error: config.json does not exist for ${safeName} at ${configPath}. Something may have gone wrong upstream. Proceeding to create/update.`, { source: 'Main-Config' });
    } else {
        try {
            existingConfig = await fs.readJson(configPath);
        } catch (readErr) {
            state.appLogger?.error(`[Main-Img-Config] Failed to parse existing config at ${configPath}, overwriting: ${readErr.message}`, { source: 'Main-Config' });
        }
    }

    const customFields = typeof file === 'object' ? { ...file } : {};
    delete customFields.path;
    delete customFields.filePath;
    delete customFields.imagePath;
    delete customFields.filename;
    delete customFields.safeName;

    const updatedConfig = {
        ...existingConfig,
        ...customFields,
        safeName,
        updatedAt: new Date().toISOString()
    };

    const tempPath = `${configPath}.${Date.now()}.${Math.random().toString(36).substring(2, 8)}.tmp`;
    await fs.outputJson(tempPath, updatedConfig, { spaces: 2 });
    await fs.move(tempPath, configPath, { overwrite: true });

    state.appLogger?.info(`[Main-Img-Config] Successfully updated config for ${safeName} at ${configPath}`, { source: 'Main-Config' });
    return { success: true, safeName };
}
import fs from 'fs-extra';
import path from 'path';
import crypto from 'crypto'; // Required for hashing
import { ipcMain, app } from 'electron';

const USER_DATA_PATH = app.getPath('userData');
const LICENSE_JSON_PATH = path.join(USER_DATA_PATH, 'metadata', 'license', 'license.json');

const LICENSE_MAP = {
    "d4e4d72572805dd642c25c6acb4fd77866adb5712a1dbf9da6210421a092585d": "LICENSE.txt"
};

export async function checkEulaStatus(log) {
    try {
        // 1. Check if the metadata file exists
        if (!(await fs.pathExists(LICENSE_JSON_PATH))) {
            return { valid: false };
        }

        const info = await fs.readJson(LICENSE_JSON_PATH);

        // 2. Validate the Hash against the Map
        // Check if the stored hash is actually one we recognize
        const expectedRelativePath = LICENSE_MAP[info.groupHash];
        if (!expectedRelativePath) {
            log?.warn('Invalid or unknown license hash detected.', 'EULA-Manager');
            return { valid: false };
        }

        // 3. Path Integrity Check
        // Ensure the path stored in the JSON matches the map's expectation
        const baseDir = app.isPackaged ? process.resourcesPath : app.getAppPath();
        const expectedFullPath = path.join(baseDir, expectedRelativePath);

        if (info.path !== expectedFullPath) {
            log?.warn('License path mismatch: stored path does not match map.', 'EULA-Manager');
            return { valid: false };
        }

        // 4. Physical File & Status Check
        const fileExists = await fs.pathExists(info.path);
        const isActive = info.status === 'active';

        if (fileExists && isActive) {
            return { valid: true };
        }

    } catch (e) {
        log?.error(`EULA Check Error: ${e.message}`, 'EULA-Manager');
    }

    return { valid: false };
}

export function setupEulaHandlers(mainWindow, onAccepted, log) {
    
    // Helper: resolving the path securely via hash
    const resolveSecurePath = async (rawCode) => {
        const baseDir = app.isPackaged ? process.resourcesPath : app.getAppPath();
        
        // 1. Check if a code was provided
        if (rawCode && rawCode.trim() !== "") {
            // 2. Hash the input code
            const hash = crypto.createHash('sha256').update(rawCode.trim()).digest('hex');
            
            // 3. Look up the hash in our whitelist
            const mappedRelativePath = LICENSE_MAP[hash];

            if (mappedRelativePath) {
                const fullPath = path.join(baseDir, mappedRelativePath);
                
                // 4. Verify the file actually exists before returning it
                if (await fs.pathExists(fullPath)) {
                    // Return the specific file and its name (for the UI)
                    return { 
                        filePath: fullPath, 
                        displayName: path.basename(fullPath), // e.g. "LISENCE_test_pro-v2.txt"
                        isSpecial: true
                    };
                } else {
                    log?.warn(`License mapped to ${mappedRelativePath} but file is missing.`, 'EULA-Manager');
                }
            }
        }
        
        // Fallback: Standard License
        return { 
            filePath: path.join(baseDir, 'LICENSE.txt'), 
            displayName: "Standard User License",
            isSpecial: false
        };
    };

    // Handler: Fetch Text & Name
    ipcMain.handle('get-eula-text', async (event, code) => {
        const { filePath, displayName, isSpecial } = await resolveSecurePath(code);
        
        try {
            log?.info(`Loading EULA: ${displayName}`, 'EULA-Manager');
            const content = await fs.readFile(filePath, 'utf8');
            return { content, displayName, isSpecial };
        } catch (err) {
            log?.error(`Failed to read license file: ${filePath}`, 'EULA-Manager');
            return { content: "Error: Could not load license text.", displayName: "Error", isSpecial: false };
        }
    });

// Handler: Accept & Save
ipcMain.once('accept-eula', async (event, code) => {
    const { filePath, isSpecial } = await resolveSecurePath(code);
    
    // 1. Determine the Group Hash
    // If it's a paid code, hash the code. 
    // If it's free, use a hardcoded "Free Group" hash.
    const groupHash = isSpecial 
        ? crypto.createHash('sha256').update(code.trim()).digest('hex')
        : "d4e4d72572805dd642c25c6acb4fd77866adb5712a1dbf9da6210421a092585d"; // Hash for "free-license"

    try {
        await fs.ensureDir(path.dirname(LICENSE_JSON_PATH));
        
        // 2. Save everything to the JSON
        await fs.writeJson(LICENSE_JSON_PATH, {
            path: filePath,
            groupHash: groupHash, // THIS is what isTargetedUpdate will check
            date: new Date().toISOString(),
            status: 'active'
        });

        log?.info(`EULA Accepted. Group: ${isSpecial ? 'Pro' : 'Free'}`, 'EULA-Manager');
        onAccepted();

    } catch (err) {
        log?.error(`Failed to save EULA acceptance: ${err.message}`, 'EULA-Manager');
    }
});

    // Handler: Decline
    ipcMain.once('decline-eula', () => {
        log?.warn('EULA Declined.', 'EULA-Manager');
        app.quit();
    });
}

/**
 * Checks if the incoming hash from somewhere matches the hash 
 * of the currently active license path.
 * @param {string} incomingHash 
 * @returns {Promise<boolean>}
 */
export async function matchesEulaHash(incomingHash) {
    try {
        if (!(await fs.pathExists(LICENSE_JSON_PATH))) return false; // Safety check
        const currentInfo = await fs.readJson(LICENSE_JSON_PATH);
        return currentInfo.groupHash === incomingHash;
    } catch (err) {
        return false;
    }
}

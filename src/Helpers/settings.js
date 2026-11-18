// main.js - The Electron Main Process
import path from 'path';
import fs from 'fs-extra';

export async function readSettingsFile(userPath, state) {
    try {
    const data = await fs.readFile(userPath, 'utf8');
    return JSON.parse(data);
    } catch (error) {
    if (error.code === 'ENOENT') {
        state.appLogger?.error(`Settings missing: ${path.basename(userPath)}. Creating from default.`, { source: 'Main-Settings' });
    }
    throw error;
    }
}
import { app, dialog, BrowserWindow } from 'electron';
import path from 'path';
import fs from 'fs-extra';
import AdmZip from 'adm-zip';

export function registerDocsHandlers(ipcMain, state) {
    /**
     * READ FILE (In-App)
     */
	ipcMain.handle('read-doc-file', async (event, relativePath) => {
	    try {
		const fullPath = path.join(state.__dirname, 'docs', relativePath);
		return await fs.readFile(fullPath, 'utf-8');
	    } catch (err) {
		console.error("Read error:", err);
		throw err;
	    }
	});
    
    /**
     * LIVE STRUCTURE (In-App)
     * Scans your /docs folder and includes content for live searching.
     */
    ipcMain.handle('get-docs-structure', async () => {
        const docsPath = path.join(state.__dirname, 'docs');
        async function scan(dir) {
        try {
            const entries = await fs.readdir(dir, { withFileTypes: true });
            const parts = await Promise.all(entries.map(async (entry) => {
                const fullPath = path.join(dir, entry.name);
                const relativePath = path.relative(docsPath, fullPath);
                if (entry.isDirectory()) {
                    return { name: entry.name, type: 'folder', children: await scan(fullPath) };
                } else if (entry.name.endsWith('.md')) {
                    const content = await fs.readFile(fullPath, 'utf-8');
                    return { 
                        name: entry.name.replace('.md', ''), 
                        type: 'file', 
                        path: relativePath,
                        content: content 
                    };
                }
                return null;
            }));
            return parts.filter(Boolean);
        } catch (e) { return []; }
        }
        return await scan(docsPath);
    });
}
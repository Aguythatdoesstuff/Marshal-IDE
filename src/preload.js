// preload.js

const { contextBridge, ipcRenderer, webUtils } = require('electron');

// Listener store for LogBroadcaster
const rendererListeners = new Map();
let LogBroadcasterReady = false;

// Listen for LogBroadcaster updates from the Main Process
ipcRenderer.on('log-broadcaster-update', (event, message) => {
    rendererListeners.forEach((listener) => {
        listener(message);
    });
});

// Initialize Log Broadcaster subscription
(async () => {
    try {
        const result = await ipcRenderer.invoke('get-log-broadcaster-methods');
        if (result && result.success) {
            LogBroadcasterReady = true;
            ipcRenderer.send('log-broadcaster-renderer-subscribe');
        }
    } catch (e) {
        console.error("Failed to initialize Log Broadcaster in preload:", e);
    }
})();

// UNIFIED CONTEXT BRIDGE (Exposed as a single `window.api` object)
contextBridge.exposeInMainWorld('api', {
    // --- IPC Communication Helpers ---
    send: (channel, data) => ipcRenderer.send(channel, data),
    
    invoke: (channel, data) => ipcRenderer.invoke(channel, data),

    on: (channel, func) => {
        const newFunc = (event, ...args) => func(...args);
        ipcRenderer.on(channel, newFunc);
        return () => ipcRenderer.removeListener(channel, newFunc);
    },

    // --- WebUtils Helper for Drag & Drop ---
    getPathForFile: (file) => webUtils.getPathForFile(file),

    // --- Application Navigation & Utilities ---
    switchPage: (page) => ipcRenderer.send('switch-page', page),
    openPath: (p) => ipcRenderer.invoke('open-path', { path: p }),
    getLogDirectory: () => ipcRenderer.invoke('get-log-directory'), 

    // --- Renderer Logger Bridge ---
    log: {
        info: (message, source) => ipcRenderer.send('renderer-log', { type: 'info', message, source }),
        warn: (message, source) => ipcRenderer.send('renderer-log', { type: 'warn', message, source }), 
        warning: (message, source) => ipcRenderer.send('renderer-log', { type: 'warn', message, source }), 
        error: (message, source) => ipcRenderer.send('renderer-log', { type: 'error', message, source }),
        debug: (message, source) => ipcRenderer.send('renderer-log', { type: 'debug', message, source }),
    },
    
    // --- Real-time Console & Log Broadcaster ---
    logBroadcaster: {
        broadcast: (message) => {
            if (LogBroadcasterReady) {
                ipcRenderer.send('log-broadcaster-broadcast', message);
            } else {
                console.warn("[LogBroadcaster Bridge]: Broadcast function not ready.");
            }
        },
        
        addListener: (event, listener) => {
            if (event !== 'log') {
                console.error(`[LogBroadcaster Bridge]: Only 'log' event is supported. Received: ${event}`);
                return () => {};
            }
            rendererListeners.set(listener, listener);
            return () => {
                rendererListeners.delete(listener);
            };
        },
        
        removeListener: (event, listener) => {
            if (event === 'log') {
                rendererListeners.delete(listener);
            }
        }
    }
});
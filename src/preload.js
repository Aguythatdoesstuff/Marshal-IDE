// preload.js

const { contextBridge, ipcRenderer } = require('electron');

// We use a Map to store the renderer-side listener functions.
const rendererListeners = new Map();
let LogBroadcasterReady = false;

// The Main Process will use 'log-broadcaster-update' to send data when an event is emitted.
ipcRenderer.on('log-broadcaster-update', (event, message) => {
    // When an update arrives, execute all registered renderer listeners
    rendererListeners.forEach((listener) => {
        // The console_module.js expects a single argument (logData), which is 'message' here.
        listener(message);
    });
});
// Use an IIFE to asynchronously call the IPC handler and set up the Main Process subscription
(async () => {
    try {
        // We invoke the handler just to confirm the LogBroadcaster is loaded in the Main Process
        const result = await ipcRenderer.invoke('get-log-broadcaster-methods');
        
        if (result && result.success) {
            LogBroadcasterReady = true;
            
            // Tell the main process to start sending us updates
            ipcRenderer.send('log-broadcaster-renderer-subscribe');
        }
    } catch (e) {
        console.error("Failed to initialize Log Broadcaster in preload:", e);
    }
})();


contextBridge.exposeInMainWorld('api', {
    // IPC INVOKE (Two-way communication, returns a Promise)
    invoke: (channel, data) => ipcRenderer.invoke(channel, data),

    // IPC SEND (One-way communication)
    send: (channel, data) => ipcRenderer.send(channel, data),

    // IPC on (Listening for Main Process messages)
    on: (channel, func) => {
        // We wrap the function to ensure arguments are safe (not the event object)
        const newFunc = (event, ...args) => func(...args);
        ipcRenderer.on(channel, newFunc);
        return () => ipcRenderer.removeListener(channel, newFunc);
    },

    // --- Standard Application Functions ---
    switchPage: (page) => ipcRenderer.send('switch-page', page),
    openPath: (p) => ipcRenderer.invoke('open-path', { path: p }),

    // ** Expose the IPC handler for the log path **
    getLogDirectory: () => ipcRenderer.invoke('get-log-directory'), 

    // --- Logger Bridge (For Renderer Logging) ---
    log: {
        info: (message, source) => ipcRenderer.send('renderer-log', { type: 'info', message, source }),
        warn: (message, source) => ipcRenderer.send('renderer-log', { type: 'warn', message, source }), 
        warning: (message, source) => ipcRenderer.send('renderer-log', { type: 'warn', message, source }), 
        error: (message, source) => ipcRenderer.send('renderer-log', { type: 'error', message, source }),
        debug: (message, source) => ipcRenderer.send('renderer-log', { type: 'debug', message, source }),
    },
    
    // --- LogBroadcaster Bridge (For Real-time Console Updates) ---
    logBroadcaster: {
        // NOTE: The 'log' event is handled by the permanent listener above, 
        // the public API is provided via the addListener/removeListener below.
        
        broadcast: (message) => {
            if (LogBroadcasterReady) {
                ipcRenderer.send('log-broadcaster-broadcast', message);
            } else {
                console.warn("[LogBroadcaster Bridge]: Broadcast function not ready.");
            }
        },
        
        /**
         * Adds an event listener (proxy).
         * This adds the listener locally and relies on the Main Process proxying all updates.
         * @param {string} event - The event name ('log').
         * @param {function} listener - The callback function.
         */
        addListener: (event, listener) => {
            // We only support the 'log' event here, which is the internal channel name
            if (event !== 'log') {
                console.error(`[LogBroadcaster Bridge]: Only 'log' event is supported. Received: ${event}`);
                return () => {};
            }
            // Add the listener to the local map for when updates comes in
            rendererListeners.set(listener, listener);
            // Return an unsubscribe function
            return () => {
                rendererListeners.delete(listener);
            };
        },
        
        /**
         * Removes a specific listener.
         * @param {string} event - The event name ('log').
         * @param {function} listener - The callback function to remove.
         */
        removeListener: (event, listener) => {
            if (event === 'log') {
                rendererListeners.delete(listener);
            }
        }
    }
});

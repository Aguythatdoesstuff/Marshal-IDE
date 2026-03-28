// modules/console_module.js


const ICONS = {
    EXPAND: '<svg class="w-4 h-4" viewBox="0 0 24 24" fill="currentColor"><path d="M7 14l5-5 5 5z"/></svg>', 
    COLLAPSE: '<svg class="w-4 h-4" viewBox="0 0 24 24" fill="currentColor"><path d="M7 10l5 5 5-5z"/></path></svg>',
};

const ELEMENTS = {
    consoleContainer: document.getElementById('console-container'),
    consoleOutput: document.getElementById('console-output'),
    consoleResizer: document.getElementById('console-resizer'),
    consoleToggleBtn: document.getElementById('console-toggle-btn'),
    consoleToggleIcon: document.getElementById('console-toggle-icon'),
    // Needed for resizing calculations
    editorConsoleContainer: document.getElementById('editor-console-container'), 
    consoleTitleClear: document.getElementById('console-title-clear'),
};

const STATE = {
    isConsoleMinimized: true,
    lastConsoleHeight: 150, 
    MINIMIZED_HEIGHT: 30, 
    MONACO_EDITOR: null,
};


/**
 * Initializes console state and retrieves CSS variables.
 * @param {object} monacoEditor The Monaco Editor instance.
 */
function initializeConsole(monacoEditor) {
    STATE.MONACO_EDITOR = monacoEditor;

    if (!ELEMENTS.consoleContainer) {
        console.error("CRITICAL: Console container (id='console-container') not found. Console is unusable.");
        return;
    }

    const rootStyle = getComputedStyle(document.documentElement);
    const cssHeightFull = rootStyle.getPropertyValue('--console-height-full').trim().replace('px', '');
    STATE.lastConsoleHeight = parseInt(cssHeightFull) || 150;
    
    const cssHeightMin = rootStyle.getPropertyValue('--console-height').trim().replace('px', '');
    STATE.MINIMIZED_HEIGHT = parseInt(cssHeightMin) || 30;

    // Set initial console state (minimized)
    ELEMENTS.consoleContainer.style.height = `${STATE.MINIMIZED_HEIGHT}px`;
    STATE.isConsoleMinimized = true;

    // Set initial icon if found
    if (ELEMENTS.consoleToggleIcon) {
        ELEMENTS.consoleToggleIcon.innerHTML = ICONS.EXPAND;
    } else {
        console.warn("Missing: consoleToggleIcon (id='console-toggle-icon'). Toggle function disabled.");
    }
}

/**
 * Prints a message to the console panel.
 */
function showConsoleMessage(message, isError = false) {
    // Check if the console is fully initialized
    if (!ELEMENTS.consoleOutput) return;

    const timestamp = new Date().toLocaleTimeString();
    const line = document.createElement('div');
    // Using simple styling as Tailwind classes might not be fully loaded/consistent here
    line.className = `p-1 border-b border-gray-800 text-sm ${isError ? 'text-red-400' : 'text-gray-300'}`;
    line.innerHTML = `<span class="text-gray-500 mr-2">[${timestamp}]</span> ${message}`;
    
    ELEMENTS.consoleOutput.appendChild(line);
    ELEMENTS.consoleOutput.scrollTop = ELEMENTS.consoleOutput.scrollHeight;
}

/**
 * Restores the console from a minimized state.
 */
function restoreConsole() {
    if (!ELEMENTS.consoleContainer || !STATE.isConsoleMinimized) return; // Already open or container missing

    ELEMENTS.consoleContainer.style.height = `${STATE.lastConsoleHeight}px`;
    if (ELEMENTS.consoleToggleIcon) {
        ELEMENTS.consoleToggleIcon.innerHTML = ICONS.COLLAPSE;
    }
    STATE.isConsoleMinimized = false;
    
    if (STATE.MONACO_EDITOR) {
         STATE.MONACO_EDITOR.layout();
    }
}

/**
 * Toggles the console between minimized and restored height.
 */
function toggleConsoleMinimize(e) {
    if (e && typeof e.stopPropagation === 'function') {
        e.stopPropagation();
    }

    if (!ELEMENTS.consoleContainer) return;
    
    const currentHeight = ELEMENTS.consoleContainer.clientHeight;

    if (STATE.isConsoleMinimized) {
        restoreConsole();
    } else {
        // Minimize: Save current height (if it's not the minimized height), then set to minimized
        if (currentHeight > STATE.MINIMIZED_HEIGHT + 5) { 
            STATE.lastConsoleHeight = currentHeight;
        }
        
        ELEMENTS.consoleContainer.style.height = `${STATE.MINIMIZED_HEIGHT}px`;
        if (ELEMENTS.consoleToggleIcon) {
            ELEMENTS.consoleToggleIcon.innerHTML = ICONS.EXPAND;
        }
        STATE.isConsoleMinimized = true;
    }
    
    if (STATE.MONACO_EDITOR) {
         STATE.MONACO_EDITOR.layout();
    }
}

/**
 * Attaches console-specific event listeners, including resizing.
 */
function attachConsoleEventListeners() {
    
    let isFunctional = true;
    let missingElements = [];

    // Check for critical elements first
    if (!ELEMENTS.consoleToggleBtn) { missingElements.push("consoleToggleBtn"); isFunctional = false; }
    if (!ELEMENTS.consoleTitleClear) { missingElements.push("consoleTitleClear"); isFunctional = false; }
    if (!ELEMENTS.consoleResizer) { missingElements.push("consoleResizer"); isFunctional = false; }
    if (!ELEMENTS.editorConsoleContainer) { missingElements.push("editorConsoleContainer"); isFunctional = false; }

    if (!isFunctional) {
        console.error(`Cannot attach console event listeners: The following required elements are missing: ${missingElements.join(', ')}. Please check your index.html.`);
        return;
    }


    // Console Toggles
    ELEMENTS.consoleToggleBtn.addEventListener('click', toggleConsoleMinimize);
    ELEMENTS.consoleTitleClear.addEventListener('click', (e) => {
        e.stopPropagation();
        if (ELEMENTS.consoleOutput) {
            // Clears and adds the single system message line
            ELEMENTS.consoleOutput.innerHTML = '<div class="p-1 text-gray-400">[System] Console Cleared.</div>';
        }
    });
    
    // --- Resizing Event Handlers (Horizontal) ---
    let isResizing = false;

    const startResizing = () => {
        
        // If minimized, restore it immediately upon starting the resize
        if (STATE.isConsoleMinimized) {
            restoreConsole();
        }
        
        isResizing = true;
        document.body.style.cursor = 'row-resize';
        document.body.style.userSelect = 'none';
    };

    const stopResizing = () => {
        if (isResizing) {
            isResizing = false;
            document.body.style.cursor = '';
            document.body.style.userSelect = '';
            if (STATE.MONACO_EDITOR) {
                 STATE.MONACO_EDITOR.layout();
            }
        }
    };

    const resizeHandler = (e) => {
        if (!isResizing) return;

        // Horizontal resizing (Console)
        const editorConsoleContainer = ELEMENTS.editorConsoleContainer;
        const mainContentRect = editorConsoleContainer.getBoundingClientRect();
        const mouseYRelativeToMain = e.clientY - mainContentRect.top;
        const totalHeight = mainContentRect.height;
        const newConsoleHeight = totalHeight - mouseYRelativeToMain;

        const max = totalHeight * 0.8;
        const min = STATE.MINIMIZED_HEIGHT;
        const finalHeight = Math.min(Math.max(newConsoleHeight, min), max);

        ELEMENTS.consoleContainer.style.height = `${finalHeight}px`;
        
        // Correctly manage console state during drag
        if (finalHeight > STATE.MINIMIZED_HEIGHT + 5) {
            STATE.lastConsoleHeight = finalHeight;
            // Ensure the state is marked as restored if dragged open
            if (STATE.isConsoleMinimized) {
                STATE.isConsoleMinimized = false;
                if (ELEMENTS.consoleToggleIcon) {
                    ELEMENTS.consoleToggleIcon.innerHTML = ICONS.COLLAPSE;
                }
            }
        } else if (finalHeight <= STATE.MINIMIZED_HEIGHT + 5) {
            // If dragged down to minimum height, consider it minimized
            if (!STATE.isConsoleMinimized) {
                STATE.isConsoleMinimized = true;
                if (ELEMENTS.consoleToggleIcon) {
                    ELEMENTS.consoleToggleIcon.innerHTML = ICONS.EXPAND;
                }
            }
        }
        
        if (STATE.MONACO_EDITOR) {
            STATE.MONACO_EDITOR.layout();
        }
    };

    // Attach listeners to resizer
    ELEMENTS.consoleResizer.addEventListener('mousedown', startResizing);
    document.addEventListener('mousemove', resizeHandler);
    document.addEventListener('mouseup', stopResizing);

    // Add touch support for resizer
    ELEMENTS.consoleResizer.addEventListener('touchstart', (e) => {
        e.preventDefault(); // Prevent scrolling
        startResizing();
    });
    document.addEventListener('touchmove', (e) => {
        if (!isResizing || !e.touches[0]) return;
        // Use the touch event's clientY for resizing
        resizeHandler({ clientY: e.touches[0].clientY });
    });
    document.addEventListener('touchend', stopResizing);
}

const handleIncomingLog = (logData) => {
    const isError = logData.level.toLowerCase() === 'error' || logData.level.toLowerCase() === 'warn';

    const formattedMessage = `[${logData.source}][${logData.level.toUpperCase()}]: ${logData.message}`;

    showConsoleMessage(formattedMessage, isError);
};

/**
 * Attaches the listener to the log broadcaster.
 */
function startLogListener() {
    console.log('Log Display initialized and ready to receive broadcasts...');

    window.api.logBroadcaster.addListener('log', handleIncomingLog);
}

/**
 * Removes the listener from the log broadcaster.
 */
function stopLogListener() {
    console.log('Stopping log broadcast listener...');

    window.api.logBroadcaster.removeListener('log', handleIncomingLog);
}



export { 
    showConsoleMessage, 
    toggleConsoleMinimize, 
    initializeConsole, 
    attachConsoleEventListeners,
    startLogListener,
    stopLogListener 
};

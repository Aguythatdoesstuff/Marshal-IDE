// ide_renderer.js
import { 
    toggleConsoleMinimize, 
    initializeConsole, 
    attachConsoleEventListeners,
    startLogListener,
    stopLogListener
} from '../modules/console_module.js';

// --- Icons and State ---
const ICONS = {
    SAVE: '<svg class="w-4 h-4" viewBox="0 0 24 24" fill="currentColor"><path d="M17 3H5C3.89 3 3 3.89 3 5v14c0 1.1.89 2 2 2h14c1.1 0 2-.9 2-2V7l-4-4zm-5 16c-1.66 0-3-1.34-3-3s1.34-3 3-3 3 1.34 3 3-1.34 3-3 3zm3-10H5V5h10v4z"/></svg>',
    FOLDER: '<svg class="w-4 h-4" fill="currentColor" viewBox="0 0 24 24"><path d="M10 4H4c-1.11 0-2 .89-2 2v12a2 2 0 002 2h16a2 2 0 002-2V8c0-1.11-.89-2-2-2h-8l-2-2z"/></svg>',
    FILE: '<svg class="w-4 h-4" fill="currentColor" viewBox="0 0 24 24"><path d="M14 2H6a2 2 0 00-2 2v16a2 2 0 002 2h12a2 2 0 002-2V8l-6-6zM15 11h-2v2h-2v-2H9V9h2V7h2v2h2v2z"/></svg>',
};

/**
 * Mapping of folder names to required file extensions.
 * Folder names are converted to lowercase for lookup.
 */
const FILE_EXTENSION_MAP = {
    'decisions': '.decision',
    'events': '.event',
    'scripted gui': '.scriptedgui',
    'scripts': '.script',
    'ideas': '.idea',
    'focuses': '.focus'
};

const ELEMENTS = {
    // Main UI containers
    fileTreeContainer: document.getElementById('file-tree-container'),
    editorConsoleContainer: document.getElementById('editor-console-container'),
    editorContainer: document.getElementById('editor-container'),
    // Resizers
    sidebarResizer: document.getElementById('sidebar-resizer'),
    importImageBtn: document.getElementById('import-image-btn'),
    // consoleResizer is now only used in the module
    sidebar: document.getElementById('sidebar'),
    // Buttons
    saveFileBtn: document.getElementById('save-file-btn'),
    backToModsBtn: document.getElementById('back-to-mods-btn'),
    // Context Menu & Modals
    contextMenu: document.getElementById('context-menu'),
    actionModal: document.getElementById('action-modal'),
    actionTitle: document.getElementById('action-title'),
    actionMessage: document.getElementById('action-message'),
    actionInput: document.getElementById('action-input'),
    actionInputContainer: document.getElementById('action-input-container'),
    confirmActionBtn: document.getElementById('confirm-action-btn'),
    cancelActionBtn: document.getElementById('cancel-action-btn'),
};

const STATE = {
    PROJECT_NAME: null,
    INPUT_DIR: null,
    OUTPUT_DIR: null,
    CURRENT_FILE_PATH: null,
    CURRENT_PROJECT_CONFIG: null,
    GLOBAL_COMPILERS: null,
    MONACO_EDITOR: null,
    IS_DIRTY: false,
    ACTIVE_RESIZER: null,
    CONTEXT_PATH: null,
    pendingAction: null, // For modal operations
};


/**
 * Defines the custom DSL languages and a shared theme using Monaco Monarch.
 * @param {object} monaco The monaco global object.
 */
function defineDslLanguages(monaco) {
    
    monaco.editor.defineTheme('myDslTheme', {
        base: 'vs-dark', 
        inherit: true,
        rules: [
            { token: 'comment', foreground: '008000' },
            { token: 'comment.block', foreground: '008000' },
            { token: 'string.quote', foreground: 'CE9178' }, 
            { token: 'string.content', foreground: 'CE9178' }, 
            { token: 'number', foreground: 'B5CEA8' },
            
            { token: 'constant.boolean', foreground: '569CD6' }, 
            { token: 'constant.scope', foreground: '9CDCFE' }, 

            { token: 'operator', foreground: 'DCDCAA' }, 
            { token: 'brackets', foreground: 'DCDCAA' },
            { token: 'id.embedded', foreground: 'DCDCAA' }, 
            
            { token: 'primary.keyword', foreground: '569CD6' }, 
            { token: 'control.keyword', foreground: 'C586C0' }, 
            
            { token: 'id.special', foreground: 'FFD700' },     
            { token: 'id.general', foreground: '9CDCFE' },    
            { token: 'id.declaration', foreground: 'FFAC33' }, 
        ],
        colors: {}
    });

    const languageDef = {
        primaryKeywords: ['country', 'event', 'title', 'desc', 'option', 'namespace', 'text', 'define', 'window',
        	'size', 'position', 'visible', 'icon', 'static', 'tooltip', 'dynamic', 'font', 'checked', 'gridbox'
        	, 'slotsize', 'format', 'array', 'template', 'button', 'on', 'overlap', 'var', 'sprite', 'bar', 'checkbox', 'click', 'scripted', 'effect'
        	, 'name', 'group', 'default', 'game', 'rule', 'trigger', 'action', 'decision', 'category', 'idea', 'tree', 'for', 'focus', 'cost', 'draggable', 'available', 'takes', 'days', 'day', 'complete', 'news', 'follow', 'of', 'require', 'prevents', 'max', 'full', 'empty', 'with', 'steps', 'horizontal', 'unprogressed', 'color', 'progressed', 'priority', 'allowed', 'available'
        ],
        controlKeywords: ['if', 'else', 'else_if', 'then', 'not', 'and', 'or', 'limit'],
        scopes: ['ROOT', 'FROM'],
        booleans: ['true', 'false', 'yes', 'no'],
        
        specialIDs: /[A-Z]{3}\.\d+/, 
        
        tokenizer: {
            root: [
                [/(country)(\s+)(event)(\s+)([a-zA-Z_$][\w$]*)/, ['primary.keyword', 'white', 'primary.keyword', 'white', 'id.declaration']],
                [/(title|desc|option|namespace)(\s+)([a-zA-Z_$][\w$]*)/, ['primary.keyword', 'white', 'id.declaration']],
                
                [/\b(AND|and|NOT|not)\b/, 'control.keyword'],

                [/[a-zA-Z_$][\w$]*/, {
                    cases: {
                        '@primaryKeywords': 'primary.keyword', 
                        '@controlKeywords': 'control.keyword',
                        '@scopes': 'constant.scope',     // Match ROOT, FROM
                        '@booleans': 'constant.boolean', // Match true, false, yes, no
                        '@default': 'id.general'            
                    }
                }],

                [/@specialIDs/, 'id.special'],
                [/\s+/, 'white'],
                [/#{/, { token: 'comment.block', next: '@blockComment' }], 
                [/#.*$/, 'comment'], 
                
                [/"/, { token: 'string.quote', bracket: '@open', next: '@string' }],
                
                [/[=!><\+\-\*\/&|@:?.]+/, 'operator'],
                [/[0-9]+/, 'number'],
                [/[{}()\[\]]/, 'brackets'], 
            ],
            
            string: [
                [/\[/, { token: 'brackets', next: '@stringEmbedded' }],
                [/[^\\\"\[]+/, 'string.content'],
                [/\\./, 'string.escape'],
                [/"/, { token: 'string.quote', bracket: '@close', next: '@pop' } ],
            ],

            stringEmbedded: [
                [/\]/, { token: 'brackets', next: '@pop' }],
                [/[=!><\+\-\*\/&|@:?.]+/, 'operator'],
                [/[a-zA-Z_$][\w$]*/, {
                    cases: {
                        '@scopes': 'constant.scope',
                        '@booleans': 'constant.boolean',
                        '@default': 'id.embedded'
                    }
                }], 
                [/[0-9]+/, 'operator'],              
                [/\s+/, 'white'],
            ],
            
            blockComment: [
                [/#\}/, { token: 'comment.block', next: '@pop' }], 
                [/[^#\n]+/, 'comment.block'],                      
                [/#/, 'comment.block'],                            
                [/\n/, 'comment.block']                            
            ]
        },
    };

    monaco.languages.register({ id: 'eventLang' });
    monaco.languages.setMonarchTokensProvider('eventLang', languageDef);

    monaco.languages.register({ id: 'decisionLang' });
    monaco.languages.setMonarchTokensProvider('decisionLang', languageDef);
    
    monaco.languages.register({ id: 'scriptedguiLang' });
    monaco.languages.setMonarchTokensProvider('scriptedguiLang', languageDef);

    monaco.languages.register({ id: 'scriptsLang' });
    monaco.languages.setMonarchTokensProvider('scriptsLang', languageDef);
    
    monaco.languages.register({ id: 'ideaLang' });
    monaco.languages.setMonarchTokensProvider('ideaLang', languageDef);
    
    monaco.languages.register({ id: 'focusLang' });
    monaco.languages.setMonarchTokensProvider('focusLang', languageDef);
}


// --- Monaco Editor Initialization ---

function initMonaco() {
    require(['vs/editor/editor.main'], () => {
        ELEMENTS.editorContainer.innerHTML = ''; 

        defineDslLanguages(monaco);

        STATE.MONACO_EDITOR = monaco.editor.create(ELEMENTS.editorContainer, {
            value: 'Select a file to begin editing.',
            language: 'plaintext', 
            theme: 'myDslTheme', 
            fixedOverflowWidgets: true,
            automaticLayout: true, 
            minimap: { enabled: false },
            readOnly: true, 
            links: false,
            overviewRerenderLanes: 0,
        });
        STATE.MONACO_EDITOR.addCommand(monaco.KeyMod.CtrlCmd | monaco.KeyCode.KeyS, () => {
            triggerSaveShortcut();
        });
        STATE.MONACO_EDITOR.onDidChangeModelContent(() => {
            if (STATE.CURRENT_FILE_PATH) {
                const isDirty = STATE.MONACO_EDITOR.isModelDirty();
                setDirtyState(isDirty);
            }
        });
        
        // Custom Monaco function to check if content changed from initial load
        STATE.MONACO_EDITOR.isModelDirty = () => {
            if (!STATE.MONACO_EDITOR.getModel()) return false;
            const model = STATE.MONACO_EDITOR.getModel();
            return model.getValue() !== model._initialContent; 
        }

        // nitialize console module with the editor instance
        initializeConsole(STATE.MONACO_EDITOR);
        startLogListener();
    });
}

/**
 * Normalizes paths by replacing backslashes with forward slashes
 * and removing trailing slashes to prevent logic errors.
 */
function normalizePath(path) {
    if (typeof path !== 'string') return '';
    return path.replace(/\\/g, '/').replace(/\/+$/, '');
}

/**
 * Updates the UI and internal state when the editor content changes.
 */
function setDirtyState(isDirty) {
    STATE.IS_DIRTY = isDirty;
    ELEMENTS.saveFileBtn.disabled = !isDirty;

    let title = 'Marshal IDE - Mod';
    if (STATE.PROJECT_NAME) {
         title = `Marshal IDE - ${STATE.PROJECT_NAME}`;
    }
    
    if (STATE.CURRENT_FILE_PATH) {
        title = `${title} - ${STATE.CURRENT_FILE_PATH.split('/').pop()}`;
    }
    
    if (isDirty) {
        title = `* ${title}`;
    }
    document.getElementById('window-title').textContent = title;
}


// --- Project and File Handling ---

/**
 * Loads the project configuration after selection.
 */
async function loadProject(projectName) {
    window.api.log.info(`[System] Attempting to load project: ${projectName}...`, 'ide-Renderer');
    
    try {
        const result = await window.api.invoke('load-project', projectName);
        
        if (result.success) {
            STATE.PROJECT_NAME = projectName;
            STATE.CURRENT_PROJECT_CONFIG = result.config;
            STATE.GLOBAL_COMPILERS = result.globalCompilers;
            STATE.INPUT_DIR = result.config.input_dir;
            
            document.getElementById('current-project-display').textContent = `Project: ${projectName}`;
            
            window.api.log.info(`[SUCCESS] Project configuration loaded.`, 'ide-Renderer');
            // Update title
            setDirtyState(false);
            loadDirectoryTree(projectName); 
            
        } else {
            window.api.log.error(`[ERROR] Failed to load project config: ${result.message}`, 'ide-Renderer');
            setTimeout(() => window.api.send('switch-page', 'index'), 2000);
        }
    } catch (error) {
        window.api.log.error(`[ERROR] IPC Error on load-project: ${error.message}`, 'ide-Renderer');
        setTimeout(() => window.api.send('switch-page', 'index'), 2000);
    }
}


/**
 * Loads the content of a file into the Monaco Editor.
 * @param {string} filePath The relative path to the file inside the project.
 */
async function loadFileContent(filePath) {
    if (STATE.IS_DIRTY && STATE.CURRENT_FILE_PATH !== filePath) {
        window.api.log.warn(`[WARNING] Changes to ${STATE.CURRENT_FILE_PATH?.split('/').pop()} not saved.`, 'ide-Renderer');
    }
    
    document.querySelectorAll('.file-item').forEach(item => item.classList.remove('active'));
    const activeItem = document.querySelector(`.file-item[data-path="${filePath}"]`);
    if (activeItem) {
        activeItem.classList.add('active');
    }
    
    window.api.log.info(`[System] Loading file: ${filePath.split('/').pop()}...`, 'ide-Renderer');
    
    try {
        const result = await window.api.invoke('get-file-content', { filePath }); 
        
        if (result.success) {
            const ext = filePath.split('.').pop();
            const language = getMonacoLanguage(ext); 

            if (STATE.MONACO_EDITOR) {
                STATE.CURRENT_FILE_PATH = filePath;
                
                const model = monaco.editor.createModel(result.content, language);
                STATE.MONACO_EDITOR.setModel(model);

                STATE.MONACO_EDITOR.getModel()._initialContent = result.content; 
                setDirtyState(false); 
                
                STATE.MONACO_EDITOR.updateOptions({ readOnly: false });
                
                window.api.log.info(`[SUCCESS] File loaded and ready for editing.`, 'ide-Renderer');
            }
        } else {
            window.api.log.error(`[ERROR] Failed to read file: ${result.message}`, 'ide-Renderer');
            STATE.MONACO_EDITOR.updateOptions({ readOnly: true });
        }
    } catch (error) {
        window.api.log.error(`[ERROR] IPC Error on get-file-content: ${error.message}`, 'ide-Renderer');
    }
}

/**
 * Maps a file extension to the corresponding Monaco Language ID.
 */
function getMonacoLanguage(ext) {
    const extension = ext.toLowerCase();
    switch (extension) {
        case 'event':
            return 'eventLang'; 
        case 'decision':
            return 'decisionLang'; 
        case 'scriptedgui':
            return 'scriptedguiLang'; 
        case 'script':
            return 'scriptsLang'; 
        case 'idea':
            return 'ideaLang'; 
        case 'focus':
            return 'focusLang'; 
        default:
            return 'plaintext';
    }
}
/**
 * Shared logic for the Ctrl+S shortcut to ensure consistency
 * across both the global window listener and the Monaco command.
 */
function triggerSaveShortcut(e) {
    if (e && typeof e.preventDefault === 'function') e.preventDefault();

    if (STATE.IS_DIRTY && !ELEMENTS.saveFileBtn.disabled) {
        saveActiveFile();
    } else if (STATE.CURRENT_FILE_PATH && !STATE.IS_DIRTY) {
        window.api.log.info("[System] File is already saved.", 'ide-Renderer');
    } else if (!STATE.CURRENT_FILE_PATH) {
        window.api.log.warn("[WARNING] Cannot save: No file is open.", 'ide-Renderer');
    }
}
/**
 * Handles saving the currently active file.
 */
async function saveActiveFile() {
    if (!STATE.CURRENT_FILE_PATH || !STATE.MONACO_EDITOR || !STATE.IS_DIRTY) {
        window.api.log.warn("[WARNING] No active, dirty file selected or editor not ready.", 'ide-Renderer');
        return;
    }
    
    const content = STATE.MONACO_EDITOR.getValue();
    
    window.api.log.info(`[System] Saving file: ${STATE.CURRENT_FILE_PATH.split('/').pop()}...`, 'ide-Renderer');
    ELEMENTS.saveFileBtn.disabled = true;

    try {
        const result = await window.api.invoke('save-file', {
            filePath: STATE.CURRENT_FILE_PATH,
            content: content
        });
        
        if (result.success) {
            window.api.log.info(`[SUCCESS] ${result.message}`, 'ide-Renderer');
            STATE.MONACO_EDITOR.getModel()._initialContent = content;
            setDirtyState(false);
        } else {
            window.api.log.error(`[ERROR] Save failed: ${result.message}`, 'ide-Renderer');
            ELEMENTS.saveFileBtn.disabled = false;
        }
    } catch (error) {
        window.api.log.error(`[ERROR] IPC Error on save-file: ${error.message}`, 'ide-Renderer');
        ELEMENTS.saveFileBtn.disabled = false;
    }
}


// --- Directory Tree Logic ---

async function loadDirectoryTree(dirPath) {
    const pathArg = dirPath === STATE.PROJECT_NAME ? STATE.INPUT_DIR : dirPath; 

    window.api.log.info(`[System] Loading directory tree for: ${dirPath}...`, 'ide-Renderer');
    
    if (dirPath === STATE.PROJECT_NAME) { 
        ELEMENTS.fileTreeContainer.innerHTML = '';
        
        const rootItem = document.createElement('div');
        rootItem.className = 'project-root-item flex items-center space-x-2 cursor-default'; 
        rootItem.innerHTML = `${ICONS.FOLDER}<span>${STATE.PROJECT_NAME}</span>`;
        ELEMENTS.fileTreeContainer.appendChild(rootItem);
        
        const childContainer = document.createElement('div');
        childContainer.id = 'root-child-container';
        childContainer.className = 'folder-contents-children'; 
        ELEMENTS.fileTreeContainer.appendChild(childContainer);
    }
    
    try {
        const result = await window.api.invoke('list-directory-contents', { dirPath: pathArg });
        
        if (result.success) {
            const container = document.getElementById('root-child-container') || document.querySelector(`.folder-contents-children[data-path="${dirPath}"]`);
            
            if (container) {
                const parentItemElement = container.parentElement.closest('.file-item');
                let level = 1;
                if (parentItemElement && parentItemElement.style.paddingLeft) {
                    level = (parseInt(parentItemElement.style.paddingLeft.replace('px', '')) / 10) + 1;
                } else if (dirPath === STATE.PROJECT_NAME) {
                    level = 1;
                }
                
                renderDirectoryContents(container, result.contents, level);
                window.api.log.info("[SUCCESS] Directory tree contents loaded.", 'ide-Renderer');
            }
        } else {
            window.api.log.error(`[ERROR] Failed to load directory contents: ${result.message}`, 'ide-Renderer');
        }
    } catch (error) {
        window.api.log.error(`[ERROR] IPC Error on list-directory-contents: ${error.message}`, 'ide-Renderer');
    }
}

function renderDirectoryContents(parentElement, contents, level = 1) {
    parentElement.innerHTML = ''; 

    contents.sort((a, b) => {
        if (a.isDir && !b.isDir) return -1;
        if (!a.isDir && b.isDir) return 1;
        return a.name.localeCompare(b.name);
    });

    contents.forEach(item => {
        const itemElement = document.createElement('div');
        itemElement.className = 'file-item transition duration-100';
        
        const extraIndent = (level * 10);
        itemElement.style.paddingLeft = `${extraIndent + 8}px`;
        itemElement.setAttribute('data-path', item.path);
        itemElement.setAttribute('data-is-dir', item.isDir);

        const iconHtml = item.isDir ? ICONS.FOLDER : ICONS.FILE;
        
        if (item.isDir) {
            const toggleIcon = document.createElement('span');
            toggleIcon.className = 'folder-toggle-icon transform rotate-0';
            toggleIcon.innerHTML = '&#9658;';
            
            itemElement.innerHTML = toggleIcon.outerHTML + iconHtml + `<span>${item.name}</span>`;
            
            const subContainer = document.createElement('div');
            subContainer.className = 'folder-contents-children hidden';
            subContainer.setAttribute('data-path', item.path);

            itemElement.addEventListener('click', (e) => {
                e.stopPropagation();
                toggleFolder(itemElement, subContainer, item.path, level + 1);
            });
            
            parentElement.appendChild(itemElement);
            parentElement.appendChild(subContainer);
        } else {
            const alignmentSpacer = '<span style="width: 12px; display: inline-block;"></span>';
            itemElement.innerHTML = alignmentSpacer + iconHtml + `<span>${item.name}</span>`;
            itemElement.addEventListener('click', (e) => {
                e.stopPropagation();
                loadFileContent(item.path);
            });
            parentElement.appendChild(itemElement);
        }
        
        itemElement.addEventListener('contextmenu', (e) => {
            e.preventDefault();
            showContextMenu(e, item.path, item.isDir);
        });
    });
}

async function toggleFolder(itemElement, subContainer, dirPath, newLevel) {
    const isCollapsed = subContainer.classList.contains('hidden');
    const toggleIcon = itemElement.querySelector('.folder-toggle-icon');

    if (isCollapsed) {
        subContainer.classList.remove('hidden');
        toggleIcon.style.transform = 'rotate(90deg)';

        if (subContainer.children.length === 0) {
            window.api.log.info(`[System] Expanding folder: ${dirPath.split('/').pop()}`, 'ide-Renderer');
            try {
                const result = await window.api.invoke('list-directory-contents', { dirPath });
                if (result.success) {
                    renderDirectoryContents(subContainer, result.contents, newLevel);
                } else {
                    subContainer.innerHTML = `<p class="text-red-400 text-xs p-2" style="padding-left: ${newLevel * 10 + 8}px;">Error loading contents: ${result.message}</p>`;
                    window.api.log.error(`[ERROR] Failed to load folder contents: ${result.message}`, 'ide-Renderer');
                }
            } catch (error) {
                window.api.log.error(`[ERROR] IPC Error loading folder: ${error.message}`, 'ide-Renderer');
            }
        }
    } else {
        subContainer.classList.add('hidden');
        toggleIcon.style.transform = 'rotate(0deg)';
    }
}

/**
 * Loads and re-renders the contents of a specific folder without refreshing the entire tree.
 * @param {string} dirPath The path of the directory to refresh (relative to the project root).
 */
async function refreshFolder(dirPath) {
    let container;
    let newLevel;
    
    if (dirPath === STATE.PROJECT_NAME || dirPath === STATE.INPUT_DIR || dirPath === '') {
        return loadDirectoryTree(STATE.PROJECT_NAME);
    }
    
    const itemElement = document.querySelector(`.file-item[data-path="${dirPath}"]`);
    container = document.querySelector(`.folder-contents-children[data-path="${dirPath}"]`);
    
    if (!container || !itemElement) {
        window.api.log.warn(`[WARNING] Could not find folder container for targeted refresh. Doing full tree reload.`, 'ide-Renderer');
        return loadDirectoryTree(STATE.PROJECT_NAME);
    }

    container.classList.remove('hidden');
    const toggleIcon = itemElement.querySelector('.folder-toggle-icon');
    if (toggleIcon) toggleIcon.style.transform = 'rotate(90deg)';
    
    const parentFileItem = itemElement.closest('.folder-contents-children').parentElement.closest('.file-item');
    if (parentFileItem && parentFileItem.style.paddingLeft) {
        newLevel = (parseInt(parentFileItem.style.paddingLeft.replace('px', '')) / 10) + 1;
    } else {
        newLevel = 1;
    }

    window.api.log.info(`[System] Refreshing folder: ${dirPath.split('/').pop()}`, 'ide-Renderer');
    try {
        const result = await window.api.invoke('list-directory-contents', { dirPath });
        if (result.success) {
            renderDirectoryContents(container, result.contents, newLevel);
        } else {
            window.api.log.error(`[ERROR] Failed to refresh folder: ${result.message}`, 'ide-Renderer');
        }
    } catch (error) {
        window.api.log.error(`[ERROR] IPC Error refreshing folder: ${error.message}`, 'ide-Renderer');
    }
}


// --- Context Menu and Modal Logic ---

/**
 * Shows the context menu and conditionally hides 'Delete' and 'Rename' for folders.
 */
function showContextMenu(e, path, isDir) {
    STATE.CONTEXT_PATH = path;
    ELEMENTS.contextMenu.classList.add('hidden');
    
    ELEMENTS.contextMenu.style.left = `${e.clientX}px`;
    ELEMENTS.contextMenu.style.top = `${e.clientY}px`;
    
    const deleteItem = ELEMENTS.contextMenu.querySelector('[data-action="delete"]');
    const renameItem = ELEMENTS.contextMenu.querySelector('[data-action="rename"]');
    const newFileItem = ELEMENTS.contextMenu.querySelector('[data-action="new-file"]'); 
    const refreshItem = ELEMENTS.contextMenu.querySelector('[data-action="refresh"]'); 

    if (isDir) {
        // Folders: Hide Delete/Rename, Show New File, Show Refresh
        deleteItem.classList.add('hidden');
        renameItem.classList.add('hidden');
        newFileItem.classList.remove('hidden'); 
        refreshItem.classList.remove('hidden');
    } else {
        // Files: Show Delete/Rename, Hide New File, Show Refresh
        deleteItem.classList.remove('hidden');
        renameItem.classList.remove('hidden');
        newFileItem.classList.add('hidden'); 
        refreshItem.classList.remove('hidden'); 
    }

    ELEMENTS.contextMenu.classList.remove('hidden');
    
    const hideMenu = (event) => {
        if (!ELEMENTS.contextMenu.contains(event.target)) {
            ELEMENTS.contextMenu.classList.add('hidden');
            document.removeEventListener('click', hideMenu);
        }
    };
    setTimeout(() => document.addEventListener('click', hideMenu), 100);
}

/**
 * Displays the action modal, with special logic for new-file based on folder extension map.
 */
function showActionModal(actionType, path, isDir) {
    STATE.pendingAction = { action: actionType, currentPath: path, isDir: isDir, forcedExtension: null };
    ELEMENTS.actionModal.classList.remove('hidden');
    ELEMENTS.actionInputContainer.classList.add('hidden');
    ELEMENTS.actionInput.value = '';
    ELEMENTS.actionInput.placeholder = '';

    switch (actionType) {
        case 'new-file':
            const parentPath = isDir ? path : path.substring(0, path.lastIndexOf('/'));
            const folderName = parentPath.split('/').pop().toLowerCase();
            const forcedExtension = FILE_EXTENSION_MAP[folderName] || '.event'; 
            
            STATE.pendingAction.currentPath = parentPath;
            STATE.pendingAction.forcedExtension = forcedExtension; 

            ELEMENTS.actionInputContainer.classList.remove('hidden');

            ELEMENTS.actionTitle.textContent = `Create New File`;
            ELEMENTS.actionMessage.innerHTML = `Enter the new **file name**.<br/>The extension will be automatically set to <code class="text-primary-blue">${forcedExtension}</code>.`;
            ELEMENTS.actionInput.placeholder = 'e.g., new_file_name';
            
            ELEMENTS.confirmActionBtn.textContent = 'Create';
            ELEMENTS.confirmActionBtn.onclick = () => handleModalAction('new-file', true);
            break;
	case 'rename':
	    const currentName = path.split(/[/\\]/).pop(); 
	    
	    // Store the extension in STATE so we can enforce it on confirm
	    const lastDotIndex = currentName.lastIndexOf('.');
	    STATE.pendingAction.originalExtension = lastDotIndex !== -1 ? currentName.substring(lastDotIndex) : '';
	    
	    // Display name without extension
	    const nameWithoutExt = lastDotIndex !== -1 ? currentName.substring(0, lastDotIndex) : currentName;

	    ELEMENTS.actionTitle.textContent = `Rename File`;
	    ELEMENTS.actionMessage.textContent = `Enter the new name for ${currentName} (extension will be preserved):`;
	    ELEMENTS.actionInputContainer.classList.remove('hidden');
	    ELEMENTS.actionInput.value = nameWithoutExt; 
	    ELEMENTS.confirmActionBtn.textContent = 'Rename';
	    ELEMENTS.confirmActionBtn.onclick = () => handleModalAction('rename', true);
	    break;
    break;
        case 'delete':
            ELEMENTS.actionTitle.textContent = `Delete File`;
            ELEMENTS.actionMessage.textContent = `Are you sure you want to permanently delete: ${path.split('/').pop()}? This action cannot be undone.`;
            ELEMENTS.confirmActionBtn.textContent = 'Delete';
            ELEMENTS.confirmActionBtn.onclick = () => handleModalAction('delete', false);
            break;
    case 'import-image': {
        const rawFileName = path.split(/[/\\]/).pop();
        const baseName = rawFileName.replace(/\.[^/.]+$/, "");
        // Force underscores and lowercase for clausewitz engine requirements and stability
        const sanitizedStart = baseName.replace(/\s+/g, '_').replace(/[^a-z0-9_]/gi, '');
        
        STATE.pendingAction.sourcePath = path;
        STATE.pendingAction.forcedExtension = '.dds';

        ELEMENTS.actionInputContainer.classList.remove('hidden');
        ELEMENTS.actionTitle.textContent = `Import Image Asset`;
        ELEMENTS.actionMessage.innerHTML = `
            <div class="text-xs text-gray-400 mb-2">Importing: <span class="text-white">${rawFileName}</span></div>
            <div class="p-2 bg-black/30 rounded border border-gray-700 text-xs mb-4">
                Destination: <span class="text-primary-blue">/GFX/</span>
            </div>
        `;
        
        ELEMENTS.actionInput.value = sanitizedStart;
        ELEMENTS.confirmActionBtn.textContent = 'Import Asset';
        
        // Strict sanitization: No spaces, no weird characters
        ELEMENTS.actionInput.oninput = (e) => {
            e.target.value = e.target.value.replace(/\s+/g, '_').replace(/[^a-z0-9_]/gi, '');
        };
        break;
    }
    }
    if (!ELEMENTS.actionInputContainer.classList.contains('hidden')) {
        setTimeout(() => ELEMENTS.actionInput.focus(), 100);
    }
}

async function handleModalAction(actionType, requiresInput) {
    const { action, currentPath, isDir, forcedExtension } = STATE.pendingAction;
    let newName = requiresInput ? ELEMENTS.actionInput.value.trim() : null;
    let result = { success: false, message: "No action taken." };

    if (requiresInput && !newName) {
        alert("Name cannot be empty.");
        return;
    }

    ELEMENTS.actionModal.classList.add('hidden');
    
    window.api.log.info(`[ACTION PENDING] ${actionType} on path: ${currentPath}`, 'ide-Renderer');

    // --- LOGIC FOR FILE CREATION ---
    if (actionType === 'new-file') {
        const parentPath = currentPath; 
        let finalName = newName;
        
        if (forcedExtension) {
            if (finalName.includes(forcedExtension)) {
                 // Do nothing
            } else if (finalName.includes('.')) {
                finalName = finalName.substring(0, finalName.lastIndexOf('.'));
                finalName += forcedExtension;
            } else {
                 finalName += forcedExtension;
            }
        } else {
            if (!finalName.includes('.')) {
                window.api.log.error(`[ERROR] File name must include an extension (e.g., '.event'). Please try again.`, 'ide-Renderer');
                setTimeout(() => showActionModal('new-file', currentPath, isDir), 100); 
                STATE.pendingAction = null; 
                return;
            }
        }
        
        const filePath = `${parentPath}/${finalName}`;

        try {
            result = await window.api.invoke('create-file', { filePath }); 
            if (result.success) {
                refreshFolder(parentPath);
                loadFileContent(filePath);
            }
        } catch (error) {
            result.message = `IPC Error creating file: ${error.message}`;
        }
    } 
else if (actionType === 'rename') {
    const oldFilePath = normalizePath(currentPath);
    const originalExt = STATE.pendingAction.originalExtension || '';
    
    // Ensure the new name doesn't have the extension, then add the protected one
    let cleanNewName = newName.replace(originalExt, ''); 
    const finalNewName = cleanNewName + originalExt;

    // Construct and normalize the new path
    const pathParts = oldFilePath.split('/');
    pathParts.pop(); // Remove old filename
    const parentDir = pathParts.join('/');
    const newFilePath = normalizePath(`${parentDir}/${finalNewName}`);
    
    if (oldFilePath === newFilePath) {
         window.api.log.warn(`[WARNING] Rename aborted: New name is identical to the old name.`, 'ide-Renderer');
         return;
    }

    try {
        result = await window.api.invoke('rename-file', { oldFilePath, newFilePath });
        if (result.success) {
            // Update editor state if the renamed file was the active one
            if (normalizePath(STATE.CURRENT_FILE_PATH) === oldFilePath) {
                STATE.CURRENT_FILE_PATH = newFilePath;
                setDirtyState(false); 
            }
            refreshFolder(parentDir || STATE.PROJECT_NAME);
        }
    } catch (error) {
        result.message = `IPC Error renaming file: ${error.message}`;
    }
}
    else if (actionType === 'import-image') {
        const sourcePath = STATE.pendingAction.sourcePath;
        // Strictly sanitize: Replace spaces with underscores, remove non-safe chars if needed
        let finalName = newName.replace(/\s+/g, '_');
        
        // Ensure extension is .dds
        if (!finalName.toLowerCase().endsWith('.dds')) {
            finalName += '.dds';
        }

        try {
            result = await window.api.invoke('import-image', { 
                sourcePath: sourcePath, 
                newFileName: finalName 
            });
            
            if (result.success) {
                // Refresh the GFX folder if it's visible, or the root
                refreshFolder('GFX'); 
                refreshFolder(STATE.PROJECT_NAME); // Fallback to root refresh to see the new folder
            }
        } catch (error) {
            result.message = `IPC Error importing image: ${error.message}`;
        }
    }
    // --- LOGIC FOR DELETE ---
    else if (actionType === 'delete') {
        const normalizedDelPath = normalizePath(currentPath);
        try {
            result = await window.api.invoke('delete-file-or-dir', { path: normalizedDelPath });
            if (result.success) {
                if (normalizePath(STATE.CURRENT_FILE_PATH) === normalizedDelPath) {
                    STATE.MONACO_EDITOR.setValue('File deleted. Select another file.');
                    STATE.MONACO_EDITOR.updateOptions({ readOnly: true });
                    STATE.CURRENT_FILE_PATH = null;
                    setDirtyState(false);
                }
                
                const pathParts = normalizedDelPath.split('/');
                pathParts.pop();
                const parentDir = pathParts.join('/');
                refreshFolder(parentDir || STATE.PROJECT_NAME);
            }
        } catch (error) {
            result.message = `IPC Error deleting file: ${error.message}`;
        }
    }


    if (result.success) {
        window.api.log.info(`[SUCCESS] ${result.message}`, 'ide-Renderer');
    } else {
        window.api.log.error(`[ERROR] Action failed: ${result.message}`, 'ide-Renderer');
    }
    
    // Clear pending action
    STATE.pendingAction = null;
}


// --- Event Listeners and Resizing ---

function attachEventListeners() {

    // Top Bar Buttons
    ELEMENTS.saveFileBtn.addEventListener('click', saveActiveFile);
    ELEMENTS.backToModsBtn.addEventListener('click', () => {
        if (STATE.IS_DIRTY && !confirm("You have unsaved changes. Are you sure you want to go back?")) {
            return;
        }
        window.api.send('switch-page', 'index');
    });
    ELEMENTS.importImageBtn.addEventListener('click', async () => {
    window.api.log.info("Opening image import dialog...", 'Renderer');
    
    const result = await window.api.invoke('import-image');

    if (result && result.success) {
        window.api.log.info(`Imported: ${result.fileName}`, 'Renderer');
        // Refresh the file tree so the new image appears immediately
        if (typeof loadDirectoryTree === 'function') {
            loadDirectoryTree(STATE.PROJECT_NAME);
        }
    } else if (result && result.message) {
        window.api.log.error(`Import failed: ${result.message}`, 'Renderer');
    }
});
    attachConsoleEventListeners();

    // Modal Listeners
    ELEMENTS.cancelActionBtn.addEventListener('click', () => {
        ELEMENTS.actionModal.classList.add('hidden');
        STATE.pendingAction = null;
    });
    ELEMENTS.actionInput.addEventListener('keydown', (e) => {
        if (e.key === 'Enter') {
            ELEMENTS.confirmActionBtn.click();
        }
    });

	document.addEventListener('keydown', (e) => {
	    // Check for Ctrl+S or Cmd+S
	    if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 's') {
            e.preventDefault(); // Stop the browser "Save Page" dialog
            e.stopPropagation(); // Stop other elements from catching this
            
            window.api.log.info("[System] Global Save Shortcut Triggered", 'ide-Renderer');
            
            // Use your existing shared logic
            triggerSaveShortcut(e);
	    }
	}, true); // The 'true' here is CRITICAL: it uses "Capture" mode to catch the key before Monaco does.

    // Context Menu Listeners (Delegated)
    ELEMENTS.contextMenu.addEventListener('click', (e) => {
        const actionItem = e.target.closest('[data-action]');
        if (actionItem) {
            const action = actionItem.getAttribute('data-action');
            const path = STATE.CONTEXT_PATH;
            if (!path) return;

            const element = document.querySelector(`.file-item[data-path="${path}"]`);
            const isDir = element ? element.getAttribute('data-is-dir') === 'true' : false;
            
            ELEMENTS.contextMenu.classList.add('hidden');
            
            if (action === 'refresh') {
                // Determine the directory to refresh
                const refreshPath = isDir ? path : path.substring(0, path.lastIndexOf('/'));
                refreshFolder(refreshPath || STATE.PROJECT_NAME);
            } else {
                showActionModal(action, path, isDir);
            }
        }
    });

    // --- Resizing Event Handlers (Only for Sidebar: 'v') ---
    
    let isResizing = false;
    let resizeType = null;

    const startResizing = (e, type) => {
        isResizing = true;
        resizeType = type;
        document.body.style.cursor = type === 'v' ? 'col-resize' : 'row-resize';
        document.body.style.userSelect = 'none';
        document.body.classList.add('resizing');
    };

    const stopResizing = () => {
        if (isResizing) {
            isResizing = false;
            document.body.style.cursor = '';
            document.body.style.userSelect = '';
            resizeType = null;
            document.body.classList.remove('resizing'); // Add this line
            if (STATE.MONACO_EDITOR) {
                 STATE.MONACO_EDITOR.layout();
            }
        }
    };

    const resizeHandler = (e) => {
        if (!isResizing) return;

        if (resizeType === 'v') {
            const newWidth = e.clientX;
            const minWidth = 150;
            const maxWidth = window.innerWidth * 0.5;
            const finalWidth = Math.min(Math.max(newWidth, minWidth), maxWidth);

            ELEMENTS.sidebar.style.width = `${finalWidth}px`;
        } 

        if (STATE.MONACO_EDITOR) {
            requestAnimationFrame(() => STATE.MONACO_EDITOR.layout());
        }
    };

    // Attach listeners to resizers
    ELEMENTS.sidebarResizer.addEventListener('mousedown', (e) => startResizing(e, 'v'));
    document.addEventListener('mousemove', resizeHandler);
    document.addEventListener('mouseup', stopResizing);
}


// --- Application Entry Point ---
window.addEventListener('load', () => {
    
    const modName = localStorage.getItem('marshal_project_to_load');
    localStorage.removeItem('marshal_project_to_load');

    if (modName) { 
        window.api.log.info(`Loading IDE for project: ${modName}`, 'Renderer-Startup');
        initMonaco(); 
        attachEventListeners();
        loadProject(modName);
    } else {
        window.api.log.warn('No project specified, returning to project selection.', 'Renderer-Startup');
        window.api.send('switch-page', 'index');
    }
});

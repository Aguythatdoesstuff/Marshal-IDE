import { STATE, ELEMENTS, normalizePath, setDirtyState } from './projectManager.js';

export const ICONS = {
    SAVE: '<svg class="w-4 h-4" viewBox="0 0 24 24" fill="currentColor"><path d="M17 3H5C3.89 3 3 3.89 3 5v14c0 1.1.89 2 2 2h14c1.1 0 2-.9 2-2V7l-4-4zm-5 16c-1.66 0-3-1.34-3-3s1.34-3 3-3 3 1.34 3 3-1.34 3-3 3zm3-10H5V5h10v4z"/></svg>',
    FOLDER: '<svg class="w-4 h-4" fill="currentColor" viewBox="0 0 24 24"><path d="M10 4H4c-1.11 0-2 .89-2 2v12a2 2 0 002 2h16a2 2 0 002-2V8c0-1.11-.89-2-2-2h-8l-2-2z"/></svg>',
    FILE: '<svg class="w-4 h-4" fill="currentColor" viewBox="0 0 24 24"><path d="M14 2H6a2 2 0 00-2 2v16a2 2 0 002 2h12a2 2 0 002-2V8l-6-6zM15 11h-2v2h-2v-2H9V9h2V7h2v2h2v2z"/></svg>',
};


/**
 * Loads the content of a file into the Monaco Editor.
 * @param {string} filePath The relative path to the file inside the project.
 */
export async function loadFileContent(filePath) {
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

export async function loadDirectoryTree(dirPath) {
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


export async function toggleFolder(itemElement, subContainer, dirPath, newLevel) {
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
export async function refreshFolder(dirPath) {
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
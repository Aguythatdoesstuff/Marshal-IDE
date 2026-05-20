// import { 
//     toggleConsoleMinimize, 
//     initializeConsole, 
//     attachConsoleEventListeners,
//     startLogListener,
//     stopLogListener
// } from '../modules/console_module.js';


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
/**
 * Mapping of folder names to required file extensions.
 * Folder names are converted to lowercase for lookup.
 */
export const FILE_EXTENSION_MAP = {
    'decisions': '.decision',
    'events': '.event',
    'scripted gui': '.scriptedgui',
    'scripts': '.script',
    'ideas': '.idea',
    'equipment': '.equipment',
    'focuses': '.focus',
    'units': '.unit',
    'history': '.history',
    'oob': '.oob',
};

/**
 * Maps a file path extension to your custom Monaco Language IDs
 */
export const getMonacoLanguage = (pathStr) => {
  if (!pathStr) return 'plaintext';
  const extension = pathStr.substring(pathStr.lastIndexOf('.') + 1).toLowerCase();
  switch (extension) {
    case 'event':       return 'eventLang'; 
    case 'decision':    return 'decisionLang'; 
    case 'scriptedgui': return 'scriptedguiLang'; 
    case 'script':      return 'scriptsLang'; 
    case 'idea':        return 'ideaLang'; 
    case 'focus':       return 'focusLang'; 
    case 'equipment':   return 'equipmentLang';
    case 'oob':         return 'oobLang';
    case 'history':     return 'historyLang';
    case 'unit':        return 'unitLang';
    default:            return 'plaintext';
  }
};

/**
 * Registers your custom DSL text language tokenizers straight into the context
 */
export const defineDslLanguages = (monacoInstance) => {
  // Theme colors preserved exactly as specified
  monacoInstance.editor.defineTheme('myDslTheme', {
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

  // Global tokens shared across all language configurations
  const sharedControlKeywords = ['if', 'else', 'else if', 'then', 'not', 'and', 'or', 'limit', 'while_loop', 'ai_chance', 'option'];
  const sharedScopes = ['ROOT', 'FROM'];
  const sharedBooleans = ['true', 'false', 'yes', 'no'];
  const specialIDsRegex = /[A-Z]{3}\.\d+/;

  // Shared tokenizer sub-states for blocks, strings, and code literals
  const baseTokenizerStates = {
    string: [
      [/\[/, { token: 'brackets', next: '@stringEmbedded' }],
      [/[^\\\"\[]+/, 'string.content'],
      [/\\./, 'string.escape'],
      [/"/, { token: 'string.quote', bracket: '@close', next: '@pop' } ]
    ],
    stringEmbedded: [
      [/\]/, { token: 'brackets', next: '@pop' }],
      [/[=><\+\-\*\/&|@:?.]+/, 'operator'],
      [/[a-zA-Z_$][\w$]*/, {
        cases: {
          '@scopes': 'constant.scope',
          '@booleans': 'constant.boolean',
          '@default': 'id.embedded'
        }
      }], 
      [/[0-9]+/, 'operator'],              
      [/\s+/, 'white']
    ],
    blockComment: [
      [/#\}/, { token: 'comment.block', next: '@pop' }], 
      [/[^#\n]+/, 'comment.block'],                      
      [/#/, 'comment.block'],                            
      [/\n/, 'comment.block']                            
    ]
  };

  // 1. EVENT LANGUAGE CONFIGURATION
  const eventLangDef = {
    primaryKeywords: ['country', 'news', 'event', 'title', 'desc', 'picture', 'option', 'name', 'namespace', 'sprite', 'is_triggered_only'],
    controlKeywords: sharedControlKeywords, scopes: sharedScopes, booleans: sharedBooleans, specialIDs: specialIDsRegex,
    tokenizer: {
      root: [
        [/(country|news)(\s+)(event)(\s+)([a-zA-Z_$][\w$]*)/, ['primary.keyword', 'white', 'primary.keyword', 'white', 'id.declaration']],
        [/(title|desc|option|namespace)(\s+)([a-zA-Z_$][\w$]*)/, ['primary.keyword', 'white', 'id.declaration']],
        [/[a-zA-Z_$][\w$]*/, { cases: { '@primaryKeywords': 'primary.keyword', '@controlKeywords': 'control.keyword', '@scopes': 'constant.scope', '@booleans': 'constant.boolean', '@default': 'id.general' } }],
        [/@specialIDs/, 'id.special'], [/\s+/, 'white'], [/#{/, { token: 'comment.block', next: '@blockComment' }], [/#.*$/, 'comment'], [/[0-9]+/, 'number'], [/[{}()\[\]]/, 'brackets'], [/[=><\+\-\*\/&|@:?.]+/, 'operator'],
        [/"/, { token: 'string.quote', bracket: '@open', next: '@string' }]
      ],
      ...baseTokenizerStates
    }
  };

  // 2. DECISION LANGUAGE CONFIGURATION
  const decisionLangDef = {
    primaryKeywords: ['category', 'decision', 'allowed', 'available', 'visible', 'cost', 'priority', 'name', 'desc', 'sprite', 'icon', 'on', 'click', 'remove', 'effect'],
    controlKeywords: sharedControlKeywords, scopes: sharedScopes, booleans: sharedBooleans, specialIDs: specialIDsRegex,
    tokenizer: {
      root: [
        [/\b(picture\s+sprite|on\s+click|remove\s+effect)\b/, 'primary.keyword'],
        [/(category|decision)(\s+)([a-zA-Z_$][\w$]*)/, ['primary.keyword', 'white', 'id.declaration']],
        [/[a-zA-Z_$][\w$]*/, { cases: { '@primaryKeywords': 'primary.keyword', '@controlKeywords': 'control.keyword', '@scopes': 'constant.scope', '@booleans': 'constant.boolean', '@default': 'id.general' } }],
        [/@specialIDs/, 'id.special'], [/\s+/, 'white'], [/#{/, { token: 'comment.block', next: '@blockComment' }], [/#.*$/, 'comment'], [/[0-9]+/, 'number'], [/[{}()\[\]]/, 'brackets'], [/[=><\+\-\*\/&|@:?.]+/, 'operator'],
        [/"/, { token: 'string.quote', bracket: '@open', next: '@string' }]
      ],
      ...baseTokenizerStates
    }
  };

  // 3. SCRIPTED GUI LANGUAGE CONFIGURATION
  const scriptedguiLangDef = {
    primaryKeywords: ['window', 'draggable', 'define', 'template', 'text', 'sprite', 'icon', 'button', 'gridbox', 'checkbox', 'overlap', 'bar', 'with', 'horizontal', 'vertical', 'steps', 'full', 'empty', 'color', 'unprogressed', 'progressed', 'size', 'max', 'position', 'visible', 'format', 'slotsize', 'var', 'array', 'on', 'click', 'font', 'static', 'tooltip'],
    controlKeywords: sharedControlKeywords, scopes: sharedScopes, booleans: sharedBooleans, specialIDs: specialIDsRegex,
    tokenizer: {
      root: [
        [/\b(max\s+size|on\s+click)\b/, 'primary.keyword'],
        [/(window|define\s+(?:template|text|sprite))(\s+)([a-zA-Z_$][\w$]*)/, ['primary.keyword', 'white', 'id.declaration']],
        [/\b(text|sprite)(\s+)([a-zA-Z_$][\w$]*)/, ['primary.keyword', 'white', 'id.declaration']],
        [/\b(x|y)(-?\d+)\b/, ['primary.keyword', 'number']],
        [/[a-zA-Z_$][\w$]*/, { cases: { '@primaryKeywords': 'primary.keyword', '@controlKeywords': 'control.keyword', '@scopes': 'constant.scope', '@booleans': 'constant.boolean', '@default': 'id.general' } }],
        [/@specialIDs/, 'id.special'], [/\s+/, 'white'], [/#.*$/, 'comment'], [/[0-9]+/, 'number'], [/[{}()\[\]]/, 'brackets'], [/[=><\+\-\*\/&|@:?.]+/, 'operator'],
        [/#{/, { token: 'comment.block', next: '@blockComment' }], [/"/, { token: 'string.quote', bracket: '@open', next: '@string' }]
      ],
      ...baseTokenizerStates
    }
  };

  // 4. SCRIPTS LANGUAGE CONFIGURATION
  const scriptsLangDef = {
    primaryKeywords: ['scripted', 'effect', 'trigger', 'game', 'rule', 'on', 'action', 'define', 'group', 'default', 'name'],
    controlKeywords: sharedControlKeywords, scopes: sharedScopes, booleans: sharedBooleans, specialIDs: specialIDsRegex,
    tokenizer: {
      root: [
        [/(scripted\s+(?:effect|trigger)|game\s+rule|on\s+action)(\s+)([a-zA-Z_$][\w$]*)/, ['primary.keyword', 'white', 'id.declaration']],
        [/[a-zA-Z_$][\w$]*/, { cases: { '@primaryKeywords': 'primary.keyword', '@controlKeywords': 'control.keyword', '@scopes': 'constant.scope', '@booleans': 'constant.boolean', '@default': 'id.general' } }],
        [/@specialIDs/, 'id.special'], [/\s+/, 'white'], [/#{/, { token: 'comment.block', next: '@blockComment' }], [/#.*$/, 'comment'], [/[0-9]+/, 'number'], [/[{}()\[\]]/, 'brackets'], [/[=><\+\-\*\/&|@:?.]+/, 'operator'],
        [/"/, { token: 'string.quote', bracket: '@open', next: '@string' }]
      ],
      ...baseTokenizerStates
    }
  };

  // 5. IDEA LANGUAGE CONFIGURATION
  const ideaLangDef = {
    primaryKeywords: ['country', 'laws', 'idea', 'allowed', 'default', 'name', 'desc', 'sprite', 'icon', 'picture'],
    controlKeywords: sharedControlKeywords, scopes: sharedScopes, booleans: sharedBooleans, specialIDs: specialIDsRegex,
    tokenizer: {
      root: [
        [/^([a-zA-Z_$][\w$]*)(\s+)(idea)(\s+)([a-zA-Z_$][\w$]*)/, ['primary.keyword', 'white', 'primary.keyword', 'white', 'id.declaration']],
        [/^(idea)(\s+)([a-zA-Z_$][\w$]*)/, ['primary.keyword', 'white', 'id.declaration']],
        [/[a-zA-Z_$][\w$]*/, { cases: { '@primaryKeywords': 'primary.keyword', '@controlKeywords': 'control.keyword', '@scopes': 'constant.scope', '@booleans': 'constant.boolean', '@default': 'id.general' } }],
        [/@specialIDs/, 'id.special'], [/\s+/, 'white'], [/#{/, { token: 'comment.block', next: '@blockComment' }], [/#.*$/, 'comment'], [/[0-9]+/, 'number'], [/[{}()\[\]]/, 'brackets'], [/[=><\+\-\*\/&|@:?.]+/, 'operator'],
        [/"/, { token: 'string.quote', bracket: '@open', next: '@string' }]
      ],
      ...baseTokenizerStates
    }
  };

  // 6. FOCUS LANGUAGE CONFIGURATION
  const focusLangDef = {
    primaryKeywords: ['default', 'tree', 'focus', 'name', 'desc', 'position', 'x', 'y', 'sprite', 'icon', 'cost', 'takes', 'days', 'day', 'for', 'available', 'on', 'complete', 'require', 'prevents', 'follow', 'of'],
    controlKeywords: sharedControlKeywords, scopes: sharedScopes, booleans: sharedBooleans, specialIDs: specialIDsRegex,
    tokenizer: {
      root: [
        [/\b(on\s+complete)\b/, 'primary.keyword'],
        [/\b(follow\s+position\s+of)(\s+)([a-zA-Z_$][\w$]*)\b/, ['primary.keyword', 'white', 'id.declaration']],
        [/\b(tree)(\s+)([a-zA-Z_$][\w$]*)(\s+)(for)(\s+)([a-zA-Z_$][\w$]*)/, ['primary.keyword', 'white', 'id.declaration', 'white', 'primary.keyword', 'white', 'id.declaration']],
        [/(tree|focus)(\s+)([a-zA-Z_$][\w$]*)/, ['primary.keyword', 'white', 'id.declaration']],
        [/\b(x|y)(-?\d+)\b/, ['primary.keyword', 'number']],
        
        // Transitions safely into an ID-only block context upon meeting requirement conditions
        [/\b(require|prevents)\b/, { token: 'primary.keyword', next: '@focusIdBlock' }],

        [/[a-zA-Z_$][\w$]*/, { cases: { '@primaryKeywords': 'primary.keyword', '@controlKeywords': 'control.keyword', '@scopes': 'constant.scope', '@booleans': 'constant.boolean', '@default': 'id.general' } }],
        [/@specialIDs/, 'id.special'], [/\s+/, 'white'], [/#{/, { token: 'comment.block', next: '@blockComment' }], [/#.*$/, 'comment'], [/[0-9]+/, 'number'], [/[{}()\[\]]/, 'brackets'], [/[=><\+\-\*\/&|@:?.]+/, 'operator'],
        [/"/, { token: 'string.quote', bracket: '@open', next: '@string' }]
      ],
      // Contextual engine tracking focus tree requirements (inline list definitions match orange, properties pop stack)
      focusIdBlock: [
        [/[a-zA-Z_$][\w$]*/, {
          cases: {
            '@primaryKeywords': { token: '@rematch', next: '@pop' },
            '@controlKeywords': { token: '@rematch', next: '@pop' },
            '@scopes':          { token: '@rematch', next: '@pop' },
            '@booleans':        { token: '@rematch', next: '@pop' },
            '@default':         'id.declaration' // Inline item listings highlight orange safely
          }
        }],
        [/[{}()\[\]=><]/, { token: '@rematch', next: '@pop' }], // Structural operators break context tracking
        [/\s+/, 'white'],
        [/#.*$/, { token: 'comment', next: '@pop' }]
      ],
      ...baseTokenizerStates
    }
  };
  // 7. EQUIPMENT LANGUAGE CONFIGURATION
  const equipmentLangDef = {
    primaryKeywords: ['sprite'],
    controlKeywords: sharedControlKeywords, scopes: sharedScopes, booleans: sharedBooleans, specialIDs: specialIDsRegex,
    tokenizer: {
      root: [
        // for units with optional ID transitioning into a block parser
        [/\b(for\s+units)\b/, { token: 'primary.keyword', next: '@forUnitsBlock' }],

        // define type <id> available from <number>
        [/^(\s*)(define\s+type)(\s+)([a-zA-Z_$][\w$]*)(\s+)(available\s+from)(\s+)([0-9]+)/, 
          ['white', 'primary.keyword', 'white', 'id.declaration', 'white', 'primary.keyword', 'white', 'number']],
        
        // equipment <id> available from <number>
        [/^(\s*)(equipment)(\s+)([a-zA-Z_$][\w$]*)(\s+)(available\s+from)(\s+)([0-9]+)/, 
          ['white', 'primary.keyword', 'white', 'id.declaration', 'white', 'primary.keyword', 'white', 'number']],

        // name/desc/short desc for <tag> "<string>"
        [/^(\s*)(name\s+for|desc\s+for|short\s+desc\s+for)(\s+)([a-zA-Z_$][\w$]*)(\s*)(")/, 
          ['white', 'primary.keyword', 'white', 'id.declaration', 'white', { token: 'string.quote', bracket: '@open', next: '@string' }]],

        // name/desc/short desc "<string>"
        [/^(\s*)(name|desc|short\s+desc)(\s*)(")/, 
          ['white', 'primary.keyword', 'white', { token: 'string.quote', bracket: '@open', next: '@string' }]],

        [/[a-zA-Z_$][\w$]*/, { cases: { '@primaryKeywords': 'primary.keyword', '@controlKeywords': 'control.keyword', '@scopes': 'constant.scope', '@booleans': 'constant.boolean', '@default': 'id.general' } }],
        [/@specialIDs/, 'id.special'], [/\s+/, 'white'], [/#{/, { token: 'comment.block', next: '@blockComment' }], [/#.*$/, 'comment'], [/[0-9]+/, 'number'], [/[{}()\[\]]/, 'brackets'], [/[=><\+\-\*\/&|@:?.]+/, 'operator'],
        [/"/, { token: 'string.quote', bracket: '@open', next: '@string' }]
      ],

      forUnitsBlock: [
        [/[a-zA-Z_$][\w$]*/, {
          cases: {
            '@primaryKeywords': { token: '@rematch', next: '@pop' },
            '@controlKeywords': { token: '@rematch', next: '@pop' },
            '@scopes':          { token: '@rematch', next: '@pop' },
            '@booleans':        { token: '@rematch', next: '@pop' },
            '@default':         'id.declaration'
          }
        }],
        [/[{}()\[\]=><]/, { token: '@rematch', next: '@pop' }],
        [/\s+/, 'white'],
        [/#.*$/, { token: 'comment', next: '@pop' }]
      ],

      ...baseTokenizerStates
    }
  };

  // 8. OOB LANGUAGE CONFIGURATION
  const oobLangDef = {
    primaryKeywords: [],
    controlKeywords: sharedControlKeywords, scopes: sharedScopes, booleans: sharedBooleans, specialIDs: specialIDsRegex,
    tokenizer: {
      root: [
        // support units
        [/^(\s*)(support\s+units)/, ['white', 'primary.keyword']],

        // division template
        [/^(\s*)(division\s+template)(\s*)(")/, ['white', 'primary.keyword', 'white', { token: 'string.quote', bracket: '@open', next: '@string' }]],

        // place division "<string>" at <number> using template "<string>"
        [/^(\s*)(place\s+division)(\s*)(")([^"]*)(")(\s+)(at)(\s+)([0-9]+)(\s+)(using\s+template)(\s*)(")([^"]*)(")/, ['white', 'primary.keyword', 'white', 'string.quote', 'string', 'string.quote', 'white', 'primary.keyword', 'white', 'number', 'white', 'primary.keyword', 'white', 'string.quote', 'string', 'string.quote']],
        
         // place airwings at <number>
        [/^(\s*)(place\s+airwings)(\s+)(at)(\s+)([0-9]+)/, ['white', 'primary.keyword', 'white', 'primary.keyword', 'white', 'number']],

        // place fleet "<string>"
        [/^(\s*)(place\s+fleet)(\s*)(")([^"]*)(")(\s+)(at)(\s+)([0-9]+)/, ['white', 'primary.keyword', 'white', 'string.quote', 'string', 'string.quote', 'white', 'primary.keyword', 'white', 'number']],

        // place taskforce "<string>"
        [/^(\s*)(place\s+taskforce)(\s*)(")([^"]*)(")(\s+)(at)(\s+)([0-9]+)/, ['white', 'primary.keyword', 'white', 'string.quote', 'string', 'string.quote', 'white', 'primary.keyword', 'white', 'number']],

        // ship "<string>" using archetype <id> for <tag>
        [/^(\s*)(ship)(\s*)(")([^"]*)(")(\s+)(using\s+archetype)(\s+)([a-zA-Z_$][\w$]*)(\s+)(for)(\s+)([a-zA-Z_$][\w$]*)/, ['white', 'primary.keyword', 'white', 'string.quote', 'string', 'string.quote', 'white', 'primary.keyword', 'white', 'id.declaration', 'white', 'primary.keyword', 'white', 'id.declaration']],

        // using design "<string>"
        [/^(\s*)(using\s+design)(\s*)(")/, ['white', 'primary.keyword', 'white', { token: 'string.quote', bracket: '@open', next: '@string' }]],

        // category "<string>" OR category <id>
        [/^(\s*)(category)(\s*)(")/, ['white', 'primary.keyword', 'white', { token: 'string.quote', bracket: '@open', next: '@string' }]],
        [/^(\s*)(category)(\s+)([a-zA-Z_$][\w$]*)/, ['white', 'primary.keyword', 'white', 'id.declaration']],

        // add production <id> for <tag>
        [/^(\s*)(add\s+production)(\s+)([a-zA-Z_$][\w$]*)(\s+)(for)(\s+)([a-zA-Z_$][\w$]*)/, ['white', 'primary.keyword', 'white', 'id.declaration', 'white', 'primary.keyword', 'white', 'id.declaration']],

        [/[a-zA-Z_$][\w$]*/, { cases: { '@primaryKeywords': 'primary.keyword', '@controlKeywords': 'control.keyword', '@scopes': 'constant.scope', '@booleans': 'constant.boolean', '@default': 'id.general' } }],
        [/@specialIDs/, 'id.special'], [/\s+/, 'white'], [/#{/, { token: 'comment.block', next: '@blockComment' }], [/#.*$/, 'comment'], [/[0-9]+/, 'number'], [/[{}()\[\]]/, 'brackets'], [/[=><\+\-\*\/&|@:?.]+/, 'operator'],
        [/"/, { token: 'string.quote', bracket: '@open', next: '@string' }]
      ],
      ...baseTokenizerStates
    }
  };

  // 9. HISTORY LANGUAGE CONFIGURATION
  const historyLangDef = {
    primaryKeywords: ['capital', 'ui', 'color', 'map', 'name', 'party', 'country', 'with', 'tag', 'of', 'is'],
    controlKeywords: [...sharedControlKeywords], 
    scopes: sharedScopes, 
    booleans: sharedBooleans, 
    specialIDs: specialIDsRegex,
    tokenizer: {
      root: [
        // country "<string>" with tag <tag>
        [/^(\s*)(country)(\s*)(")([^"]*)(")(\s+)(with\s+tag)(\s+)([a-zA-Z_$][\w$]*)/, 
          ['white', 'primary.keyword', 'white', 'string.quote', 'string', 'string.quote', 'white', 'primary.keyword', 'white', 'id.declaration']],

        // UI & Map colors: ui color / map color
        [/^(\s*)(ui\s+color|map\s+color)\b/, ['white', 'primary.keyword']],

        // party name <ideology> [short|long] "<string>"
        [/^(\s*)(party\s+name)(\s+)([a-zA-Z_$][\w$]*)(\s+)(short|long)(\s*)(")([^"]*)(")/, 
          ['white', 'primary.keyword', 'white', 'id.declaration', 'white', 'primary.keyword', 'white', 'string.quote', 'string', 'string.quote']],
        [/^(\s*)(party\s+name)(\s+)([a-zA-Z_$][\w$]*)(\s*)(")([^"]*)(")/, 
          ['white', 'primary.keyword', 'white', 'id.declaration', 'white', 'string.quote', 'string', 'string.quote']],

        // Base names: name def/adj "<string>"
        [/^(\s*)(name)(\s+)(def|adj)(\s*)(")([^"]*)(")/, 
          ['white', 'primary.keyword', 'white', 'primary.keyword', 'white', 'string.quote', 'string', 'string.quote']],

        // Single line entry rule for name if dynamic statements: switches to token-by-token state
        [/^(\s*)(name)(\s+)(if)\b/, ['white', 'primary.keyword', 'white', { token: 'control.keyword', next: '@nameIfTokens' }]],
        
        // Condition keywords for dynamic name checks
        [/\b(is\s+subject\s+of|is\s+subject|overlord\s+of|sub-ideology|ideology|autonomy|subject\s+of|overlord)\b/, 'primary.keyword'],
        [/\b(def|adj)\b/, 'primary.keyword'],

        [/[a-zA-Z_$][\w$]*/, { 
          cases: { 
            '@primaryKeywords': 'primary.keyword', 
            '@controlKeywords': 'control.keyword', 
            '@scopes': 'constant.scope', 
            '@booleans': 'constant.boolean', 
            '@default': 'id.general' 
          } 
        }],
        [/@specialIDs/, 'id.special'], [/\s+/, 'white'], [/#{/, { token: 'comment.block', next: '@blockComment' }], [/#.*$/, 'comment'], [/[0-9]+/, 'number'], [/[{}()\[\]]/, 'brackets'], [/[=><\+\-\*\/&|@:?.]+/, 'operator'],
        [/"/, { token: 'string.quote', bracket: '@open', next: '@string' }]
      ],

      nameIfTokens: [
        // Match string literal end-of-rule and return to root
        [/"/, { token: 'string.quote', bracket: '@open', next: '@stringThenRoot' }],

        // Condition keywords and connectors
        [/\b(sub-ideology|ideology|autonomy|overlord\s+of|subject\s+of|is\s+subject\s+of|is\s+subject|overlord)\b/, 'primary.keyword'],
        [/\b(is|of|def|adj)\b/, 'primary.keyword'],
        [/\band\b/, 'control.keyword'],

        // Any token right after keywords is recognized as an ID
        [/[a-zA-Z_$][\w$]*/, 'id.declaration'],

        [/\s+/, 'white'],
        [/#.*$/, 'comment']
      ],

      stringThenRoot: [
        [/[^\\"]+/, 'string'],
        [/\\./, 'string.escape'],
        [/"/, { token: 'string.quote', bracket: '@close', next: '@popall' }]
      ],

      ...baseTokenizerStates
    }
  };

  // 10. UNIT LANGUAGE CONFIGURATION
  const unitLangDef = {
    primaryKeywords: [
      'unit', 'model', 'types', 'categories', 'required', 'equipment',
      'abbreviation', 'name', 'desc', 'map_icon_category', 'priority', 'ai_priority'
    ],
    controlKeywords: sharedControlKeywords, 
    scopes: sharedScopes, 
    booleans: sharedBooleans, 
    specialIDs: specialIDsRegex,
    tokenizer: {
      root: [
        // 1. Multi-word string commands
        [/^(\s*)(abbreviation|name|desc)(\s*)(")/, ['white', 'primary.keyword', 'white', { token: 'string.quote', bracket: '@open', next: '@string' }]],
        [/^(\s*)(unit\s+model)(\s*)(")/, ['white', 'primary.keyword', 'white', { token: 'string.quote', bracket: '@open', next: '@string' }]],

        // 2. Multi-word ID/Block commands (Placed BEFORE generic unit rule)
        [/^(\s*)(unit\s+model)(\s+)([a-zA-Z_$][\w$]*)/, ['white', 'primary.keyword', 'white', 'id.declaration']],
        
        [/^(\s*)(unit\s+types)(\s+)([a-zA-Z_$][\w$]*)/, ['white', 'primary.keyword', 'white', { token: 'id.declaration', next: '@unitIdBlock' }]],
        [/^(\s*)(unit\s+types)\b/, ['white', { token: 'primary.keyword', next: '@unitIdBlock' }]],

        [/^(\s*)(unit\s+categories)(\s+)([a-zA-Z_$][\w$]*)/, ['white', 'primary.keyword', 'white', { token: 'id.declaration', next: '@unitIdBlock' }]],
        [/^(\s*)(unit\s+categories)\b/, ['white', { token: 'primary.keyword', next: '@unitIdBlock' }]],

        // 3. Required equipment keyword (parsed normally, NO ID block so contents stay general)
        [/^(\s*)(required\s+equipment)\b/, ['white', 'primary.keyword']],

        // 4. Generic single-word unit command
        [/^(\s*)(unit)(\s+)([a-zA-Z_$][\w$]*)/, ['white', 'primary.keyword', 'white', 'id.declaration']],

        [/[a-zA-Z_$][\w$]*/, { 
          cases: { 
            '@primaryKeywords': 'primary.keyword',
            '@controlKeywords': 'control.keyword', 
            '@scopes': 'constant.scope', 
            '@booleans': 'constant.boolean', 
            '@default': 'id.general' 
          } 
        }],
        [/@specialIDs/, 'id.special'], [/\s+/, 'white'], [/#{/, { token: 'comment.block', next: '@blockComment' }], [/#.*$/, 'comment'], [/[0-9]+/, 'number'], [/[{}()\[\]]/, 'brackets'], [/[=><\+\-\*\/&|@:?.]+/, 'operator'],
        [/"/, { token: 'string.quote', bracket: '@open', next: '@string' }]
      ],

      // Contextual engine tracking unit lists (pops cleanly when hitting keywords, operators, or comments)
      unitIdBlock: [
        [/[a-zA-Z_$][\w$]*/, {
          cases: {
            '@primaryKeywords': { token: '@rematch', next: '@pop' },
            '@controlKeywords': { token: '@rematch', next: '@pop' },
            '@scopes':         { token: '@rematch', next: '@pop' },
            '@booleans':       { token: '@rematch', next: '@pop' },
            '@default':        'id.declaration' // Inline item listings highlight orange safely[cite: 4]
          }
        }],
        [/[{}()\[\]=><]/, { token: '@rematch', next: '@pop' }], // Structural operators/assignments break context tracking[cite: 4]
        [/\s+/, 'white'],
        [/#.*$/, { token: 'comment', next: '@pop' }]
      ],

      ...baseTokenizerStates
    }
  };

  // Registration mapping dictionary binds configurations cleanly to target Monaco workspaces
  const languagesRegistry = {
    eventLang: eventLangDef,
    decisionLang: decisionLangDef,
    scriptedguiLang: scriptedguiLangDef,
    scriptsLang: scriptsLangDef,
    ideaLang: ideaLangDef,
    focusLang: focusLangDef,
    equipmentLang: equipmentLangDef,
    oobLang: oobLangDef,
    historyLang: historyLangDef,
    unitLang: unitLangDef
  };

  Object.entries(languagesRegistry).forEach(([langId, configuration]) => {
    if (!monacoInstance.languages.getLanguages().some(l => l.id === langId)) {
      monacoInstance.languages.register({ id: langId });
      monacoInstance.languages.setMonarchTokensProvider(langId, configuration);
    }
  });
};
// scripted_gui_compiler.js
import * as path from 'path';
import { cleanLinesFromComments, convertLogicBlock } from './logic_parser.js'; 
import { getPathConfig, getOutputFilePath } from './compiler_helpers.js';

const SPACES_PER_INDENT = 4;
const repeat = (n) => " ".repeat(n);

export function compile(ideContent, config, inputPath) {
    try {
        const cleanedLines = cleanLinesFromComments(ideContent);
        const FILE_EXTENSION = path.extname(inputPath);
        const PATH_MAP = getPathConfig(FILE_EXTENSION);

        // --- Output Buffers ---
        let guiInterfaceLines = ["guiTypes = {"];
        let gfxInterfaceLines = ["spriteTypes = {"]; 
        let scriptedGuiLogic = ["scripted_gui = {"]; 
        let scriptedLocDefinitions = []; 
        let localizationData = {}; 

        // --- State Tracking ---
        let currentWindowId = '';
        let currentElementId = ''; 
        let currentElementType = ''; 
        
        let scopeStack = []; 
        let locCounters = {}; 
        let mergedLogic = {};
        let windowDependencies = {}; 

        // --- Auto-ID Counters ---
        let globalWindowCount = 1;
        let elementCounters = {}; 

        for (let i = 0; i < cleanedLines.length; i++) {
            const lineWithSpaces = cleanedLines[i];
            const line = lineWithSpaces.trim();
            if (!line) continue;

            const currentIndent = lineWithSpaces.match(/^\s*/)[0].length;

            // ============================================================
            // 1. SCOPE CLOSING LOGIC
            // ============================================================
            while (scopeStack.length > 0 && currentIndent <= scopeStack[scopeStack.length - 1].indent) {
                const lastScope = scopeStack.pop();

                // GFX GENERATION: Generate progressbartype when the bar scope closes
                if (lastScope.isBar) {
                    gfxInterfaceLines.push(`    progressbartype = {`);
                    gfxInterfaceLines.push(`        name = "GFX_${lastScope.name}"`);
                    gfxInterfaceLines.push(`        textureFile1 = "${lastScope.barData.full}"`);
                    gfxInterfaceLines.push(`        textureFile2 = "${lastScope.barData.empty}"`);
                    gfxInterfaceLines.push(`        size = { x = ${lastScope.barData.width} y = ${lastScope.barData.height} }`);
                    gfxInterfaceLines.push(`        color = { ${lastScope.barData.color1} }`);
                    gfxInterfaceLines.push(`        colortwo = { ${lastScope.barData.color2} }`);
                    gfxInterfaceLines.push(`        effectFile = "gfx/FX/progress.lua"`);
                    gfxInterfaceLines.push(`        horizontal = ${lastScope.barData.horizontal ? 'yes' : 'no'}`);
                    gfxInterfaceLines.push(`        steps = ${lastScope.barData.steps}`);
                    gfxInterfaceLines.push(`    }`);
                }

                let closeIndent = 8; 
                if (lastScope.type === 'window' || lastScope.type === 'template') {
                    closeIndent = 4;
                }
                else if (lastScope.type === 'element') {
                    const isInTemplate = scopeStack.some(s => s.type === 'template');
                    closeIndent = isInTemplate ? 12 : 8;
                }

                guiInterfaceLines.push(`${repeat(closeIndent)}}`);

                if (scopeStack.length > 0) {
                    const parent = scopeStack[scopeStack.length - 1];
                    if (parent.type === 'template' || parent.type === 'window') {
                        currentWindowId = parent.name;
                        currentElementId = (parent.type === 'template') ? parent.name : '';
                    }
                    if (parent.type === 'element') currentElementType = ''; 
                }
            }

            // ============================================================
            // 2. SPECIAL BLOCKS
            // ============================================================
            
            const defineMatch = line.match(/^define\s+(text|sprite)\s+(?:"([^"]+)"|([^\s]+))/i);
            if (defineMatch) {
                const defType = defineMatch[1].toLowerCase();
                const locName = defineMatch[2] || defineMatch[3];
                const locBlock = collectIndentedBlock(cleanedLines, i);
                scriptedLocDefinitions.push(...compileScriptedLocBlock(locName, locBlock, localizationData, defType === 'sprite'));
                scriptedLocDefinitions.push(""); 
                i += locBlock.length; 
                continue;
            }

            const windowMatch = line.match(/^(draggable\s+)?window(?:\s+(?:"([^"]+)"|([^\s]+)))?/i);
            if (windowMatch) {
                const isDraggable = !!windowMatch[1];
                let winName = windowMatch[2] || windowMatch[3] || `window_${globalWindowCount++}`;
                
                currentWindowId = winName.replace(/\s+/g, '_');
                mergedLogic[currentWindowId] = { visible: null, elements: {}, isTemplate: false };
                elementCounters = {}; 

                guiInterfaceLines.push(`    containerWindowType = {`);
                guiInterfaceLines.push(`        name = "${currentWindowId}"`);
                if (isDraggable) guiInterfaceLines.push(`        moveable = yes`);
                
                scopeStack.push({ type: 'window', indent: currentIndent, name: currentWindowId });
                locCounters[currentWindowId] = { static_tt: 1, dynamic_tt: 1, text: 1 };
                continue;
            }

            const templateMatch = line.match(/^define\s+template\s+(?:"([^"]+)"|([^\s]+))/i);
            if (templateMatch) {
                const templateName = (templateMatch[1] || templateMatch[2]).replace(/\s+/g, '_');
                currentWindowId = templateName; 
                mergedLogic[templateName] = { visible: null, elements: {}, isTemplate: true };
                elementCounters = {}; 
                guiInterfaceLines.push(`    containerWindowType = {`);
                guiInterfaceLines.push(`        name = "${templateName}_entry"`); 
                scopeStack.push({ type: 'template', indent: currentIndent, name: templateName });
                locCounters[templateName] = { static_tt: 1, dynamic_tt: 1, text: 1 };
                continue;
            }

            // ============================================================
            // 3. GUI ELEMENTS
            // ============================================================
            const barMatch = line.match(/^(horizontal\s+|vertical\s+)?bar\s+with\s+(\d+)\s+steps(?:\s+(?:"([^"]+)"|([^\s]+)))?/i);
                const typeMatch = line.match(/^(icon|button|text|gridbox|checkbox|overlap)(?:\s+(?:"([^"]+)"|([^\s]+)))?/i);

                if (barMatch || typeMatch) {
                    let typeStr, nameStr, isHorizontal = false, steps = 100;

                    if (barMatch) {
                typeStr = 'bar';
                // If it's explicitly "vertical", set to false. Otherwise, check for "horizontal".
                const orientation = barMatch[1] ? barMatch[1].trim().toLowerCase() : '';
                isHorizontal = orientation === 'horizontal' || orientation === ''; 
                
                steps = parseInt(barMatch[2]);
                nameStr = barMatch[3] || barMatch[4];
            } else {
                    typeStr = typeMatch[1].toLowerCase();
                    nameStr = typeMatch[2] || typeMatch[3];
                }

                const currentScope = scopeStack.length > 0 ? scopeStack[scopeStack.length - 1] : null;
                const isPropertyText = (typeStr === 'text' && currentScope && currentScope.type === 'element');

                if (!isPropertyText) {
                    if (typeStr === 'gridbox' && !nameStr) {
                         nameStr = `${currentWindowId}_gridbox_UNNAMED_${Math.floor(Math.random() * 1000)}`;
                    }
                    else if (!nameStr) {
                        if (!elementCounters[typeStr]) elementCounters[typeStr] = 1;
                        nameStr = `${currentWindowId}_${typeStr}_${elementCounters[typeStr]++}`;
                    }

                    nameStr = nameStr.replace(/\s+/g, '_');
                    
                    if (typeStr === 'gridbox' && currentWindowId && mergedLogic[currentWindowId] && !mergedLogic[currentWindowId].isTemplate) {
                        if (!windowDependencies[currentWindowId]) windowDependencies[currentWindowId] = [];
                        windowDependencies[currentWindowId].push(nameStr);
                    }

                    const depthStr = repeat(8);
                    const typeMap = {
                        icon: 'iconType', button: 'buttonType', text: 'instantTextBoxType',
                        bar: 'iconType', gridbox: 'gridBoxType', checkbox: 'checkBoxType',
                        overlap: 'overlappingElementsBoxType'
                    };
                    
                    guiInterfaceLines.push(`${depthStr}${typeMap[typeStr]} = {`);
                    guiInterfaceLines.push(`${depthStr}    name = "${nameStr}"`);

                    // Bars must reference their GFX explicitly
                    if (typeStr === 'bar') {
                        guiInterfaceLines.push(`${depthStr}    spriteType = "GFX_${nameStr}"`);
                    }

                    scopeStack.push({ 
                        type: 'element', 
                        indent: currentIndent, 
                        name: nameStr, 
                        isBar: typeStr === 'bar',
                        barData: typeStr === 'bar' ? { 
                            steps, 
                            horizontal: isHorizontal, 
                            full: '', 
                            empty: '',
                            color1: '0.0 0.0 0.0',
                            color2: '0.0 0.0 0.0'
                        } : null
                    });
                    
                    currentElementId = nameStr;
                    currentElementType = typeStr; 

                    if (!mergedLogic[currentWindowId].elements[nameStr]) {
                        mergedLogic[currentWindowId].elements[nameStr] = { type: typeStr };
                    }
                    if (!locCounters[nameStr]) locCounters[nameStr] = { static_tt: 1, dynamic_tt: 1, text: 1 };
                    continue; 
                }
            }

            // ============================================================
            // 4. PROPERTIES & LOGIC
            // ============================================================
            let propIndent = repeat(12); 
            const stackTop = scopeStack[scopeStack.length - 1];
            if (stackTop && (stackTop.type === 'window' || stackTop.type === 'template')) propIndent = repeat(8);

            const activeId = currentElementId || currentWindowId;

            // --- Bar Specific Properties ---
            if (line.startsWith('full bar sprite')) {
                stackTop.barData.full = line.match(/"([^"]+)"/)[1];
                continue;
            }
            if (line.startsWith('empty bar sprite')) {
                stackTop.barData.empty = line.match(/"([^"]+)"/)[1];
                continue;
            }
            if (line.startsWith('unprogressed color')) {
                stackTop.barData.color2 = line.replace('unprogressed color', '').trim();
                continue;
            }
            if (line.startsWith('progressed color')) {
                stackTop.barData.color1 = line.replace('progressed color', '').trim();
                continue;
            }

            // --- Generic Properties ---
            if (line.startsWith('position')) {
                guiInterfaceLines.push(`${propIndent}${line.replace(/position\s+x(\d+)\s+y(\d+)/, 'position = { x = $1 y = $2 }')}`);
            } 
            else if (line.startsWith('size') || line.startsWith('slotsize') || line.startsWith('max size')) {
                const isSlot = line.startsWith('slotsize');
                const isMax = line.startsWith('max size');
                const numMatch = line.match(/(\d+)/);

                if (line.includes('%') && !isSlot && !isMax && (currentElementType === 'icon' || currentElementType === 'button') && numMatch) {
                    guiInterfaceLines.push(`${propIndent}scale = ${parseFloat(numMatch[0]) / 100}`);
                } else {
                    const xMatch = line.match(/x(\d+)/);
                    const yMatch = line.match(/y(\d+)/);
                    
                    if (xMatch && yMatch) {
                        if (stackTop.isBar && !isMax && !isSlot) {
                            stackTop.barData.width = xMatch[1];
                            stackTop.barData.height = yMatch[1];
                        } else if (isMax) {
                            guiInterfaceLines.push(`${propIndent}maxWidth = ${xMatch[1]}`);
                            guiInterfaceLines.push(`${propIndent}maxHeight = ${yMatch[1]}`);
                        } else {
                            const prop = isSlot ? 'slotsize' : 'size';
                            guiInterfaceLines.push(`${propIndent}${prop} = { width = ${xMatch[1]} height = ${yMatch[1]} }`);
                        }
                    }
                }
            }
            else if (line.startsWith('font')) {
                const fontName = line.split(/\s+/)[1].replace(/"/g, ''); 
                guiInterfaceLines.push(`${propIndent}font = "${fontName}"`);
            }
            else if (line.startsWith('sprite')) {
                const spriteValMatch = line.match(/^sprite\s+(.+)$/i);
                if (spriteValMatch) {
                    const spriteVal = spriteValMatch[1].trim();
                    const extraSettings = collectIndentedBlock(cleanedLines, i);
                    i += extraSettings.length;
                    const isStatic = /^["']/.test(spriteVal);
                    if (isStatic) {
                        const spriteName = spriteVal.replace(/["']/g, '');
                        if (stackTop.type === 'window') {
                            guiInterfaceLines.push(`${propIndent}background = {`);
                            guiInterfaceLines.push(`${propIndent}    name = "Background"`);
                            guiInterfaceLines.push(`${propIndent}    quadTextureSprite = "${spriteName}"`);
                            extraSettings.forEach(settingLine => {
                                guiInterfaceLines.push(`${propIndent}    ${settingLine.trim()}`);
                            });
                            guiInterfaceLines.push(`${propIndent}}`);
                        } else {
                            guiInterfaceLines.push(`${propIndent}spriteType = "${spriteName}"`); 
                        }
                    } else {
                        if (stackTop.type === 'element' || stackTop.type === 'template') {
                            if (!mergedLogic[currentWindowId].elements[currentElementId]) {
                                mergedLogic[currentWindowId].elements[currentElementId] = {};
                            }
                            mergedLogic[currentWindowId].elements[currentElementId].dynamic_sprite = spriteVal;
                        }
                    }
                }
            }
            else if (line.startsWith('var') || line.startsWith('array')) {
                if (!mergedLogic[currentWindowId].elements[currentElementId]) {
                    mergedLogic[currentWindowId].elements[currentElementId] = {};
                }
                const varName = line.split(/\s+/)[1].replace(/"/g, '');
                mergedLogic[currentWindowId].elements[currentElementId][line.startsWith('var') ? 'value' : 'array'] = varName;
                
                if (stackTop.isBar) {
                    mergedLogic[currentWindowId].elements[currentElementId].isBar = true;
                    mergedLogic[currentWindowId].elements[currentElementId].barVar = varName;
                }
            }
            else if (line.startsWith('text')) {
                processGuiText(line, currentWindowId, currentElementId, currentElementType, guiInterfaceLines, localizationData, propIndent, locCounters);
            }
            else if (line.startsWith('static tooltip')) {
                const txt = line.match(/"([^"]+)"/)?.[1];
                const locKey = `${activeId}_static_tt_${locCounters[activeId].static_tt++}`;
                localizationData[locKey] = `"${txt}"`;
                guiInterfaceLines.push(`${propIndent}pdx_tooltip = ${locKey}`);
            }
            else if (line.startsWith('visible') || line.startsWith('on click') || line.startsWith('checked if')) {
                const logicType = line.startsWith('visible') ? 'visible' : (line.startsWith('on click') ? 'effect' : 'is_selected');
                const rawLogicLines = collectIndentedBlock(cleanedLines, i);
                i += rawLogicLines.length;

                const processedLines = rawLogicLines.map(l => {
                    const dynamicMatch = l.trim().match(/^dynamic tooltip\s+"([^"]+)"/);
                    if (dynamicMatch) {
                        const locKey = `${activeId}_dynamic_tt_${locCounters[activeId].dynamic_tt++}`;
                        localizationData[locKey] = `"${dynamicMatch[1]}"`;
                        return `custom_effect_tooltip = ${locKey}`; 
                    }
                    return l;
                });
                
                const compiled = convertLogicBlock(processedLines, 4);
                
                if (stackTop.type === 'element' || stackTop.type === 'template') {
                    if (!mergedLogic[currentWindowId].elements[currentElementId]) {
                        mergedLogic[currentWindowId].elements[currentElementId] = {};
                    }
                    mergedLogic[currentWindowId].elements[currentElementId][logicType] = compiled;
                } else if (stackTop.type === 'window' && logicType === 'visible') {
                    mergedLogic[currentWindowId].visible = compiled;
                }
            }
            else {
                guiInterfaceLines.push(`${propIndent}${line}`);
            }
        }

        // --- Final Scope Cleanup ---
        while (scopeStack.length > 0) {
            const last = scopeStack.pop();
            if (last.isBar) {
                gfxInterfaceLines.push(`    progressbartype = {`);
                gfxInterfaceLines.push(`        name = "GFX_${last.name}"`);
                gfxInterfaceLines.push(`        textureFile1 = "${last.barData.full}"`);
                gfxInterfaceLines.push(`        textureFile2 = "${last.barData.empty}"`);
                gfxInterfaceLines.push(`        size = { x = ${last.barData.width} y = ${last.barData.height} }`);
                gfxInterfaceLines.push(`        colortwo = { ${last.barData.color1} }`); // swaped color and colortwo because the game engine wants to be special
                gfxInterfaceLines.push(`        color = { ${last.barData.color2} }`);
                gfxInterfaceLines.push(`        effectFile = "gfx/FX/progress.lua"`);
                gfxInterfaceLines.push(`        horizontal = ${last.barData.horizontal ? 'yes' : 'no'}`);
                gfxInterfaceLines.push(`        steps = ${last.barData.steps}`);
                gfxInterfaceLines.push(`    }`);
            }
            let closeIndent = (last.type === 'window' || last.type === 'template') ? 4 : 8;
            guiInterfaceLines.push(`${repeat(closeIndent)}}`);
        }
        guiInterfaceLines.push("}");
        gfxInterfaceLines.push("}");

        // ============================================================
        // 5. BUILD SCRIPTED GUI LOGIC FILE
        // ============================================================
        for (const [winId, data] of Object.entries(mergedLogic)) {
            if (data.isTemplate) continue;

            scriptedGuiLogic.push(`    ${winId} = {`);
            scriptedGuiLogic.push(`        window_name = "${winId}"`);
            scriptedGuiLogic.push(`        context_type = player_context`);

            if (data.visible) {
                scriptedGuiLogic.push(`        visible = {`);
                scriptedGuiLogic.push(...data.visible.map(l => `    ${l}`));
                scriptedGuiLogic.push(`        }`);
            }

            const elementsWithProperties = Object.entries(data.elements).filter(([_, b]) => b.dynamic_sprite || b.isBar);
            if (elementsWithProperties.length > 0) {
                scriptedGuiLogic.push(`        properties = {`);
                for (const [elId, blocks] of elementsWithProperties) {
                    scriptedGuiLogic.push(`            ${elId} = {`);
                    if (blocks.dynamic_sprite) {
                        scriptedGuiLogic.push(`                image = "[${blocks.dynamic_sprite}]"`);
                    }
                    if (blocks.isBar) {
                        scriptedGuiLogic.push(`                frame = ${blocks.barVar}`);
                    }
                    scriptedGuiLogic.push(`            }`);
                }
                scriptedGuiLogic.push(`        }`);
            }

            const gridElements = Object.entries(data.elements).filter(([_, b]) => b.type === 'gridbox');
            if (gridElements.length > 0) {
                scriptedGuiLogic.push(`        dynamic_lists = {`);
                for (const [elId, blocks] of gridElements) {
                    scriptedGuiLogic.push(`            ${elId} = {`);
                    if (blocks.array) scriptedGuiLogic.push(`                array = ${blocks.array}`);
                    scriptedGuiLogic.push(`                entry_container = "${elId}_entry"`); 
                    scriptedGuiLogic.push(`            }`);
                }
                scriptedGuiLogic.push(`        }`);
            }

            let allLogicElements = Object.entries(data.elements).filter(([_, b]) => {
                if (b.type === 'gridbox') return false;
                return b.visible || b.effect || b.is_selected || b.value || b.array;
            });

            const usedTemplates = windowDependencies[winId] || [];
            usedTemplates.forEach(templateName => {
                const templateData = mergedLogic[templateName];
                if (templateData && templateData.isTemplate) {
                     const templateLogic = Object.entries(templateData.elements).filter(([_, b]) => {
                        return b.effect || b.is_selected || b.visible;
                     });
                     allLogicElements = allLogicElements.concat(templateLogic);
                }
            });

            const elementsWithEffects = allLogicElements.filter(([_, blocks]) => blocks.effect);
            if (elementsWithEffects.length > 0) {
                scriptedGuiLogic.push(`        effects = {`);
                for (const [elId, blocks] of elementsWithEffects) {
                    scriptedGuiLogic.push(`            ${elId}_click = {`); 
                    scriptedGuiLogic.push(...blocks.effect.map(l => `    ${l}`));
                    scriptedGuiLogic.push(`            }`);
                }
                scriptedGuiLogic.push(`        }`);
            }


            // 1. Generate the TRIGGER block for Element Visibility (Required for specific elements)
            const elementsWithVisible = allLogicElements.filter(([_, blocks]) => blocks.visible);
            
            if (elementsWithVisible.length > 0) {
                scriptedGuiLogic.push(`        triggers = {`);
                for (const [elId, blocks] of elementsWithVisible) {
                    scriptedGuiLogic.push(`            ${elId}_visible = {`);
                    scriptedGuiLogic.push(...blocks.visible.map(l => `    ${l}`)); 
                    scriptedGuiLogic.push(`            }`);
                }
                scriptedGuiLogic.push(`        }`);
            }

            // 2. Generate the standard element blocks for non-visibility logic (value, is_selected)
            const elementsWithOtherLogic = allLogicElements.filter(([_, blocks]) => 
                blocks.is_selected || (blocks.value && !blocks.isBar)
            );

            for (const [elId, blocks] of elementsWithOtherLogic) {
                scriptedGuiLogic.push(`        ${elId} = {`);
                if (blocks.value) scriptedGuiLogic.push(`            value = ${blocks.value}`);
                if (blocks.is_selected) {
                    scriptedGuiLogic.push(`            is_selected = {`);
                    scriptedGuiLogic.push(...blocks.is_selected.map(l => `    ${l}`));
                    scriptedGuiLogic.push(`            }`);
                }
                scriptedGuiLogic.push(`        }`);
            }

            // 2. Keep the original output structure strictly for is_selected and value
            const elementsWithScopeLogic = allLogicElements.filter(([_, blocks]) => 
                blocks.is_selected || (blocks.value && !blocks.isBar)
            );

            for (const [elId, blocks] of elementsWithScopeLogic) {
                scriptedGuiLogic.push(`        ${elId} = {`);
                if (blocks.value) scriptedGuiLogic.push(`            value = ${blocks.value}`);
                if (blocks.is_selected) {
                    scriptedGuiLogic.push(`            is_selected = {`);
                    scriptedGuiLogic.push(...blocks.is_selected.map(l => `    ${l}`));
                    scriptedGuiLogic.push(`            }`);
                }
                scriptedGuiLogic.push(`        }`);
            }
            scriptedGuiLogic.push(`    }`);
        }

        scriptedGuiLogic.push("}");
        let locOutput = ["l_english:"];
        for (const [key, val] of Object.entries(localizationData)) locOutput.push(` ${key}:0 ${val}`);
        
        return {
            success: true,
            outputs: [
                { path: getOutputFilePath(inputPath, '.gui', null, PATH_MAP.guiinterface), content: guiInterfaceLines.join('\n') },
                { path: getOutputFilePath(inputPath, '.gfx', null, PATH_MAP.guiinterface), content: gfxInterfaceLines.join('\n') }, 
                { path: getOutputFilePath(inputPath, '.txt', null, PATH_MAP.guiscripted), content: scriptedGuiLogic.join('\n') },
                { path: getOutputFilePath(inputPath, '.txt', null, PATH_MAP.scriptedguiLocalisation), content: scriptedLocDefinitions.length > 0 ? scriptedLocDefinitions.join('\n').trim() : "" },
                { path: getOutputFilePath(inputPath, '_l_english.yml', null, PATH_MAP.guiLocalisation), content: locOutput.join('\n') }
            ]
        };
    } catch (e) { return { success: false, message: e.message }; }
}

function processGuiText(line, winId, elId, type, guiBlocks, locMap, indent, counters) {
    const val = line.match(/text\s+(.+)/)[1].trim();
    const activeId = elId || winId;
    if (val.startsWith('"')) {
        const cleanText = val.replace(/"/g, '');
        const locKey = `${activeId}_text_${counters[activeId].text++}`;
        locMap[locKey] = `"${cleanText}"`;
        const prop = (type === 'button') ? 'buttonText' : 'text';
        guiBlocks.push(`${indent}${prop} = ${locKey}`);
    } else {
        const prop = (type === 'button') ? 'buttonText' : 'text';
        guiBlocks.push(`${indent}${prop} = "[${val}]"`); 
    }
}

function compileScriptedLocBlock(name, lines, locMap, isSprite = false) {
    let output = [`defined_text = {`, `    name = ${name}`];
    let entryCount = 1;
    let i = 0;
    while (i < lines.length) {
        const line = lines[i].trim();
        if (line.startsWith('if') || line.startsWith('else_if') || line.startsWith('else if')) {
            const block = collectIndentedBlock(lines, i);
            i += block.length;
            let conditions = [], resultText = "", inThen = false;
            for (let subLine of block) {
                if (subLine.trim() === 'then') { inThen = true; continue; }
                if (inThen || subLine.startsWith('text') || subLine.startsWith('sprite')) {
                     const match = subLine.match(/(?:text|sprite)\s+(?:"([^"]+)"|([^\s]+))/);
                     if (match) resultText = match[1] || match[2];
                } else conditions.push(subLine);
            }
            output.push(`    text = {`, `        trigger = {`);
            output.push(...convertLogicBlock(conditions, 3)); 
            output.push(`        }`);
            if (isSprite) {
                output.push(`        localization_key = "${resultText}"`, `    }`);
            } else {
                const key = `${name}_${entryCount++}`;
                locMap[key] = `"${resultText}"`;
                output.push(`        localization_key = ${key}`, `    }`);
            }
        } 
        else if (line.startsWith('else')) {
            const block = collectIndentedBlock(lines, i);
            i += block.length;
            let resultText = "";
            for (let subLine of block) {
                const match = subLine.match(/(?:text|sprite)\s+(?:"([^"]+)"|([^\s]+))/);
                if (match) resultText = match[1] || match[2];
            }
            output.push(`    text = {`);
            if (isSprite) {
                output.push(`        localization_key = "${resultText}"`, `    }`);
            } else {
                const key = `${name}_${entryCount++}`;
                locMap[key] = `"${resultText}"`;
                output.push(`        localization_key = ${key}`, `    }`);
            }
        }
        i++;
    }
    output.push(`}`); 
    return output;
}

function collectIndentedBlock(lines, startIdx) {
    const block = [];
    if (startIdx + 1 >= lines.length) return block;
    const baseIndent = lines[startIdx].match(/^\s*/)[0].length;
    for (let i = startIdx + 1; i < lines.length; i++) {
        if (lines[i].trim() === "") continue;
        if (lines[i].match(/^\s*/)[0].length > baseIndent) block.push(lines[i]);
        else break;
    }
    return block;
}

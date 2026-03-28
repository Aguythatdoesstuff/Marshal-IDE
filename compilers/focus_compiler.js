import * as path from 'path';
import { cleanLinesFromComments, convertLogicBlock, getHoi4Cost } from './logic_parser.js'; 
import { getPathConfig, getOutputFilePath } from './compiler_helpers.js';

const TAB_TO_SPACES = "    ";

export function compile(ideContent, config, inputPath) {
    try {
        const contentWithSpaces = ideContent.replace(/\t/g, TAB_TO_SPACES);
        const cleanedLines = cleanLinesFromComments(contentWithSpaces);
        
        const FILE_EXTENSION = path.extname(inputPath);
        const PATH_MAP = getPathConfig(FILE_EXTENSION) || {};

        const focusOutputFolder = PATH_MAP.focuses || PATH_MAP.image || "common/national_focus";
        const locOutputFolder = PATH_MAP.focusLocalisation || PATH_MAP.definition || "localisation/english/national_focus";

        let treeHeader = { id: '', isDefault: false, targetTag: null, extraLines: [] };
        let focuses = [];
        let locData = [];
        
        let currentFocusId = '';
        let currentFocusCost = null;
        let currentLines = [];
        let isParsingHeader = false;

        const flushBlock = () => {
            if (isParsingHeader) {
                isParsingHeader = false;
            } else if (currentFocusId) {
                focuses.push(processFocus(currentFocusId, currentFocusCost, currentLines, locData));
                currentFocusId = '';
                currentFocusCost = null;
                currentLines = [];
            }
        };

        for (let i = 0; i < cleanedLines.length; i++) {
            const lineRaw = cleanedLines[i];
            const lineTrimmed = lineRaw.trim();
            if (!lineTrimmed) continue;

            const headerMatch = lineTrimmed.match(/^(default\s+)?tree\s+([^\s]+)(?:\s+for\s+([A-Z0-9]{3}))?/i);
            if (headerMatch) {
                flushBlock();
                treeHeader.isDefault = !!headerMatch[1];
                treeHeader.id = headerMatch[2];
                treeHeader.targetTag = headerMatch[3] || null; // New line: stores the TAG
                isParsingHeader = true;
                continue;
            }

            const focusMatch = lineTrimmed.match(/^focus\s+(?!=\s)([a-zA-Z0-9_\-]+)(?:\s+takes\s+(\d+)\s*(?:days?|day)?)?/i);
            
            if (focusMatch) {
                flushBlock();
                currentFocusId = focusMatch[1];
                if (focusMatch[2]) {
                    currentFocusCost = getHoi4Cost(focusMatch[2]);
                } else {
                    currentFocusCost = null;
                }
                isParsingHeader = false;
                continue;
            }

            if (isParsingHeader) {
                if (lineTrimmed.startsWith('name "')) {
                    const match = lineTrimmed.match(/"([^"]*)"/); 
                    if (match) locData.push(` ${treeHeader.id}:0 "${match[1]}"`); 
                } else if (lineTrimmed.startsWith('desc "')) {
                    const match = lineTrimmed.match(/"([^"]*)"/); 
                    if (match) locData.push(` ${treeHeader.id}_desc:0 "${match[1]}"`);
                } else {
                    treeHeader.extraLines.push(lineRaw);
                }
            } else if (currentFocusId) {
                if (lineTrimmed === '}' && i === cleanedLines.length - 1) continue;
                currentLines.push(lineRaw);
            }
        }
        flushBlock();

        if (!treeHeader.id) {
            const locFileName = path.basename(inputPath, FILE_EXTENSION) + "_l_english.yml";
            return { 
                success: true, 
                outputs: [
                    { path: getOutputFilePath(inputPath, '.txt', null, focusOutputFolder), content: "\n" },
                    { path: path.join(locOutputFolder, locFileName), content: "\n" }
                ]
            };
        }

        let hoi4Output = [];
        hoi4Output.push(`focus_tree = {`);
        hoi4Output.push(`    id = ${treeHeader.id}`);
        if (treeHeader.isDefault) hoi4Output.push(`    default = yes`);
        
        if (treeHeader.targetTag) {
            hoi4Output.push(`    country = {`);
            hoi4Output.push(`        factor = 0`);
            hoi4Output.push(`        modifier = {`);
            hoi4Output.push(`            add = 100`);
            hoi4Output.push(`            original_tag = ${treeHeader.targetTag.toUpperCase()}`);
            hoi4Output.push(`        }`);
            hoi4Output.push(`    }`);
        }

        if (treeHeader.extraLines.length > 0) {
            const cleanHeaderLines = treeHeader.extraLines.filter(l => l.trim() !== '}');
            const headerLogic = convertLogicBlock(cleanHeaderLines, 1);
            hoi4Output.push(...headerLogic);
        }

        hoi4Output.push(``);

        // Indent focuses so they are inside the tree header
        const indentedFocuses = focuses.map(f => {
            return f.split('\n').map(line => "    " + line).join('\n');
        }).join('\n\n');
        
        hoi4Output.push(indentedFocuses);
        hoi4Output.push(`}`);

        const finalOutputs = [
            {
                path: getOutputFilePath(inputPath, '.txt', null, focusOutputFolder),
                content: hoi4Output.join('\n')
            }
        ];

        if (locData.length > 0) {
            const locFileName = path.basename(inputPath, FILE_EXTENSION) + "_l_english.yml";
            finalOutputs.push({
                path: path.join(locOutputFolder, locFileName),
                content: `\ufeffl_english:\n${locData.join('\n')}`
            });
        }

        return { success: true, outputs: finalOutputs };

    } catch (e) { return { success: false, message: e.message }; }
}

function processFocus(id, cost, lines, locData) {
    let processedLines = [];
    let hasName = false;
    let hasDesc = false;

    if (cost !== null) {
        processedLines.push(`    cost = ${cost}`);
    }

    const linesToProcess = lines.filter((l, idx) => !(l.trim() === '}' && idx === lines.length - 1));

    let activeBlockType = null; // 'require', 'prevents', or 'follow'
    let baseIndentSize = -1;

    for (let line of linesToProcess) {
        let trimmed = line.trim();
        if (!trimmed) continue;

        let currentIndent = line.match(/^\s*/)[0].length;

        // Check for start of new multi-line or single-line blocks
        if (trimmed.startsWith('require')) {
            activeBlockType = 'require';
            baseIndentSize = currentIndent;
            const targetId = trimmed.replace('require', '').trim();
            if (targetId) processedLines.push(`    prerequisite = { focus = ${targetId} }`);
            continue;
        } 
        else if (trimmed.startsWith('prevents')) {
            activeBlockType = 'prevents';
            baseIndentSize = currentIndent;
            const targetId = trimmed.replace('prevents', '').trim();
            if (targetId) processedLines.push(`    mutually_exclusive = { focus = ${targetId} }`);
            continue;
        }
        else if (trimmed.startsWith('follow position of')) {
            activeBlockType = 'follow';
            baseIndentSize = currentIndent;
            const targetId = trimmed.replace('follow position of', '').trim();
            if (targetId) processedLines.push(`    relative_position_id = ${targetId}`);
            continue;
        }

        // If we are currently inside a block (indented further than the keyword)
        if (activeBlockType && currentIndent > baseIndentSize) {
            if (activeBlockType === 'require') {
                processedLines.push(`    prerequisite = { focus = ${trimmed} }`);
            } else if (activeBlockType === 'prevents') {
                processedLines.push(`    mutually_exclusive = { focus = ${trimmed} }`);
            } else if (activeBlockType === 'follow') {
                processedLines.push(`    relative_position_id = ${trimmed}`);
            }
            continue;
        } else {
            // We've exited the tabbed block
            activeBlockType = null;
        }

        // Standard syntax handling
        if (trimmed.startsWith('name "')) {
            const match = trimmed.match(/"([^"]*)"/);
            if (match) {
                locData.push(` ${id}:0 "${match[1]}"`);
                hasName = true;
            }
        } 
        else if (trimmed.startsWith('desc "')) {
            const match = trimmed.match(/"([^"]*)"/);
            if (match) {
                locData.push(` ${id}_desc:0 "${match[1]}"`);
                hasDesc = true;
            }
        }
        else if (trimmed.startsWith('position ')) {
            const coords = trimmed.match(/x(-?\d+)\s+y(-?\d+)/);
            if (coords) {
                processedLines.push(`    x = ${coords[1]}`);
                processedLines.push(`    y = ${coords[2]}`);
            }
        }
        else if (trimmed.startsWith('sprite ')) {
            const icon = trimmed.replace('sprite ', '').trim().replace(/"/g, '');
            processedLines.push(`    icon = ${icon}`);
        }
        else if (trimmed.startsWith('on complete')) {
            processedLines.push(`    completion_reward = {`);
        }
        else {
            processedLines.push(line);
        }
    }

    if (!hasName) locData.push(` ${id}:0 "${id}"`);
    if (!hasDesc) locData.push(` ${id}_desc:0 ""`);

    const body = convertLogicBlock(processedLines, 1);
    
    return [`focus = {`, `    id = ${id}`, ...body, `}`].join('\n');
}

import * as path from 'path';
import { cleanLinesFromComments, convertLogicBlock } from './logic_parser.js'; 
import { getPathConfig, getOutputFilePath } from './compiler_helpers.js';

const TAB_TO_SPACES = "    ";

export function compile(ideContent, config, inputPath) {
    console.log(`[Compiler] Starting compilation for: ${inputPath}`);
    try {
        const contentWithSpaces = ideContent.replace(/\t/g, TAB_TO_SPACES);
        const cleanedLines = cleanLinesFromComments(contentWithSpaces);
        
        const FILE_EXTENSION = path.extname(inputPath);
        const PATH_MAP = getPathConfig(FILE_EXTENSION);
        const INPUT_FILENAME = path.basename(inputPath, FILE_EXTENSION);

        const CATEGORIES = ['scripted_effects', 'scripted_triggers', 'game_rules', 'on_actions'];
        
        let activeCategories = new Set();
        let outputs = { scripted_effects: [], scripted_triggers: [], game_rules: [], on_actions: [] };
        let locData = []; 

        let currentType = null; 
        let currentName = '';
        let currentLines = [];

        const sanitize = (str) => str.toUpperCase().replace(/[^A-Z0-9_]/g, '_');

        const flushCurrentBlock = () => {
            if (!currentType) return;
            activeCategories.add(currentType); 

            if (currentType === 'game_rules') {
                let processedLines = [];
                for (let line of currentLines) {
                    let trimmed = line.trim();
                    let indent = line.match(/^\s*/)[0];

                    if (trimmed.startsWith('name "')) {
                        const txt = trimmed.match(/"([^"]+)"/)[1];
                        const locKey = sanitize(`RULE_${currentName}`);
                        locData.push(` ${locKey}:0 "${txt}"`);
                        processedLines.push(`${indent}name = "${locKey}"`);
                    } 
                    else if (trimmed.startsWith('group "')) {
                        const txt = trimmed.match(/"([^"]+)"/)[1];
                        const groupKey = sanitize(`GROUP_${currentName}`);
                        locData.push(` ${groupKey}:0 "${txt}"`);
                        processedLines.push(`${indent}group = "${groupKey}"`);
                    }
                    else if (trimmed.includes('option "') || trimmed.includes('default "')) {
                        const isDefault = trimmed.startsWith('default');
                        const txt = trimmed.match(/"([^"]+)"/)[1];
                        const optId = sanitize(txt); 
                        const locKey = sanitize(`RULE_${currentName}_${optId}`);
                        locData.push(` ${locKey}:0 "${txt}"`);
                        
                        processedLines.push(`${indent}${isDefault ? 'default' : 'option'} = {`);
                        processedLines.push(`${indent}    name = ${optId}`);
                        processedLines.push(`${indent}    text = "${locKey}"`);
                    }
                    else {
                        processedLines.push(line);
                    }
                }

                const convertedBody = convertLogicBlock(processedLines, 1);
                outputs.game_rules.push(`${currentName} = {\n${convertedBody.join('\n')}\n}`);
            } 
            else if (currentType === 'on_actions') {
                outputs.on_actions.push(convertLogicBlock(currentLines, 1).join('\n'));
            } 
            else {
                const body = convertLogicBlock(currentLines, 1);
                outputs[currentType].push(`${currentName} = {\n${body.join('\n')}\n}`);
            }
        };

        for (let i = 0; i < cleanedLines.length; i++) {
            const lineRaw = cleanedLines[i];
            const lineTrimmed = lineRaw.trim();
            if (!lineTrimmed) continue;

            const headerMatch = lineTrimmed.match(/^(scripted\s+effect|game\s+rule|scripted\s+trigger|on\s+action)(?:\s+["']?([^\s"']+)["']?)?/i);
            if (headerMatch) {
                flushCurrentBlock();
                const rawType = headerMatch[1].toLowerCase().replace(/\s+/g, '_');
                
                if (rawType.startsWith('scripted_effect')) currentType = 'scripted_effects';
                else if (rawType.startsWith('scripted_trigger')) currentType = 'scripted_triggers';
                else if (rawType.startsWith('game_rule')) currentType = 'game_rules';
                else if (rawType.startsWith('on_action')) currentType = 'on_actions';
                
                currentName = headerMatch[2] || ''; 
                currentLines = [];
                continue;
            }
            if (currentType) {
                if (lineTrimmed === '}') continue; 
                currentLines.push(lineRaw);
            }
        }
        flushCurrentBlock();

        const finalOutputs = [];
        
        CATEGORIES.forEach(key => {
            const outputPath = getOutputFilePath(inputPath, '.txt', null, PATH_MAP[key]);
            if (!outputPath) return;

            if (activeCategories.has(key)) {
                let content = (key === 'on_actions') 
                    ? `on_actions = {\n${outputs[key].join('\n\n')}\n}`
                    : outputs[key].join('\n\n');
                
                finalOutputs.push({ path: outputPath, content: content, action: 'write' });
            } else {
                // Signal the watcher to delete this category's file
                finalOutputs.push({ path: outputPath, action: 'delete' });
            }
        });

        // Localization
        const locFileName = INPUT_FILENAME + "_l_english.yml";
        const locPath = path.join(PATH_MAP.game_rules_localisation || "localisation/english", locFileName);

        if (locData.length > 0) {
            finalOutputs.push({
                path: locPath,
                content: `\ufeffl_english:\n${locData.join('\n')}`,
                action: 'write'
            });
        } else {
            finalOutputs.push({ path: locPath, action: 'delete' });
        }

        return { success: true, outputs: finalOutputs };
    } catch (e) { 
        return { success: false, message: e.message }; 
    }
}
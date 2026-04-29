import * as path from 'path';
import { cleanLinesFromComments, convertLogicBlock } from './logic_parser.js'; 
import { getPathConfig, getOutputFilePath } from './compiler_helpers.js';

export function compile(ideContent, config, inputPath) {
    try {
        const cleanedLines = cleanLinesFromComments(ideContent);
		let allHoi4Blocks = [];
        let allLocalizationData = {};
        let currentEventLines = [];
        let eventId = '';
        let eventType = '';
        let currentNamespace = '';

        for (const lineWithSpaces of cleanedLines) {
            const line = lineWithSpaces.trim();
            if (!line) continue;

            const ideMatch = line.match(/^(.+?)\s+event\s+([^\s]+)$/i);
            
            if (ideMatch) {
                if (eventId) {
                    allHoi4Blocks.push(processEvent(currentEventLines, eventId, eventType, allLocalizationData));
                }

                let rawType = ideMatch[1].trim().toLowerCase();
                eventType = rawType.replace(/\s+/g, '_') + '_event';
                eventId = ideMatch[2];
                currentEventLines = [];

                const idParts = eventId.split('.');
                if (idParts.length > 1) {
                    const newNamespace = idParts[0];
                    if (newNamespace !== currentNamespace) {
                        currentNamespace = newNamespace;
                        allHoi4Blocks.push(`add_namespace = ${currentNamespace}`);
                    }
                }
                continue;
            }
            if (eventId) currentEventLines.push(lineWithSpaces);
        }

        // Flush last event
        if (eventId) {
            allHoi4Blocks.push(processEvent(currentEventLines, eventId, eventType, allLocalizationData));
        }

        const FILE_EXTENSION = path.extname(inputPath);
        const INPUT_FILENAME = path.basename(inputPath, FILE_EXTENSION);
        const PATH_MAP = getPathConfig(FILE_EXTENSION);

        const finalOutputs = [];

        const eventOutputPath = getOutputFilePath(inputPath, config.target_hoi4_ext || '.txt', config.output_base_name, PATH_MAP.event);
        if (allHoi4Blocks.length > 0) {
            finalOutputs.push({
                path: eventOutputPath,
                content: allHoi4Blocks.join('\n\n'),
                action: 'write'
            });
        } else {
            finalOutputs.push({ path: eventOutputPath, action: 'delete' });
        }

        const locFileName = INPUT_FILENAME + (config.target_loc_ext || "_l_english.yml");
        const locPath = path.join(PATH_MAP.eventLocalisation || "localisation/english", locFileName);

        const locEntries = Object.entries(allLocalizationData);
        if (locEntries.length > 0) {
            let locLines = ['\ufeffl_english:'];
            for (const [key, value] of locEntries) {
                locLines.push(` ${key}:0 ${value}`);
            }
            finalOutputs.push({
                path: locPath,
                content: locLines.join('\n'),
                action: 'write'
            });
        } else {
            finalOutputs.push({ path: locPath, action: 'delete' });
        }

        return {
            success: true,
            outputs: finalOutputs
        };
    } catch (e) {
        return { success: false, message: e.message };
    }
}

function processEvent(lines, id, type, globalLoc) {
    let outputHeader = [`${type} = {`, `    id = ${id}`];
    let optionCount = 0;
    let preProcessedLines = [];
    let triggeredOnlyValue = 'yes'; 

    for(let rawLine of lines) {
        let trimmed = rawLine.trim();
        const indent = rawLine.match(/^\s*/)[0];
        
        // Match: is_triggered_only = yes/no
        const triggeredMatch = trimmed.match(/^is_triggered_only\s*=\s*(yes|no)/i);
        if (triggeredMatch) {
            triggeredOnlyValue = triggeredMatch[1].toLowerCase();
            continue; 
        }

        // Match: title "Text" or title = "Text"
        const titleMatch = trimmed.match(/^title\s*=?\s*"([^"]+)"/i);
        if (titleMatch) {
            globalLoc[`${id}_title`] = `"${titleMatch[1]}"`;
            preProcessedLines.push(`${indent}title = ${id}_title`);
            continue;
        } 

        // Match: desc "Text" or desc = "Text"
        const descMatch = trimmed.match(/^desc\s*=?\s*"([^"]+)"/i);
        if (descMatch) {
            globalLoc[`${id}_desc`] = `"${descMatch[1]}"`;
            preProcessedLines.push(`${indent}desc = ${id}_desc`);
            continue;
        } 

        // Match: sprite "GFX_name" or sprite GFX_name
        const spriteMatch = trimmed.match(/^sprite\s*=?\s*"?([^"\s]+)"?/i);
        if (spriteMatch) {
            preProcessedLines.push(`${indent}picture = "${spriteMatch[1]}"`);
            continue;
        }

        // Match: option "Text"
        const optMatch = trimmed.match(/^option\s*=?\s*"([^"]+)"/i);
        if (optMatch) {
            optionCount++;
            const optKey = `${id}_option_${optionCount}`;
            globalLoc[optKey] = `"${optMatch[1]}"`;
            preProcessedLines.push(`${indent}option`); 
            preProcessedLines.push(`${indent}    name = ${optKey}`);
            continue;
        } 
        
        preProcessedLines.push(rawLine);
    }

    outputHeader.push(`    is_triggered_only = ${triggeredOnlyValue}`);
    const convertedBody = convertLogicBlock(preProcessedLines, 1);
    return [...outputHeader, ...convertedBody, '}'].join('\n');
}

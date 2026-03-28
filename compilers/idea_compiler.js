import * as path from 'path';
import { cleanLinesFromComments, convertLogicBlock } from './logic_parser.js'; 
import { getPathConfig, getOutputFilePath } from './compiler_helpers.js';

const TAB_TO_SPACES = "    ";

export function compile(ideContent, config, inputPath) {
    try {
        const contentWithSpaces = ideContent.replace(/\t/g, TAB_TO_SPACES);
        const cleanedLines = cleanLinesFromComments(contentWithSpaces);
        
        const FILE_EXTENSION = path.extname(inputPath);
        const PATH_MAP = getPathConfig(FILE_EXTENSION);

        let categories = {};
        let locData = [];
        let hasDefinitions = false; 

        let i = 0;
        while (i < cleanedLines.length) {
            const lineRaw = cleanedLines[i];
            const lineTrimmed = lineRaw.trim();
            
            if (!lineTrimmed) { i++; continue; }

            // Dynamic Type Regex: [TYPE] idea [ID]
            const defineMatch = lineTrimmed.match(/^(.+?)\s+idea\s+(?:"([^"]+)"|([^\s]+))/i);

            if (defineMatch) {
                hasDefinitions = true;
                let rawType = defineMatch[1].trim();
                const type = rawType.replace(/\s+/g, '_'); 
                const id = defineMatch[2] || defineMatch[3]; 

                if (!categories[type]) { categories[type] = []; }

                let currentBlockLines = [];
                i++; 
                while (i < cleanedLines.length) {
                    const nextLine = cleanedLines[i];
                    const nextTrimmed = nextLine.trim();
                    if (nextTrimmed && !nextLine.match(/^\s/)) { break; }
                    if (nextTrimmed) { currentBlockLines.push(nextLine); }
                    i++;
                }

                let processedLines = [];
                let hasName = false;

                for (let line of currentBlockLines) {
                    let trimmed = line.trim();
                    let indent = line.match(/^\s*/)[0];

                    if (trimmed.startsWith('name "')) {
                        const txt = trimmed.match(/"([^"]+)"/)[1];
                        locData.push(` ${id}:0 "${txt}"`);
                        hasName = true;
                    }
                    else if (trimmed.startsWith('desc "')) {
                        const txt = trimmed.match(/"([^"]+)"/)[1];
                        const descKey = `${id}_desc`;
                        locData.push(` ${descKey}:0 "${txt}"`);
                    }
                    else if (trimmed.startsWith('sprite ')) {
                        let iconId = trimmed.replace('sprite ', '').trim();
                        // Remove existing quotes if present
                        iconId = iconId.replace(/^["'](.+)["']$/, '$1');
                        processedLines.push(`${indent}picture = "${iconId}"`);
                    }
                    else {
                        processedLines.push(line);
                    }
                }

                if (!hasName) { locData.push(` ${id}:0 "${id}"`); }

                const convertedBody = convertLogicBlock(processedLines, 3);
                categories[type].push(`        ${id} = {\n${convertedBody.join('\n')}\n        }`);
            } else {
                i++;
            }
        }

        const outputs = [];
        let mainContent = "";
        
        if (hasDefinitions) {
            mainContent = "ideas = {\n";
            for (const [type, blocks] of Object.entries(categories)) {
                mainContent += `    ${type} = {\n${blocks.join('\n\n')}\n    }\n`;
            }
            mainContent += "}";
        } else {
            mainContent = "\n";
        }

        outputs.push({
            path: getOutputFilePath(inputPath, '.txt', null, PATH_MAP.ideas),
            content: mainContent
        });

        const locFileName = path.basename(inputPath, FILE_EXTENSION) + "_l_english.yml";
        outputs.push({
            path: path.join(PATH_MAP.gideasLocalisation || "localisation/english/ideas", locFileName),
            content: `\ufeffl_english:\n${locData.join('\n')}`
        });

        return { success: true, outputs: outputs };
    } catch (e) { return { success: false, message: e.message }; }
}

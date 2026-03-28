import * as path from 'path';
import {
    cleanLinesFromComments,
    convertLogicBlock
} from './logic_parser.js';
import {
    getPathConfig,
    getOutputFilePath
} from './compiler_helpers.js';

const INDENT = "    "; 

export function compile(ideContent, config, inputPath) {
    try {
        const contentWithSpaces = ideContent.replace(/\t/g, INDENT);
        const cleanedLines = cleanLinesFromComments(contentWithSpaces);

        const FILE_EXTENSION = path.extname(inputPath);
        const PATH_MAP = getPathConfig(FILE_EXTENSION);

        const categories = {};
        const locMap = new Map();

        let i = 0;
        while (i < cleanedLines.length) {
            const line = cleanedLines[i];
            const trimmed = line.trim();

            if (!trimmed) { i++; continue; }

            const catMatch = trimmed.match(/^category\s+(.+)$/i);
            if (catMatch) {
                const catId = stripQuotes(catMatch[1]);
                categories[catId] ??= { props: [], decisions: [] };

                i++;
                while (i < cleanedLines.length && cleanedLines[i].startsWith(INDENT)) {
                    const catLine = cleanedLines[i].slice(INDENT.length);
                    const catTrimmed = catLine.trim();

                    if (!catTrimmed) { i++; continue; }

                    const decMatch = catTrimmed.match(/^decision\s+(.+)$/i);
                    if (decMatch) {
                        const decId = stripQuotes(decMatch[1]);
                        const decLines = [];

                        i++;
                        while (
                            i < cleanedLines.length &&
                            cleanedLines[i].startsWith(INDENT + INDENT)
                        ) {
                            decLines.push(cleanedLines[i].slice(INDENT.length * 2));
                            i++;
                        }

                        categories[catId].decisions.push(
                            processDecisionBlock(decId, decLines, locMap)
                        );
                        continue;
                    }

                    // Handle blocks like "allowed" or "available" at category level
                    const header = normalizeBlockHeader(catLine);
                    if (header) {
                        const blockLines = [];
                        i++;
                        while (i < cleanedLines.length && cleanedLines[i].startsWith(INDENT + INDENT)) {
                            blockLines.push(cleanedLines[i].slice(INDENT.length));
                            i++;
                        }
                        const logic = convertLogicBlock(blockLines, 1);
                        categories[catId].props.push(`${header} = {\n${logic.join('\n')}\n${INDENT}}`);
                        continue;
                    }

                    const prop = processCommonDSL(catId, catLine, locMap);
                    if (prop) categories[catId].props.push(prop);
                    i++;
                }
                continue;
            }

            i++;
        }

        /* ================= OUTPUT GENERATION ================= */
        const categoryOutputContent = Object.entries(categories).map(([id, data]) =>
            `${id} = {\n${
                data.props.map(p => INDENT + p).join("\n")
            }\n}`
        ).join("\n\n");

        const categoryOutput = {
            path: getOutputFilePath(inputPath, '.txt', null, PATH_MAP.decisionCategory),
            content: categoryOutputContent
        };

        const decisionOutputContent = Object.entries(categories)
            .filter(([_, d]) => d.decisions.length)
            .map(([id, d]) =>
                `${id} = {\n${d.decisions.join("\n\n")}\n}`
            ).join("\n\n");

        const decisionOutput = {
            path: getOutputFilePath(inputPath, '.txt', null, PATH_MAP.decision),
            content: decisionOutputContent
        };

        const baseName = path.basename(inputPath, FILE_EXTENSION);
        const localizationOutput = {
            path: path.join(PATH_MAP.decisionLocalisation, `${baseName}_l_english.yml`),
            content: `\ufeffl_english:\n${Array.from(locMap.values()).join("\n")}`
        };

        return {
            success: true,
            outputs: [categoryOutput, decisionOutput, localizationOutput]
        };

    } catch (e) {
        return { success: false, message: e.message };
    }
}

function stripQuotes(v) {
    return v.replace(/^["']|["']$/g, '');
}

function processCommonDSL(id, line, locMap) {
    const t = line.trim();
    if (!t) return null;

    if (t.startsWith('name "')) {
        locMap.set(id, ` ${id}:0 "${t.match(/"([^"]+)"/)[1]}"`);
        return null;
    }

    if (t.startsWith('desc "')) {
        locMap.set(id + "_desc", ` ${id}_desc:0 "${t.match(/"([^"]+)"/)[1]}"`);
        return null;
    }

    if (t.startsWith('sprite ') || t.startsWith('icon ')) {
        const iconName = stripQuotes(t.replace(/^(sprite|icon)\s+/, ''));
        return `icon = ${iconName}`;
    }

    if (t.startsWith('priority ')) {
        const val = t.replace(/^priority\s+/, '');
        return `priority = ${val}`;
    }

    if (t.startsWith('cost')) {
        const val = t.replace(/^cost\s*=?\s*/, '');
        return `cost = ${val || '0'}`;
    }

    // Logic for scripted effects: only append = yes if not already an assignment/block
    if (!t.includes('=') && !t.includes('{')) {
        return `${t} = yes`;
    }

    return t;
}

function normalizeBlockHeader(line) {
    const t = line.trim().toLowerCase();
    if (t === "on click") return "complete_effect";
    if (t === "available") return "available";
    if (t === "visible") return "visible";
    if (t === "allowed") return "allowed";
    if (t === "remove effect") return "remove_effect";
    return null;
}

function processDecisionBlock(id, lines, locMap) {
    const out = [];
    const indent = INDENT; 
    const inner = INDENT + INDENT; 

    out.push(`${indent}${id} = {`);

    let i = 0;
    while (i < lines.length) {
        const header = normalizeBlockHeader(lines[i]);

        if (header) {
            const blockLines = [];
            i++;
            while (i < lines.length && lines[i].startsWith(INDENT)) {
                blockLines.push(lines[i]);
                i++;
            }

            const logic = convertLogicBlock(blockLines, 3);
            out.push(`${inner}${header} = {`);
            out.push(...logic);
            out.push(`${inner}}`);
            continue;
        }

        const common = processCommonDSL(id, lines[i], locMap);
        if (common) out.push(`${inner}${common}`);
        i++;
    }

    out.push(`${indent}}`);
    return out.join("\n");
}

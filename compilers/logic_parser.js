// logic_parser.js
const COMPOUND_TRIGGERS = ['or', 'and', 'not', 'option', 'limit', 'if', 'else_if', 'else', 'then', 'while_loop', 'ai_chance'];
const INDENT_SPACES = 4;
const getIndentString = (level) => ' '.repeat(Math.max(0, level * INDENT_SPACES));

/**
 * Strips Zero-Width characters, BOMs, and Non-Breaking Spaces
 * that often get injected by AI or mixed-encoding copy-pasting. to ensure nothing breaks
 */
function sanitizeInput(content) {
    return content.replace(/[\u200B-\u200D\uFEFF\u00A0]/g, '');
}

/**
 * removes comments from the input
 */
export function cleanLinesFromComments(content) {
    const sanitizedContent = sanitizeInput(content);
    const cleanedLines = [];
    let inBlockComment = false;
    const lines = sanitizedContent.split(/\r?\n/);
    for (let currentLine of lines) {
        const blockStart = currentLine.indexOf('#{');
        const blockEnd = currentLine.indexOf('#}');
        if (inBlockComment) {
            if (blockEnd !== -1) { inBlockComment = false; currentLine = currentLine.substring(blockEnd + 2); }
            else continue;
        }
        if (blockStart !== -1) {
            inBlockComment = true;
            currentLine = currentLine.substring(0, blockStart);
            if (blockEnd > blockStart) { inBlockComment = false; currentLine += currentLine.substring(blockEnd + 2); }
        } 
        const hashIndex = currentLine.indexOf('#');
        if (hashIndex !== -1) currentLine = currentLine.substring(0, hashIndex);
        if (currentLine.trim().length > 0) cleanedLines.push(currentLine);
    }
    return stripRedundantBrackets(cleanedLines);
}

/**
 * Removes lines that are purely structural closing brackets
 * while preserving inline/one-liner brackets.
 */
function stripRedundantBrackets(lines) {
    return lines.filter(line => {
        const trimmed = line.trim();
        
        // Rule: If line is ONLY '}', it's structural/redundant.
        // If it contains both '{' and '}', it's a one-liner and we keep it.
        const hasOpen = trimmed.includes('{');
        const hasClose = trimmed.includes('}');

        if (hasClose && !hasOpen) {
            // Check if '}' is the first/only meaningful character
            // This kills the line if it's just "    }"
            if (trimmed === '}') {
                return false; 
            }
        }
        
        return true;
    });
}

export function convertLogicBlock(logicLines, startIndent) {
    const lines = [];
    let currentHoI4Indent = startIndent; 
    let blockStack = []; 

    for (const lineWithSpaces of logicLines) {
        const line = lineWithSpaces.trim(); 
        if (line.length === 0) continue;

        const originalSpaces = lineWithSpaces.match(/^\s*/)[0].length;
        let ideLevel = originalSpaces / INDENT_SPACES;

        let cleanLine = line.toLowerCase().replace('else if', 'else_if');
        const keyLower = cleanLine.split(/\s+/)[0];

        // 1. "then" handling: Closes the virtual 'limit' block
        if (keyLower === 'then') {
            while (blockStack.length > 0 && blockStack[blockStack.length - 1].type !== 'limit') {
                blockStack.pop();
                currentHoI4Indent--;
                lines.push(getIndentString(currentHoI4Indent) + '}');
            }
            if (blockStack.length > 0 && blockStack[blockStack.length - 1].type === 'limit') {
                blockStack.pop();
                currentHoI4Indent--;
                lines.push(getIndentString(currentHoI4Indent) + '}');
            }
            continue; 
        }

        // 2. Indentation-based Closing 
        while (blockStack.length > 0 && ideLevel <= blockStack[blockStack.length - 1].ideLevel) {
            blockStack.pop();
            currentHoI4Indent--;
            lines.push(getIndentString(currentHoI4Indent) + '}');
        }

        // 3. The Multi-Bracket & Auto-Closing 
        const openBrackets = (line.match(/\{/g) || []).length;
        const closedBrackets = (line.match(/\}/g) || []).length;
        const netBrackets = openBrackets - closedBrackets;

        const isCompound = COMPOUND_TRIGGERS.includes(keyLower);
        
        // Only trigger DSL auto-opening if the user didn't write the brackets themselves
        if (isCompound && !line.includes('{')) {
            if (keyLower === 'else_if' || keyLower === 'if') {
                lines.push(getIndentString(currentHoI4Indent) + `${keyLower} = {`);
                currentHoI4Indent++;
                lines.push(getIndentString(currentHoI4Indent) + `limit = {`);
                blockStack.push({ type: keyLower, ideLevel: ideLevel });
                blockStack.push({ type: 'limit', ideLevel: ideLevel + 0.01 }); 
                currentHoI4Indent++;
            } else {
                lines.push(getIndentString(currentHoI4Indent) + `${keyLower} = {`);
                blockStack.push({ type: keyLower, ideLevel: ideLevel });
                currentHoI4Indent++;
            }
        } else {
            // Standard assignment or manual scope
            const parts = line.split(/\s+/);
            const key = parts[0];
            const val = line.substring(key.length).trim();
            
            let output = (val.length === 0 || ['=', '>', '<', '!', '{'].includes(val[0])) 
                ? `${key} ${val}` 
                : `${key} = ${val}`;
            
            lines.push(getIndentString(currentHoI4Indent) + output);

            // If the line opened more brackets than it closed, track them in the stack
            if (netBrackets > 0) {
                for (let i = 0; i < netBrackets; i++) {
                    blockStack.push({ type: 'manual_scope', ideLevel: ideLevel });
                    currentHoI4Indent++;
                }
            } else if (netBrackets < 0) {
                // If user closed more than they opened on this line, reduce current indent
                currentHoI4Indent = Math.max(startIndent, currentHoI4Indent + netBrackets);
            }
        }
    }

    // Final cleanup for any unclosed blocks
    while (blockStack.length > 0) {
        blockStack.pop();
        currentHoI4Indent--;
        lines.push(getIndentString(currentHoI4Indent) + '}');
    }
    return lines;
}


/**
 * Calculates the Hearts of Iron IV 'cost' value based on a duration in days.
 * * @param {number} days - The intended duration of the focus.
 * @returns {number} - The value for the 'cost' attribute, formatted to 3 decimal places.
 */
export function getHoi4Cost(days) {
    // HOI4 calculates focus progress as: 1 cost unit = 7 days.
    const exactCost = days / 7;

    // we have to add a small value so that if you input 5 days it actually displays 5 days ingame
    // due to a game UI quirk
    const safeCost = exactCost + 0.001; 
    
    return parseFloat(safeCost.toFixed(3));
}

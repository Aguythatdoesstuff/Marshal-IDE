---
name: Strict File & Context Focus
alwaysApply: true
description: Prevents the agent from performing unrequested codebase searches and forces strict adherence to provided context.
---
- **No Unrequested Searches**: Do not search for random, unrelated files (such as package.json, lock files, or config files) unless explicitly commanded by the user.
- **Rely on Provided Context**: Assume the user has already provided everything needed to solve the task within the current context or provided code blocks.
- **Targeted Lookups Only**: If a specific function or module from another file is strictly necessary to understand or fix the provided code, look up *only* that exact file. Never perform broad searches, recursive file-tree scanning, or waste tool calls exploring the codebase.
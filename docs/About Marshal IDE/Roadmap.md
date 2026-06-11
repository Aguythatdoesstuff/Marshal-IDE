# Future Updates & Roadmap

This document outlines the planned trajectory for Marshal IDE. Note: Features marked as "Researching" are experimental.

## 🛠 v1.3.0 – Security Update
### Out-of-Process Behavioral Security Engine (.NET 10)
- **Zero-Trust Monitoring (Dependency Attacks & Zero-Days)**: Implementation of a bare-metal native C# watchdog engine to isolate runtime components from the OS.
- **Dynamic File System Sandboxing**: Automatic monitoring of the `userdata` directory config files to dynamically whitelist project workspace paths without IPC overhead.
- **Least-Privilege Enforcement**: Strict scoping of the Mod Importer (`importer.exe`) to read-only access within user-defined asset paths, and write-only access to localized workspace outputs.
- **Extension Filtering**: OS-level file handle interception to block components from reading unauthorized code files (`.js`, `.cs`, etc.), using a strict `SIGINT` → `SIGKILL` escalation path upon violation.
- **Dual-Watchdog Heartbeat System**: Bidirectional polling loops checking process tables every 200ms. If the security engine is forcefully terminated via kernel-level `SIGKILL`, the main app instantly drops into a safe-mode emergency shutdown to protect the user environment.
  
---

## 🛠 v1.4.0 – Compiler Update
### Compiler changes
- **Scripted Gui Scale**: add width and heighth to be a % instead of only coordinates

---
## 🔭 Long-term Research & Development
- **Asset Suite**: 
    - Built-in `.dds` image previewer.
    - Integrated JPEG/PNG to `.dds` converter.
- **Workflow Tools**:
    - Integrated Git GUI for version control.
    - Legacy Mod Importer to convert vanilla-style code into Marshal DSL.
    - Improved DSL code validation.
- ### DSL Expansions
    - **MIO Support**: Dedicated DSL for Military Industrial Organizations.
    - **Country Definitions**: Streamlined syntax for defining new nations and tags.

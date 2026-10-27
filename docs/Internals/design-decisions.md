# Marshal IDE — Internal Design Decisions

This document records the reasoning behind Marshal IDE's major architectural and implementation decisions. It focuses on **why the project is built this way**, including performance considerations, historical context, trade-offs, and planned architectural changes.

It is not a general codebase tour. Obvious file responsibilities and standard framework behavior are intentionally omitted.

Implementation details and planned changes are distinguished where relevant. Roadmap items describe intended behavior and should not be treated as already implemented.

## 1. Project Philosophy

### 1.1 Marshal Script exists to make HOI4 modding less tedious

Marshal Script began as an experiment. HOI4 modding had started to feel repetitive, and other activities were not particularly interesting at the time. After learning about npm during an internship, the idea developed through questions, experimentation, and an initial compiler prototype.

The early prototypes were built with AI assistance. As the project grew, the tooling was progressively expanded and refactored into a proper IDE.

Marshal Script proved useful enough that it became a tool its creator continued to use for their own modding.

That feedback loop matters: Marshal is designed around practical modding workflows rather than abstract language-design goals.

The primary objective is to automate repetitive work, reduce avoidable mistakes, and make mod development faster and more enjoyable without unnecessarily restricting what modders can create.

### 1.2 Automate what is reasonable; preserve user control where it matters

Marshal aims to make as much of HOI4 modding as reasonably possible accessible through its DSLs and IDE.

However, automation should not come at the cost of flexibility or correctness. Important values that affect game behavior, such as identifiers and country tags, remain under the modder's control and are validated where appropriate.

The guiding principle is to remove unnecessary work without hiding important decisions or recreating functionality the game engine already provides.

## 2. Marshal Script and the Compiler

### 2.1 Transpile to native HOI4 scripting

Marshal Script transpiles into native HOI4 scripting and supporting game files.

It does not introduce a new runtime that the game must execute, nor does it attempt to replace the HOI4 scripting language. Doing so would require modifying, injecting into, or otherwise working around the game's engine to support a new execution format.

Generating files the game already understands avoids that unnecessary complexity.

This also means the output remains ordinary mod content that can be inspected and used within the existing HOI4 modding ecosystem.

### 2.2 Native syntax remains available

Marshal does not attempt to reinvent every HOI4 scripting construct.

Native syntax can be used where a Marshal abstraction is unnecessary or where the underlying game already provides the required behavior. This avoids spending development time recreating engine functionality and helps preserve flexibility when new or uncommon game mechanics are needed.

Marshal's DSLs provide convenience and structure, not an obligation to express every possible operation through a custom abstraction.

### 2.3 Minimal wrappers instead of excessive abstraction

A Marshal DSL should automate boilerplate and make common operations easier without hiding values that are important to functionality.

If the compiler can generate something reliably from the information already available, automation is preferred. If a value is essential to game behavior and cannot safely be inferred, it should generally remain explicit.

This is particularly important for identifiers and country tags. Such values are not merely cosmetic: invalid or conflicting values can cause broken references or unexpected engine behavior.

The compiler enforces relevant rules, including restrictions on country tags informed by the HOI4 wiki. Tags identified as unsafe or unsuitable are rejected rather than silently accepted.

### 2.4 Why the compiler is written in C#

The compiler was moved from JavaScript to C# to improve performance.

Marshal's compiler performs substantial parsing, validation, and transpilation work. A compiled language was chosen to handle this workload more efficiently than the original Node.js implementation.

Performance matters because the compiler is part of an IDE workflow: compilation should be fast enough to support frequent iteration without making the user wait unnecessarily.

The C# implementation is maintained separately from the importer, allowing the compiler to focus on transforming Marshal source into native game files.

### 2.5 Validation precedes parsing

The compiler's processing contract is validation-first. The validator and parser have distinct responsibilities.

The validator:

- Cleans and normalizes the digested source.
- Checks identifiers and other engine-sensitive constraints.
- Detects invalid constructs before output generation.
- Records information discovered during validation, including identifier-related metadata and source-line information.

The parser then reads the validated source and builds a structured abstract syntax tree (AST). It receives the metadata collected during validation instead of having to rediscover the same information independently.

The AST is subsequently consumed by the compiler to generate native HOI4 scripting and supporting files.

This separation serves two purposes:

1. **Correctness:** invalid input should be caught before it can produce broken output.
2. **Efficiency:** information already established by validation should be reused rather than needlessly recomputed.

The parser is not intended to duplicate the validator's checks. Each stage has a defined responsibility.

### 2.6 Why Marshal has a specialized validator

HOI4 mods can contain content from the base game, DLC, and third-party mods. The compiler cannot realistically maintain a complete authoritative list of every identifier that might be valid in every user's installation.

Validation therefore needs to be conservative and targeted.

Marshal checks constructs it can evaluate reliably, including identifier rules and country-tag restrictions, while avoiding the pretense that it can resolve every possible mod-specific reference.

Human-readable localization text is not treated the same way as engine-sensitive identifiers. The compiler should validate values where invalid input can cause concrete problems without imposing arbitrary restrictions on ordinary names and text.

The goal is to catch mistakes before they become confusing in-game failures, not to claim complete knowledge of every mod's contents.

### 2.7 Output generation and localization

An input filename generally determines its main output filename. The compiler emits supporting files when the relevant DSL or native game format requires them.

Localization is generated into the appropriate output structure, organized by language and content type. For example, focus localization belongs in the corresponding localization directory and generated language file.

Some output names depend on information contained in the source rather than on the input filename alone. Country-history output is an important example because generated paths or names can depend on country-specific data.

This distinction is significant for output cleanup: the mapping between a source file and its generated files cannot always be reconstructed from the source extension or filename.

### 2.8 Compiler-to-IDE communication

The compiler communicates structured information back to the IDE using JSON messages over IPC.

For example, a validation report can include total error and warning counts and a list of affected files. The IDE can consume this information without having to interpret human-readable log messages as if they were a machine protocol.

The main process can also request structured compiler information, such as an AST serialized as JSON.

This keeps the IDE's integration with the compiler explicit and allows compiler results to support features beyond simply writing output files.

## 3. Importer Architecture

### 3.1 The importer and compiler are separate projects

The importer and compiler are maintained as separate Visual Studio projects under the C# directory.

Their responsibilities are different:

- **Importer:** transfers and transforms existing mod content into a Marshal workspace.
- **Compiler:** validates and transpiles Marshal source into native HOI4 content.

Keeping them separate avoids coupling import-specific processing to the compiler's responsibilities.

### 3.2 Why the importer is written in C#

The importer is designed for high-throughput processing of potentially very large mods.

A large import may involve multiple gigabytes of content. Some input structures also need to be consolidated: for example, localization and focus-tree content may be combined into a single Marshal source representation.

These operations can require substantial memory and filesystem activity.

C# was chosen to provide high performance and more controlled resource usage than the original JavaScript-based approach. The importer is intended to process large projects efficiently, with filesystem throughput often becoming a major limiting factor.

The importer and compiler therefore share a language but remain independently responsible for their respective workloads.

### 3.3 Publishing the C# components

The importer and compiler are published from their respective Visual Studio projects using platform-specific publish settings.

They are distributed as self-contained executables, avoiding a requirement for users to separately install the .NET runtime. Executable names vary by platform, such as `Compiler.exe` on Windows and `Compiler` on Linux.

This makes deployment more predictable and reduces external runtime prerequisites.

## 4. IDE Frontend and Runtime

### 4.1 JavaScript and Vue

The frontend uses JavaScript and Vue because the JavaScript ecosystem provides a mature collection of tools and frameworks for building interactive interfaces.

Vue offers a lightweight component-based approach that suits the IDE's interface. The project also uses Tailwind CSS to reduce unnecessary stylesheet output: its build process generates styles for the classes used by the application rather than shipping a large collection of unused CSS rules.

These choices prioritize a maintainable, responsive interface without making the frontend needlessly heavy.

### 4.2 Electron for Windows and Linux

Electron provides a consistent desktop application environment across Windows and Linux.

The alternative would be to maintain more platform-specific behavior and presentation logic, potentially including separate workarounds for each operating system. Electron reduces that burden by allowing the project to share its frontend and much of its desktop integration.

Electron does have performance and memory overhead compared with a native application. Improvements in Electron and its surrounding ecosystem have helped reduce that cost, and Marshal applies additional optimizations where useful.

### 4.3 Bytecode precompilation

The Electron-Vite configuration enables `bytecode: true` for both the main-process and preload builds.

This applies to the Electron JavaScript bundles produced by those build targets. The renderer has a separate Vue and Tailwind build and does not enable the same bytecode option in the supplied configuration.

Precompiled bytecode can reduce repeated JavaScript parsing and compilation work when the application starts. It does not eliminate all runtime work, and the benefit depends on the code and execution environment.

The decision is a performance optimization layered on top of Electron, rather than a substitute for choosing an efficient architecture.

## 5. Build and Distribution

### 5.1 Build commands

Marshal's build pipeline depends on the requested npm script:

- npm start — Runs the Electron-Vite build and launches Electron with warning tracing.(Dev enviornment/dev mode)

- npm run dist — Builds the application and packages it using Electron Builder.

- npm run dist:win — Builds and packages the Windows distribution.

- npm run dist:linux — Builds and packages the Linux distribution.

- npm run dist:all — Builds and packages Windows and Linux distributions.

- npm run license-gen — Generates the third-party license inventory from package.json.

The main and preload bundles are built as CommonJS outputs. Electron-Vite also builds the renderer from the Vue application entry point.

### 5.2 Why packaging requires explicit resource handling

Marshal combines multiple runtime technologies and independently published components. The Electron application cannot be distributed correctly by packaging only the frontend bundle.

Electron Builder copies additional resources into the packaged application, including:

- Third-party license files.
- Published C# components.
- Published Python components.
- The project license.

The C# and Python components are copied from their published-component directories into the packaged application's `published-components` directory.

The build configuration also uses static copying to place required project resources alongside the built main-process code, while excluding source trees and development-only directories that should not be copied through that rule.

This resource layout is an important part of the runtime contract: changing where components are published or packaged may require corresponding changes to how the application locates and launches them.

The build is consequently more involved than a frontend-only application. It must coordinate JavaScript bundling, platform-specific executables, Python tooling, licenses, and final packaging.

## 6. Workspace State and Synchronization

### 6.1 Workspace metadata must travel with the workspace

Marshal stores important workspace metadata within the workspace itself.

This information can include the project name, configuration required by other components, and synchronization state. Keeping required metadata alongside the project means another person importing the workspace can recover the information needed to use it correctly.

Workspace metadata is therefore not all disposable build output. Some of it is part of the workspace's persistent state and must be preserved across import and export.

Individual caches may be regenerable, but that does not make every field in the metadata files safe to discard.

### 6.2 The sync engine tracks source-file state

The sync engine maintains a manifest under the workspace metadata directory. It records source files and their relative paths, sizes, modification times, and fast content hashes.

At startup, the engine compares the current input directory against the previously recorded manifest.

This detects changes that occurred while the workspace was closed, including:

- New files added outside the IDE.
- Modified files.
- Deleted files.
- Same-size modifications where the modification time changed.

For performance, the fast hash incorporates file size and samples the beginning and end of larger files rather than always hashing the entire file. Small files are hashed in full.

This is a deliberate compromise: it makes routine synchronization cheaper than reading every byte of every large file, while providing a useful signal for detecting offline changes.

The hash is a fast change-detection mechanism, not a cryptographic guarantee that two arbitrary files are identical.

### 6.3 Manifest writes are coordinated and atomic

The sync engine batches concurrent save requests to avoid redundant writes and races between overlapping operations.

It writes the updated manifest to a temporary file and then moves that file into place, replacing the previous manifest. This reduces the chance of leaving a partially written JSON manifest if a write is interrupted.

The implementation also supports migration from an older manifest layout.

If the manifest is corrupted or unreadable, the engine logs the problem and warns that it may no longer be possible to identify deletions reliably from the previous session. This is important because missing synchronization history can otherwise leave obsolete generated files behind.

## 7. Output Ownership and Deletion

### 7.1 Why the original cleanup design became fragile

The original deletion handler used manual mappings and reconstructed generated output names based on source types, extensions, and naming conventions.

This was sufficient for simpler outputs, but it became increasingly brittle as the compiler gained more DSLs and dynamic output naming.

The central problem is not merely that a filename can be guessed incorrectly. The compiler may know information that the JavaScript deletion handler does not have, such as a country tag or name extracted from a source file.

To reconstruct every output path correctly, the deletion handler would have to reproduce a significant amount of compiler logic in JavaScript.

That would create two implementations of the same naming rules. Every new DSL or output convention could require synchronized changes in both places, making cleanup increasingly error-prone.

### 7.2 v1.5.0: compiler-generated output manifests

The planned v1.5.0 metadata-driven cleanup replaces those duplicated heuristics with compiler-generated output manifests.

The compiler will record the exact relative output paths produced by each source file. The IDE can then use the compiler's own output record to determine which generated files belong to a source and which files have become obsolete.

This approach is intended to support reliable cleanup for dynamically named outputs, including country-history files, without requiring the deletion handler to understand the compiler's naming rules.

The design follows a simple ownership principle:

**The component that knows which outputs it generated should be the source of truth for those outputs.**

The existing source-sync manifest and the planned compiler-output manifest serve different purposes:

- The **sync manifest** tracks the state of input files between workspace sessions.
- The **output manifest** tracks the generated files associated with each source file.

The output manifest should also be updated safely. The previous valid record should remain available until a new compilation succeeds, so a failed or partial compilation does not incorrectly make previously valid outputs appear obsolete.

This is a planned architectural change for v1.5.0, not a claim that the new mechanism is already implemented.

## 8. Image Conversion Pipeline

### 8.1 Python as the conversion orchestrator

Image conversion uses a persistent Python worker, managed by the Electron main process.

Python was chosen because it works well as an orchestration layer for external tools and has a mature ecosystem of image-processing libraries available if additional processing is required.

Although Node.js can also spawn subprocesses, Python was preferred for this workload, where launching and coordinating external conversion processes is central to the job.

The worker communicates through standard streams, with the JavaScript integration responsible for starting it, communicating with it, and managing its lifecycle.

### 8.2 Why the Python worker persists

The Python worker is kept alive rather than repeatedly launched for each image-conversion request.

Image conversion can involve many requests and repeated subprocess launches. Starting a fresh orchestration process for every request would add unnecessary startup overhead.

A persistent worker keeps the orchestration process available while individual conversions are requested as needed.

The worker itself is not the DDS conversion engine: it launches the conversion utility for the actual work.

### 8.3 Why texconv

Marshal uses the custom texconv binary from the Texconv-Custom-DLL project.

The tool was selected for its speed and established capabilities in DDS conversion. It supports a broad range of source image formats, including common formats such as PNG and JPEG and formats such as TGA that are useful in modding workflows.

The goal is to accept a wide variety of source images and reliably produce the DDS assets required by HOI4.

The third-party project's licensing information is retained in the repository's licenses directory.

### 8.4 Layered conversion APIs

Image processing is exposed through reusable functions rather than being reimplemented in each IPC handler.

The main process manages the Python worker and communication. A reusable processing function handles conversion requests, while higher-level logic supplies the arguments appropriate to the requested image type.

This allows both import workflows and the frontend to request processing through the same underlying path.

For example, the frontend may need to regenerate an image cache that is missing or has never been created. It should be able to request that work through IPC without having to know the details of Python, texconv, or the conversion pipeline.

The layered API keeps callers simple while centralizing the more complex processing behavior.

### 8.5 Image caches and metadata

The pipeline produces two PNG representations for different UI purposes:

- A full-size PNG cache for viewing an image in the IDE.
- A 32×32 thumbnail for file-tree previews.

The thumbnail is embedded as base64 in the image configuration file so that the file tree can access a small preview without separately opening an image file for every item.

The image configuration also stores important metadata, including the image's asset type. Other components can use this information to determine how an asset should be handled.

This configuration is persistent workspace state, not merely a disposable cache. The metadata may be necessary to interpret or manage the workspace after it is imported elsewhere. Regenerable image data and essential metadata should therefore be treated differently.

Temporary files normally use the operating system's temporary directory, with exact handling depending on the operation.

### 8.6 Semantic asset types and managed output locations

The image importer is built around HOI4 asset types rather than generic file conversion. It places imported assets directly into the appropriate HOI4 image subdirectories, following the IDE's expected project structure. It does not generally create a separate `ManagedByMarshalIDE` folder to isolate its outputs.

Marshal IDE treats its generated files as IDE-managed outputs, even when they live alongside other project files. It assumes control over those outputs and does not ask whether the user has manually edited them before overwriting them. If a generated file is changed without updating its corresponding source input, a later generation can overwrite that change without warning. The source input is authoritative; generated output is not intended to be edited independently.

Semantic asset types determine what the importer generates and where it places the results. For example, selecting **Country Flag** and supplying a country tag lets the importer generate the required DDS sizes from a single source image and place them in the appropriate game directories. This removes the need to manually create each variant or determine its destination. The IDE handles the game-specific output requirements, while the source image and relevant asset metadata remain associated with the workspace.

## 9. Process Lifecycle and Stability

### 9.1 Persistent compiler processes are workspace-specific

When a workspace is opened, the IDE starts a persistent compiler process configured for that workspace, including its output paths.

The compiler is not treated as a single globally configured service shared indiscriminately across workspaces. Workspace-specific configuration belongs to the process serving that workspace.

When the workspace is left, the associated compiler and watcher are shut down.

This design supports long-lived compiler sessions without requiring a new compiler process for every compilation request.

### 9.2 Watcher-to-compiler flow

The watcher detects file changes inside the workspace's configured input root and requests compilation through the main process. The main process forwards the request to the persistent compiler.

This keeps the watcher focused on detecting changes and the compiler focused on validation and code generation.

The main process also mediates requests for other compiler information, such as an AST returned as JSON.

### 9.3 v1.5.1: shutdown ordering and IPC organization

The planned v1.5.1 stability work addresses a known shutdown-ordering problem involving the watcher and compiler. The ordering can interfere with reliable session archiving and process cleanup.

The fix is important because a correct steady-state compilation workflow is not sufficient if processes are left running or session cleanup becomes unreliable when a workspace closes.

The release also plans to move IPC handlers out of the increasingly large `main.js` into more modular files.

These changes address lifecycle reliability and maintainability without changing the core principle that the main process coordinates the desktop application and its supporting processes.

## 10. Release Roadmap and Known Weaknesses

### Roadmap Context

The sections below document planned work and explain the technical context behind certain upcoming changes. They are not the authoritative or necessarily up-to-date release roadmap. For the latest roadmap, see `About Marshal IDE/Roadmap.md`.

This section exists to preserve additional development context: why certain changes are planned, which architectural weaknesses they address, and why particular improvements matter.

### v1.5.0 — Developer Experience Update

The planned v1.5.0 work includes:

- Built-in DDS image preview.
- Integrated JPEG/PNG-to-DDS conversion.
- Compiler-generated output manifests for metadata-driven cleanup.
- Reliable deletion and renaming cleanup for dynamically named outputs.
- Updated hardcoded release notes in the What's New modal.

The output-manifest work is particularly significant because it removes the need to maintain duplicated output-naming logic in the deletion handler. Instead, the IDE can use the compiler's record of generated files to determine which outputs belong to each source input, including outputs whose names depend on source content.

### v1.5.1 — Stability and Maintainability

The planned v1.5.1 work includes:

- Fixing watcher/compiler shutdown ordering.
- Improving process cleanup and session archiving reliability.
- Moving IPC handlers out of `main.js` to improve modularity.

These items provide technical context for known weaknesses that have become more apparent as Marshal IDE's features and process interactions have grown. Their inclusion here explains the reasoning behind the planned work; consult `About Marshal IDE/Roadmap.md`

## 11. Project Distribution and Licensing

Marshal IDE is a closed-source project. Access to repository contribution mechanisms is restricted by the project owner, and pull requests from unauthorized users are not accepted.

The applicable End User License Agreement (EULA) governs the use and distribution of Marshal IDE and its components. You can review the license terms in `LICENSE.txt`. When launching the IDE, the EULA manager will require you to accept the applicable EULA, or a specific EULA presented for that situation, before proceeding. By using the IDE, you must accept the applicable license agreement to continue.

Although GitHub's platform behavior may allow a repository to be forked, that does not grant permission to redistribute or develop an unauthorized fork. The project's EULA governs permitted use and distribution.

The application also includes third-party dependencies and tools. Their licenses are retained in the repository's license materials, and the build pipeline includes generation of a third-party license inventory.

Contributors or maintainers changing dependencies, published binaries, or packaging rules must preserve the relevant licensing and distribution requirements.

---

## Summary of Architectural Principles

Marshal's major design decisions follow a consistent set of principles:

1. **Generate native game content.** Marshal should make HOI4 modding easier without requiring a replacement game runtime.
2. **Automate repetitive work, not important decisions.** DSLs should reduce boilerplate while preserving control over meaningful game-specific values.
3. **Validate before generating output.** Invalid source should be caught before it can produce broken game files.
4. **Separate responsibilities.** The importer, compiler, watcher, image pipeline, and UI have distinct jobs.
5. **Choose tools for actual workloads.** C# handles performance-sensitive importing and compilation; Python orchestrates image conversion; Vue and Electron provide the cross-platform IDE.
6. **Keep state portable.** Essential workspace metadata should travel with the workspace rather than depend on hidden local state.
7. **Avoid duplicated sources of truth.** Compiler-generated output manifests replace JavaScript logic that would otherwise have to reproduce compiler naming rules.
8. **Optimize based on real needs.** Performance improvements should address actual workloads and observed bottlenecks rather than hypothetical extremes.
9. **Treat lifecycle correctness as part of architecture.** Persistent workers and processes need reliable startup, communication, and shutdown behavior.
10. **Document planned behavior honestly.** Roadmap items should remain distinguishable from implemented guarantees.

These principles are intended to guide future internal changes while preserving the practical goals that led to Marshal IDE in the first place.
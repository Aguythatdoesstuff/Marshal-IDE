import { spawn } from 'child_process';
import path from 'path';
import { BrowserWindow } from 'electron'; //
import { resolveBinaryPath } from './binaryUtils.js';
// // src/Helpers/binary.js
function logIfAvailable(loggerFn, type, message, source = 'Binary-Lifecycle') {
	if (typeof loggerFn !== 'function') return;

	try {
		loggerFn({ type, message, source });
	} catch {
		// Ignore logger failures during teardown/shutdown
	}
}

// process spawning



const PROCESS_STATE_KEYS = {
	compiler: 'compilerProcess',
	'img-converter': 'imgConverterProcess',
	importer: 'importerProcess'
};

const BINARY_SPAWN_SOURCE = 'Binary-Spawn';

function buildProcessArgs(processName, state) {
	const logDir = typeof state?.getLogsSessionDir === 'function' ? state.getLogsSessionDir() : undefined;

	if (processName === 'compiler') {
		const cacheDir = state?.METADATA_DIR ? path.join(state.METADATA_DIR, 'fileOutCache') : undefined;
		return [
			`--output=${state?.OUTPUT_DIR ?? ''}`,
			`--cachePath=${cacheDir ?? ''}`,
			`--debug=${logDir ?? ''}`
		];
	}

	if (processName === 'img-converter') {
		return logDir ? [`--log-path=${logDir}`] : [];
	}

	if (processName === 'importer') {
		const importerInput = state?.currentProjectConfig?.input_dir ?? state?.INPUT_DIR ?? '';
		const importerOutput = state?.OUTPUT_DIR ?? state?.currentProjectConfig?.output_dir ?? '';
		return [
			`--input=${importerInput}`,
			`--output=${importerOutput}`,
			`--debug=${logDir ?? ''}`
		];
	}

	throw new Error(`Unsupported binary process: "${processName}"`);
}

function attachLineStream(stream, onLine) {
	if (!stream) return;

	let buffer = '';
	stream.on('data', (chunk) => {
		buffer += chunk.toString();
		const lines = buffer.split('\n');
		buffer = lines.pop() ?? '';

		for (const line of lines) {
			const trimmed = line.trim();
			if (trimmed) {
				onLine(trimmed);
			}
		}
	});
}

function getActiveBrowserWindow() {
	const wins = BrowserWindow.getAllWindows();
	return wins.length > 0 ? wins[0] : null;
}

export async function spawnBinary(processName, state) {
	const stateKey = PROCESS_STATE_KEYS[processName];
	if (!stateKey) {
		throw new Error(`Unknown binary process: "${processName}"`);
	}

	const loggerFn = state?.handleIpcLog;

	if (state[stateKey]) {
		await stopProcesses([state[stateKey]], loggerFn, BINARY_SPAWN_SOURCE);
		state[stateKey] = null;
	}

	const binaryPath = resolveBinaryPath(processName, (type, message) => {
		logIfAvailable(loggerFn, type, message, BINARY_SPAWN_SOURCE);
	});
	const args = buildProcessArgs(processName, state);

	logIfAvailable(loggerFn, 'info', `Spawning ${processName} process: ${binaryPath}`, BINARY_SPAWN_SOURCE);

	return new Promise((resolve, reject) => {
		let settled = false;
		let child;

		const finishResolve = (value) => {
			if (settled) return;
			settled = true;
			resolve(value);
		};

		const finishReject = (err) => {
			if (settled) return;
			settled = true;
			reject(err);
		};

		try {
			child = spawn(binaryPath, args);
		} catch (err) {
			logIfAvailable(loggerFn, 'error', `Failed to spawn ${processName}: ${err.message}`, BINARY_SPAWN_SOURCE);
			finishReject(err);
			return;
		}

		state[stateKey] = child;

		attachLineStream(child.stdout, (line) => {
			if (processName === 'importer') {
				logIfAvailable(loggerFn, 'info', `[Importer] ${line}`, BINARY_SPAWN_SOURCE);
				const activeWin = getActiveBrowserWindow();
				if (activeWin && !activeWin.isDestroyed()) {
					activeWin.webContents.send('importer-stdout-line', line);
				}
				return;
			}

			const prefix = processName === 'compiler' ? '[Compiler Output]' : '[ImgConverter Output]';
			logIfAvailable(loggerFn, 'info', `${prefix}: ${line}`, BINARY_SPAWN_SOURCE);
		});

		attachLineStream(child.stderr, (line) => {
			if (processName === 'importer') {
				logIfAvailable(loggerFn, 'error', `[Importer Error] ${line}`, BINARY_SPAWN_SOURCE);
				const activeWin = getActiveBrowserWindow();
				if (activeWin && !activeWin.isDestroyed()) {
					activeWin.webContents.send('importer-stderr-line', line);
				}
				return;
			}

			const prefix = processName === 'compiler' ? '[Compiler Error]' : '[ImgConverter Error]';
			logIfAvailable(loggerFn, 'error', `${prefix}: ${line}`, BINARY_SPAWN_SOURCE);
		});

		child.on('error', (err) => {
			if (state[stateKey] === child) {
				state[stateKey] = null;
			}
			logIfAvailable(loggerFn, 'error', `${processName} process error: ${err.message}`, BINARY_SPAWN_SOURCE);
			finishReject(err);
		});

		child.on('close', (code) => {
			if (state[stateKey] === child) {
				state[stateKey] = null;
			}

			if (code === 0) {
				logIfAvailable(loggerFn, 'info', `${processName} process exited cleanly with code 0.`, BINARY_SPAWN_SOURCE);
				finishResolve(child);
			} else {
				logIfAvailable(loggerFn, 'error', `${processName} process exited with code ${code}.`, BINARY_SPAWN_SOURCE);
				if (processName === 'importer') {
					finishReject(new Error(`Importer exited with code ${code}`));
				} else {
					finishResolve(child);
				}
			}
		});

		if (processName !== 'importer') {
			finishResolve(child);
		}
	});
}

export async function spawnBinaries(processNames = [], state) {
	if (!Array.isArray(processNames) || processNames.length === 0) {
		return [];
	}

	return Promise.all(processNames.map((name) => spawnBinary(name, state)));
}




// process shutdown sequence


function stopSingleProcess(proc, loggerFn, source = 'Binary-Lifecycle') {
	if (!proc || proc.killed || proc.exitCode !== null) {
		return Promise.resolve();
	}

	return new Promise((resolve) => {
		let settled = false;
		let sigintTimer = null;
		let sigkillTimer = null;

		const cleanup = () => {
			if (settled) return;
			settled = true;
			if (sigintTimer) {
				clearTimeout(sigintTimer);
				sigintTimer = null;
			}
			if (sigkillTimer) {
				clearTimeout(sigkillTimer);
				sigkillTimer = null;
			}
			proc.removeAllListeners('exit');
			proc.removeAllListeners('close');
			proc.removeAllListeners('error');
			resolve();
		};

		proc.once('exit', () => {
			logIfAvailable(loggerFn, 'info', `Process (PID: ${proc.pid}) exited cleanly.`, source);
			cleanup();
		});
		proc.once('close', cleanup);
		proc.once('error', (err) => {
			logIfAvailable(loggerFn, 'error', `Error while stopping process (PID: ${proc.pid}): ${err.message}`, source);
			cleanup();
		});

		try {
			logIfAvailable(loggerFn, 'info', `Sending SIGTERM to process (PID: ${proc.pid}).`, source);
			proc.kill('SIGTERM');
		} catch (err) {
			logIfAvailable(loggerFn, 'warn', `SIGTERM failed for process (PID: ${proc.pid}): ${err.message}`, source);
		}

		sigintTimer = setTimeout(() => {
			if (settled || proc.exitCode !== null) {
				cleanup();
				return;
			}
			try {
				logIfAvailable(loggerFn, 'warn', `Escalating to SIGINT for process (PID: ${proc.pid}).`, source);
				proc.kill('SIGINT');
			} catch (err) {
				logIfAvailable(loggerFn, 'warn', `SIGINT failed for process (PID: ${proc.pid}): ${err.message}`, source);
			}
		}, 1500);

		sigkillTimer = setTimeout(() => {
			if (settled || proc.exitCode !== null) {
				cleanup();
				return;
			}
			try {
				logIfAvailable(loggerFn, 'error', `Escalating to SIGKILL for process (PID: ${proc.pid}).`, source);
				proc.kill('SIGKILL');
			} catch (err) {
				logIfAvailable(loggerFn, 'error', `SIGKILL failed for process (PID: ${proc.pid}): ${err.message}`, source);
			}
			cleanup();
		}, 3000);
	});
}

export async function stopProcesses(processes = [], loggerFn = null, source = 'Binary-Lifecycle') {
	if (!Array.isArray(processes) || processes.length === 0) {
		logIfAvailable(loggerFn, 'warn', 'stopProcesses called with a non-array or empty array', source);
		return;
	}

	const validProcesses = processes.filter(Boolean);
	await Promise.all(validProcesses.map((proc) => stopSingleProcess(proc, loggerFn, source)));
}
// --- Console API Methods ---
export const clearLogConsole = (e) => {
  if (e) e.stopPropagation();
  consoleLogs.value = [
    { timestamp: new Date().toLocaleTimeString(), message: 'Console Cleared.', isError: false }
  ];
};

export const showConsoleMessage = (message, isError = false) => {
  consoleLogs.value.push({
    timestamp: new Date().toLocaleTimeString(),
    message: message,
    isError: isError
  });
  
  nextTick(() => {
    if (consoleOutputElement.value) {
      consoleOutputElement.value.scrollTop = consoleOutputElement.value.scrollHeight;
    }
  });
};


// IPC Log Communication channels
export const handleIncomingLog = (logData) => {
  const isError = logData.level.toLowerCase() === 'error' || logData.level.toLowerCase() === 'warn';
  const formattedMessage = `[${logData.source}][${logData.level.toUpperCase()}]: ${logData.message}`;
  showConsoleMessage(formattedMessage, isError);
};
import { createApp } from 'vue'
import App from './App.vue'
import './assets/main.scss'

const app = createApp(App)

/**
 * Universal dispatcher. Safely targets the correct pre-exposed `window.api.log`
 * methods provided by your preload bridge configuration.
 */
const dispatchToInternalLogger = (level, message, source = 'Renderer-CatchAll') => {
  if (window.api && window.api.log && window.api.log[level]) {
    window.api.log[level](message, source);
  } else {
    // Standard platform backup warning if running in a pure browser context
    originalConsole.warn(`[Bridge Missing] Deferred Log (${level}) [${source}]: ${message}`);
  }
};

// ========================================================
// 1. INTERCEPT VUE FRAMEWORK ERRORS & LIFECYCLE CRASHES
// ========================================================
app.config.errorHandler = (err, instance, info) => {
  const msg = `[Vue Runtime Error]: ${err.stack || err.message} (Context: ${info})`;
  dispatchToInternalLogger('error', msg, 'Vue-Framework');
  
  // Retain normal DevTools terminal trace viewing
  originalConsole.error(err);
};

// ========================================================
// 2. INTERCEPT ALL RUNTIME CONSOLE METHODS
// ========================================================
const originalConsole = {
  log: console.log,
  info: console.info,
  warn: console.warn,
  error: console.error
};

['log', 'info', 'warn', 'error'].forEach((level) => {
  console[level] = (...args) => {
    // Ensure normal DevTools logging functionality stays intact
    originalConsole[level].apply(console, args);

    // Safely decompose text blocks, complex object trees, or reactive Proxies
    const message = args.map(arg => {
      if (arg instanceof Error) return arg.stack;
      if (arg && typeof arg === 'object') {
        try { return JSON.stringify(arg); } catch (e) { return String(arg); }
      }
      return String(arg);
    }).join(' ');

    // SMART FILTER: Prevent circular logging loops if a Winston string leaks back
    if (/^\d{4}-\d{2}-\d{2}/.test(message)) return;

    // Direct string maps over to your preload methods
    const determinedLevel = level === 'log' ? 'info' : level;
    dispatchToInternalLogger(determinedLevel, message, 'Console-Intercept');
  };
});

// ========================================================
// 3. INTERCEPT GLOBAL SYNTAX EXCEPTIONS & ASYNC REJECTIONS
// ========================================================
window.addEventListener('error', (event) => {
  const msg = `[Unhandled Exception]: ${event.message} at ${event.filename}:${event.lineno}`;
  dispatchToInternalLogger('error', msg, 'Global-Exception');
});

window.addEventListener('unhandledrejection', (event) => {
  const msg = `[Unhandled Promise Rejection]: ${event.reason?.stack || event.reason}`;
  dispatchToInternalLogger('error', msg, 'Global-Promise');
});

if (window.api && window.api.logBroadcaster) {
  window.api.logBroadcaster.addListener('log', (logData) => {
    if (!logData) return;

    // Dispatch a clean DOM custom event that any UI console element can listen for
    const consoleEvent = new CustomEvent('marshal-runtime-log', { 
      detail: logData 
    });
    window.dispatchEvent(consoleEvent);
  });
}

app.mount('#app')
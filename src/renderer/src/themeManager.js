// src/utils/themeManager.js

// 1. Exact copy of your defaults from main.scss
const DEFAULT_THEME = {
  '--sidebar-bg': '#252526',
  '--editor-bg': '#1e1e1e',
  '--card-bg': '#2d2d30',
  '--text-color': '#cccccc',
  '--primary-blue': '#007acc',
  '--border-color': '#2d2d30',
  '--text-muted': '#858585',
};

export const themeManager = {
  // Get current values (either from localStorage or defaults)
  getTheme() {
    const saved = localStorage.getItem('user-custom-theme');
    return saved ? JSON.parse(saved) : { ...DEFAULT_THEME };
  },

  // Apply a specific color variable to the whole app
  setVariable(key, value) {
    const currentTheme = this.getTheme();
    currentTheme[key] = value;
    
    // Save to localStorage and update the browser DOM
    localStorage.setItem('user-custom-theme', JSON.stringify(currentTheme));
    document.documentElement.style.setProperty(key, value);
  },

  // Initialize theme on app startup
  init() {
    const theme = this.getTheme();
    Object.entries(theme).forEach(([key, value]) => {
      document.documentElement.style.setProperty(key, value);
    });
  },

  // Reset everything back to factory settings
  reset() {
    localStorage.removeItem('user-custom-theme');
    Object.entries(DEFAULT_THEME).forEach(([key, value]) => {
      document.documentElement.style.setProperty(key, value);
    });
    return { ...DEFAULT_THEME }; // Return defaults to update Vue state
  }
};
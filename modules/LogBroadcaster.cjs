// LogBroadcaster.cjs
const EventEmitter = require('events'); // CJS syntax (required by Main Process)

const logBroadcaster = new EventEmitter();
logBroadcaster.setMaxListeners(20);

module.exports = logBroadcaster; // CJS export

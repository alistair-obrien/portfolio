let runtimePromise;

async function ensureRuntime() {
  if (!runtimePromise) {
    runtimePromise = initializeRuntime();
  }

  return runtimePromise;
}

async function initializeRuntime() {
  const { dotnet } = await import("./_framework/dotnet.js");
  const runtime = await dotnet.withDiagnosticTracing(false).create();
  const config = runtime.getConfig();
  const exports = await runtime.getAssemblyExports(config.mainAssemblyName);

  await runtime.runMain();

  return exports.BrowserGameExports;
}

function parseEnvelope(payload) {
  const envelope = JSON.parse(payload);

  if (!envelope || typeof envelope !== "object") {
    throw new Error("The GenOSys runtime returned an invalid response.");
  }

  return envelope;
}

function serializeCommand(command) {
  return JSON.stringify(command);
}

function serializeCommands(commands) {
  return JSON.stringify(commands);
}

export function createCommand(type, payload) {
  return { Type: type, Payload: payload };
}

export async function createSession() {
  const exports = await ensureRuntime();
  return parseEnvelope(exports.CreateSession());
}

export async function resetSession(sessionId) {
  const exports = await ensureRuntime();
  return parseEnvelope(exports.ResetSession(sessionId));
}

export async function disposeSession(sessionId) {
  const exports = await ensureRuntime();
  return parseEnvelope(exports.DisposeSession(sessionId));
}

export async function getGameState(sessionId) {
  const exports = await ensureRuntime();
  return parseEnvelope(exports.GetGameState(sessionId));
}

export async function executeCommand(sessionId, command) {
  const exports = await ensureRuntime();
  return parseEnvelope(exports.ExecuteCommand(sessionId, serializeCommand(command)));
}

export async function executeCommands(sessionId, commands) {
  const exports = await ensureRuntime();
  return parseEnvelope(exports.ExecuteCommands(sessionId, serializeCommands(commands)));
}

export async function executeTrackedCommand(sessionId, command) {
  const exports = await ensureRuntime();
  return parseEnvelope(exports.ExecuteTrackedCommand(sessionId, serializeCommand(command)));
}

export async function executeTrackedCommands(sessionId, commands) {
  const exports = await ensureRuntime();
  return parseEnvelope(exports.ExecuteTrackedCommands(sessionId, serializeCommands(commands)));
}

export async function executePreviewCommand(sessionId, command) {
  const exports = await ensureRuntime();
  return parseEnvelope(exports.ExecutePreviewCommand(sessionId, serializeCommand(command)));
}

export async function executePreviewCommands(sessionId, commands) {
  const exports = await ensureRuntime();
  return parseEnvelope(exports.ExecutePreviewCommands(sessionId, serializeCommands(commands)));
}

export async function handleRequest(sessionId, request) {
  const exports = await ensureRuntime();
  return parseEnvelope(exports.HandleRequest(sessionId, JSON.stringify(request)));
}

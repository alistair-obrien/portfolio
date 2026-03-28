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
    throw new Error("The roguelike engine returned an invalid response.");
  }

  return envelope;
}

export async function createSession() {
  const exports = await ensureRuntime();
  return parseEnvelope(exports.CreateSession());
}

export async function getSessionState(sessionId) {
  const exports = await ensureRuntime();
  return parseEnvelope(exports.GetSessionState(sessionId));
}

export async function resetSession(sessionId) {
  const exports = await ensureRuntime();
  return parseEnvelope(exports.ResetSession(sessionId));
}

export async function previewPlayerMoveToCell(sessionId, mapId, x, y) {
  const exports = await ensureRuntime();
  return parseEnvelope(exports.PreviewPlayerMoveToCell(sessionId, mapId, x, y));
}

export async function movePlayerToCell(sessionId, mapId, x, y) {
  const exports = await ensureRuntime();
  return parseEnvelope(exports.MovePlayerToCell(sessionId, mapId, x, y));
}

export async function disposeSession(sessionId) {
  const exports = await ensureRuntime();
  return exports.DisposeSession(sessionId);
}

let runtimePromise;

async function ensureRuntime() {
  if (!runtimePromise) {
    runtimePromise = initializeRuntime();
  }

  return runtimePromise;
}

async function initializeRuntime() {
  const runtimeQuery = new URL(import.meta.url).search || "";
  const { dotnet } = await import(`./_framework/dotnet.js${runtimeQuery}`);
  const runtime = await dotnet.withDiagnosticTracing(false).create();
  const config = runtime.getConfig();
  const exports = await runtime.getAssemblyExports(config.mainAssemblyName);

  await runtime.runMain();

  return exports.BrowserProcGenExports;
}

function parseEnvelope(payload) {
  const envelope = JSON.parse(payload);

  if (!envelope || typeof envelope !== "object") {
    throw new Error("The proc-gen engine returned an invalid response.");
  }

  return envelope;
}

export async function generateMap(request) {
  const exports = await ensureRuntime();
  return parseEnvelope(exports.GenerateMap(JSON.stringify(request)));
}

export async function getGeneratorCatalog() {
  const exports = await ensureRuntime();
  return parseEnvelope(exports.GetGeneratorCatalog());
}

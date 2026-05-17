let runtimePromise;
let runtimeInstance = null;
let runtimeProgress = { phase: "idle", percent: 0 };
let runtimePulseTimer = null;
let persistentDirectoryHandle = null;
const persistentAssetUrlCache = new Map();
const runtimeProgressListeners = new Set();
const logPrefix = "[GenOSys Loader]";
const directoryStoreName = "genosys-web-file-system";
const directoryStoreKey = "persistent-directory";
const browserStorageKey = "persistent-files";
const dataRootModeStorageKey = "genosys-data-root-mode";
const devPublicDataRootEndpoint = "/__genosys-dev-data";
const bundledDataRootUrl = new URL("../genosys-data/index.json", import.meta.url).toString();
const dataRootModes = {
  local: "local",
  devPublic: "dev-public",
};
const transportFormats = {
  json: "json",
  binary: "binary",
};
let transportFormat = transportFormats.json;
let utf8Encoder = null;
let utf8Decoder = null;

const binaryTags = {
  null: 0,
  false: 1,
  true: 2,
  double: 3,
  string: 4,
  array: 5,
  object: 6,
  integer: 7,
};

function elapsedMs(startedAt) {
  return Math.round((performance.now() - startedAt) * 10) / 10;
}

function logLoaderStep(operationName, fields = {}) {
  const ok = fields.ok !== false && !fields.error;
  const durationMs = getPrimaryDurationMs(fields);
  const durationText = typeof durationMs === "number" ? `${durationMs.toFixed(1)}ms` : "";

  console.info(
    `%c${logPrefix}%c ${operationName}%c ${ok ? "OK" : "FAIL"}%c${durationText ? ` ${durationText}` : ""}`,
    "font-weight: 700; color: #bd93f9;",
    "color: inherit; font-weight: 400;",
    ok
      ? "color: #3ddc84; font-weight: 700;"
      : "color: #ff5f57; font-weight: 700;",
    getDurationStyle(durationMs),
    getLoaderLogDetails(fields)
  );
}

function getLoaderLogDetails(fields = {}) {
  return compactFields({
    timing: compactFields({
      stepMs: fields.stepMs,
      totalMs: fields.totalMs,
      elapsedMs: fields.elapsedMs,
      durationMs: fields.durationMs,
    }),
    progress: compactFields({
      percent: fields.percent,
    }),
    transfer: compactFields({
      loadedBytes: fields.loadedBytes,
      totalBytes: fields.totalBytes,
    }),
    files: fields.files,
    path: fields.path,
    error: fields.error,
  });
}

function getPrimaryDurationMs(fields = {}) {
  for (const key of ["stepMs", "totalMs", "elapsedMs", "durationMs"]) {
    const value = fields[key];
    if (typeof value === "number" && Number.isFinite(value)) {
      return value;
    }
  }

  return undefined;
}

function getDurationStyle(totalMs) {
  if (typeof totalMs !== "number" || !Number.isFinite(totalMs) || totalMs < 30) {
    return "color: inherit; font-weight: 400;";
  }

  if (totalMs < 100) {
    return "color: #ffd866; font-weight: 700;";
  }

  if (totalMs < 500) {
    return "color: #ff9f43; font-weight: 700;";
  }

  return "color: #ff5f57; font-weight: 700;";
}

function compactFields(fields = {}) {
  return Object.fromEntries(
    Object.entries(fields).filter(([, value]) =>
      value !== null &&
      value !== undefined &&
      value !== false &&
      value !== 0 &&
      value !== "" &&
      !(Array.isArray(value) && value.length === 0) &&
      !(isPlainObject(value) && Object.keys(value).length === 0)
    )
  );
}

function isPlainObject(value) {
  return typeof value === "object" &&
    value !== null &&
    Object.getPrototypeOf(value) === Object.prototype;
}

function setRuntimeProgress(phase, percent) {
  runtimeProgress = {
    phase,
    percent: Math.max(0, Math.min(100, Math.round(percent))),
  };

  for (const listener of runtimeProgressListeners) {
    listener(runtimeProgress);
  }
}

function startRuntimePulse(phase, startPercent, maxPercent) {
  stopRuntimePulse();
  setRuntimeProgress(phase, Math.max(runtimeProgress.percent, startPercent));

  runtimePulseTimer = setInterval(() => {
    if (runtimeProgress.percent < maxPercent) {
      setRuntimeProgress(phase, runtimeProgress.percent + 1);
    }
  }, 250);
}

function stopRuntimePulse() {
  if (runtimePulseTimer !== null) {
    clearInterval(runtimePulseTimer);
    runtimePulseTimer = null;
  }
}

async function ensureRuntime() {
  if (!runtimePromise) {
    runtimePromise = initializeRuntime();
  }

  return runtimePromise;
}

async function initializeRuntime() {
  const loadStartedAt = performance.now();
  let stepStartedAt = loadStartedAt;

  function markRuntimeStep(phase, percent, operationName, fields = {}) {
    setRuntimeProgress(phase, percent);
    logLoaderStep(operationName, {
      stepMs: elapsedMs(stepStartedAt),
      totalMs: elapsedMs(loadStartedAt),
      percent: runtimeProgress.percent,
      ...fields,
    });
    stepStartedAt = performance.now();
  }

  setRuntimeProgress("loading-dotnet-module", 2);
  const { dotnet } = await import("./_framework/dotnet.js");
  markRuntimeStep("creating-runtime", 8, "CreatingRuntime");

  let loadedBytes = 0;
  let totalBytes = 0;
  let resourcesDownloaded = false;
  markRuntimeStep("loading-runtime-resources", 10, "LoadingRuntimeResources");
  startRuntimePulse("loading-runtime-resources", 12, 66);
  const runtimeBuilder = dotnet
    .withDiagnosticTracing(false)
    .withModuleConfig({
      onDownloadResourceProgress(loaded, total) {
        loadedBytes = loaded;
        totalBytes = total;
        const ratio = total > 0 ? loaded / total : 0;
        if (ratio >= 1) {
          if (!resourcesDownloaded) {
            resourcesDownloaded = true;
            logLoaderStep("RuntimeResourcesDownloaded", {
              stepMs: elapsedMs(stepStartedAt),
              totalMs: elapsedMs(loadStartedAt),
              loadedBytes,
              totalBytes,
            });
          }
          setRuntimeProgress("initializing-runtime", Math.max(runtimeProgress.percent, 88));
          return;
        }

        setRuntimeProgress(
          "loading-runtime-resources",
          Math.max(runtimeProgress.percent, 10 + ratio * 58)
        );
      }
    });

  let runtime;
  const createStartedAt = performance.now();
  try {
    runtime = await runtimeBuilder.create();
    runtimeInstance = runtime;
  } finally {
    stopRuntimePulse();
  }
  logLoaderStep("RuntimeCreated", {
    stepMs: elapsedMs(createStartedAt),
    totalMs: elapsedMs(loadStartedAt),
    loadedBytes,
    totalBytes,
  });

  markRuntimeStep("reading-config", 90, "ReadingConfig");
  const config = runtime.getConfig();

  markRuntimeStep("loading-assembly-exports", 94, "LoadingAssemblyExports");
  const exportsStartedAt = performance.now();
  const exports = await runtime.getAssemblyExports(config.mainAssemblyName);
  logLoaderStep("AssemblyExportsLoaded", {
    stepMs: elapsedMs(exportsStartedAt),
    totalMs: elapsedMs(loadStartedAt),
  });

  markRuntimeStep("creating-session", 97, "CreatingSession");
  return exports.BrowserGameExports;
}

function parseEnvelope(payload) {
  const envelope = JSON.parse(payload);

  if (!envelope || typeof envelope !== "object") {
    throw new Error("The GenOSys runtime returned an invalid response.");
  }

  return envelope;
}

function byteLength(value) {
  return typeof TextEncoder !== "undefined"
    ? new TextEncoder().encode(value ?? "").length
    : String(value ?? "").length;
}

function normalizeTransportFormat(format) {
  return format === transportFormats.binary ? transportFormats.binary : transportFormats.json;
}

function getTextEncoder() {
  utf8Encoder ??= new TextEncoder();
  return utf8Encoder;
}

function getTextDecoder() {
  utf8Decoder ??= new TextDecoder();
  return utf8Decoder;
}

function encodeBinaryEnvelope(value) {
  const bytes = [];
  writeBinaryValue(bytes, value === undefined ? null : value);
  return new Uint8Array(bytes);
}

function decodeBinaryEnvelope(bytes) {
  const reader = {
    bytes: bytes instanceof Uint8Array ? bytes : new Uint8Array(bytes),
    offset: 0,
  };
  const value = readBinaryValue(reader);
  if (reader.offset !== reader.bytes.length) {
    throw new Error("Binary transport payload contains trailing data.");
  }
  return value;
}

function writeBinaryValue(bytes, value) {
  if (value === null || value === undefined) {
    bytes.push(binaryTags.null);
    return;
  }

  if (typeof value === "boolean") {
    bytes.push(value ? binaryTags.true : binaryTags.false);
    return;
  }

  if (typeof value === "number") {
    if (Number.isSafeInteger(value)) {
      bytes.push(binaryTags.integer);
      writeSignedVarInt(bytes, value);
      return;
    }

    bytes.push(binaryTags.double);
    writeDouble(bytes, value);
    return;
  }

  if (typeof value === "string") {
    bytes.push(binaryTags.string);
    writeBinaryString(bytes, value);
    return;
  }

  if (Array.isArray(value)) {
    bytes.push(binaryTags.array);
    writeVarInt(bytes, value.length);
    for (const entry of value) {
      writeBinaryValue(bytes, entry === undefined ? null : entry);
    }
    return;
  }

  if (typeof value === "object") {
    const entries = Object.entries(value).filter(([, entryValue]) => entryValue !== undefined);
    bytes.push(binaryTags.object);
    writeVarInt(bytes, entries.length);
    for (const [entryKey, entryValue] of entries) {
      writeBinaryString(bytes, entryKey);
      writeBinaryValue(bytes, entryValue);
    }
    return;
  }

  bytes.push(binaryTags.null);
}

function readBinaryValue(reader) {
  const tag = readByte(reader);
  switch (tag) {
    case binaryTags.null:
      return null;
    case binaryTags.false:
      return false;
    case binaryTags.true:
      return true;
    case binaryTags.double:
      return readDouble(reader);
    case binaryTags.string:
      return readBinaryString(reader);
    case binaryTags.array:
      return readBinaryArray(reader);
    case binaryTags.object:
      return readBinaryObject(reader);
    case binaryTags.integer:
      return readSignedVarInt(reader);
    default:
      throw new Error(`Unknown binary transport tag '${tag}'.`);
  }
}

function writeBinaryString(bytes, value) {
  const encoded = getTextEncoder().encode(value);
  writeVarInt(bytes, encoded.length);
  for (const byte of encoded) {
    bytes.push(byte);
  }
}

function readBinaryString(reader) {
  const length = readVarInt(reader);
  const end = reader.offset + length;
  if (end > reader.bytes.length) {
    throw new Error("Binary transport string length is invalid.");
  }
  const value = getTextDecoder().decode(reader.bytes.subarray(reader.offset, end));
  reader.offset = end;
  return value;
}

function readBinaryArray(reader) {
  const length = readVarInt(reader);
  const value = [];
  for (let index = 0; index < length; index += 1) {
    value.push(readBinaryValue(reader));
  }
  return value;
}

function readBinaryObject(reader) {
  const length = readVarInt(reader);
  const value = {};
  for (let index = 0; index < length; index += 1) {
    const key = readBinaryString(reader);
    value[key] = readBinaryValue(reader);
  }
  return value;
}

function writeDouble(bytes, value) {
  const buffer = new ArrayBuffer(8);
  new DataView(buffer).setFloat64(0, value, true);
  for (const byte of new Uint8Array(buffer)) {
    bytes.push(byte);
  }
}

function readDouble(reader) {
  if (reader.offset + 8 > reader.bytes.length) {
    throw new Error("Binary transport number ended early.");
  }
  const value = new DataView(reader.bytes.buffer, reader.bytes.byteOffset + reader.offset, 8).getFloat64(0, true);
  reader.offset += 8;
  return value;
}

function writeSignedVarInt(bytes, value) {
  writeVarInt(bytes, value >= 0 ? value * 2 : (-value * 2) - 1);
}

function readSignedVarInt(reader) {
  const value = readVarInt(reader);
  return value % 2 === 0 ? value / 2 : -((value + 1) / 2);
}

function writeVarInt(bytes, value) {
  let remaining = Math.max(0, Math.floor(value));
  while (remaining >= 0x80) {
    bytes.push((remaining & 0x7F) | 0x80);
    remaining = Math.floor(remaining / 128);
  }
  bytes.push(remaining);
}

function readVarInt(reader) {
  let result = 0;
  let shift = 0;
  while (shift < 53) {
    const value = readByte(reader);
    result += (value & 0x7F) * (2 ** shift);
    if ((value & 0x80) === 0) {
      return result;
    }
    shift += 7;
  }
  throw new Error("Binary transport varint is too large.");
}

function readByte(reader) {
  if (reader.offset >= reader.bytes.length) {
    throw new Error("Binary transport payload ended early.");
  }
  return reader.bytes[reader.offset++];
}

function normalizeDataRootMode(mode) {
  return mode === dataRootModes.devPublic ? dataRootModes.devPublic : dataRootModes.local;
}

function getStoredDataRootMode() {
  try {
    return normalizeDataRootMode(localStorage.getItem(dataRootModeStorageKey));
  } catch {
    return dataRootModes.local;
  }
}

function setStoredDataRootMode(mode) {
  const normalized = normalizeDataRootMode(mode);
  try {
    localStorage.setItem(dataRootModeStorageKey, normalized);
  } catch {
    // Storage can be unavailable in restrictive browser modes; keep the runtime switch working in memory.
  }
  return normalized;
}

function devPublicDataRootUrl(path) {
  return `${devPublicDataRootEndpoint}${path}`;
}

function persistentDataUrl(relativePath) {
  return new URL(`../genosys-data/${encodeRelativeUrlPath(relativePath)}`, import.meta.url).toString();
}

function encodeRelativeUrlPath(relativePath) {
  return normalizePersistentPath(relativePath)
    .split("/")
    .map((part) => encodeURIComponent(part))
    .join("/");
}

async function fetchJson(url, options = {}) {
  const response = await fetch(url, {
    cache: "no-store",
    ...options,
    headers: {
      ...(options.headers ?? {}),
      "accept": "application/json",
    },
  });

  if (!response.ok) {
    throw new Error(`Request failed ${response.status} ${response.statusText}`);
  }

  return await response.json();
}

async function getDevPublicFiles() {
  const response = await fetchJson(devPublicDataRootUrl("/files"));
  return Array.isArray(response?.files) ? response.files : [];
}

function persistentRuntimeFiles(files) {
  return (Array.isArray(files) ? files : []).map((file) => {
    if (file?.IsDirectory === true || isTextPersistentPath(file?.Path)) {
      return file;
    }

    return null;
  }).filter(Boolean);
}

async function putDevPublicFiles(files) {
  await fetchJson(devPublicDataRootUrl("/files"), {
    method: "PUT",
    headers: {
      "content-type": "application/json",
    },
    body: JSON.stringify({ files: Array.isArray(files) ? files : [] }),
  });
  return true;
}

async function getBundledDataFiles() {
  try {
    const response = await fetchJson(bundledDataRootUrl);
    const entries = Array.isArray(response?.files) ? response.files : [];
    const files = [];
    for (const entry of entries) {
      const path = normalizePersistentPath(entry?.Path);
      if (!path) {
        continue;
      }

      if (entry?.IsDirectory === true) {
        files.push({ Path: path, Text: null, IsDirectory: true });
        continue;
      }

      const url = publicDataFileUrl(path);
      if (isTextPersistentPath(path)) {
        const fileResponse = await fetch(url, { cache: "no-store" });
        if (!fileResponse.ok) {
          throw new Error(`Request failed ${fileResponse.status} ${fileResponse.statusText}`);
        }
        files.push({ Path: path, Text: await fileResponse.text(), IsDirectory: false });
      } else {
        const fileResponse = await fetch(url, { cache: "no-store" });
        if (!fileResponse.ok) {
          throw new Error(`Request failed ${fileResponse.status} ${fileResponse.statusText}`);
        }
        files.push({
          Path: path,
          Text: null,
          Base64Data: arrayBufferToBase64(await fileResponse.arrayBuffer()),
          IsDirectory: false,
        });
      }
    }

    return files;
  } catch (error) {
    logLoaderStep("BundledDataUnavailable", { ok: false, error });
    return [];
  }
}

function normalizePersistentPath(relativePath) {
  if (typeof relativePath !== "string") {
    return "";
  }

  return relativePath
    .replace(/\\/g, "/")
    .split("/")
    .filter((part) => part && part !== "." && part !== "..")
    .join("/");
}

function publicDataFileUrl(relativePath) {
  const encodedPath = normalizePersistentPath(relativePath)
    .split("/")
    .map((part) => encodeURIComponent(part))
    .join("/");
  return new URL(`../genosys-data/${encodedPath}`, import.meta.url).toString();
}

function isTextPersistentPath(path) {
  return /\.(json|txt|md|yarn|svg)$/i.test(path ?? "");
}

async function fileToBase64(file) {
  const buffer = await file.arrayBuffer();
  const bytes = new Uint8Array(buffer);
  let binary = "";
  for (let i = 0; i < bytes.length; i += 0x8000) {
    binary += String.fromCharCode(...bytes.subarray(i, i + 0x8000));
  }
  return btoa(binary);
}

function base64ToBytes(base64Data) {
  const binary = atob(base64Data ?? "");
  const bytes = new Uint8Array(binary.length);
  for (let index = 0; index < binary.length; index += 1) {
    bytes[index] = binary.charCodeAt(index);
  }
  return bytes;
}

function arrayBufferToBase64(buffer) {
  const bytes = new Uint8Array(buffer);
  let binary = "";
  for (let i = 0; i < bytes.length; i += 0x8000) {
    binary += String.fromCharCode(...bytes.subarray(i, i + 0x8000));
  }
  return btoa(binary);
}

function mergePersistentFiles(...groups) {
  const entriesByPath = new Map();
  for (const group of groups) {
    if (!Array.isArray(group)) {
      continue;
    }

    for (const entry of group) {
      const path = normalizePersistentPath(entry?.Path);
      if (!path) {
        continue;
      }

      entriesByPath.set(path, {
        ...entry,
        Path: path,
      });
    }
  }

  return [...entriesByPath.values()]
    .sort((left, right) => {
      if (left.IsDirectory === true && right.IsDirectory !== true) {
        return -1;
      }
      if (left.IsDirectory !== true && right.IsDirectory === true) {
        return 1;
      }
      return left.Path.localeCompare(right.Path);
    });
}

function mergeInteropMetrics(response, metrics) {
  if (!response || typeof response !== "object") {
    return response;
  }

  const totalBeforeSerializeMs = Number(response.__interopMetrics?.totalBeforeSerializeMs);
  if (
    typeof metrics.wasmCallMs === "number" &&
    Number.isFinite(totalBeforeSerializeMs)
  ) {
    metrics.wasmReturnMs = Math.max(0, metrics.wasmCallMs - totalBeforeSerializeMs);
  }

  response.__interopMetrics = {
    ...(response.__interopMetrics ?? {}),
    ...metrics,
  };
  return response;
}

function parseEnvelopeWithMetrics(payload, metrics) {
  const parseStartedAt = performance.now();
  const envelope = parseEnvelope(payload);
  metrics.responseParseMs = elapsedMs(parseStartedAt);
  metrics.responseBytes = byteLength(payload);
  return mergeInteropMetrics(envelope, metrics);
}

function readManagedHeapBytes(exports) {
  if (!exports || typeof exports.GetManagedHeapBytes !== "function") {
    return undefined;
  }

  const value = Number(exports.GetManagedHeapBytes());
  return Number.isFinite(value) ? value : undefined;
}

function readWasmMemoryBytes() {
  const memory =
    runtimeInstance?.Module?.wasmMemory ??
    runtimeInstance?.Module?.asm?.memory ??
    runtimeInstance?.Module?.HEAPU8?.buffer ??
    runtimeInstance?.Module?.HEAP8?.buffer;
  const buffer = memory instanceof WebAssembly.Memory ? memory.buffer : memory;
  const bytes = Number(buffer?.byteLength);
  return Number.isFinite(bytes) ? bytes : undefined;
}

function recordManagedHeapDelta(metrics) {
  if (
    typeof metrics.managedHeapBeforeWasmCallBytes !== "number" ||
    typeof metrics.managedHeapAfterWasmCallBytes !== "number"
  ) {
    return;
  }

  metrics.managedHeapDeltaBytes =
    metrics.managedHeapAfterWasmCallBytes - metrics.managedHeapBeforeWasmCallBytes;
}

function recordWasmMemoryDelta(metrics) {
  if (
    typeof metrics.wasmMemoryBeforeWasmCallBytes !== "number" ||
    typeof metrics.wasmMemoryAfterWasmCallBytes !== "number"
  ) {
    return;
  }

  metrics.wasmMemoryDeltaBytes =
    metrics.wasmMemoryAfterWasmCallBytes - metrics.wasmMemoryBeforeWasmCallBytes;
}

function hasFileSystemAccess() {
  return typeof window !== "undefined" && typeof window.showDirectoryPicker === "function";
}

function openPersistentStore() {
  return new Promise((resolve, reject) => {
    const request = indexedDB.open(directoryStoreName, 2);
    request.onupgradeneeded = () => {
      if (!request.result.objectStoreNames.contains("handles")) {
        request.result.createObjectStore("handles");
      }
      if (!request.result.objectStoreNames.contains("files")) {
        request.result.createObjectStore("files");
      }
    };
    request.onerror = () => reject(request.error);
    request.onsuccess = () => resolve(request.result);
  });
}

async function getStoredDirectoryHandle() {
  if (!("indexedDB" in window)) {
    return null;
  }

  const db = await openPersistentStore();
  return await new Promise((resolve, reject) => {
    const tx = db.transaction("handles", "readonly");
    const request = tx.objectStore("handles").get(directoryStoreKey);
    request.onerror = () => reject(request.error);
    request.onsuccess = () => resolve(request.result ?? null);
    tx.oncomplete = () => db.close();
  });
}

async function setStoredDirectoryHandle(handle) {
  if (!("indexedDB" in window)) {
    return;
  }

  const db = await openPersistentStore();
  await new Promise((resolve, reject) => {
    const tx = db.transaction("handles", "readwrite");
    tx.objectStore("handles").put(handle, directoryStoreKey);
    tx.onerror = () => reject(tx.error);
    tx.oncomplete = () => {
      db.close();
      resolve();
    };
  });
}

async function getStoredBrowserFiles() {
  if (!("indexedDB" in window)) {
    return null;
  }

  const db = await openPersistentStore();
  return await new Promise((resolve, reject) => {
    const tx = db.transaction("files", "readonly");
    const request = tx.objectStore("files").get(browserStorageKey);
    request.onerror = () => reject(request.error);
    request.onsuccess = () => {
      const files = request.result;
      resolve(Array.isArray(files) ? files : null);
    };
    tx.oncomplete = () => db.close();
  });
}

async function setStoredBrowserFiles(files) {
  if (!("indexedDB" in window)) {
    return false;
  }

  const db = await openPersistentStore();
  await new Promise((resolve, reject) => {
    const tx = db.transaction("files", "readwrite");
    tx.objectStore("files").put(Array.isArray(files) ? files : [], browserStorageKey);
    tx.onerror = () => reject(tx.error);
    tx.oncomplete = () => {
      db.close();
      resolve();
    };
  });
  return true;
}

async function ensureDirectoryPermission(handle, mode = "readwrite") {
  if (!handle) {
    return false;
  }

  const options = { mode };
  if (await handle.queryPermission(options) === "granted") {
    return true;
  }

  return await handle.requestPermission(options) === "granted";
}

async function* walkDirectory(handle, prefix = "") {
  for await (const [name, child] of handle.entries()) {
    const path = prefix ? `${prefix}/${name}` : name;
    if (child.kind === "directory") {
      yield { path, handle: child, isDirectory: true };
      yield* walkDirectory(child, path);
      continue;
    }

    if (child.kind === "file") {
      yield { path, handle: child, isDirectory: false };
    }
  }
}

async function readDirectoryJsonFiles(handle) {
  const files = [];
  for await (const entry of walkDirectory(handle)) {
    if (entry.isDirectory) {
      files.push({ Path: entry.path, Text: null, IsDirectory: true });
      continue;
    }

    const file = await entry.handle.getFile();
    files.push(isTextPersistentPath(entry.path)
      ? { Path: entry.path, Text: await file.text(), IsDirectory: false }
      : { Path: entry.path, Text: null, Base64Data: await fileToBase64(file), IsDirectory: false });
  }
  return files;
}

async function getOrCreateDirectory(rootHandle, pathParts) {
  let current = rootHandle;
  for (const part of pathParts) {
    current = await current.getDirectoryHandle(part, { create: true });
  }
  return current;
}

async function writeDirectoryJsonFiles(rootHandle, files) {
  const directories = files.filter((file) => file.IsDirectory === true);
  const fileEntries = files.filter((file) => file.IsDirectory !== true);
  const nextDirectories = new Set(directories.map((file) => file.Path));
  const nextFiles = new Set(fileEntries.map((file) => file.Path));
  const staleDirectories = [];

  for await (const entry of walkDirectory(rootHandle)) {
    if (entry.isDirectory) {
      if (!nextDirectories.has(entry.path)) {
        staleDirectories.push(entry.path);
      }
      continue;
    }

    if (!nextFiles.has(entry.path)) {
      await removePath(rootHandle, entry.path, false);
    }
  }

  for (const directory of directories) {
    await getOrCreateDirectory(rootHandle, directory.Path.split("/").filter(Boolean));
  }

  for (const file of fileEntries) {
    const parts = file.Path.split("/").filter(Boolean);
    const fileName = parts.pop();
    if (!fileName) {
      continue;
    }

    const directory = await getOrCreateDirectory(rootHandle, parts);
    const fileHandle = await directory.getFileHandle(fileName, { create: true });
    const writable = await fileHandle.createWritable();
    if (file.Base64Data) {
      await writable.write(base64ToBytes(file.Base64Data));
    } else {
      await writable.write(file.Text ?? "");
    }
    await writable.close();
  }

  for (const path of staleDirectories.sort((left, right) => right.split("/").length - left.split("/").length)) {
    try {
      await removePath(rootHandle, path, true);
    } catch (error) {
      logLoaderStep("PersistentDirectoryRemoveFailed", { ok: false, path, error });
    }
  }
}

async function removePath(rootHandle, path, recursive) {
  const parts = path.split("/").filter(Boolean);
  const fileName = parts.pop();
  if (!fileName) {
    return;
  }

  let directory = rootHandle;
  for (const part of parts) {
    directory = await directory.getDirectoryHandle(part);
  }
  await directory.removeEntry(fileName, { recursive });
}

async function ensurePersistentDirectoryHandle() {
  if (persistentDirectoryHandle) {
    return persistentDirectoryHandle;
  }

  persistentDirectoryHandle = await getStoredDirectoryHandle();
  if (!persistentDirectoryHandle) {
    return null;
  }

  if (!(await ensureDirectoryPermission(persistentDirectoryHandle, "readwrite"))) {
    persistentDirectoryHandle = null;
  }

  return persistentDirectoryHandle;
}

async function syncDirectoryToRuntime(exports) {
  const handle = await ensurePersistentDirectoryHandle();
  if (!handle) {
    return false;
  }

  const files = await readDirectoryJsonFiles(handle);
  importPersistentFilesToRuntime(exports, files, "persistent-directory-imported");
  return true;
}

async function getBrowserStoreFiles() {
  try {
    const files = await getStoredBrowserFiles();
    return Array.isArray(files) ? files : null;
  } catch (error) {
    logLoaderStep("PersistentBrowserStoreImportFailed", { ok: false, error });
    return null;
  }
}

async function syncBrowserStoreToRuntime(exports) {
  const files = await getBrowserStoreFiles();
  if (!files) {
    return false;
  }

  importPersistentFilesToRuntime(exports, files, "persistent-browser-store-imported");
  return true;
}

async function getDevPublicDataFiles() {
  try {
    const files = await getDevPublicFiles();
    return Array.isArray(files) ? files : [];
  } catch (error) {
    logLoaderStep("PersistentDevPublicImportUnavailable", { ok: false, error });
    return null;
  }
}

async function getPublicAuthoringFiles() {
  const devPublicFiles = await getDevPublicDataFiles();
  if (devPublicFiles !== null) {
    return devPublicFiles;
  }

  return await getBundledDataFiles();
}

async function getLocalPersistentFiles() {
  const handle = await ensurePersistentDirectoryHandle();
  if (handle) {
    return await readDirectoryJsonFiles(handle);
  }

  return await getBrowserStoreFiles();
}

function getAssetRelativePath(renderKey) {
  const prefix = "asset:";
  if (typeof renderKey !== "string" || !renderKey.toLowerCase().startsWith(prefix)) {
    return "";
  }

  const relativePath = normalizePersistentPath(renderKey.slice(prefix.length));
  return relativePath.startsWith("assets/") ? relativePath : "";
}

async function getDirectoryFile(rootHandle, relativePath) {
  const parts = normalizePersistentPath(relativePath).split("/").filter(Boolean);
  if (parts.length === 0) {
    return null;
  }

  let current = rootHandle;
  for (const part of parts.slice(0, -1)) {
    current = await current.getDirectoryHandle(part);
  }

  return await (await current.getFileHandle(parts[parts.length - 1])).getFile();
}

function mimeTypeForPath(relativePath) {
  const extension = relativePath.split(".").pop()?.toLowerCase() ?? "";
  switch (extension) {
    case "jpg":
    case "jpeg":
      return "image/jpeg";
    case "webp":
      return "image/webp";
    case "gif":
      return "image/gif";
    case "svg":
      return "image/svg+xml";
    case "png":
    default:
      return "image/png";
  }
}

function rememberPersistentAssetUrl(relativePath, sourceKey, createUrl) {
  const current = persistentAssetUrlCache.get(relativePath);
  if (current?.sourceKey === sourceKey) {
    return current.url;
  }

  if (current?.url) {
    URL.revokeObjectURL(current.url);
  }

  const url = createUrl();
  persistentAssetUrlCache.set(relativePath, { sourceKey, url });
  return url;
}

function findPersistentFile(files, relativePath) {
  const normalized = normalizePersistentPath(relativePath);
  return (Array.isArray(files) ? files : []).find((file) =>
    file?.IsDirectory !== true &&
    normalizePersistentPath(file?.Path) === normalized
  ) ?? null;
}

export async function resolveAssetUrl(renderKey) {
  const relativePath = getAssetRelativePath(renderKey);
  if (!relativePath) {
    return null;
  }

  if (getStoredDataRootMode() === dataRootModes.devPublic) {
    return persistentDataUrl(relativePath);
  }

  const handle = await ensurePersistentDirectoryHandle();
  if (handle) {
    try {
      const file = await getDirectoryFile(handle, relativePath);
      return rememberPersistentAssetUrl(
        relativePath,
        `directory:${file.size}:${file.lastModified}`,
        () => URL.createObjectURL(file)
      );
    } catch {
      return persistentDataUrl(relativePath);
    }
  }

  const storedFiles = await getBrowserStoreFiles();
  const file = findPersistentFile(storedFiles, relativePath);
  if (file?.Base64Data) {
    return rememberPersistentAssetUrl(
      relativePath,
      `browser:${file.Base64Data.length}`,
      () => URL.createObjectURL(new Blob([base64ToBytes(file.Base64Data)], {
        type: mimeTypeForPath(relativePath),
      }))
    );
  }

  return persistentDataUrl(relativePath);
}

function importPersistentFilesToRuntime(exports, files, label) {
  const sourceFiles = Array.isArray(files) ? files : [];
  const normalizedFiles = persistentRuntimeFiles(sourceFiles);
  parseEnvelope(exports.ImportPersistentFiles(JSON.stringify(normalizedFiles)));
  logLoaderStep(label, {
    files: normalizedFiles.length,
    skippedBinaryFiles: sourceFiles.length - normalizedFiles.length,
  });
  return normalizedFiles.length > 0;
}

async function syncDevPublicModeToRuntime(exports) {
  const publicFiles = await getPublicAuthoringFiles();
  importPersistentFilesToRuntime(exports, publicFiles, "PersistentDevPublicModeImported");
  return true;
}

async function syncLocalModeToRuntime(exports) {
  const publicFiles = await getPublicAuthoringFiles();
  const localFiles = await getLocalPersistentFiles();
  const files = mergePersistentFiles(publicFiles, localFiles);
  importPersistentFilesToRuntime(exports, files, "PersistentLocalModeImported");
  return true;
}

async function syncRuntimeToDirectory(exports, metrics = null) {
  const handle = await ensurePersistentDirectoryHandle();
  if (!handle) {
    return false;
  }

  const exportStartedAt = performance.now();
  const files = JSON.parse(exports.ExportPersistentFiles());
  if (metrics) {
    metrics.persistenceExportMs = elapsedMs(exportStartedAt);
    metrics.persistentFileCount = Array.isArray(files) ? files.length : null;
  }
  if (!Array.isArray(files)) {
    return false;
  }

  const writeStartedAt = performance.now();
  await writeDirectoryJsonFiles(handle, files);
  if (metrics) {
    metrics.persistenceWriteMs = elapsedMs(writeStartedAt);
    metrics.persistenceTarget = "directory";
  }
  logLoaderStep("PersistentDirectoryExported", { files: files.length });
  return true;
}

async function syncRuntimeToBrowserStore(exports, metrics = null) {
  try {
    const exportStartedAt = performance.now();
    const files = JSON.parse(exports.ExportPersistentFiles());
    if (metrics) {
      metrics.persistenceExportMs = elapsedMs(exportStartedAt);
      metrics.persistentFileCount = Array.isArray(files) ? files.length : null;
    }
    if (!Array.isArray(files)) {
      return false;
    }

    const writeStartedAt = performance.now();
    const saved = await setStoredBrowserFiles(files);
    if (metrics) {
      metrics.persistenceWriteMs = elapsedMs(writeStartedAt);
      metrics.persistenceTarget = saved ? "browser-storage" : null;
    }
    if (saved) {
      logLoaderStep("PersistentBrowserStoreExported", { files: files.length });
    }
    return saved;
  } catch (error) {
    logLoaderStep("PersistentBrowserStoreExportFailed", { ok: false, error });
    return false;
  }
}

async function syncRuntimeToDevPublic(exports, metrics = null) {
  try {
    const exportStartedAt = performance.now();
    const files = JSON.parse(exports.ExportPersistentFiles());
    if (metrics) {
      metrics.persistenceExportMs = elapsedMs(exportStartedAt);
      metrics.persistentFileCount = Array.isArray(files) ? files.length : null;
    }
    if (!Array.isArray(files)) {
      return false;
    }

    const writeStartedAt = performance.now();
    const saved = await putDevPublicFiles(files);
    if (metrics) {
      metrics.persistenceWriteMs = elapsedMs(writeStartedAt);
      metrics.persistenceTarget = saved ? "dev-public" : null;
    }
    if (saved) {
      logLoaderStep("PersistentDevPublicExported", { files: files.length });
    }
    return saved;
  } catch (error) {
    logLoaderStep("PersistentDevPublicExportFailed", { ok: false, error });
    return false;
  }
}

export async function createSession() {
  const startedAt = performance.now();
  const exports = await ensureRuntime();
  if (getStoredDataRootMode() === dataRootModes.devPublic) {
    await syncDevPublicModeToRuntime(exports);
  } else {
    await syncLocalModeToRuntime(exports);
  }
  setRuntimeProgress("creating-session", 98);
  const session = parseEnvelope(exports.CreateSession());
  logLoaderStep("SessionCreated", {
    stepMs: elapsedMs(startedAt),
  });
  setRuntimeProgress("ready", 100);
  return session;
}

export function getRuntimeProgress() {
  return runtimeProgress;
}

export function subscribeRuntimeProgress(listener) {
  runtimeProgressListeners.add(listener);
  listener(runtimeProgress);

  return () => {
    runtimeProgressListeners.delete(listener);
  };
}

export function getTransportFormat() {
  return transportFormat;
}

export function setTransportFormat(format) {
  transportFormat = normalizeTransportFormat(format);
  return transportFormat;
}

export async function disposeSession(sessionId) {
  const exports = await ensureRuntime();
  return parseEnvelope(exports.DisposeSession(sessionId));
}

export async function handleMessage(sessionId, message) {
  const exports = await ensureRuntime();
  const bridgeStartedAt = performance.now();
  const bridgeMetrics = {
    managedHeapBeforeWasmCallBytes: readManagedHeapBytes(exports),
    wasmMemoryBeforeWasmCallBytes: readWasmMemoryBytes(),
  };

  let response;
  if (transportFormat === transportFormats.binary) {
    if (typeof exports.HandleMessageBinary !== "function") {
      throw new Error("The GenOSys runtime does not expose binary transport.");
    }

    const serializeStartedAt = performance.now();
    const messageBytes = encodeBinaryEnvelope(message);
    bridgeMetrics.requestSerializeMs = elapsedMs(serializeStartedAt);
    bridgeMetrics.requestBytes = messageBytes.byteLength;
    bridgeMetrics.transportFormat = transportFormats.binary;

    const wasmStartedAt = performance.now();
    const responseBytes = exports.HandleMessageBinary(sessionId, messageBytes);
    bridgeMetrics.wasmCallMs = elapsedMs(wasmStartedAt);
    bridgeMetrics.managedHeapAfterWasmCallBytes = readManagedHeapBytes(exports);
    bridgeMetrics.wasmMemoryAfterWasmCallBytes = readWasmMemoryBytes();
    bridgeMetrics.responseBytes = responseBytes.byteLength;
    recordManagedHeapDelta(bridgeMetrics);
    recordWasmMemoryDelta(bridgeMetrics);

    const parseStartedAt = performance.now();
    response = decodeBinaryEnvelope(responseBytes);
    bridgeMetrics.responseParseMs = elapsedMs(parseStartedAt);
  } else {
    const serializeStartedAt = performance.now();
    const messageJson = JSON.stringify(message);
    bridgeMetrics.requestSerializeMs = elapsedMs(serializeStartedAt);
    bridgeMetrics.requestBytes = byteLength(messageJson);
    bridgeMetrics.transportFormat = transportFormats.json;

    const wasmStartedAt = performance.now();
    const responseJson = exports.HandleMessage(sessionId, messageJson);
    bridgeMetrics.wasmCallMs = elapsedMs(wasmStartedAt);
    bridgeMetrics.managedHeapAfterWasmCallBytes = readManagedHeapBytes(exports);
    bridgeMetrics.wasmMemoryAfterWasmCallBytes = readWasmMemoryBytes();
    recordManagedHeapDelta(bridgeMetrics);
    recordWasmMemoryDelta(bridgeMetrics);

    response = parseEnvelopeWithMetrics(responseJson, bridgeMetrics);
  }

  bridgeMetrics.totalJsBridgeMs = elapsedMs(bridgeStartedAt);
  return mergeInteropMetrics(response, bridgeMetrics);
}

export async function choosePersistentDirectory() {
  if (!hasFileSystemAccess()) {
    const exports = await ensureRuntime();
    const persisted = (await syncBrowserStoreToRuntime(exports)) ||
      (await syncRuntimeToBrowserStore(exports));

    if (!persisted) {
      return {
        ok: false,
        error: "This browser does not support choosing a writable directory and browser storage is unavailable."
      };
    }

    return {
      ok: true,
      name: "Browser storage",
      mode: "browser-storage",
      warning: "This browser does not support choosing a writable directory, so GenOSys is using browser storage for persistence."
    };
  }

  const handle = await window.showDirectoryPicker({
    id: "genosys-template-data",
    mode: "readwrite",
    startIn: "documents",
  });
  persistentDirectoryHandle = handle;
  await setStoredDirectoryHandle(handle);

  const exports = await ensureRuntime();
  await syncLocalModeToRuntime(exports);

  return {
    ok: true,
    name: handle.name,
    mode: "directory",
  };
}

export async function hasPersistentDirectory() {
  return !!(await ensurePersistentDirectoryHandle()) || !!(await getStoredBrowserFiles());
}

export function getDataRootMode() {
  return getStoredDataRootMode();
}

export async function setDataRootMode(mode) {
  const nextMode = setStoredDataRootMode(mode);
  const exports = await ensureRuntime();

  if (nextMode === dataRootModes.devPublic) {
    await syncDevPublicModeToRuntime(exports);
  } else {
    await syncLocalModeToRuntime(exports);
  }

  return {
    ok: true,
    mode: nextMode,
  };
}

export async function getDevPublicDataRootStatus() {
  try {
    const response = await fetchJson(devPublicDataRootUrl("/status"));
    return {
      available: response?.available === true,
      path: typeof response?.path === "string" ? response.path : null,
      fileCount: typeof response?.fileCount === "number" ? response.fileCount : null,
      error: null,
    };
  } catch (error) {
    return {
      available: false,
      path: null,
      fileCount: null,
      error: String(error),
    };
  }
}

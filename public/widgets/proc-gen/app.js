const runtimeConfig = window.__PROCGEN_APP_CONFIG__ || {};
const generatorSelect = document.getElementById("generator");
const generatorTitle = document.getElementById("generator-title");
const controlsHeader = document.getElementById("controls-header");
const generatorSection = document.getElementById("generator-section");
const resetButton = document.getElementById("reset");
// const autoRegenCheckbox = document.getElementById("auto-regen");
const inputsRoot = document.getElementById("inputs");
// const generateButton = document.getElementById("generate");
const fitViewButton = document.getElementById("fit-view");
const fullscreenViewButton = document.getElementById("fullscreen-view");
const toggleGridButton = document.getElementById("toggle-grid");
const renderModePicker = document.getElementById("render-mode-picker");
const renderFlatButton = document.getElementById("render-flat");
const renderShaderButton = document.getElementById("render-shader");
// const statusNode = document.getElementById("status");
// const viewportMetaNode = document.getElementById("viewport-meta");
const storyPicker = document.getElementById("story-picker");
const storySelect = document.getElementById("story-select");
const regionLegend = document.getElementById("region-legend");
const canvasShell = document.getElementById("canvas-shell");
const flatCanvas = document.getElementById("canvas-flat");
const shaderCanvas = document.getElementById("canvas-shader");
const flatCtx = flatCanvas.getContext("2d");
const appRoot = document.getElementById("procgen-app") || flatCanvas.closest(".procgen-app") || document.body;

const storageKey = runtimeConfig.storageKey || "procgen-devapp-state";
const syncGroup = String(runtimeConfig.syncGroup || "").trim();
const syncInstanceId = String(runtimeConfig.instanceId || `procgen-${Math.random().toString(36).slice(2)}`);
const procGenSyncEventName = "procgen:sync-options";
const autoRegenDelayMs = 180;
const viewportPadding = 32;
const minZoomMultiplier = 0.35;
const maxZoom = 96;

let generatorDefinitions = [];
let generatorDefinitionById = {};
let currentMap = null;
let selectedStoryIndex = 0;
let regenTimer = 0;
let requestSequence = 0;
let currentAbortController = null;
let renderFrameId = 0;
let storySnapshotCanvases = [];
let storyMaskBuffers = [];
let storyMaskTextures = [];
let gridEnabled = false;
let runtimeBridgePromise = null;
let renderMode = "flat";
let gl = null;
let shaderRuntime = null;
let noiseTexture = null;
let fullscreenTarget = null;
let suppressSyncBroadcast = false;

const knownEmbedParams = new Set([
  "embed",
  "widget",
  "minimal",
  "generator",
  "title",
  "hideTitle",
  "hideGenerator",
  "hideReset",
  "hideLegend",
  "hideStoryPicker",
  "hideFit",
  "hideFullscreen",
  "hideGrid",
  "hideRenderer",
  "lockGenerator",
  "grid",
  "showGrid",
  "renderer",
  "renderMode",
  "lockOptions",
  "hideOptions",
]);

const embedConfig = parseEmbedConfig();

const viewportState = {
  scale: 1,
  offsetX: 0,
  offsetY: 0,
  fitScale: 1,
  hasUserAdjusted: false,
};

const dragState = {
  pointerId: null,
  lastX: 0,
  lastY: 0,
};

const gestureState = {
  activePointers: new Map(),
  pinchDistance: 0,
  pinchScale: 1,
  pinchMapX: 0,
  pinchMapY: 0,
};

async function ensureRuntimeBridge() {
  if (!runtimeBridgePromise) {
    runtimeBridgePromise = createRuntimeBridge();
  }

  return runtimeBridgePromise;
}

async function createRuntimeBridge() {
  if (runtimeConfig.mode === "wasm") {
    if (!runtimeConfig.moduleUrl) {
      throw new Error("ProcGen widget mode requires a moduleUrl.");
    }

    const runtimeImport = new Function("moduleUrl", "return import(/* @vite-ignore */ moduleUrl);");
    const moduleExports = await runtimeImport(runtimeConfig.moduleUrl);

    return {
      getGeneratorCatalog: () => moduleExports.getGeneratorCatalog(),
      generateMap: (request) => moduleExports.generateMap(request),
    };
  }

  const apiBaseUrl = runtimeConfig.apiBaseUrl || "";

  return {
    async getGeneratorCatalog() {
      const response = await fetch(`${apiBaseUrl}/api/generators`);
      if (!response.ok) {
        throw new Error(`Failed to load generators (HTTP ${response.status}).`);
      }

      return response.json();
    },
    async generateMap(request) {
      const response = await fetch(`${apiBaseUrl}/api/generate`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify(request),
      });

      if (!response.ok) {
        throw new Error(`Generation failed with HTTP ${response.status}.`);
      }

      return response.json();
    },
  };
}

function parseBooleanParam(searchParams, key) {
  const value = searchParams.get(key);
  if (value == null) {
    return false;
  }

  const normalized = value.trim().toLowerCase();
  return normalized !== "" && normalized !== "0" && normalized !== "false" && normalized !== "off" && normalized !== "no";
}

function parseCsvSet(value) {
  if (!value) {
    return new Set();
  }

  return new Set(
    value
      .split(",")
      .map((entry) => entry.trim())
      .filter(Boolean),
  );
}

function slugifyToken(value) {
  return String(value || "")
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "");
}

function parseEmbedConfig() {
  const searchParams = new URLSearchParams(runtimeConfig.search || window.location.search);
  const optionOverrides = {};

  searchParams.forEach((value, key) => {
    if (!knownEmbedParams.has(key)) {
      optionOverrides[key] = value;
    }
  });

  return {
    isEmbed: parseBooleanParam(searchParams, "embed")
      || parseBooleanParam(searchParams, "widget")
      || parseBooleanParam(searchParams, "minimal"),
    generatorId: searchParams.get("generator") || "",
    customTitle: searchParams.get("title") || "",
    hideTitle: parseBooleanParam(searchParams, "hideTitle"),
    hideGenerator: parseBooleanParam(searchParams, "hideGenerator"),
    hideReset: parseBooleanParam(searchParams, "hideReset"),
    hideLegend: parseBooleanParam(searchParams, "hideLegend"),
    hideStoryPicker: parseBooleanParam(searchParams, "hideStoryPicker"),
    hideFit: parseBooleanParam(searchParams, "hideFit"),
    hideFullscreen: parseBooleanParam(searchParams, "hideFullscreen"),
    hideGridButton: parseBooleanParam(searchParams, "hideGrid"),
    hideRenderer: parseBooleanParam(searchParams, "hideRenderer"),
    lockGenerator: parseBooleanParam(searchParams, "lockGenerator"),
    showGrid: parseBooleanParam(searchParams, "grid") || parseBooleanParam(searchParams, "showGrid"),
    renderMode: searchParams.get("renderer") || searchParams.get("renderMode") || "",
    hiddenOptions: parseCsvSet(searchParams.get("hideOptions")),
    lockedOptions: parseCsvSet(searchParams.get("lockOptions")),
    fullscreenUrl: searchParams.get("fullscreenUrl") || "",
    optionOverrides,
  };
}

function resolveFullscreenTarget() {
  return appRoot.closest(".procgen-inline-shell") || appRoot;
}

function canUseEmbeddedFullscreen() {
  return embedConfig.isEmbed && runtimeConfig.mode === "wasm";
}

function getThemeColor(variableName, fallback) {
  const value = getComputedStyle(appRoot).getPropertyValue(variableName).trim();
  return value || fallback;
}

function normalizeRenderMode(value) {
  return String(value || "").toLowerCase() === "shader" ? "shader" : "flat";
}

function shouldHideLegendForViewport() {
  return embedConfig.hideLegend
    || (embedConfig.isEmbed && window.matchMedia("(max-width: 720px)").matches);
}

function updateGeneratorTitle() {
  if (!generatorTitle) {
    return;
  }

  if (embedConfig.customTitle) {
    generatorTitle.textContent = embedConfig.customTitle;
    if (!embedConfig.isEmbed && runtimeConfig.updateDocumentTitle !== false) {
      document.title = embedConfig.customTitle;
    }
    return;
  }

  const displayName = generatorDefinitionById[generatorSelect.value]?.displayName || "ProcGen";
  generatorTitle.textContent = `${displayName} Generator`;
  if (!embedConfig.isEmbed && runtimeConfig.updateDocumentTitle !== false) {
    document.title = generatorTitle.textContent;
  }
}

function clamp(value, min, max) {
  return Math.min(max, Math.max(min, value));
}

function getDefaultState() {
  const firstGenerator = generatorDefinitions[0];
  return {
    generatorId: firstGenerator?.id || "",
    options: getDefaultOptions(firstGenerator?.id),
    autoRegen: true,
    selectedStory: 0,
    gridEnabled: false,
    renderMode: normalizeRenderMode(embedConfig.renderMode),
  };
}

function getDefaultOptions(generatorId) {
  const definition = generatorDefinitionById[generatorId];
  if (!definition) {
    return {};
  }

  return Object.fromEntries(
    (definition.options || []).map((option) => [option.key, option.defaultValue]),
  );
}

function getOptionInitialValue(option, defaults) {
  return normalizeOptionValue(
    option,
    Number(defaults?.[option.key] ?? option.value ?? option.defaultValue),
  );
}

function loadState() {
  try {
    const raw = localStorage.getItem(storageKey);
    if (!raw) {
      return getDefaultState();
    }

    const parsed = JSON.parse(raw);
    const generatorId = generatorDefinitionById[parsed.generatorId]
      ? parsed.generatorId
      : (generatorDefinitions[0]?.id || "");
    const options = { ...getDefaultOptions(generatorId), ...(parsed.options || {}) };

    return {
      generatorId,
      options,
      autoRegen: parsed.autoRegen !== false,
      selectedStory: Number.isFinite(parsed.selectedStory) ? parsed.selectedStory : 0,
      gridEnabled: parsed.gridEnabled === true,
      renderMode: normalizeRenderMode(parsed.renderMode || embedConfig.renderMode),
    };
  } catch {
    return getDefaultState();
  }
}

function getProcGenSyncStore() {
  if (!window.__procgenSyncStore || typeof window.__procgenSyncStore !== "object") {
    window.__procgenSyncStore = {};
  }

  return window.__procgenSyncStore;
}

function saveState() {
  localStorage.setItem(
    storageKey,
    JSON.stringify({
      generatorId: generatorSelect.value,
      options: readOptions(),
      autoRegen: true,
      //autoRegenCheckbox.checked,
      selectedStory: selectedStoryIndex,
      gridEnabled,
      renderMode,
    }),
  );
}

function setOptionControlValue(option, value) {
  const normalized = normalizeOptionValue(option, value);

  inputsRoot.querySelectorAll(`.option-control[name="${option.key}"]`).forEach((control) => {
    control.value = String(normalized);
  });

  if (isRotationOption(option)) {
    inputsRoot.querySelectorAll(".rotation-choice-button").forEach((button) => {
      const isSelected = Number(button.dataset.value) === normalized;
      button.classList.toggle("is-selected", isSelected);
      button.setAttribute("aria-pressed", isSelected ? "true" : "false");
    });
    refreshShapeChoiceButtons();
  }

  if (option.key === "shape") {
    inputsRoot.querySelectorAll(".shape-choice-button").forEach((button) => {
      const isSelected = Number(button.dataset.value) === normalized;
      button.classList.toggle("is-selected", isSelected);
      button.setAttribute("aria-pressed", isSelected ? "true" : "false");
    });
    refreshRotationChoiceButtons();
  }

  return normalized;
}

function broadcastSynchronizedOptions() {
  if (!syncGroup || suppressSyncBroadcast) {
    return;
  }

  const payload = {
    group: syncGroup,
    instanceId: syncInstanceId,
    generatorId: generatorSelect.value,
    options: readOptions(),
  };

  getProcGenSyncStore()[syncGroup] = payload;
  window.dispatchEvent(new CustomEvent(procGenSyncEventName, { detail: payload }));
}

function applySynchronizedOptions(
  options,
  {
    resetViewport = false,
    shouldGenerate = true,
  } = {},
) {
  if (!options || typeof options !== "object") {
    return false;
  }

  let changed = false;

  (generatorDefinitionById[generatorSelect.value]?.options || []).forEach((option) => {
    if (!(option.key in options)) {
      return;
    }

    const rawValue = Number(options[option.key]);
    if (!Number.isFinite(rawValue)) {
      return;
    }

    const nextValue = normalizeOptionValue(option, rawValue);
    const controls = inputsRoot.querySelectorAll(`.option-control[name="${option.key}"]`);
    if (controls.length === 0) {
      return;
    }

    const currentValue = Number(controls[0].value);
    if (currentValue === nextValue) {
      return;
    }

    setOptionControlValue(option, nextValue);
    changed = true;
  });

  if (!changed) {
    return false;
  }

  saveState();

  if (shouldGenerate) {
    queueGenerate(resetViewport);
  }

  return true;
}

function seedSynchronizedOptions() {
  if (!syncGroup) {
    return;
  }

  const existing = getProcGenSyncStore()[syncGroup];
  if (existing?.instanceId && existing.instanceId !== syncInstanceId) {
    suppressSyncBroadcast = true;
    try {
      applySynchronizedOptions(existing.options, { shouldGenerate: false });
    } finally {
      suppressSyncBroadcast = false;
    }
    return;
  }

  broadcastSynchronizedOptions();
}

function handleSynchronizedOptions(event) {
  const detail = event?.detail;
  if (!detail || detail.group !== syncGroup || detail.instanceId === syncInstanceId) {
    return;
  }

  suppressSyncBroadcast = true;
  try {
    applySynchronizedOptions(detail.options, { resetViewport: false, shouldGenerate: true });
  } finally {
    suppressSyncBroadcast = false;
  }
}

function parseOptionOverride(option, rawValue) {
  const numericValue = Number(rawValue);
  if (Number.isFinite(numericValue)) {
    return normalizeOptionValue(option, numericValue);
  }

  if (!isChoiceOption(option)) {
    return option.defaultValue;
  }

  const normalizedRawValue = slugifyToken(rawValue);
  const matchingIndex = option.choiceLabels.findIndex((label) => slugifyToken(label) === normalizedRawValue);
  if (matchingIndex >= 0) {
    return option.minValue + matchingIndex;
  }

  return option.defaultValue;
}

function applyEmbedOptionOverrides(generatorId, options) {
  const definition = generatorDefinitionById[generatorId];
  if (!definition) {
    return options;
  }

  const overriddenOptions = { ...options };

  (definition.options || []).forEach((option) => {
    if (!(option.key in embedConfig.optionOverrides)) {
      return;
    }

    overriddenOptions[option.key] = parseOptionOverride(option, embedConfig.optionOverrides[option.key]);
  });

  return overriddenOptions;
}

function getInitialOptions(generatorId, savedState) {
  const savedOptions = savedState?.generatorId === generatorId
    ? (savedState.options || {})
    : {};

  return {
    ...getDefaultOptions(generatorId),
    ...applyEmbedOptionOverrides(generatorId, {}),
    ...savedOptions,
  };
}

function applyEmbedChrome() {
  appRoot.classList.toggle("embed-mode", embedConfig.isEmbed);

  if (controlsHeader) {
    controlsHeader.hidden = embedConfig.hideTitle;
  }

  if (generatorSection) {
    generatorSection.hidden = embedConfig.hideGenerator;
  }

  if (resetButton) {
    resetButton.hidden = embedConfig.hideReset;
  }

  if (regionLegend) {
    regionLegend.hidden = embedConfig.hideLegend || regionLegend.hidden;
  }

  if (storyPicker) {
    storyPicker.hidden = embedConfig.hideStoryPicker || storyPicker.hidden;
  }

  if (fitViewButton) {
    fitViewButton.hidden = embedConfig.hideFit;
  }

  if (fullscreenViewButton) {
    fullscreenViewButton.hidden = !canUseEmbeddedFullscreen() || embedConfig.hideFullscreen;
  }

  if (toggleGridButton) {
    toggleGridButton.hidden = embedConfig.hideGridButton;
  }

  if (renderModePicker) {
    renderModePicker.hidden = embedConfig.hideRenderer;
  }
}

function setStatus(message) {
  // statusNode.textContent = message;
}

function updateViewportMeta() {
  if (!currentMap) {
    // viewportMetaNode.textContent = "Waiting for a generated layout.";
    return;
  }
}

function requestRender() {
  if (renderFrameId) {
    return;
  }

  renderFrameId = window.requestAnimationFrame(() => {
    renderFrameId = 0;
    renderMap();
  });
}

function getOptionDefinition(key) {
  return generatorDefinitionById[generatorSelect.value]?.options?.find((option) => option.key === key) || null;
}

function isChoiceOption(option) {
  return Array.isArray(option?.choiceLabels) && option.choiceLabels.length > 0;
}

function isRotationOption(option) {
  return option?.key === "rotation";
}

function isShapeOption(option) {
  return option?.key === "shape";
}

function getOptionControlValue(key, fallback = 0) {
  const control = inputsRoot.querySelector(`.option-control[name="${key}"]`);
  if (!control) {
    return fallback;
  }

  const parsed = Number(control.value);
  return Number.isFinite(parsed) ? parsed : fallback;
}

function getCorridorPreviewRects(shapeValue, rotationValue) {
  const frame = { x: 3, y: 3, width: 18, height: 18 };
  const corridor = 4;
  const top = { x: frame.x, y: frame.y, width: frame.width, height: corridor };
  const bottom = { x: frame.x, y: frame.y + frame.height - corridor, width: frame.width, height: corridor };
  const left = { x: frame.x, y: frame.y, width: corridor, height: frame.height };
  const right = { x: frame.x + frame.width - corridor, y: frame.y, width: corridor, height: frame.height };
  const horizontalLine = {
    x: frame.x,
    y: frame.y + Math.floor((frame.height - corridor) / 2),
    width: frame.width,
    height: corridor,
  };
  const verticalLine = {
    x: frame.x + Math.floor((frame.width - corridor) / 2),
    y: frame.y,
    width: corridor,
    height: frame.height,
  };

  switch (shapeValue) {
    case 0:
      return rotationValue === 1 || rotationValue === 3 ? [verticalLine] : [horizontalLine];
    case 1:
      switch (rotationValue) {
        case 1: return [top, right];
        case 2: return [bottom, right];
        case 3: return [bottom, left];
        default: return [top, left];
      }
    case 2:
      switch (rotationValue) {
        case 1: return [top, left, right];
        case 2: return [top, bottom, right];
        case 3: return [left, right, bottom];
        default: return [top, left, bottom];
      }
    default:
      return [top, left, right, bottom];
  }
}

function createCorridorPreviewSvg(shapeValue, rotationValue) {
  const rects = getCorridorPreviewRects(shapeValue, rotationValue);
  const bars = rects
    .map((rect) => `<rect x="${rect.x}" y="${rect.y}" width="${rect.width}" height="${rect.height}" rx="1.5" ry="1.5"></rect>`)
    .join("");

  return `
    <svg class="rotation-preview-icon" viewBox="0 0 24 24" aria-hidden="true">
      ${bars}
    </svg>
  `;
}

function refreshRotationChoiceButtons() {
  const shapeValue = getOptionControlValue("shape", 3);
  inputsRoot.querySelectorAll(".rotation-choice-group").forEach((group) => {
    group.querySelectorAll(".rotation-choice-button").forEach((button) => {
      const rotationValue = Number(button.dataset.value || "0");
      const icon = button.querySelector(".rotation-choice-icon");
      if (icon) {
        icon.innerHTML = createCorridorPreviewSvg(shapeValue, rotationValue);
      }
    });
  });
}

function refreshShapeChoiceButtons() {
  const rotationValue = getOptionControlValue("rotation", 0);
  inputsRoot.querySelectorAll(".shape-choice-group").forEach((group) => {
    group.querySelectorAll(".shape-choice-button").forEach((button) => {
      const shapeValue = Number(button.dataset.value || "0");
      const icon = button.querySelector(".shape-choice-icon");
      if (icon) {
        icon.innerHTML = createCorridorPreviewSvg(shapeValue, rotationValue);
      }
    });
  });
}

function updateGridButtonState() {
  if (!toggleGridButton) {
    return;
  }

  toggleGridButton.classList.toggle("is-active", gridEnabled);
  toggleGridButton.setAttribute("aria-pressed", gridEnabled ? "true" : "false");
}

function normalizeOptionValue(option, value) {
  if (!Number.isFinite(value)) {
    return option.defaultValue;
  }

  const step = option.step || 1;
  const rounded = Math.round(value / step) * step;
  return clamp(rounded, option.minValue, option.maxValue);
}

function buildFullscreenUrl() {
  const baseUrl = embedConfig.fullscreenUrl || "/widgets/proc-gen/index.html";
  const url = new URL(baseUrl, window.location.origin);
  const params = url.searchParams;

  params.set("generator", generatorSelect.value);
  params.set("renderer", renderMode);

  if (gridEnabled) {
    params.set("grid", "1");
  } else {
    params.delete("grid");
  }

  Object.entries(readOptions()).forEach(([key, value]) => {
    params.set(key, String(value));
  });

  [
    "embed",
    "widget",
    "minimal",
    "hideTitle",
    "hideGenerator",
    "hideReset",
    "hideLegend",
    "hideStoryPicker",
    "hideFit",
    "hideFullscreen",
    "hideGrid",
    "hideRenderer",
    "lockGenerator",
    "lockOptions",
    "hideOptions",
    "title",
    "fullscreenUrl",
    "storageKey",
  ].forEach((key) => params.delete(key));

  return url.toString();
}

function isFullscreenActive() {
  return Boolean(document.fullscreenElement && fullscreenTarget && document.fullscreenElement === fullscreenTarget);
}

function updateFullscreenButtonState() {
  if (!fullscreenViewButton) {
    return;
  }

  const active = isFullscreenActive();
  fullscreenViewButton.classList.toggle("is-active", active);
  fullscreenViewButton.setAttribute("aria-pressed", active ? "true" : "false");
  fullscreenViewButton.setAttribute("aria-label", active ? "Exit fullscreen" : "Expand widget to fullscreen");
  fullscreenViewButton.title = active ? "Exit fullscreen" : "Expand widget to fullscreen";
}

async function toggleFullscreenView() {
  if (!fullscreenTarget) {
    fullscreenTarget = resolveFullscreenTarget();
  }

  if (isFullscreenActive()) {
    if (document.exitFullscreen) {
      await document.exitFullscreen();
    }
    return;
  }

  if (fullscreenTarget?.requestFullscreen) {
    await fullscreenTarget.requestFullscreen();
    fitViewportToMap();
    requestRender();
    return;
  }

  window.open(buildFullscreenUrl(), "_blank", "noopener,noreferrer");
}

function updateTooltipPlacement(tooltip) {
  if (!(tooltip instanceof HTMLElement)) {
    return;
  }

  const bubble = tooltip.querySelector(".tooltip-bubble");
  if (!(bubble instanceof HTMLElement)) {
    return;
  }

  tooltip.classList.remove("tooltip-below");
  const bubbleRect = bubble.getBoundingClientRect();
  const needsBelow = bubbleRect.top < 12;
  tooltip.classList.toggle("tooltip-below", needsBelow);
}

function buildInputs() {
  const definition = generatorDefinitionById[generatorSelect.value];
  const savedState = loadState();
  const defaults = getInitialOptions(generatorSelect.value, savedState);
  inputsRoot.innerHTML = "";

  (definition?.options || []).forEach((option) => {
    if (embedConfig.hiddenOptions.has(option.key)) {
      return;
    }

    const field = document.createElement("section");
    const header = document.createElement("div");
    const titleWrap = document.createElement("div");
    const name = document.createElement("div");
    const meta = document.createElement("div");
    const controls = document.createElement("div");
    const isLocked = embedConfig.lockedOptions.has(option.key);
    const initialValue = getOptionInitialValue(option, defaults);

    field.className = "option-field";
    header.className = "option-header";
    titleWrap.className = "option-title";
    name.className = "option-name";
    meta.className = "option-meta";
    controls.className = "option-controls";
    name.textContent = option.label;
    meta.textContent = isChoiceOption(option) ? "" : `${option.minValue} to ${option.maxValue}`;
    meta.hidden = isChoiceOption(option);

    if (option.description) {
      const tooltip = document.createElement("span");
      const trigger = document.createElement("button");
      const bubble = document.createElement("span");

      tooltip.className = "tooltip";
      trigger.className = "tooltip-trigger";
      trigger.type = "button";
      trigger.setAttribute("aria-label", `${option.label} help`);
      trigger.textContent = "?";

      bubble.className = "tooltip-bubble";
      bubble.textContent = option.description;

      const refreshPlacement = () => updateTooltipPlacement(tooltip);
      trigger.addEventListener("mouseenter", refreshPlacement);
      trigger.addEventListener("focus", refreshPlacement);
      tooltip.addEventListener("mouseenter", refreshPlacement);

      tooltip.append(trigger, bubble);
      titleWrap.append(tooltip, name);
    } else {
      titleWrap.appendChild(name);
    }

    const applyValue = (value) => {
      if (isLocked) {
        return;
      }

      setOptionControlValue(option, value);
      onSettingsChanged(false);
    };

    if (isChoiceOption(option)) {
      field.classList.add("is-choice");
      controls.classList.add("is-choice");

      if (isRotationOption(option) || isShapeOption(option)) {
        const hidden = document.createElement("input");
        const group = document.createElement("div");
        const buttonClassName = isRotationOption(option) ? "rotation-choice-button" : "shape-choice-button";
        const groupClassName = isRotationOption(option) ? "rotation-choice-group" : "shape-choice-group";
        const iconClassName = isRotationOption(option) ? "rotation-choice-icon" : "shape-choice-icon";
        const labelClassName = isRotationOption(option) ? "rotation-choice-label" : "shape-choice-label";

        hidden.type = "hidden";
        hidden.name = option.key;
        hidden.value = String(initialValue);
        hidden.className = "option-control";

        group.className = groupClassName;

        option.choiceLabels.forEach((label, index) => {
          const value = option.minValue + index;
          const button = document.createElement("button");
          const icon = document.createElement("span");
          const text = document.createElement("span");
          const isSelected = value === initialValue;

          button.type = "button";
          button.className = buttonClassName;
          button.dataset.value = String(value);
          button.setAttribute("aria-pressed", isSelected ? "true" : "false");
          button.classList.toggle("is-selected", isSelected);
          button.title = label;
          button.disabled = isLocked;

          icon.className = iconClassName;
          icon.innerHTML = isRotationOption(option)
            ? createCorridorPreviewSvg(getOptionControlValue("shape", 3), value)
            : createCorridorPreviewSvg(value, getOptionControlValue("rotation", 0));

          text.className = labelClassName;
          text.textContent = label;

          button.append(icon, text);
          button.addEventListener("click", () => {
            applyValue(value);
          });

          group.appendChild(button);
        });

        controls.append(hidden, group);
      } else {
        const select = document.createElement("select");
        select.className = "option-choice option-control";
        select.name = option.key;

        option.choiceLabels.forEach((label, index) => {
          const choice = document.createElement("option");
          choice.value = String(option.minValue + index);
          choice.textContent = label;
          select.appendChild(choice);
        });

        select.value = String(initialValue);
        select.disabled = isLocked;
        select.addEventListener("change", () => {
          applyValue(Number(select.value));
        });

        controls.appendChild(select);
      }
    } else {
      const slider = document.createElement("input");
      const number = document.createElement("input");

      slider.className = "option-slider option-control";
      number.className = "option-number option-control";

      slider.type = "range";
      slider.name = option.key;
      slider.min = option.minValue;
      slider.max = option.maxValue;
      slider.step = option.step || 1;
      slider.value = String(initialValue);
      slider.disabled = isLocked;

      number.type = "number";
      number.name = option.key;
      number.min = option.minValue;
      number.max = option.maxValue;
      number.step = option.step || 1;
      number.value = String(initialValue);
      number.disabled = isLocked;

      slider.addEventListener("input", () => {
        applyValue(Number(slider.value));
      });

      number.addEventListener("input", () => {
        if (!Number.isFinite(number.valueAsNumber)) {
          return;
        }

        applyValue(number.valueAsNumber);
      });

      number.addEventListener("change", () => {
        applyValue(number.valueAsNumber);
      });

      controls.append(slider, number);
    }

    header.append(titleWrap, meta);
    field.append(header, controls);
    inputsRoot.appendChild(field);
  });

  refreshRotationChoiceButtons();
  refreshShapeChoiceButtons();
}

function readOptions() {
  const options = getInitialOptions(generatorSelect.value, loadState());

  inputsRoot.querySelectorAll(".option-control").forEach((input) => {
    const option = getOptionDefinition(input.name);
    if (!option) {
      return;
    }

    const rawValue = input instanceof HTMLInputElement && input.type === "number"
      ? input.valueAsNumber
      : Number(input.value);
    options[input.name] = normalizeOptionValue(option, rawValue);
  });

  return options;
}

function applyResolvedOptions(generatorId, resolvedOptions) {
  const definition = generatorDefinitionById[generatorId];
  if (!definition || !Array.isArray(resolvedOptions) || resolvedOptions.length === 0) {
    return false;
  }

  definition.options = resolvedOptions;
  return true;
}

function parseColorToRgb(color, fallback) {
  const normalized = String(color || "").trim();
  const hexMatch = normalized.match(/^#([0-9a-f]{6})$/i);
  if (hexMatch) {
    const value = hexMatch[1];
    return [
      Number.parseInt(value.slice(0, 2), 16) / 255,
      Number.parseInt(value.slice(2, 4), 16) / 255,
      Number.parseInt(value.slice(4, 6), 16) / 255,
    ];
  }

  const rgbMatch = normalized.match(/^rgba?\(([^)]+)\)$/i);
  if (rgbMatch) {
    const channels = rgbMatch[1]
      .split(",")
      .slice(0, 3)
      .map((entry) => Number.parseFloat(entry.trim()));
    if (channels.length === 3 && channels.every((channel) => Number.isFinite(channel))) {
      return channels.map((channel) => clamp(channel, 0, 255) / 255);
    }
  }

  return fallback;
}

function createShader(type, source) {
  const shader = gl.createShader(type);
  gl.shaderSource(shader, source);
  gl.compileShader(shader);

  if (!gl.getShaderParameter(shader, gl.COMPILE_STATUS)) {
    const message = gl.getShaderInfoLog(shader) || "Unknown shader compilation failure.";
    gl.deleteShader(shader);
    throw new Error(message);
  }

  return shader;
}

function createNoiseTexture() {
  const size = 128;
  const data = new Uint8Array(size * size * 4);

  for (let index = 0; index < data.length; index += 4) {
    const noise = Math.floor(Math.random() * 256);
    data[index] = noise;
    data[index + 1] = noise;
    data[index + 2] = noise;
    data[index + 3] = 255;
  }

  const texture = gl.createTexture();
  gl.bindTexture(gl.TEXTURE_2D, texture);
  gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, size, size, 0, gl.RGBA, gl.UNSIGNED_BYTE, data);
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.REPEAT);
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.REPEAT);
  return texture;
}

function ensureShaderRuntime() {
  if (shaderRuntime || !shaderCanvas) {
    return shaderRuntime;
  }

  gl = shaderCanvas.getContext("webgl2", { alpha: false, antialias: false });
  if (!gl) {
    return null;
  }

  const shaderSource = window.ProcGenShaders;
  if (!shaderSource?.PROCGEN_SHADER_VERTEX_SOURCE || !shaderSource?.PROCGEN_SHADER_FRAGMENT_SOURCE) {
    return null;
  }

  const vertexShader = createShader(gl.VERTEX_SHADER, shaderSource.PROCGEN_SHADER_VERTEX_SOURCE);
  const fragmentShader = createShader(gl.FRAGMENT_SHADER, shaderSource.PROCGEN_SHADER_FRAGMENT_SOURCE);
  const program = gl.createProgram();
  gl.attachShader(program, vertexShader);
  gl.attachShader(program, fragmentShader);
  gl.linkProgram(program);

  if (!gl.getProgramParameter(program, gl.LINK_STATUS)) {
    const message = gl.getProgramInfoLog(program) || "Unknown shader link failure.";
    throw new Error(message);
  }

  const buffer = gl.createBuffer();
  gl.bindBuffer(gl.ARRAY_BUFFER, buffer);
  gl.bufferData(
    gl.ARRAY_BUFFER,
    new Float32Array([
      -1, -1,
      1, -1,
      -1, 1,
      -1, 1,
      1, -1,
      1, 1,
    ]),
    gl.STATIC_DRAW,
  );

  shaderRuntime = {
    program,
    buffer,
    uniforms: {
      aPosition: gl.getAttribLocation(program, "a_position"),
      mask: gl.getUniformLocation(program, "u_mask"),
      noise: gl.getUniformLocation(program, "u_noise"),
      maskSize: gl.getUniformLocation(program, "u_mask_size"),
      viewportSize: gl.getUniformLocation(program, "u_viewport_size"),
      cameraCenter: gl.getUniformLocation(program, "u_camera_center"),
      scale: gl.getUniformLocation(program, "u_scale"),
      backdropTop: gl.getUniformLocation(program, "u_backdrop_top"),
      backdropBottom: gl.getUniformLocation(program, "u_backdrop_bottom"),
      floorLight: gl.getUniformLocation(program, "u_floor_light"),
      floorShadow: gl.getUniformLocation(program, "u_floor_shadow"),
      wallLight: gl.getUniformLocation(program, "u_wall_light"),
      wallShadow: gl.getUniformLocation(program, "u_wall_shadow"),
      outlineColor: gl.getUniformLocation(program, "u_outline_color"),
      propColor: gl.getUniformLocation(program, "u_prop_color"),
      itemColor: gl.getUniformLocation(program, "u_item_color"),
    },
  };

  noiseTexture = createNoiseTexture();
  rebuildShaderTextures();
  return shaderRuntime;
}

function disposeShaderTextures() {
  if (!gl || storyMaskTextures.length === 0) {
    storyMaskTextures = [];
    return;
  }

  storyMaskTextures.forEach((texture) => gl.deleteTexture(texture));
  storyMaskTextures = [];
}

function createMaskSnapshot(layers) {
  if (!currentMap) {
    return null;
  }

  const imageData = new Uint8Array(currentMap.width * currentMap.height * 4);
  const setChannel = (values, channelIndex) => {
    if (!Array.isArray(values)) {
      return;
    }

    for (let index = 0; index < values.length; index += 2) {
      const x = values[index];
      const y = values[index + 1];
      const pixelIndex = (y * currentMap.width + x) * 4 + channelIndex;
      imageData[pixelIndex] = 255;
    }
  };

  const setRects = (rects, channelIndex) => {
    if (!Array.isArray(rects)) {
      return;
    }

    rects.forEach((rect) => {
      for (let y = rect.y; y < rect.y + rect.height; y += 1) {
        for (let x = rect.x; x < rect.x + rect.width; x += 1) {
          const pixelIndex = (y * currentMap.width + x) * 4 + channelIndex;
          imageData[pixelIndex] = 255;
        }
      }
    });
  };

  setChannel(layers?.walls || [], 0);
  setChannel(layers?.lowWalls || [], 1);
  setChannel(layers?.windows || [], 2);

  if (Array.isArray(currentMap.regions) && currentMap.regions.length > 0) {
    currentMap.regions.forEach((region) => {
      if ((region.kind || "").toLowerCase() === "outside") {
        return;
      }

      setRects(region.rects || [], 3);
    });
  } else {
    setChannel(layers?.floors || [], 3);
    setChannel(layers?.ceilings || [], 3);
  }

  return {
    width: currentMap.width,
    height: currentMap.height,
    data: imageData,
  };
}

function rebuildShaderTextures() {
  disposeShaderTextures();

  if (!gl || storyMaskBuffers.length === 0) {
    return;
  }

  storyMaskTextures = storyMaskBuffers.map((maskBuffer) => {
    const texture = gl.createTexture();
    gl.bindTexture(gl.TEXTURE_2D, texture);
    gl.pixelStorei(gl.UNPACK_ALIGNMENT, 1);
    gl.texImage2D(
      gl.TEXTURE_2D,
      0,
      gl.RGBA,
      maskBuffer.width,
      maskBuffer.height,
      0,
      gl.RGBA,
      gl.UNSIGNED_BYTE,
      maskBuffer.data,
    );
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.NEAREST);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.NEAREST);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
    return texture;
  });
}

function updateRenderModeUi() {
  flatCanvas.hidden = false;
  shaderCanvas.hidden = renderMode !== "shader";

  if (renderFlatButton) {
    const isActive = renderMode === "flat";
    renderFlatButton.classList.toggle("is-active", isActive);
    renderFlatButton.setAttribute("aria-pressed", isActive ? "true" : "false");
  }

  if (renderShaderButton) {
    const isActive = renderMode === "shader";
    renderShaderButton.classList.toggle("is-active", isActive);
    renderShaderButton.setAttribute("aria-pressed", isActive ? "true" : "false");
  }
}

function queueGenerate(resetViewport = true) {
  window.clearTimeout(regenTimer);
  regenTimer = window.setTimeout(() => {
    generate({ resetViewport }).catch(handleGenerationError);
  }, autoRegenDelayMs);
}

function onSettingsChanged(resetViewport = false) {
  saveState();
  broadcastSynchronizedOptions();

  // if (autoRegenCheckbox.checked) {
    queueGenerate(resetViewport);
    return;
  // }

  setStatus("Settings changed. Auto regenerate is off.");
}

function getVisibleLayers(map) {
  if (!map) {
    return null;
  }

  if (Array.isArray(map.storyLayers) && map.storyLayers[selectedStoryIndex]) {
    return map.storyLayers[selectedStoryIndex];
  }

  return map.layers;
}

function resizeCanvas() {
  const rect = canvasShell.getBoundingClientRect();
  const width = Math.max(1, Math.floor(rect.width));
  const height = Math.max(1, Math.floor(rect.height));
  const dpr = window.devicePixelRatio || 1;

  [flatCanvas, shaderCanvas].forEach((surface) => {
    surface.width = Math.floor(width * dpr);
    surface.height = Math.floor(height * dpr);
    surface.style.width = `${width}px`;
    surface.style.height = `${height}px`;
  });

  if (gl) {
    gl.viewport(0, 0, shaderCanvas.width, shaderCanvas.height);
  }

  if (currentMap && !viewportState.hasUserAdjusted) {
    fitViewportToMap();
  }

  requestRender();
}

function fitViewportToMap() {
  if (!currentMap) {
    viewportState.scale = 1;
    viewportState.offsetX = 0;
    viewportState.offsetY = 0;
    viewportState.fitScale = 1;
    viewportState.hasUserAdjusted = false;
    updateViewportMeta();
    return;
  }

  const rect = canvasShell.getBoundingClientRect();
  const availableWidth = Math.max(1, rect.width - viewportPadding * 2);
  const availableHeight = Math.max(1, rect.height - viewportPadding * 2);
  const fitScale = Math.min(availableWidth / currentMap.width, availableHeight / currentMap.height);

  viewportState.fitScale = fitScale;
  viewportState.scale = fitScale;
  viewportState.offsetX = (rect.width - currentMap.width * fitScale) / 2;
  viewportState.offsetY = (rect.height - currentMap.height * fitScale) / 2;
  viewportState.hasUserAdjusted = false;
  updateViewportMeta();
}

function renderStoryPicker() {
  if (!currentMap) {
    storyPicker.hidden = true;
    storySelect.innerHTML = "";
    updateViewportMeta();
    return;
  }

  storyPicker.hidden = embedConfig.hideStoryPicker || currentMap.stories <= 1;
  storySelect.innerHTML = "";

  for (let storyIndex = currentMap.stories - 1; storyIndex >= 0; storyIndex -= 1) {
    const option = document.createElement("option");
    option.value = String(storyIndex);
    option.textContent = `Floor ${storyIndex + 1}`;
    storySelect.appendChild(option);
  }

  storySelect.value = String(selectedStoryIndex);

  updateViewportMeta();
}

function drawFlatCells(targetContext, values, color) {
  if (!Array.isArray(values) || values.length === 0) {
    return;
  }

  targetContext.fillStyle = color;

  for (let index = 0; index < values.length; index += 2) {
    const x = values[index];
    const y = values[index + 1];
    targetContext.fillRect(x, y, 1, 1);
  }
}

function createStorySnapshot(layers) {
  if (!currentMap) {
    return null;
  }

  const snapshot = document.createElement("canvas");
  snapshot.width = currentMap.width;
  snapshot.height = currentMap.height;

  const snapshotContext = snapshot.getContext("2d");
  snapshotContext.imageSmoothingEnabled = false;
  snapshotContext.fillStyle = getThemeColor("--canvas", "#242932");
  snapshotContext.fillRect(0, 0, currentMap.width, currentMap.height);

  if (Array.isArray(currentMap.regions) && currentMap.regions.length > 0) {
    drawRegions(snapshotContext, currentMap.regions);
  } else {
    drawFlatCells(snapshotContext, layers?.floors || [], getThemeColor("--map-floor", "#2f3643"));
    drawFlatCells(snapshotContext, layers?.ceilings || [], getThemeColor("--map-floor", "#2f3643"));
  }
  drawFlatCells(snapshotContext, layers?.lowWalls || [], getThemeColor("--map-low-wall", "#b59e82"));
  drawFlatCells(snapshotContext, layers?.walls || [], getThemeColor("--map-wall", "#d87b24"));
  drawFlatCells(snapshotContext, layers?.doors || [], getThemeColor("--map-door", "#f6f1c7"));
  drawFlatCells(snapshotContext, layers?.windows || [], getThemeColor("--map-window", "#84b8ff"));

  return snapshot;
}

function getRegionColor(kind) {
  switch ((kind || "").toLowerCase()) {
    case "corridor":
      return getThemeColor("--region-corridor", "rgba(64, 142, 255, 0.56)");
    case "outside":
      return getThemeColor("--region-outside", "#353e4d");
    case "accesscore":
    case "access-core":
      return getThemeColor("--region-access-core", "#5a4b78");
    case "elevator":
      return getThemeColor("--region-elevator", "#8a6e42");
    case "stair":
    case "stair-up":
    case "stair-down":
      return getThemeColor("--region-stair", "#7a5560");
    case "apartmentzone":
    case "apartment-zone":
      return getThemeColor("--region-apartment-zone", "#55604c");
    case "apartment":
      return getThemeColor("--region-apartment", "rgba(255, 68, 68, 0.62)");
    case "apartment-potential":
      return getThemeColor("--region-apartment", "rgba(255, 68, 68, 0.62)");
    case "apartment-placement":
      return getThemeColor("--region-apartment-placement", "rgba(255, 136, 64, 0.66)");
    case "balcony":
      return getThemeColor("--region-balcony", "#5b6773");
    case "room-ldk":
      return getThemeColor("--region-room-ldk", "#72899a");
    case "room-bedroom":
      return getThemeColor("--region-room-bedroom", "#617786");
    case "room-bath":
      return getThemeColor("--region-room-bath", "#607b83");
    case "room-wc":
      return getThemeColor("--region-room-wc", "#6d6f87");
    case "room-entry":
      return getThemeColor("--region-room-entry", "#56616e");
    default:
      return getThemeColor("--region-default", "#404958");
  }
}

function drawRegions(targetContext, regions) {
  regions.forEach((region) => {
    targetContext.fillStyle = getRegionColor(region.kind);
    (region.rects || []).forEach((rect) => {
      targetContext.fillRect(rect.x, rect.y, rect.width, rect.height);
    });
  });
}

function getEdgeColor(edge) {
  const edgeId = edge?.id || "";
  switch (edgeId) {
    case "facade-north":
      return "#ff8a5b";
    case "facade-east":
      return "#56c7ff";
    case "facade-south":
      return "#9bdb4d";
    case "facade-west":
      return "#f2c14e";
    default:
      return "#f77fbe";
  }
}

function getEdgePlacementInfo(points) {
  if (!Array.isArray(points) || points.length < 4) {
    return null;
  }

  let minX = Number.POSITIVE_INFINITY;
  let maxX = Number.NEGATIVE_INFINITY;
  let minY = Number.POSITIVE_INFINITY;
  let maxY = Number.NEGATIVE_INFINITY;
  let totalLength = 0;
  const segments = [];

  for (let index = 0; index <= points.length - 4; index += 2) {
    const x1 = points[index];
    const y1 = points[index + 1];
    const x2 = points[index + 2];
    const y2 = points[index + 3];
    const length = Math.hypot(x2 - x1, y2 - y1);
    segments.push({ x1, y1, x2, y2, length });
    totalLength += length;
  }

  for (let index = 0; index < points.length; index += 2) {
    minX = Math.min(minX, points[index]);
    maxX = Math.max(maxX, points[index]);
    minY = Math.min(minY, points[index + 1]);
    maxY = Math.max(maxY, points[index + 1]);
  }

  const width = maxX - minX;
  const height = maxY - minY;
  const mapCenterX = currentMap.width * 0.5;
  const mapCenterY = currentMap.height * 0.5;
  const anchorX = (minX + maxX) * 0.5;
  const anchorY = (minY + maxY) * 0.5;

  let side = "top";
  if (width >= height) {
    side = anchorY <= mapCenterY ? "top" : "bottom";
  } else {
    side = anchorX <= mapCenterX ? "left" : "right";
  }

  return {
    minX,
    maxX,
    minY,
    maxY,
    width,
    height,
    totalLength,
    segments,
    anchorX,
    anchorY,
    side,
  };
}

function getAdjacentFeature(edge) {
  return String(edge?.metadata?.adjacentFeature || edge?.facadeMetadata?.adjacentFeature || "").toLowerCase();
}

function formatMetadataToken(value) {
  const normalized = String(value || "").trim();
  if (!normalized) {
    return "";
  }

  return normalized
    .replace(/([a-z])([A-Z])/g, "$1 $2")
    .replace(/[-_]+/g, " ")
    .replace(/\b\w/g, (match) => match.toUpperCase());
}

function getDaylightLabel(edge) {
  return formatMetadataToken(edge?.metadata?.daylight || edge?.facadeMetadata?.daylight);
}

function getEdgeMetadataLines(edge) {
  const adjacentFeature = getAdjacentFeature(edge);
  const daylight = getDaylightLabel(edge);
  const lines = [];

  if (adjacentFeature) {
    lines.push(adjacentFeature === "street" ? "Street" : adjacentFeature === "building" ? "Building" : adjacentFeature);
  }

  if (daylight) {
    lines.push(`${daylight} daylight`);
  }

  if (lines.length > 0) {
    return lines;
  }

  return String(edge?.detail || "")
    .split("|")
    .map((part) => part.trim())
    .filter(Boolean)
    .slice(0, 2);
}

function drawEdgeAdjacentPreviews(targetContext, edges) {
  if (!Array.isArray(edges) || edges.length === 0 || !currentMap) {
    return;
  }

  targetContext.save();
  targetContext.translate(viewportState.offsetX, viewportState.offsetY);
  targetContext.scale(viewportState.scale, viewportState.scale);
  targetContext.lineCap = "round";
  targetContext.lineJoin = "round";

  edges.forEach((edge) => {
    const placement = getEdgePlacementInfo(edge?.points);
    if (!placement) {
      return;
    }

    const adjacentFeature = getAdjacentFeature(edge);
    const isStreet = adjacentFeature === "street";
    const margin = 0.7;
    const fadeDepth = isStreet ? 4.4 : 2.8;
    const buildingTick = 1.55;
    const color = getEdgeColor(edge);
    const lineAlpha = isStreet ? "88" : "55";
    const previewLineWidth = Math.max(0.04, 1 / Math.max(viewportState.scale, 1));

    switch (placement.side) {
      case "left":
      {
        const previewX = placement.minX - margin;
        targetContext.strokeStyle = `${color}${lineAlpha}`;
        targetContext.lineWidth = previewLineWidth;
        targetContext.beginPath();
        targetContext.moveTo(previewX, placement.minY);
        targetContext.lineTo(previewX, placement.maxY);
        targetContext.stroke();

        if (isStreet) {
          const gradient = targetContext.createLinearGradient(previewX - fadeDepth, placement.anchorY, previewX, placement.anchorY);
          gradient.addColorStop(0, "rgba(0, 0, 0, 0)");
          gradient.addColorStop(1, `${color}1f`);
          targetContext.fillStyle = gradient;
          targetContext.fillRect(previewX - fadeDepth, placement.minY, fadeDepth, Math.max(0.001, placement.maxY - placement.minY));

          const laneX = previewX - Math.max(0.65, fadeDepth * 0.5);
          targetContext.save();
          targetContext.strokeStyle = "rgba(236, 241, 247, 0.26)";
          targetContext.setLineDash([0.55, 0.45]);
          targetContext.beginPath();
          targetContext.moveTo(laneX, placement.minY + 0.3);
          targetContext.lineTo(laneX, placement.maxY - 0.3);
          targetContext.stroke();
          targetContext.restore();
        } else {
          const gradient = targetContext.createLinearGradient(previewX - fadeDepth, placement.anchorY, previewX, placement.anchorY);
          gradient.addColorStop(0, "rgba(0, 0, 0, 0)");
          gradient.addColorStop(1, "rgba(150, 160, 178, 0.12)");
          targetContext.fillStyle = gradient;
          targetContext.fillRect(previewX - fadeDepth, placement.minY, fadeDepth, Math.max(0.001, placement.maxY - placement.minY));
          targetContext.beginPath();
          targetContext.moveTo(previewX - buildingTick, placement.minY);
          targetContext.lineTo(previewX, placement.minY);
          targetContext.moveTo(previewX - buildingTick, placement.maxY);
          targetContext.lineTo(previewX, placement.maxY);
          targetContext.stroke();
        }
        break;
      }
      case "right":
      {
        const previewX = placement.maxX + margin;
        targetContext.strokeStyle = `${color}${lineAlpha}`;
        targetContext.lineWidth = previewLineWidth;
        targetContext.beginPath();
        targetContext.moveTo(previewX, placement.minY);
        targetContext.lineTo(previewX, placement.maxY);
        targetContext.stroke();

        if (isStreet) {
          const gradient = targetContext.createLinearGradient(previewX + fadeDepth, placement.anchorY, previewX, placement.anchorY);
          gradient.addColorStop(0, "rgba(0, 0, 0, 0)");
          gradient.addColorStop(1, `${color}1f`);
          targetContext.fillStyle = gradient;
          targetContext.fillRect(previewX, placement.minY, fadeDepth, Math.max(0.001, placement.maxY - placement.minY));

          const laneX = previewX + Math.max(0.65, fadeDepth * 0.5);
          targetContext.save();
          targetContext.strokeStyle = "rgba(236, 241, 247, 0.26)";
          targetContext.setLineDash([0.55, 0.45]);
          targetContext.beginPath();
          targetContext.moveTo(laneX, placement.minY + 0.3);
          targetContext.lineTo(laneX, placement.maxY - 0.3);
          targetContext.stroke();
          targetContext.restore();
        } else {
          const gradient = targetContext.createLinearGradient(previewX + fadeDepth, placement.anchorY, previewX, placement.anchorY);
          gradient.addColorStop(0, "rgba(0, 0, 0, 0)");
          gradient.addColorStop(1, "rgba(150, 160, 178, 0.12)");
          targetContext.fillStyle = gradient;
          targetContext.fillRect(previewX, placement.minY, fadeDepth, Math.max(0.001, placement.maxY - placement.minY));
          targetContext.beginPath();
          targetContext.moveTo(previewX, placement.minY);
          targetContext.lineTo(previewX + buildingTick, placement.minY);
          targetContext.moveTo(previewX, placement.maxY);
          targetContext.lineTo(previewX + buildingTick, placement.maxY);
          targetContext.stroke();
        }
        break;
      }
      case "bottom":
      {
        const previewY = placement.maxY + margin;
        targetContext.strokeStyle = `${color}${lineAlpha}`;
        targetContext.lineWidth = previewLineWidth;
        targetContext.beginPath();
        targetContext.moveTo(placement.minX, previewY);
        targetContext.lineTo(placement.maxX, previewY);
        targetContext.stroke();

        if (isStreet) {
          const gradient = targetContext.createLinearGradient(placement.anchorX, previewY + fadeDepth, placement.anchorX, previewY);
          gradient.addColorStop(0, "rgba(0, 0, 0, 0)");
          gradient.addColorStop(1, `${color}1f`);
          targetContext.fillStyle = gradient;
          targetContext.fillRect(placement.minX, previewY, Math.max(0.001, placement.maxX - placement.minX), fadeDepth);

          const laneY = previewY + Math.max(0.65, fadeDepth * 0.5);
          targetContext.save();
          targetContext.strokeStyle = "rgba(236, 241, 247, 0.26)";
          targetContext.setLineDash([0.55, 0.45]);
          targetContext.beginPath();
          targetContext.moveTo(placement.minX + 0.3, laneY);
          targetContext.lineTo(placement.maxX - 0.3, laneY);
          targetContext.stroke();
          targetContext.restore();
        } else {
          const gradient = targetContext.createLinearGradient(placement.anchorX, previewY + fadeDepth, placement.anchorX, previewY);
          gradient.addColorStop(0, "rgba(0, 0, 0, 0)");
          gradient.addColorStop(1, "rgba(150, 160, 178, 0.12)");
          targetContext.fillStyle = gradient;
          targetContext.fillRect(placement.minX, previewY, Math.max(0.001, placement.maxX - placement.minX), fadeDepth);
          targetContext.beginPath();
          targetContext.moveTo(placement.minX, previewY);
          targetContext.lineTo(placement.minX, previewY + buildingTick);
          targetContext.moveTo(placement.maxX, previewY);
          targetContext.lineTo(placement.maxX, previewY + buildingTick);
          targetContext.stroke();
        }
        break;
      }
      default:
      {
        const previewY = placement.minY - margin;
        targetContext.strokeStyle = `${color}${lineAlpha}`;
        targetContext.lineWidth = previewLineWidth;
        targetContext.beginPath();
        targetContext.moveTo(placement.minX, previewY);
        targetContext.lineTo(placement.maxX, previewY);
        targetContext.stroke();

        if (isStreet) {
          const gradient = targetContext.createLinearGradient(placement.anchorX, previewY - fadeDepth, placement.anchorX, previewY);
          gradient.addColorStop(0, "rgba(0, 0, 0, 0)");
          gradient.addColorStop(1, `${color}1f`);
          targetContext.fillStyle = gradient;
          targetContext.fillRect(placement.minX, previewY - fadeDepth, Math.max(0.001, placement.maxX - placement.minX), fadeDepth);

          const laneY = previewY - Math.max(0.65, fadeDepth * 0.5);
          targetContext.save();
          targetContext.strokeStyle = "rgba(236, 241, 247, 0.26)";
          targetContext.setLineDash([0.55, 0.45]);
          targetContext.beginPath();
          targetContext.moveTo(placement.minX + 0.3, laneY);
          targetContext.lineTo(placement.maxX - 0.3, laneY);
          targetContext.stroke();
          targetContext.restore();
        } else {
          const gradient = targetContext.createLinearGradient(placement.anchorX, previewY - fadeDepth, placement.anchorX, previewY);
          gradient.addColorStop(0, "rgba(0, 0, 0, 0)");
          gradient.addColorStop(1, "rgba(150, 160, 178, 0.12)");
          targetContext.fillStyle = gradient;
          targetContext.fillRect(placement.minX, previewY - fadeDepth, Math.max(0.001, placement.maxX - placement.minX), fadeDepth);
          targetContext.beginPath();
          targetContext.moveTo(placement.minX, previewY - buildingTick);
          targetContext.lineTo(placement.minX, previewY);
          targetContext.moveTo(placement.maxX, previewY - buildingTick);
          targetContext.lineTo(placement.maxX, previewY);
          targetContext.stroke();
        }
        break;
      }
    }
  });

  targetContext.restore();
}

function drawMetaEdges(targetContext, edges) {
  if (!Array.isArray(edges) || edges.length === 0 || !currentMap) {
    return;
  }

  targetContext.save();
  targetContext.translate(viewportState.offsetX, viewportState.offsetY);
  targetContext.scale(viewportState.scale, viewportState.scale);
  targetContext.lineCap = "round";
  targetContext.lineJoin = "round";

  edges.forEach((edge) => {
    const points = edge?.points;
    if (!Array.isArray(points) || points.length < 4) {
      return;
    }

    targetContext.strokeStyle = getEdgeColor(edge);
    targetContext.globalAlpha = 0.92;
    targetContext.lineWidth = Math.max(0.08, 1.4 / Math.max(viewportState.scale, 1));
    targetContext.beginPath();
    targetContext.moveTo(points[0], points[1]);

    for (let index = 2; index < points.length; index += 2) {
      targetContext.lineTo(points[index], points[index + 1]);
    }

    targetContext.stroke();
  });

  targetContext.restore();
}

function drawEdgeLabels(targetContext, edges) {
  if (!Array.isArray(edges) || edges.length === 0 || !currentMap || viewportState.scale < 2) {
    return;
  }

  targetContext.save();
  targetContext.translate(viewportState.offsetX, viewportState.offsetY);
  targetContext.scale(viewportState.scale, viewportState.scale);
  targetContext.textAlign = "center";
  targetContext.textBaseline = "middle";

  edges.forEach((edge) => {
    const label = String(edge?.label || "").trim();
    const metadataLines = getEdgeMetadataLines(edge);
    const placement = getEdgePlacementInfo(edge?.points);
    if (!label || !placement) {
      return;
    }

    const adjacentFeature = getAdjacentFeature(edge);
    const streetExtra = adjacentFeature === "street" ? 4.25 : 0.95;
    const labelGap = 1.9 + streetExtra;
    let labelX = placement.anchorX;
    let labelY = placement.anchorY;

    switch (placement.side) {
      case "left":
        labelX = placement.minX - labelGap;
        break;
      case "right":
        labelX = placement.maxX + labelGap;
        break;
      case "bottom":
        labelY = placement.maxY + labelGap;
        break;
      default:
        labelY = placement.minY - labelGap;
        break;
    }

    targetContext.textAlign = placement.side === "left"
      ? "right"
      : placement.side === "right"
        ? "left"
        : "center";
    const titleSize = Math.max(0.72, 6.5 / Math.max(viewportState.scale, 1));
    const metaSize = Math.max(0.52, titleSize * 0.62);
    const metaGap = 0.14;
    const blockGap = 0.22;

    targetContext.font = `700 ${titleSize}px ui-sans-serif, system-ui, sans-serif`;
    targetContext.lineWidth = Math.max(0.05, 0.95 / Math.max(viewportState.scale, 1));
    targetContext.strokeStyle = "rgba(17, 22, 29, 0.92)";
    targetContext.fillStyle = getEdgeColor(edge);
    targetContext.textBaseline = "middle";

    const detailBlockHeight = metadataLines.length > 0
      ? metadataLines.length * metaSize + (metadataLines.length - 1) * metaGap
      : 0;
    const totalBlockHeight = titleSize + (metadataLines.length > 0 ? blockGap + detailBlockHeight : 0);
    let titleY = labelY;

    if (placement.side === "top" || placement.side === "bottom") {
      titleY = labelY - totalBlockHeight * 0.5 + titleSize * 0.5;
    }

    targetContext.strokeText(label, labelX, titleY);
    targetContext.fillText(label, labelX, titleY);

    if (metadataLines.length > 0) {
      targetContext.font = `600 ${metaSize}px ui-sans-serif, system-ui, sans-serif`;
      targetContext.fillStyle = "rgba(214, 222, 232, 0.9)";
      metadataLines.forEach((line, index) => {
        const lineY = titleY + titleSize * 0.5 + blockGap + metaSize * 0.5 + index * (metaSize + metaGap);

        targetContext.strokeText(line, labelX, lineY);
        targetContext.fillText(line, labelX, lineY);
      });
    }

    targetContext.textAlign = "center";
    targetContext.textBaseline = "middle";
  });

  targetContext.restore();
}

function drawLowWallCells(targetContext, values) {
  if (!Array.isArray(values) || values.length === 0 || !currentMap) {
    return;
  }

  const fillColor = getThemeColor("--map-low-wall", "#bba78e");

  targetContext.save();
  targetContext.translate(viewportState.offsetX, viewportState.offsetY);
  targetContext.scale(viewportState.scale, viewportState.scale);
  targetContext.fillStyle = fillColor;

  for (let index = 0; index < values.length; index += 2) {
    const x = values[index];
    const y = values[index + 1];
    targetContext.fillRect(x, y, 1, 1);
  }

  targetContext.restore();
}

function drawFeatureOverlays(targetContext, regions) {
  if (!Array.isArray(regions) || regions.length === 0 || !currentMap) {
    return;
  }

  const elevatorColor = getThemeColor("--region-elevator", "#8a6e42");
  const stairColor = getThemeColor("--region-stair", "#7a5560");
  targetContext.save();
  targetContext.translate(viewportState.offsetX, viewportState.offsetY);
  targetContext.scale(viewportState.scale, viewportState.scale);
  targetContext.lineCap = "square";
  targetContext.lineJoin = "miter";

  regions.forEach((region) => {
    const kind = String(region.kind || "").toLowerCase();
    if (kind !== "elevator" && kind !== "stair" && kind !== "stair-up" && kind !== "stair-down") {
      return;
    }

    (region.rects || []).forEach((rect) => {
      if (!rect || rect.width <= 0 || rect.height <= 0) {
        return;
      }

      const inset = Math.max(0.35, Math.min(rect.width, rect.height) * 0.18);
      const x = rect.x + inset;
      const y = rect.y + inset;
      const width = Math.max(0.2, rect.width - inset * 2);
      const height = Math.max(0.2, rect.height - inset * 2);

      const isStair = kind === "stair" || kind === "stair-up" || kind === "stair-down";
      targetContext.strokeStyle = kind === "elevator" ? elevatorColor : stairColor;
      targetContext.fillStyle = kind === "elevator" ? `${elevatorColor}22` : `${stairColor}18`;
      targetContext.lineWidth = Math.max(0.12, 1 / Math.max(viewportState.scale, 1));

      targetContext.fillRect(x, y, width, height);
      targetContext.strokeRect(x, y, width, height);

      if (kind === "elevator") {
        const centerX = rect.x + rect.width / 2;
        const centerY = rect.y + rect.height / 2;
        targetContext.beginPath();
        targetContext.moveTo(centerX, y + 0.35);
        targetContext.lineTo(centerX, y + height - 0.35);
        targetContext.moveTo(x + 0.35, centerY);
        targetContext.lineTo(x + width - 0.35, centerY);
        targetContext.stroke();
        return;
      }

      const steps = Math.max(3, Math.min(6, Math.floor(Math.max(rect.width, rect.height) / 1.6)));
      targetContext.beginPath();

      if (rect.width >= rect.height) {
        const run = width / steps;
        for (let step = 0; step < steps; step += 1) {
          const stepX = x + step * run;
          targetContext.moveTo(stepX, y + height);
          targetContext.lineTo(stepX + run, y + height);
          targetContext.lineTo(stepX + run, y + height - ((step + 1) / steps) * height);
        }
      } else {
        const rise = height / steps;
        for (let step = 0; step < steps; step += 1) {
          const stepY = y + step * rise;
          targetContext.moveTo(x, stepY + rise);
          targetContext.lineTo(x, stepY);
          targetContext.lineTo(x + ((step + 1) / steps) * width, stepY);
        }
      }

      targetContext.stroke();

      const arrowSize = Math.max(0.4, Math.min(width, height) * 0.28);
      const centerX = rect.x + rect.width / 2;
      const centerY = rect.y + rect.height / 2;
      targetContext.beginPath();
      if (kind === "stair-down") {
        targetContext.moveTo(centerX - arrowSize * 0.4, centerY - arrowSize * 0.25);
        targetContext.lineTo(centerX, centerY + arrowSize * 0.35);
        targetContext.lineTo(centerX + arrowSize * 0.4, centerY - arrowSize * 0.25);
      } else {
        targetContext.moveTo(centerX - arrowSize * 0.4, centerY + arrowSize * 0.25);
        targetContext.lineTo(centerX, centerY - arrowSize * 0.35);
        targetContext.lineTo(centerX + arrowSize * 0.4, centerY + arrowSize * 0.25);
      }
      targetContext.stroke();
    });
  });

  targetContext.restore();
}

function drawRegionLabels(targetContext, regions) {
  if (!Array.isArray(regions) || regions.length === 0 || !currentMap) {
    return;
  }

  targetContext.save();
  targetContext.translate(viewportState.offsetX, viewportState.offsetY);
  targetContext.scale(viewportState.scale, viewportState.scale);
  targetContext.textAlign = "center";
  targetContext.textBaseline = "middle";
  targetContext.fillStyle = "rgba(242, 236, 226, 0.92)";
  targetContext.strokeStyle = "rgba(23, 27, 35, 0.68)";

  regions.forEach((region) => {
    const kind = String(region.kind || "").toLowerCase();
    if (!kind.startsWith("room-")) {
      return;
    }

    (region.rects || []).forEach((rect) => {
      if (!rect || rect.width < 5 || rect.height < 4) {
        return;
      }

      const fontSize = Math.max(1.8, Math.min(rect.width * 0.22, rect.height * 0.42, 3.2));
      targetContext.font = `600 ${fontSize}px ui-sans-serif, system-ui, sans-serif`;
      targetContext.lineWidth = Math.max(0.08, 0.6 / Math.max(viewportState.scale, 1));
      const centerX = rect.x + rect.width / 2;
      const centerY = rect.y + rect.height / 2;
      targetContext.strokeText(region.label, centerX, centerY);
      targetContext.fillText(region.label, centerX, centerY);
    });
  });

  targetContext.restore();
}

function renderRegionLegend() {
  if (!regionLegend) {
    return;
  }

  if (shouldHideLegendForViewport()) {
    regionLegend.hidden = true;
    regionLegend.innerHTML = "";
    return;
  }

  const regions = currentMap?.regions || [];
  // const edges = currentMap?.edges || [];
  // if ((!Array.isArray(regions) || regions.length === 0) && (!Array.isArray(edges) || edges.length === 0)) {
  //   regionLegend.hidden = true;
  //   regionLegend.innerHTML = "";
  //   return;
  // }

  const seen = new Set();
  const items = [];

  regions.forEach((region) => {
    const key = `region:${region.kind}:${region.label}`;
    if ((region.kind || "").toLowerCase().startsWith("room-")) {
      return;
    }
    if (seen.has(key)) {
      return;
    }

    seen.add(key);
    items.push({
      kind: region.kind,
      label: region.label,
      color: getRegionColor(region.kind),
    });
  });

  // edges.forEach((edge) => {
  //   const key = `edge:${edge.kind}:${edge.label}`;
  //   if (seen.has(key)) {
  //     return;
  //   }

  //   seen.add(key);
  //   items.push({
  //     kind: edge.kind,
  //     label: edge.detail ? `${edge.label} - ${edge.detail}` : edge.label,
  //     color: getEdgeColor(edge),
  //   });
  // });

  regionLegend.innerHTML = "";
  items.forEach((itemData) => {
    const item = document.createElement("div");
    const swatch = document.createElement("span");
    const label = document.createElement("span");

    item.className = "legend-item";
    swatch.className = "legend-swatch";
    swatch.style.background = itemData.color;
    label.className = "legend-label";
    label.textContent = itemData.label;

    item.append(swatch, label);
    regionLegend.appendChild(item);
  });

  regionLegend.hidden = false;
}

function rebuildStorySnapshots() {
  storySnapshotCanvases = [];
  storyMaskBuffers = [];

  if (!currentMap) {
    disposeShaderTextures();
    return;
  }

  if (Array.isArray(currentMap.storyLayers) && currentMap.storyLayers.length > 0) {
    storySnapshotCanvases = currentMap.storyLayers.map((layers) => createStorySnapshot(layers));
    storyMaskBuffers = currentMap.storyLayers.map((layers) => createMaskSnapshot(layers));
    rebuildShaderTextures();
    return;
  }

  storySnapshotCanvases = [createStorySnapshot(currentMap.layers)];
  storyMaskBuffers = [createMaskSnapshot(currentMap.layers)];
  rebuildShaderTextures();
}

function getCurrentStorySnapshot() {
  if (storySnapshotCanvases.length === 0) {
    return null;
  }

  return storySnapshotCanvases[selectedStoryIndex] || storySnapshotCanvases[0];
}

function getCurrentStoryMaskTexture() {
  if (storyMaskTextures.length === 0) {
    return null;
  }

  return storyMaskTextures[selectedStoryIndex] || storyMaskTextures[0];
}

function renderMap() {
  const rect = canvasShell.getBoundingClientRect();
  const width = Math.max(1, rect.width);
  const height = Math.max(1, rect.height);
  const dpr = window.devicePixelRatio || 1;

  if (renderMode === "shader" && renderShaderMap(width, height, dpr)) {
    updateViewportMeta();
    return;
  }

  flatCtx.setTransform(dpr, 0, 0, dpr, 0, 0);
  flatCtx.clearRect(0, 0, width, height);
  flatCtx.fillStyle = getThemeColor("--canvas", "#242932");
  flatCtx.fillRect(0, 0, width, height);

  if (!currentMap) {
    updateViewportMeta();
    return;
  }
  const snapshot = getCurrentStorySnapshot();
  if (!snapshot) {
    updateViewportMeta();
    return;
  }

  flatCtx.save();
  flatCtx.imageSmoothingEnabled = false;
  flatCtx.drawImage(
    snapshot,
    viewportState.offsetX,
    viewportState.offsetY,
    currentMap.width * viewportState.scale,
    currentMap.height * viewportState.scale,
  );

  if (gridEnabled) {
    drawGrid(flatCtx);
  }

  drawEdgeAdjacentPreviews(flatCtx, currentMap.edges || []);
  drawMetaEdges(flatCtx, currentMap.edges || []);

  flatCtx.strokeStyle = getThemeColor("--map-outline", "rgba(255, 255, 255, 0.08)");
  flatCtx.lineWidth = 1;
  flatCtx.strokeRect(
    viewportState.offsetX,
    viewportState.offsetY,
    currentMap.width * viewportState.scale,
    currentMap.height * viewportState.scale,
  );
  flatCtx.restore();
  drawFeatureOverlays(flatCtx, currentMap.regions);
  drawEdgeLabels(flatCtx, currentMap.edges || []);
  drawRegionLabels(flatCtx, currentMap.regions);

  updateViewportMeta();
}

function renderShaderMap(width, height, dpr) {
  const runtime = ensureShaderRuntime();
  const maskTexture = getCurrentStoryMaskTexture();
  if (!runtime || !currentMap || !maskTexture) {
    return false;
  }

  gl.viewport(0, 0, shaderCanvas.width, shaderCanvas.height);
  gl.clearColor(0, 0, 0, 1);
  gl.clear(gl.COLOR_BUFFER_BIT);
  gl.useProgram(runtime.program);
  gl.bindBuffer(gl.ARRAY_BUFFER, runtime.buffer);
  gl.enableVertexAttribArray(runtime.uniforms.aPosition);
  gl.vertexAttribPointer(runtime.uniforms.aPosition, 2, gl.FLOAT, false, 0, 0);

  const cssToDeviceScale = dpr;
  const viewportWidth = width * cssToDeviceScale;
  const viewportHeight = height * cssToDeviceScale;
  const scaledOffsetX = viewportState.offsetX * cssToDeviceScale;
  const scaledOffsetY = viewportState.offsetY * cssToDeviceScale;
  const scaledMapScale = viewportState.scale * cssToDeviceScale;
  const cameraCenterX = (viewportWidth * 0.5 - scaledOffsetX) / Math.max(scaledMapScale, 0.0001);
  const cameraCenterY = (viewportHeight * 0.5 - scaledOffsetY) / Math.max(scaledMapScale, 0.0001);

  gl.activeTexture(gl.TEXTURE0);
  gl.bindTexture(gl.TEXTURE_2D, maskTexture);
  gl.uniform1i(runtime.uniforms.mask, 0);

  gl.activeTexture(gl.TEXTURE1);
  gl.bindTexture(gl.TEXTURE_2D, noiseTexture);
  gl.uniform1i(runtime.uniforms.noise, 1);

  gl.uniform2f(runtime.uniforms.maskSize, currentMap.width, currentMap.height);
  gl.uniform2f(runtime.uniforms.viewportSize, viewportWidth, viewportHeight);
  gl.uniform2f(runtime.uniforms.cameraCenter, cameraCenterX, cameraCenterY);
  gl.uniform1f(runtime.uniforms.scale, scaledMapScale);
  gl.uniform3fv(runtime.uniforms.backdropTop, parseColorToRgb(getThemeColor("--canvas", "#242932"), [0.14, 0.16, 0.2]));
  gl.uniform3fv(runtime.uniforms.backdropBottom, parseColorToRgb(getThemeColor("--surface-strong", "#252c36"), [0.14, 0.16, 0.2]));
  gl.uniform3fv(runtime.uniforms.floorLight, parseColorToRgb(getThemeColor("--region-corridor", "#4a657d"), [0.29, 0.4, 0.49]));
  gl.uniform3fv(runtime.uniforms.floorShadow, parseColorToRgb(getThemeColor("--map-floor", "#435163"), [0.25, 0.31, 0.38]));
  gl.uniform3fv(runtime.uniforms.wallLight, parseColorToRgb(getThemeColor("--map-wall", "#dbc6ad"), [0.86, 0.78, 0.68]));
  gl.uniform3fv(runtime.uniforms.wallShadow, parseColorToRgb("#8f785f", [0.56, 0.47, 0.37]));
  gl.uniform3fv(runtime.uniforms.outlineColor, [0.88, 0.86, 0.82]);
  gl.uniform3fv(runtime.uniforms.propColor, parseColorToRgb(getThemeColor("--map-low-wall", "#b59e82"), [0.71, 0.62, 0.51]));
  gl.uniform3fv(runtime.uniforms.itemColor, parseColorToRgb(getThemeColor("--map-window", "#9bdcff"), [0.61, 0.86, 1]));
  gl.drawArrays(gl.TRIANGLES, 0, 6);

  if (gridEnabled || currentMap) {
    flatCtx.setTransform(dpr, 0, 0, dpr, 0, 0);
    flatCtx.clearRect(0, 0, width, height);
    if (gridEnabled) {
      drawGrid(flatCtx);
    }
    drawEdgeAdjacentPreviews(flatCtx, currentMap.edges || []);
    drawMetaEdges(flatCtx, currentMap.edges || []);
    flatCtx.save();
    flatCtx.strokeStyle = getThemeColor("--map-outline", "rgba(255, 255, 255, 0.08)");
    flatCtx.lineWidth = 1;
    flatCtx.strokeRect(
      viewportState.offsetX,
      viewportState.offsetY,
      currentMap.width * viewportState.scale,
      currentMap.height * viewportState.scale,
    );
    flatCtx.restore();
    drawFeatureOverlays(flatCtx, currentMap.regions);
    drawEdgeLabels(flatCtx, currentMap.edges || []);
    drawRegionLabels(flatCtx, currentMap.regions);
  }

  return true;
}

function drawGrid(targetContext) {
  if (!currentMap) {
    return;
  }

  const scaledWidth = currentMap.width * viewportState.scale;
  const scaledHeight = currentMap.height * viewportState.scale;
  const startX = viewportState.offsetX;
  const startY = viewportState.offsetY;

  targetContext.save();
  targetContext.strokeStyle = getThemeColor("--map-grid", "rgba(255, 255, 255, 0.08)");
  targetContext.lineWidth = 1;
  targetContext.beginPath();

  for (let x = 0; x <= currentMap.width; x += 1) {
    const drawX = Math.round(startX + x * viewportState.scale) + 0.5;
    targetContext.moveTo(drawX, startY);
    targetContext.lineTo(drawX, startY + scaledHeight);
  }

  for (let y = 0; y <= currentMap.height; y += 1) {
    const drawY = Math.round(startY + y * viewportState.scale) + 0.5;
    targetContext.moveTo(startX, drawY);
    targetContext.lineTo(startX + scaledWidth, drawY);
  }

  targetContext.stroke();
  targetContext.restore();
}

function setMap(map, resetViewport) {
  currentMap = map;
  selectedStoryIndex = clamp(selectedStoryIndex, 0, Math.max(0, (map?.stories || 1) - 1));
  rebuildStorySnapshots();
  renderRegionLegend();
  saveState();
  renderStoryPicker();

  if (resetViewport || !viewportState.hasUserAdjusted) {
    fitViewportToMap();
  }

  requestRender();
}

function handleGenerationError(error) {
  if (error?.name === "AbortError") {
    return;
  }

  setStatus(error instanceof Error ? error.message : "Generation failed.");
}

async function generate({ resetViewport = true } = {}) {
  saveState();
  window.clearTimeout(regenTimer);

  if (currentAbortController) {
    currentAbortController.abort();
  }

  const controller = new AbortController();
  const requestId = ++requestSequence;
  currentAbortController = controller;

  setStatus("Generating...");

  try {
    const runtimeBridge = await ensureRuntimeBridge();
    if (controller.signal.aborted) {
      return;
    }

    const envelope = await runtimeBridge.generateMap({
      generatorId: generatorSelect.value,
      options: readOptions(),
    });
    if (requestId !== requestSequence) {
      return;
    }

    if (!envelope.ok) {
      setStatus(envelope.errorMessage || "Generation failed.");
      setMap(null, true);
      return;
    }

    if (applyResolvedOptions(generatorSelect.value, envelope.resolvedOptions)) {
      buildInputs();
    }

    setMap(envelope.map, resetViewport);
    setStatus(`Generated ${envelope.map.width}x${envelope.map.height} (${envelope.map.stories} stories).`);
  } finally {
    if (currentAbortController === controller) {
      currentAbortController = null;
    }
  }
}

function onWheel(event) {
  if (!currentMap) {
    return;
  }

  event.preventDefault();

  const rect = canvasShell.getBoundingClientRect();
  const mouseX = event.clientX - rect.left;
  const mouseY = event.clientY - rect.top;
  const factor = Math.exp(-event.deltaY * 0.0015);
  const minZoom = Math.max(viewportState.fitScale * minZoomMultiplier, 0.1);
  const nextScale = clamp(viewportState.scale * factor, minZoom, maxZoom);
  const mapX = (mouseX - viewportState.offsetX) / viewportState.scale;
  const mapY = (mouseY - viewportState.offsetY) / viewportState.scale;

  viewportState.scale = nextScale;
  viewportState.offsetX = mouseX - mapX * nextScale;
  viewportState.offsetY = mouseY - mapY * nextScale;
  viewportState.hasUserAdjusted = true;
  requestRender();
}

function getPinchMetrics() {
  const pointers = [...gestureState.activePointers.values()];
  if (pointers.length < 2) {
    return null;
  }

  const [first, second] = pointers;
  const deltaX = second.clientX - first.clientX;
  const deltaY = second.clientY - first.clientY;

  return {
    distance: Math.hypot(deltaX, deltaY),
    centerX: (first.clientX + second.clientX) * 0.5,
    centerY: (first.clientY + second.clientY) * 0.5,
  };
}

function beginPinchGesture() {
  const metrics = getPinchMetrics();
  if (!metrics || !currentMap) {
    return;
  }

  const rect = canvasShell.getBoundingClientRect();
  const centerX = metrics.centerX - rect.left;
  const centerY = metrics.centerY - rect.top;

  gestureState.pinchDistance = Math.max(metrics.distance, 1);
  gestureState.pinchScale = viewportState.scale;
  gestureState.pinchMapX = (centerX - viewportState.offsetX) / Math.max(viewportState.scale, 0.0001);
  gestureState.pinchMapY = (centerY - viewportState.offsetY) / Math.max(viewportState.scale, 0.0001);
  dragState.pointerId = null;
  canvasShell.classList.remove("is-dragging");
}

function updatePinchGesture() {
  const metrics = getPinchMetrics();
  if (!metrics || !currentMap) {
    return;
  }

  const rect = canvasShell.getBoundingClientRect();
  const centerX = metrics.centerX - rect.left;
  const centerY = metrics.centerY - rect.top;
  const minZoom = Math.max(viewportState.fitScale * minZoomMultiplier, 0.1);
  const nextScale = clamp(
    gestureState.pinchScale * (Math.max(metrics.distance, 1) / Math.max(gestureState.pinchDistance, 1)),
    minZoom,
    maxZoom,
  );

  viewportState.scale = nextScale;
  viewportState.offsetX = centerX - gestureState.pinchMapX * nextScale;
  viewportState.offsetY = centerY - gestureState.pinchMapY * nextScale;
  viewportState.hasUserAdjusted = true;
  requestRender();
}

function onPointerDown(event) {
  if (!currentMap) {
    return;
  }

  if (event.target instanceof Element && (event.target.closest(".canvas-overlay") || event.target.closest(".canvas-bottom-bar"))) {
    return;
  }

  gestureState.activePointers.set(event.pointerId, {
    clientX: event.clientX,
    clientY: event.clientY,
  });

  canvasShell.setPointerCapture(event.pointerId);

  if (gestureState.activePointers.size >= 2) {
    beginPinchGesture();
    return;
  }

  dragState.pointerId = event.pointerId;
  dragState.lastX = event.clientX;
  dragState.lastY = event.clientY;
  canvasShell.classList.add("is-dragging");
}

function onPointerMove(event) {
  if (!gestureState.activePointers.has(event.pointerId)) {
    return;
  }

  gestureState.activePointers.set(event.pointerId, {
    clientX: event.clientX,
    clientY: event.clientY,
  });

  if (gestureState.activePointers.size >= 2) {
    updatePinchGesture();
    return;
  }

  if (dragState.pointerId !== event.pointerId) {
    return;
  }

  const deltaX = event.clientX - dragState.lastX;
  const deltaY = event.clientY - dragState.lastY;
  dragState.lastX = event.clientX;
  dragState.lastY = event.clientY;
  viewportState.offsetX += deltaX;
  viewportState.offsetY += deltaY;
  viewportState.hasUserAdjusted = true;
  requestRender();
}

function stopDragging(event) {
  gestureState.activePointers.delete(event.pointerId);

  if (canvasShell.hasPointerCapture(event.pointerId)) {
    canvasShell.releasePointerCapture(event.pointerId);
  }

  if (gestureState.activePointers.size >= 2) {
    beginPinchGesture();
    return;
  }

  const remainingPointer = [...gestureState.activePointers.entries()][0];
  if (remainingPointer) {
    const [pointerId, pointer] = remainingPointer;
    dragState.pointerId = pointerId;
    dragState.lastX = pointer.clientX;
    dragState.lastY = pointer.clientY;
    canvasShell.classList.add("is-dragging");
    return;
  }

  dragState.pointerId = null;
  canvasShell.classList.remove("is-dragging");
}

generatorSelect.addEventListener("change", () => {
  selectedStoryIndex = 0;
  const nextOptions = applyEmbedOptionOverrides(generatorSelect.value, getDefaultOptions(generatorSelect.value));
  localStorage.setItem(
    storageKey,
    JSON.stringify({
      generatorId: generatorSelect.value,
      options: nextOptions,
      autoRegen: true, 
      // autoRegenCheckbox.checked,
      selectedStory: 0,
      gridEnabled,
      renderMode,
    }),
  );
  updateGeneratorTitle();
  buildInputs();
  onSettingsChanged(true);
});

// autoRegenCheckbox.addEventListener("change", () => {
//   saveState();

//   if (autoRegenCheckbox.checked) {
//     queueGenerate(false);
//     return;
//   }

//   window.clearTimeout(regenTimer);
//   setStatus("Auto regenerate disabled.");
// });

// generateButton.addEventListener("click", () => {
//   generate({ resetViewport: true }).catch(handleGenerationError);
// });

fitViewButton.addEventListener("click", () => {
  fitViewportToMap();
  requestRender();
});

fullscreenViewButton?.addEventListener("click", () => {
  toggleFullscreenView().catch(() => {
    window.open(buildFullscreenUrl(), "_blank", "noopener,noreferrer");
  });
});

toggleGridButton?.addEventListener("click", () => {
  gridEnabled = !gridEnabled;
  updateGridButtonState();
  saveState();
  requestRender();
});

renderFlatButton?.addEventListener("click", () => {
  renderMode = "flat";
  updateRenderModeUi();
  saveState();
  requestRender();
});

renderShaderButton?.addEventListener("click", () => {
  renderMode = "shader";
  const runtime = ensureShaderRuntime();
  if (!runtime) {
    renderMode = "flat";
  }
  updateRenderModeUi();
  saveState();
  requestRender();
});

resetButton.addEventListener("click", () => {
  selectedStoryIndex = 0;
  const nextOptions = applyEmbedOptionOverrides(generatorSelect.value, getDefaultOptions(generatorSelect.value));
  localStorage.setItem(
    storageKey,
    JSON.stringify({
      generatorId: generatorSelect.value,
      options: nextOptions,
      autoRegen: true,
      selectedStory: 0,
      gridEnabled,
      renderMode,
    }),
  );
  buildInputs();
  onSettingsChanged(true);
});

storySelect.addEventListener("change", () => {
  selectedStoryIndex = Number(storySelect.value) || 0;
  saveState();
  requestRender();
});

canvasShell.addEventListener("wheel", onWheel, { passive: false });
canvasShell.addEventListener("pointerdown", onPointerDown);
canvasShell.addEventListener("pointermove", onPointerMove);
canvasShell.addEventListener("pointerup", stopDragging);
canvasShell.addEventListener("pointercancel", stopDragging);

const resizeObserver = new ResizeObserver(() => {
  resizeCanvas();
});

if (syncGroup) {
  window.addEventListener(procGenSyncEventName, handleSynchronizedOptions);
}

async function initialize() {
  setStatus("Loading generators...");
  const runtimeBridge = await ensureRuntimeBridge();
  const catalog = await runtimeBridge.getGeneratorCatalog();
  generatorDefinitions = catalog.generators || [];
  generatorDefinitionById = Object.fromEntries(generatorDefinitions.map((definition) => [definition.id, definition]));

  generatorSelect.innerHTML = "";
  generatorDefinitions.forEach((definition) => {
    const option = document.createElement("option");
    option.value = definition.id;
    option.textContent = definition.displayName;
    generatorSelect.appendChild(option);
  });

  const initialState = loadState();
  const preferredGeneratorId = generatorDefinitionById[embedConfig.generatorId]
    ? embedConfig.generatorId
    : initialState.generatorId;
  generatorSelect.value = preferredGeneratorId || generatorDefinitions[0]?.id || "";
  generatorSelect.disabled = embedConfig.lockGenerator || embedConfig.hideGenerator;
  // autoRegenCheckbox.checked = initialState.autoRegen !== false;
  selectedStoryIndex = initialState.selectedStory || 0;
  gridEnabled = embedConfig.showGrid || initialState.gridEnabled === true;
  renderMode = normalizeRenderMode(embedConfig.renderMode || initialState.renderMode);
  fullscreenTarget = resolveFullscreenTarget();

  applyEmbedChrome();
  updateGeneratorTitle();
  buildInputs();
  seedSynchronizedOptions();
  resizeObserver.observe(canvasShell);
  resizeCanvas();
  updateGridButtonState();
  updateRenderModeUi();
  updateFullscreenButtonState();
  if (renderMode === "shader") {
    ensureShaderRuntime();
  }

  if (!generatorSelect.value) {
    setStatus("No generators registered.");
    setMap(null, true);
    return;
  }

  await generate({ resetViewport: true });
}

document.addEventListener("fullscreenchange", () => {
  updateFullscreenButtonState();
  fitViewportToMap();
  requestRender();
});

initialize().catch((error) => {
  setStatus(error instanceof Error ? error.message : "Initialization failed.");
  setMap(null, true);
});

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
    hideGridButton: parseBooleanParam(searchParams, "hideGrid"),
    hideRenderer: parseBooleanParam(searchParams, "hideRenderer"),
    lockGenerator: parseBooleanParam(searchParams, "lockGenerator"),
    showGrid: parseBooleanParam(searchParams, "grid") || parseBooleanParam(searchParams, "showGrid"),
    renderMode: searchParams.get("renderer") || searchParams.get("renderMode") || "",
    hiddenOptions: parseCsvSet(searchParams.get("hideOptions")),
    lockedOptions: parseCsvSet(searchParams.get("lockOptions")),
    optionOverrides,
  };
}

function getThemeColor(variableName, fallback) {
  const value = getComputedStyle(appRoot).getPropertyValue(variableName).trim();
  return value || fallback;
}

function normalizeRenderMode(value) {
  return String(value || "").toLowerCase() === "shader" ? "shader" : "flat";
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

  return applyEmbedOptionOverrides(
    generatorId,
    {
      ...getDefaultOptions(generatorId),
      ...savedOptions,
    },
  );
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
    const initialValue = normalizeOptionValue(
      option,
      Number(defaults[option.key] ?? option.defaultValue),
    );

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

      tooltip.append(trigger, bubble);
      titleWrap.append(tooltip, name);
    } else {
      titleWrap.appendChild(name);
    }

    const applyValue = (value) => {
      if (isLocked) {
        return;
      }

      const normalized = normalizeOptionValue(option, value);
      controls.querySelectorAll(".option-control").forEach((control) => {
        control.value = String(normalized);
      });
      if (isRotationOption(option)) {
        controls.querySelectorAll(".rotation-choice-button").forEach((button) => {
          const isSelected = Number(button.dataset.value) === normalized;
          button.classList.toggle("is-selected", isSelected);
          button.setAttribute("aria-pressed", isSelected ? "true" : "false");
        });
        refreshShapeChoiceButtons();
      }
      if (option.key === "shape") {
        controls.querySelectorAll(".shape-choice-button").forEach((button) => {
          const isSelected = Number(button.dataset.value) === normalized;
          button.classList.toggle("is-selected", isSelected);
          button.setAttribute("aria-pressed", isSelected ? "true" : "false");
        });
        refreshRotationChoiceButtons();
      }
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
      characterColor: gl.getUniformLocation(program, "u_character_color"),
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
  setChannel(layers?.doors || [], 1);
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

  storyPicker.hidden = embedConfig.hideStoryPicker;
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
  drawFlatCells(snapshotContext, layers?.walls || [], getThemeColor("--map-wall", "#d87b24"));
  drawFlatCells(snapshotContext, layers?.doors || [], getThemeColor("--map-door", "#9df18f"));
  drawFlatCells(snapshotContext, layers?.windows || [], getThemeColor("--map-window", "#84b8ff"));

  return snapshot;
}

function getRegionColor(kind) {
  switch ((kind || "").toLowerCase()) {
    case "corridor":
      return getThemeColor("--region-corridor", "#365c7b");
    case "outside":
      return getThemeColor("--region-outside", "#353e4d");
    case "accesscore":
    case "access-core":
      return getThemeColor("--region-access-core", "#5a4b78");
    case "elevator":
      return getThemeColor("--region-elevator", "#8a6e42");
    case "stair":
      return getThemeColor("--region-stair", "#7a5560");
    case "apartmentzone":
    case "apartment-zone":
      return getThemeColor("--region-apartment-zone", "#55604c");
    case "apartment":
      return getThemeColor("--region-apartment", "#49636f");
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

function renderRegionLegend() {
  if (!regionLegend) {
    return;
  }

  if (embedConfig.hideLegend) {
    regionLegend.hidden = true;
    regionLegend.innerHTML = "";
    return;
  }

  const regions = currentMap?.regions || [];
  if (!Array.isArray(regions) || regions.length === 0) {
    regionLegend.hidden = true;
    regionLegend.innerHTML = "";
    return;
  }

  const seen = new Set();
  const items = regions.filter((region) => {
    const key = `${region.kind}:${region.label}`;
    if (seen.has(key)) {
      return false;
    }

    seen.add(key);
    return true;
  });

  regionLegend.innerHTML = "";
  items.forEach((region) => {
    const item = document.createElement("div");
    const swatch = document.createElement("span");
    const label = document.createElement("span");

    item.className = "legend-item";
    swatch.className = "legend-swatch";
    swatch.style.background = getRegionColor(region.kind);
    label.className = "legend-label";
    label.textContent = region.label;

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

  flatCtx.strokeStyle = getThemeColor("--map-outline", "rgba(255, 255, 255, 0.08)");
  flatCtx.lineWidth = 1;
  flatCtx.strokeRect(
    viewportState.offsetX,
    viewportState.offsetY,
    currentMap.width * viewportState.scale,
    currentMap.height * viewportState.scale,
  );
  flatCtx.restore();

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
  gl.uniform3fv(runtime.uniforms.backdropBottom, parseColorToRgb(getThemeColor("--surface-strong", "#252c36"), [0.15, 0.17, 0.21]));
  gl.uniform3fv(runtime.uniforms.floorLight, parseColorToRgb(getThemeColor("--region-corridor", "#35556d"), [0.21, 0.33, 0.43]));
  gl.uniform3fv(runtime.uniforms.floorShadow, parseColorToRgb(getThemeColor("--map-floor", "#2f3643"), [0.18, 0.21, 0.26]));
  gl.uniform3fv(runtime.uniforms.wallLight, parseColorToRgb(getThemeColor("--map-wall", "#d87b24"), [0.84, 0.48, 0.14]));
  gl.uniform3fv(runtime.uniforms.wallShadow, parseColorToRgb("#6f3d10", [0.44, 0.24, 0.06]));
  gl.uniform3fv(runtime.uniforms.outlineColor, [1, 1, 1]);
  gl.uniform3fv(runtime.uniforms.propColor, parseColorToRgb(getThemeColor("--map-door", "#9df18f"), [0.62, 0.95, 0.56]));
  gl.uniform3fv(runtime.uniforms.itemColor, parseColorToRgb(getThemeColor("--map-window", "#84b8ff"), [0.52, 0.72, 1]));
  gl.uniform3fv(runtime.uniforms.characterColor, [0.93, 0.33, 0.47]);
  gl.drawArrays(gl.TRIANGLES, 0, 6);

  if (gridEnabled || currentMap) {
    flatCtx.setTransform(dpr, 0, 0, dpr, 0, 0);
    flatCtx.clearRect(0, 0, width, height);
    if (gridEnabled) {
      drawGrid(flatCtx);
    }
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

function onPointerDown(event) {
  if (!currentMap) {
    return;
  }

  if (event.target instanceof Element && event.target.closest(".canvas-overlay")) {
    return;
  }

  dragState.pointerId = event.pointerId;
  dragState.lastX = event.clientX;
  dragState.lastY = event.clientY;
  canvasShell.classList.add("is-dragging");
  canvasShell.setPointerCapture(event.pointerId);
}

function onPointerMove(event) {
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
  if (dragState.pointerId !== event.pointerId) {
    return;
  }

  dragState.pointerId = null;
  canvasShell.classList.remove("is-dragging");

  if (canvasShell.hasPointerCapture(event.pointerId)) {
    canvasShell.releasePointerCapture(event.pointerId);
  }
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

  applyEmbedChrome();
  updateGeneratorTitle();
  buildInputs();
  resizeObserver.observe(canvasShell);
  resizeCanvas();
  updateGridButtonState();
  updateRenderModeUi();
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

initialize().catch((error) => {
  setStatus(error instanceof Error ? error.message : "Initialization failed.");
  setMap(null, true);
});

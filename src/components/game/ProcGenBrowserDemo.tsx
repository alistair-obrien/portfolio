import {
  startTransition,
  useDeferredValue,
  useEffect,
  useRef,
  useState,
  type PointerEvent,
} from "react";
import { createPortal } from "react-dom";

type GeneratorId = "apartment" | "apartment-floor" | "apartment-building";

type ControlDefinition = {
  key: string;
  label: string;
  min: number;
  max: number;
  step: number;
  description: string;
};

type GeneratorDefinition = {
  id: GeneratorId;
  label: string;
  eyebrow: string;
  description: string;
  controls: ControlDefinition[];
  defaults: Record<string, number>;
};

type EngineLayers = {
  walls?: number[] | null;
  props?: number[] | null;
  items?: number[] | null;
  characters?: number[] | null;
};

type EngineMap = {
  width?: number;
  height?: number;
  layers?: EngineLayers | null;
};

type EngineEnvelope = {
  ok?: boolean;
  errorMessage?: string | null;
  map?: EngineMap | null;
};

type ViewportSize = {
  width: number;
  height: number;
};

type CameraState = {
  centerX: number;
  centerY: number;
  zoom: number;
};

type MapTexture = {
  imageData: ImageData;
  width: number;
  height: number;
};

type ShaderRenderer = {
  gl: WebGL2RenderingContext;
  program: WebGLProgram;
  vao: WebGLVertexArrayObject;
  maskTexture: WebGLTexture;
  noiseTexture: WebGLTexture;
  uniforms: {
    mask: WebGLUniformLocation;
    noise: WebGLUniformLocation;
    maskSize: WebGLUniformLocation;
    viewportSize: WebGLUniformLocation;
    cameraCenter: WebGLUniformLocation;
    scale: WebGLUniformLocation;
    backdropTop: WebGLUniformLocation;
    backdropBottom: WebGLUniformLocation;
    floorLight: WebGLUniformLocation;
    floorShadow: WebGLUniformLocation;
    wallLight: WebGLUniformLocation;
    wallShadow: WebGLUniformLocation;
    outlineColor: WebGLUniformLocation;
    propColor: WebGLUniformLocation;
    itemColor: WebGLUniformLocation;
    characterColor: WebGLUniformLocation;
  };
};

type ThemePalette = {
  backdropTop: [number, number, number];
  backdropBottom: [number, number, number];
  floorLight: [number, number, number];
  floorShadow: [number, number, number];
  wallLight: [number, number, number];
  wallShadow: [number, number, number];
  outlineColor: [number, number, number];
  propColor: [number, number, number];
  itemColor: [number, number, number];
  characterColor: [number, number, number];
};

type ProcGenEngineModule = {
  generateMap(request: {
    generatorId: GeneratorId;
    options: Record<string, number>;
  }): Promise<EngineEnvelope>;
};

const GENERATOR_DEFINITIONS: GeneratorDefinition[] = [
  {
    id: "apartment-building",
    label: "Apartment Building",
    eyebrow: "Showcase",
    description:
      "A courtyard-style residential block with corridors, subdivisions, and apartment interiors.",
    defaults: {
      width: 120,
      height: 120,
      corridorWidth: 8,
      minApartmentWidth: 20,
      maxApartmentWidth: 64,
      minApartmentDepth: 15,
      maxApartmentDepth: 64,
      doorWidth: 3,
    },
    controls: [
      {
        key: "width",
        label: "Width",
        min: 96,
        max: 384,
        step: 8,
        description: "Overall building footprint width in cells.",
      },
      {
        key: "height",
        label: "Height",
        min: 96,
        max: 384,
        step: 8,
        description: "Overall building footprint height in cells.",
      },
      {
        key: "corridorWidth",
        label: "Corridor Width",
        min: 8,
        max: 48,
        step: 2,
        description: "Thickness of the circulation ring around the inner apartments.",
      },
      {
        key: "minApartmentWidth",
        label: "Min Apartment Width",
        min: 6,
        max: 48,
        step: 1,
        description: "Smallest allowed width when subdividing apartment rows.",
      },
      {
        key: "maxApartmentWidth",
        label: "Max Apartment Width",
        min: 6,
        max: 64,
        step: 1,
        description: "Largest allowed width when subdividing apartment rows.",
      },
      {
        key: "minApartmentDepth",
        label: "Min Apartment Depth",
        min: 6,
        max: 48,
        step: 1,
        description: "Smallest allowed apartment depth.",
      },
      {
        key: "maxApartmentDepth",
        label: "Max Apartment Depth",
        min: 6,
        max: 64,
        step: 1,
        description: "Largest allowed apartment depth.",
      },
      {
        key: "doorWidth",
        label: "Door Width",
        min: 1,
        max: 6,
        step: 1,
        description: "Width of the cutouts opened into corridor-facing walls.",
      },
    ],
  },
  {
    id: "apartment-floor",
    label: "Apartment Floor",
    eyebrow: "Midsize",
    description:
      "A faster floorplate-level generator that shows corridor logic without the full building envelope.",
    defaults: {
      width: 34,
      height: 18,
    },
    controls: [
      {
        key: "width",
        label: "Width",
        min: 8,
        max: 256,
        step: 2,
        description: "Floor width in cells.",
      },
      {
        key: "height",
        label: "Height",
        min: 8,
        max: 256,
        step: 2,
        description: "Floor height in cells.",
      },
    ],
  },
  {
    id: "apartment",
    label: "Apartment",
    eyebrow: "Focused",
    description:
      "A compact apartment layout showing the smallest unit-level rules in the family.",
    defaults: {
      width: 18,
      height: 12,
    },
    controls: [
      {
        key: "width",
        label: "Width",
        min: 6,
        max: 128,
        step: 1,
        description: "Apartment width in cells.",
      },
      {
        key: "height",
        label: "Height",
        min: 6,
        max: 128,
        step: 1,
        description: "Apartment height in cells.",
      },
    ],
  },
];

const GENERATOR_BY_ID = Object.fromEntries(
  GENERATOR_DEFINITIONS.map((definition) => [definition.id, definition]),
) as Record<GeneratorId, GeneratorDefinition>;

const MIN_CAMERA_ZOOM = 0.2;
const MAX_CAMERA_ZOOM = 12;

const DEFAULT_THEME_PALETTE: ThemePalette = {
  backdropTop: [0.17, 0.19, 0.23],
  backdropBottom: [0.11, 0.12, 0.16],
  floorLight: [0.27, 0.29, 0.33],
  floorShadow: [0.15, 0.16, 0.2],
  wallLight: [0.87, 0.48, 0.11],
  wallShadow: [0.62, 0.28, 0.09],
  outlineColor: [0.56, 0.39, 0.25],
  propColor: [0.31, 0.31, 0.33],
  itemColor: [0.14, 0.41, 0.61],
  characterColor: [0.82, 0.41, 0.56],
};

const THEME_PALETTES: Record<string, ThemePalette> = {
  caramellatte: {
    backdropTop: [0.96, 0.93, 0.89],
    backdropBottom: [0.9, 0.84, 0.76],
    floorLight: [0.95, 0.91, 0.86],
    floorShadow: [0.86, 0.79, 0.71],
    wallLight: [0.72, 0.38, 0.18],
    wallShadow: [0.54, 0.27, 0.12],
    outlineColor: [0.42, 0.23, 0.12],
    propColor: [0.49, 0.42, 0.33],
    itemColor: [0.22, 0.46, 0.58],
    characterColor: [0.63, 0.33, 0.4],
  },
  dim: {
    backdropTop: [0.19, 0.22, 0.27],
    backdropBottom: [0.12, 0.14, 0.18],
    floorLight: [0.26, 0.3, 0.36],
    floorShadow: [0.17, 0.19, 0.24],
    wallLight: [0.45, 0.76, 0.86],
    wallShadow: [0.23, 0.48, 0.58],
    outlineColor: [0.14, 0.2, 0.25],
    propColor: [0.48, 0.58, 0.63],
    itemColor: [0.86, 0.72, 0.42],
    characterColor: [0.81, 0.48, 0.63],
  },
  dark: {
    backdropTop: [0.14, 0.16, 0.19],
    backdropBottom: [0.08, 0.09, 0.11],
    floorLight: [0.21, 0.24, 0.28],
    floorShadow: [0.12, 0.13, 0.16],
    wallLight: [0.39, 0.8, 0.86],
    wallShadow: [0.18, 0.42, 0.48],
    outlineColor: [0.08, 0.12, 0.15],
    propColor: [0.42, 0.47, 0.51],
    itemColor: [0.88, 0.72, 0.34],
    characterColor: [0.86, 0.37, 0.55],
  },
  coffee: {
    backdropTop: [0.25, 0.19, 0.16],
    backdropBottom: [0.14, 0.1, 0.08],
    floorLight: [0.34, 0.27, 0.23],
    floorShadow: [0.21, 0.16, 0.13],
    wallLight: [0.85, 0.63, 0.39],
    wallShadow: [0.56, 0.36, 0.2],
    outlineColor: [0.18, 0.12, 0.09],
    propColor: [0.5, 0.42, 0.35],
    itemColor: [0.26, 0.58, 0.66],
    characterColor: [0.79, 0.42, 0.35],
  },
};

const SHADER_VERTEX_SOURCE = `#version 300 es
in vec2 a_position;
out vec2 v_uv;

void main() {
  gl_Position = vec4(a_position, 0.0, 1.0);
  v_uv = vec2(a_position.x * 0.5 + 0.5, 1.0 - (a_position.y * 0.5 + 0.5));
}
`;

const SHADER_FRAGMENT_SOURCE = `#version 300 es
precision highp float;
precision highp int;

in vec2 v_uv;
out vec4 outColor;

uniform sampler2D u_mask;
uniform sampler2D u_noise;
uniform vec2 u_mask_size;
uniform vec2 u_viewport_size;
uniform vec2 u_camera_center;
uniform float u_scale;
uniform vec3 u_backdrop_top;
uniform vec3 u_backdrop_bottom;
uniform vec3 u_floor_light;
uniform vec3 u_floor_shadow;
uniform vec3 u_wall_light;
uniform vec3 u_wall_shadow;
uniform vec3 u_outline_color;
uniform vec3 u_prop_color;
uniform vec3 u_item_color;
uniform vec3 u_character_color;

const vec4 INNER_OUTLINE_COLOR = vec4(1.0, 1.0, 1.0, 1.0);
const vec2 GRADIENT_DIRECTION = vec2(-0.8, -0.6);
const float FLOOR_GRADIENT_STRENGTH = 0.85;
const float WALL_GRADIENT_STRENGTH = 1.0;
const float DETAIL_GRADIENT_STRENGTH = 1.0;

const float PROP_SHADOW_STRENGTH = 0.5;
const vec2 PROP_SHADOW_DIRECTION = vec2(1.0, 1.0);
const float PROP_SHADOW_FALLOFF = 2.89;
const int PROP_SHADOW_SAMPLES = 4;

const float PROP_AO_STRENGTH = 0.25;
const float PROP_AO_EDGE_REACH = 0.5;
const float PROP_AO_FALLOFF = 1.0;

const float SHADOW_STRENGTH = 1.5;
const vec2 SHADOW_DIRECTION = vec2(5.0, 5.0);
const float SHADOW_FALLOFF = 2.89;
const int SHADOW_SAMPLES = 16;

const float AO_STRENGTH = 0.25;
const float AO_EDGE_REACH = 0.75;
const float AO_FALLOFF = 1.0;

const float OUTLINE_PIXELS = 2.0;
const float OUTLINE_FALLOFF = 0.5;

const vec4 PROP_OUTLINE_COLOR = vec4(0.7529412, 0.4862745, 0.28235295, 1.0);
const float PROP_OUTLINE_PIXELS = 2.0;
const float PROP_OUTLINE_FALLOFF = 0.0;

const vec4 ITEM_OUTLINE_COLOR = vec4(0.0, 0.0, 0.0, 0.7019608);
const float ITEM_OUTLINE_PIXELS = 2.0;
const float ITEM_OUTLINE_FALLOFF = 1.0;

const float FLOOR_NOISE_STRENGTH = 0.18;
const vec3 FLOOR_NOISE_COLOR = vec3(0.0);

float sampleWallMask(vec2 uv) {
  return texture(u_mask, uv).r;
}

float samplePropMask(vec2 uv) {
  return texture(u_mask, uv).g;
}

float sampleItemMask(vec2 uv) {
  return texture(u_mask, uv).b;
}

float sampleCharacterMask(vec2 uv) {
  return texture(u_mask, uv).a;
}

float sampleChannelLinear(vec2 uv, int channelIndex) {
  vec2 texelPos = uv * u_mask_size - 0.5;
  vec2 base = floor(texelPos);
  vec2 frac = fract(texelPos);
  vec2 minTexel = vec2(0.0);
  vec2 maxTexel = u_mask_size - 1.0;

  vec2 p00 = clamp(base, minTexel, maxTexel);
  vec2 p10 = clamp(base + vec2(1.0, 0.0), minTexel, maxTexel);
  vec2 p01 = clamp(base + vec2(0.0, 1.0), minTexel, maxTexel);
  vec2 p11 = clamp(base + vec2(1.0, 1.0), minTexel, maxTexel);

  vec4 s00 = texture(u_mask, (p00 + 0.5) / u_mask_size);
  vec4 s10 = texture(u_mask, (p10 + 0.5) / u_mask_size);
  vec4 s01 = texture(u_mask, (p01 + 0.5) / u_mask_size);
  vec4 s11 = texture(u_mask, (p11 + 0.5) / u_mask_size);

  float c00 = channelIndex == 0 ? s00.r : channelIndex == 1 ? s00.g : channelIndex == 2 ? s00.b : s00.a;
  float c10 = channelIndex == 0 ? s10.r : channelIndex == 1 ? s10.g : channelIndex == 2 ? s10.b : s10.a;
  float c01 = channelIndex == 0 ? s01.r : channelIndex == 1 ? s01.g : channelIndex == 2 ? s01.b : s01.a;
  float c11 = channelIndex == 0 ? s11.r : channelIndex == 1 ? s11.g : channelIndex == 2 ? s11.b : s11.a;

  float sx0 = mix(c00, c10, frac.x);
  float sx1 = mix(c01, c11, frac.x);
  return mix(sx0, sx1, frac.y);
}

float edgeAo(float present, float dist, float reach, float falloff) {
  if (present < 0.5) return 0.0;
  return pow(max(0.0, 1.0 - dist / reach), falloff);
}

vec2 safeNormalize(vec2 value) {
  float len = length(value);
  if (len <= 0.0001) return vec2(0.0, -1.0);
  return value / len;
}

float directionalGradient(vec2 uv, vec2 direction) {
  vec2 n = safeNormalize(direction);
  float t = dot(uv - 0.5, n) * 1.41421356 + 0.5;
  return clamp(t, 0.0, 1.0);
}

float cornerAo(float present, vec2 distFromCorner, float reach, float falloff) {
  if (present < 0.5) return 0.0;
  float dist = max(distFromCorner.x, distFromCorner.y);
  return pow(max(0.0, 1.0 - dist / reach), falloff);
}

vec3 shadeDetail(vec3 baseColor, float grad, float strength) {
  vec3 shadowColor = baseColor * (1.0 - 0.35 * strength);
  vec3 lightColor = min(baseColor * (1.0 + 0.18 * strength), vec3(1.0));
  return mix(shadowColor, lightColor, grad);
}

float sampleMaskChannel(vec2 uv, int channelIndex) {
  vec4 sampleValue = texture(u_mask, uv);
  return channelIndex == 0 ? sampleValue.r : channelIndex == 1 ? sampleValue.g : channelIndex == 2 ? sampleValue.b : sampleValue.a;
}

float computeInnerOutline(vec2 uv, vec2 texel, vec2 tileUv, float reach, float falloff, int channelIndex) {
  float floorL = 1.0 - sampleMaskChannel(uv + texel * vec2(-1.0, 0.0), channelIndex);
  float floorR = 1.0 - sampleMaskChannel(uv + texel * vec2(1.0, 0.0), channelIndex);
  float floorT = 1.0 - sampleMaskChannel(uv + texel * vec2(0.0, -1.0), channelIndex);
  float floorB = 1.0 - sampleMaskChannel(uv + texel * vec2(0.0, 1.0), channelIndex);
  float floorNW = 1.0 - sampleMaskChannel(uv + texel * vec2(-1.0, -1.0), channelIndex);
  float floorNE = 1.0 - sampleMaskChannel(uv + texel * vec2(1.0, -1.0), channelIndex);
  float floorSW = 1.0 - sampleMaskChannel(uv + texel * vec2(-1.0, 1.0), channelIndex);
  float floorSE = 1.0 - sampleMaskChannel(uv + texel * vec2(1.0, 1.0), channelIndex);

  float outlineL = edgeAo(floorL, tileUv.x, reach, falloff);
  float outlineR = edgeAo(floorR, 1.0 - tileUv.x, reach, falloff);
  float outlineT = edgeAo(floorT, tileUv.y, reach, falloff);
  float outlineB = edgeAo(floorB, 1.0 - tileUv.y, reach, falloff);
  float outlineCardinal = max(max(outlineL, outlineR), max(outlineT, outlineB));

  float diagNW = floorNW * (1.0 - max(floorL, floorT));
  float diagNE = floorNE * (1.0 - max(floorR, floorT));
  float diagSW = floorSW * (1.0 - max(floorL, floorB));
  float diagSE = floorSE * (1.0 - max(floorR, floorB));

  float outlineNW = cornerAo(diagNW, vec2(tileUv.x, tileUv.y), reach, falloff);
  float outlineNE = cornerAo(diagNE, vec2(1.0 - tileUv.x, tileUv.y), reach, falloff);
  float outlineSW = cornerAo(diagSW, vec2(tileUv.x, 1.0 - tileUv.y), reach, falloff);
  float outlineSE = cornerAo(diagSE, vec2(1.0 - tileUv.x, 1.0 - tileUv.y), reach, falloff);
  float outlineCorner = max(max(outlineNW, outlineNE), max(outlineSW, outlineSE));

  return max(outlineCardinal, outlineCorner);
}

void main() {
  vec2 screenPoint = v_uv * u_viewport_size;
  vec2 mapPoint = u_camera_center + (screenPoint - 0.5 * u_viewport_size) / max(u_scale, 0.0001);
  vec2 uv = mapPoint / u_mask_size;
  vec3 backdrop = mix(u_backdrop_top, u_backdrop_bottom, clamp(v_uv.y * 0.9 + v_uv.x * 0.15, 0.0, 1.0));

  if (uv.x < 0.0 || uv.y < 0.0 || uv.x > 1.0 || uv.y > 1.0) {
    outColor = vec4(backdrop, 1.0);
    return;
  }

  vec2 texel = 1.0 / u_mask_size;
  vec2 tileUv = fract(mapPoint);
  float center = sampleWallMask(uv);
  float propMask = samplePropMask(uv);
  float itemMask = sampleItemMask(uv);
  float characterMask = sampleCharacterMask(uv);
  float sceneGrad = directionalGradient(uv, GRADIENT_DIRECTION);
  float openFloor = (1.0 - center) * (1.0 - propMask) * (1.0 - itemMask) * (1.0 - characterMask);

  float shadowRay = 0.0;
  float shadowNorm = 0.0;
  vec2 shadowStep = texel * SHADOW_DIRECTION / float(max(SHADOW_SAMPLES, 1));
  vec2 shadowPerp = safeNormalize(vec2(-SHADOW_DIRECTION.y, SHADOW_DIRECTION.x)) * texel;

  for (int i = 1; i <= 16; i++) {
    if (i > SHADOW_SAMPLES) break;
    float fi = float(i) / float(max(SHADOW_SAMPLES, 1));
    float weight = exp(-SHADOW_FALLOFF * fi);
    vec2 sampleUv = uv - shadowStep * float(i);
    vec2 spread = shadowPerp * (0.18 + 0.30 * fi);

    float centerTap = sampleChannelLinear(sampleUv, 0);
    float sidePos = sampleChannelLinear(sampleUv + spread, 0);
    float sideNeg = sampleChannelLinear(sampleUv - spread, 0);
    float sideGate = smoothstep(0.25, 0.75, centerTap);
    float ribbon = centerTap * 0.85 + (sidePos + sideNeg) * (0.075 * sideGate);

    shadowRay += ribbon * weight;
    shadowNorm += weight;
  }

  float shadow = clamp(shadowRay / max(shadowNorm, 0.001), 0.0, 1.0);

  float propShadow = 0.0;
  if (openFloor > 0.5) {
    float propShadowRay = 0.0;
    float propShadowNorm = 0.0;
    vec2 propShadowStep = texel * PROP_SHADOW_DIRECTION / float(max(PROP_SHADOW_SAMPLES, 1));
    vec2 propShadowPerp = safeNormalize(vec2(-PROP_SHADOW_DIRECTION.y, PROP_SHADOW_DIRECTION.x)) * texel;

    for (int i = 1; i <= 8; i++) {
      if (i > PROP_SHADOW_SAMPLES) break;
      float fi = float(i) / float(max(PROP_SHADOW_SAMPLES, 1));
      float weight = exp(-PROP_SHADOW_FALLOFF * fi);
      vec2 sampleUv = uv - propShadowStep * float(i);
      vec2 spread = propShadowPerp * (0.10 + 0.22 * fi);

      float centerTap = sampleChannelLinear(sampleUv, 1);
      float sidePos = sampleChannelLinear(sampleUv + spread, 1);
      float sideNeg = sampleChannelLinear(sampleUv - spread, 1);
      float sideGate = smoothstep(0.15, 0.65, centerTap);
      float ribbon = centerTap * 0.88 + (sidePos + sideNeg) * (0.06 * sideGate);

      propShadowRay += ribbon * weight;
      propShadowNorm += weight;
    }

    propShadow = clamp(propShadowRay / max(propShadowNorm, 0.001), 0.0, 1.0);
  }

  float ao = 0.0;
  if (center < 0.5) {
    float wallL = sampleWallMask(uv + texel * vec2(-1.0, 0.0));
    float wallR = sampleWallMask(uv + texel * vec2(1.0, 0.0));
    float wallT = sampleWallMask(uv + texel * vec2(0.0, -1.0));
    float wallB = sampleWallMask(uv + texel * vec2(0.0, 1.0));
    float wallNW = sampleWallMask(uv + texel * vec2(-1.0, -1.0));
    float wallNE = sampleWallMask(uv + texel * vec2(1.0, -1.0));
    float wallSW = sampleWallMask(uv + texel * vec2(-1.0, 1.0));
    float wallSE = sampleWallMask(uv + texel * vec2(1.0, 1.0));

    float aoL = edgeAo(wallL, tileUv.x, AO_EDGE_REACH, AO_FALLOFF);
    float aoR = edgeAo(wallR, 1.0 - tileUv.x, AO_EDGE_REACH, AO_FALLOFF);
    float aoT = edgeAo(wallT, tileUv.y, AO_EDGE_REACH, AO_FALLOFF);
    float aoB = edgeAo(wallB, 1.0 - tileUv.y, AO_EDGE_REACH, AO_FALLOFF);
    float aoCardinal = max(max(aoL, aoR), max(aoT, aoB));

    float diagNW = wallNW * (1.0 - max(wallL, wallT));
    float diagNE = wallNE * (1.0 - max(wallR, wallT));
    float diagSW = wallSW * (1.0 - max(wallL, wallB));
    float diagSE = wallSE * (1.0 - max(wallR, wallB));

    float aoNW = cornerAo(diagNW, vec2(tileUv.x, tileUv.y), AO_EDGE_REACH, AO_FALLOFF);
    float aoNE = cornerAo(diagNE, vec2(1.0 - tileUv.x, tileUv.y), AO_EDGE_REACH, AO_FALLOFF);
    float aoSW = cornerAo(diagSW, vec2(tileUv.x, 1.0 - tileUv.y), AO_EDGE_REACH, AO_FALLOFF);
    float aoSE = cornerAo(diagSE, vec2(1.0 - tileUv.x, 1.0 - tileUv.y), AO_EDGE_REACH, AO_FALLOFF);
    float aoCorner = max(max(aoNW, aoNE), max(aoSW, aoSE));

    ao = max(aoCardinal, aoCorner);
  }

  float propAo = 0.0;
  if (openFloor > 0.5) {
    float propL = samplePropMask(uv + texel * vec2(-1.0, 0.0));
    float propR = samplePropMask(uv + texel * vec2(1.0, 0.0));
    float propT = samplePropMask(uv + texel * vec2(0.0, -1.0));
    float propB = samplePropMask(uv + texel * vec2(0.0, 1.0));
    float propNW = samplePropMask(uv + texel * vec2(-1.0, -1.0));
    float propNE = samplePropMask(uv + texel * vec2(1.0, -1.0));
    float propSW = samplePropMask(uv + texel * vec2(-1.0, 1.0));
    float propSE = samplePropMask(uv + texel * vec2(1.0, 1.0));

    float propAoL = edgeAo(propL, tileUv.x, PROP_AO_EDGE_REACH, PROP_AO_FALLOFF);
    float propAoR = edgeAo(propR, 1.0 - tileUv.x, PROP_AO_EDGE_REACH, PROP_AO_FALLOFF);
    float propAoT = edgeAo(propT, tileUv.y, PROP_AO_EDGE_REACH, PROP_AO_FALLOFF);
    float propAoB = edgeAo(propB, 1.0 - tileUv.y, PROP_AO_EDGE_REACH, PROP_AO_FALLOFF);
    float propAoCardinal = max(max(propAoL, propAoR), max(propAoT, propAoB));

    float propDiagNW = propNW * (1.0 - max(propL, propT));
    float propDiagNE = propNE * (1.0 - max(propR, propT));
    float propDiagSW = propSW * (1.0 - max(propL, propB));
    float propDiagSE = propSE * (1.0 - max(propR, propB));

    float propAoNW = cornerAo(propDiagNW, vec2(tileUv.x, tileUv.y), PROP_AO_EDGE_REACH, PROP_AO_FALLOFF);
    float propAoNE = cornerAo(propDiagNE, vec2(1.0 - tileUv.x, tileUv.y), PROP_AO_EDGE_REACH, PROP_AO_FALLOFF);
    float propAoSW = cornerAo(propDiagSW, vec2(tileUv.x, 1.0 - tileUv.y), PROP_AO_EDGE_REACH, PROP_AO_FALLOFF);
    float propAoSE = cornerAo(propDiagSE, vec2(1.0 - tileUv.x, 1.0 - tileUv.y), PROP_AO_EDGE_REACH, PROP_AO_FALLOFF);
    float propAoCorner = max(max(propAoNW, propAoNE), max(propAoSW, propAoSE));

    propAo = max(propAoCardinal, propAoCorner);
  }

  float pxToReach = 1.0 / max(u_scale, 0.0001);
  float innerOutline = center > 0.5 ? computeInnerOutline(uv, texel, tileUv, OUTLINE_PIXELS * pxToReach, OUTLINE_FALLOFF, 0) : 0.0;
  float propInnerOutline = propMask > 0.5 ? computeInnerOutline(uv, texel, tileUv, PROP_OUTLINE_PIXELS * pxToReach, PROP_OUTLINE_FALLOFF, 1) : 0.0;
  float itemInnerOutline = itemMask > 0.5 ? computeInnerOutline(uv, texel, tileUv, ITEM_OUTLINE_PIXELS * pxToReach, ITEM_OUTLINE_FALLOFF, 2) : 0.0;

  float wallShadowTerm = shadow * SHADOW_STRENGTH;
  float wallAoTerm = ao * AO_STRENGTH;
  float wallOcclusion = clamp(wallShadowTerm + wallAoTerm - (wallShadowTerm * wallAoTerm), 0.0, 1.0);
  float propShadowTerm = propShadow * PROP_SHADOW_STRENGTH;
  float propAoTerm = propAo * PROP_AO_STRENGTH;

  vec3 floorGradColor = mix(u_floor_shadow, u_floor_light, sceneGrad);
  vec3 floorColor = mix(u_floor_shadow, floorGradColor, FLOOR_GRADIENT_STRENGTH);
  floorColor *= 1.0 - propShadowTerm;
  floorColor *= 1.0 - propAoTerm;

  float noiseValue = texture(u_noise, fract(uv * vec2(10.0, 10.0))).r;
  floorColor = mix(floorColor, FLOOR_NOISE_COLOR, noiseValue * FLOOR_NOISE_STRENGTH);

  vec3 wallGradColor = mix(u_wall_shadow, u_wall_light, sceneGrad);
  vec3 wallColor = mix(u_wall_shadow, wallGradColor, WALL_GRADIENT_STRENGTH);
  wallColor = mix(wallColor, u_outline_color, innerOutline * INNER_OUTLINE_COLOR.a);

  vec3 propColor = shadeDetail(u_prop_color, sceneGrad, DETAIL_GRADIENT_STRENGTH);
  propColor = mix(propColor, u_outline_color, propInnerOutline * PROP_OUTLINE_COLOR.a);

  vec3 itemColor = shadeDetail(u_item_color, sceneGrad, DETAIL_GRADIENT_STRENGTH);
  itemColor = mix(itemColor, ITEM_OUTLINE_COLOR.rgb, itemInnerOutline * ITEM_OUTLINE_COLOR.a);

  vec3 characterColor = shadeDetail(u_character_color, sceneGrad, DETAIL_GRADIENT_STRENGTH);

  vec3 base = mix(floorColor, wallColor, center);
  base = mix(base, propColor, propMask);
  base = mix(base, itemColor, itemMask);
  base = mix(base, characterColor, characterMask);
  base *= 1.0 - wallOcclusion * (1.0 - center);

  outColor = vec4(base, 1.0);
}
`;

function loadEngineModule() {
  const moduleUrl = "/widgets/proc-gen/engine-loader.js";
  const runtimeImport = new Function(
    "path",
    "return import(path);",
  ) as (path: string) => Promise<unknown>;

  return runtimeImport(moduleUrl) as Promise<ProcGenEngineModule>;
}

function createNoiseTextureData(size: number) {
  const data = new Uint8Array(size * size * 4);

  for (let index = 0; index < size * size; index += 1) {
    const x = index % size;
    const y = Math.floor(index / size);
    const seed = Math.sin(x * 12.9898 + y * 78.233) * 43758.5453;
    const value = Math.floor((seed - Math.floor(seed)) * 255);
    const offset = index * 4;
    data[offset] = value;
    data[offset + 1] = value;
    data[offset + 2] = value;
    data[offset + 3] = 255;
  }

  return data;
}

function createShader(gl: WebGL2RenderingContext, type: number, source: string) {
  const shader = gl.createShader(type);
  if (!shader) {
    return null;
  }

  gl.shaderSource(shader, source);
  gl.compileShader(shader);

  if (gl.getShaderParameter(shader, gl.COMPILE_STATUS)) {
    return shader;
  }

  console.error(gl.getShaderInfoLog(shader));
  gl.deleteShader(shader);
  return null;
}

function createProgram(gl: WebGL2RenderingContext, vertexSource: string, fragmentSource: string) {
  const vertexShader = createShader(gl, gl.VERTEX_SHADER, vertexSource);
  const fragmentShader = createShader(gl, gl.FRAGMENT_SHADER, fragmentSource);

  if (!vertexShader || !fragmentShader) {
    if (vertexShader) {
      gl.deleteShader(vertexShader);
    }
    if (fragmentShader) {
      gl.deleteShader(fragmentShader);
    }
    return null;
  }

  const program = gl.createProgram();
  if (!program) {
    gl.deleteShader(vertexShader);
    gl.deleteShader(fragmentShader);
    return null;
  }

  gl.attachShader(program, vertexShader);
  gl.attachShader(program, fragmentShader);
  gl.linkProgram(program);

  gl.deleteShader(vertexShader);
  gl.deleteShader(fragmentShader);

  if (gl.getProgramParameter(program, gl.LINK_STATUS)) {
    return program;
  }

  console.error(gl.getProgramInfoLog(program));
  gl.deleteProgram(program);
  return null;
}

function createTexture(gl: WebGL2RenderingContext) {
  const texture = gl.createTexture();
  if (!texture) {
    return null;
  }

  gl.bindTexture(gl.TEXTURE_2D, texture);
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.NEAREST);
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.NEAREST);
  gl.bindTexture(gl.TEXTURE_2D, null);
  return texture;
}

function createRepeatTexture(gl: WebGL2RenderingContext) {
  const texture = gl.createTexture();
  if (!texture) {
    return null;
  }

  gl.bindTexture(gl.TEXTURE_2D, texture);
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.REPEAT);
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.REPEAT);
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
  gl.bindTexture(gl.TEXTURE_2D, null);
  return texture;
}

function clamp(value: number, min: number, max: number) {
  return Math.min(max, Math.max(min, value));
}

function normalizeThemeName(rawThemeName: string | null | undefined) {
  const themeName = rawThemeName?.trim().toLowerCase();

  if (themeName === "light") {
    return "caramellatte";
  }

  if (themeName && themeName in THEME_PALETTES) {
    return themeName as keyof typeof THEME_PALETTES;
  }

  if (typeof window !== "undefined" && window.matchMedia("(prefers-color-scheme: dark)").matches) {
    return "dim";
  }

  return "caramellatte";
}

function readThemePalette(): ThemePalette {
  if (typeof document === "undefined") {
    return DEFAULT_THEME_PALETTE;
  }

  const themeName = normalizeThemeName(
    document.documentElement.getAttribute("data-theme"),
  );

  if (themeName === "caramellatte") {
    return {
      ...DEFAULT_THEME_PALETTE,
      floorLight: THEME_PALETTES.caramellatte.floorLight,
      floorShadow: THEME_PALETTES.caramellatte.floorShadow,
    };
  }

  return DEFAULT_THEME_PALETTE;
}

function parseNumericInput(rawValue: string, fallback: number) {
  const parsed = Number(rawValue);
  return Number.isFinite(parsed) ? parsed : fallback;
}

function alignToStep(value: number, min: number, step: number) {
  if (step <= 1) {
    return value;
  }

  if (value <= min) {
    return min;
  }

  const steps = Math.ceil((value - min) / step);
  return min + steps * step;
}

function getMinimumBuildingDimension(options: Record<string, number>) {
  return options.corridorWidth * 2 + options.minApartmentDepth * 4 + 1;
}

function getControlBounds(
  generatorId: GeneratorId,
  control: ControlDefinition,
  options: Record<string, number>,
) {
  let min = control.min;
  let max = control.max;

  if (generatorId === "apartment-building") {
    if (control.key === "width" || control.key === "height") {
      min = alignToStep(
        Math.max(control.min, getMinimumBuildingDimension(options)),
        control.min,
        control.step,
      );
    }

    if (control.key === "minApartmentWidth") {
      max = options.maxApartmentWidth;
    }

    if (control.key === "maxApartmentWidth") {
      min = options.minApartmentWidth;
    }

    if (control.key === "minApartmentDepth") {
      max = options.maxApartmentDepth;
    }

    if (control.key === "maxApartmentDepth") {
      min = options.minApartmentDepth;
    }
  }

  return { min, max };
}

function getTileSize(map: EngineMap) {
  const width = map.width ?? 1;
  const height = map.height ?? 1;
  const maxDimension = Math.max(width, height);

  if (maxDimension >= 240) {
    return 4;
  }

  if (maxDimension >= 180) {
    return 5;
  }

  if (maxDimension >= 128) {
    return 6;
  }

  if (maxDimension >= 84) {
    return 8;
  }

  if (maxDimension >= 48) {
    return 10;
  }

  return 16;
}

function toMaskImage(map: EngineMap) {
  const width = Math.max(1, Math.floor(map.width ?? 1));
  const height = Math.max(1, Math.floor(map.height ?? 1));
  const data = new Uint8ClampedArray(width * height * 4);
  const layers = map.layers ?? {};

  populateChannel(data, width, height, layers.walls ?? [], 0);
  populateChannel(data, width, height, layers.props ?? [], 1);
  populateChannel(data, width, height, layers.items ?? [], 2);
  populateChannel(data, width, height, layers.characters ?? [], 3);

  return new ImageData(data, width, height);
}

function ensureShaderRenderer(canvas: HTMLCanvasElement) {
  const gl = canvas.getContext("webgl2", {
    alpha: true,
    antialias: true,
    depth: false,
    stencil: false,
    premultipliedAlpha: true,
    preserveDrawingBuffer: false,
  });

  if (!gl) {
    return null;
  }

  const program = createProgram(gl, SHADER_VERTEX_SOURCE, SHADER_FRAGMENT_SOURCE);
  if (!program) {
    return null;
  }

  const vao = gl.createVertexArray();
  const positionBuffer = gl.createBuffer();
  const maskTexture = createTexture(gl);
  const noiseTexture = createRepeatTexture(gl);

  if (!vao || !positionBuffer || !maskTexture || !noiseTexture) {
    if (vao) {
      gl.deleteVertexArray(vao);
    }
    if (positionBuffer) {
      gl.deleteBuffer(positionBuffer);
    }
    if (maskTexture) {
      gl.deleteTexture(maskTexture);
    }
    if (noiseTexture) {
      gl.deleteTexture(noiseTexture);
    }
    gl.deleteProgram(program);
    return null;
  }

  gl.bindVertexArray(vao);
  gl.bindBuffer(gl.ARRAY_BUFFER, positionBuffer);
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

  const positionLocation = gl.getAttribLocation(program, "a_position");
  gl.enableVertexAttribArray(positionLocation);
  gl.vertexAttribPointer(positionLocation, 2, gl.FLOAT, false, 0, 0);
  gl.bindVertexArray(null);
  gl.bindBuffer(gl.ARRAY_BUFFER, null);

  const noiseSize = 64;
  const noiseData = createNoiseTextureData(noiseSize);
  gl.bindTexture(gl.TEXTURE_2D, noiseTexture);
  gl.texImage2D(
    gl.TEXTURE_2D,
    0,
    gl.RGBA,
    noiseSize,
    noiseSize,
    0,
    gl.RGBA,
    gl.UNSIGNED_BYTE,
    noiseData,
  );
  gl.bindTexture(gl.TEXTURE_2D, null);

  const uniforms = {
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
  };

  if (
    !uniforms.mask ||
    !uniforms.noise ||
    !uniforms.maskSize ||
    !uniforms.viewportSize ||
    !uniforms.cameraCenter ||
    !uniforms.scale ||
    !uniforms.backdropTop ||
    !uniforms.backdropBottom ||
    !uniforms.floorLight ||
    !uniforms.floorShadow ||
    !uniforms.wallLight ||
    !uniforms.wallShadow ||
    !uniforms.outlineColor ||
    !uniforms.propColor ||
    !uniforms.itemColor ||
    !uniforms.characterColor
  ) {
    gl.deleteTexture(maskTexture);
    gl.deleteTexture(noiseTexture);
    gl.deleteBuffer(positionBuffer);
    gl.deleteVertexArray(vao);
    gl.deleteProgram(program);
    return null;
  }

  return {
    gl,
    program,
    vao,
    maskTexture,
    noiseTexture,
    uniforms,
  } satisfies ShaderRenderer;
}

function uploadMaskTexture(renderer: ShaderRenderer, texture: MapTexture) {
  const { gl } = renderer;
  gl.bindTexture(gl.TEXTURE_2D, renderer.maskTexture);
  gl.texImage2D(
    gl.TEXTURE_2D,
    0,
    gl.RGBA,
    texture.width,
    texture.height,
    0,
    gl.RGBA,
    gl.UNSIGNED_BYTE,
    texture.imageData.data,
  );
  gl.bindTexture(gl.TEXTURE_2D, null);
}

function populateChannel(
  target: Uint8ClampedArray,
  width: number,
  height: number,
  layer: number[],
  channelOffset: number,
) {
  for (let index = 0; index < layer.length; index += 2) {
    const x = layer[index];
    const y = layer[index + 1];

    if (
      typeof x !== "number" ||
      typeof y !== "number" ||
      x < 0 ||
      y < 0 ||
      x >= width ||
      y >= height
    ) {
      continue;
    }

    const pixelIndex = (y * width + x) * 4 + channelOffset;
    target[pixelIndex] = 255;
  }
}

function channelAt(mask: ImageData, x: number, y: number, channelOffset: number) {
  const pixelIndex = (y * mask.width + x) * 4 + channelOffset;
  return mask.data[pixelIndex] ?? 0;
}

function isChannelFilled(mask: ImageData, x: number, y: number, channelOffset: number) {
  if (x < 0 || y < 0 || x >= mask.width || y >= mask.height) {
    return false;
  }

  return channelAt(mask, x, y, channelOffset) > 0;
}

function hashCell(x: number, y: number) {
  let value = x * 374761393 + y * 668265263;
  value = (value ^ (value >>> 13)) * 1274126177;
  return ((value ^ (value >>> 16)) >>> 0) / 4294967295;
}

function createMapTexture(map: EngineMap) {
  if (!map.width || !map.height) {
    return null;
  }

  return {
    imageData: toMaskImage(map),
    width: Math.max(1, Math.floor(map.width)),
    height: Math.max(1, Math.floor(map.height)),
  } satisfies MapTexture;
}

function getBaseScale(texture: MapTexture, viewport: ViewportSize) {
  if (viewport.width <= 0 || viewport.height <= 0) {
    return 1;
  }

  return Math.min(viewport.width / texture.width, viewport.height / texture.height);
}

function clampCamera(camera: CameraState, texture: MapTexture, viewport: ViewportSize) {
  const scale = getBaseScale(texture, viewport) * camera.zoom;
  const halfVisibleWidth = viewport.width / (2 * scale);
  const halfVisibleHeight = viewport.height / (2 * scale);
  const minCenterX = Math.min(halfVisibleWidth, texture.width - halfVisibleWidth);
  const maxCenterX = Math.max(halfVisibleWidth, texture.width - halfVisibleWidth);
  const minCenterY = Math.min(halfVisibleHeight, texture.height - halfVisibleHeight);
  const maxCenterY = Math.max(halfVisibleHeight, texture.height - halfVisibleHeight);

  camera.centerX = clamp(camera.centerX, minCenterX, maxCenterX);
  camera.centerY = clamp(camera.centerY, minCenterY, maxCenterY);
}

function createDefaultCamera(texture: MapTexture): CameraState {
  return {
    centerX: texture.width / 2,
    centerY: texture.height / 2,
    zoom: 1,
  };
}

function drawViewport(
  canvas: HTMLCanvasElement,
  renderer: ShaderRenderer,
  texture: MapTexture,
  viewport: ViewportSize,
  camera: CameraState,
  themePalette: ThemePalette,
) {
  const { gl } = renderer;
  if (viewport.width <= 0 || viewport.height <= 0) {
    return;
  }

  const dpr = window.devicePixelRatio || 1;
  canvas.width = Math.max(1, Math.floor(viewport.width * dpr));
  canvas.height = Math.max(1, Math.floor(viewport.height * dpr));
  const scale = getBaseScale(texture, viewport) * camera.zoom;
  gl.viewport(0, 0, canvas.width, canvas.height);
  gl.clearColor(0, 0, 0, 0);
  gl.clear(gl.COLOR_BUFFER_BIT);
  gl.useProgram(renderer.program);
  gl.bindVertexArray(renderer.vao);

  gl.activeTexture(gl.TEXTURE0);
  gl.bindTexture(gl.TEXTURE_2D, renderer.maskTexture);
  gl.activeTexture(gl.TEXTURE1);
  gl.bindTexture(gl.TEXTURE_2D, renderer.noiseTexture);

  gl.uniform1i(renderer.uniforms.mask, 0);
  gl.uniform1i(renderer.uniforms.noise, 1);
  gl.uniform2f(renderer.uniforms.maskSize, texture.width, texture.height);
  gl.uniform2f(renderer.uniforms.viewportSize, viewport.width, viewport.height);
  gl.uniform2f(renderer.uniforms.cameraCenter, camera.centerX, camera.centerY);
  gl.uniform1f(renderer.uniforms.scale, scale);
  gl.uniform3f(renderer.uniforms.backdropTop, ...themePalette.backdropTop);
  gl.uniform3f(renderer.uniforms.backdropBottom, ...themePalette.backdropBottom);
  gl.uniform3f(renderer.uniforms.floorLight, ...themePalette.floorLight);
  gl.uniform3f(renderer.uniforms.floorShadow, ...themePalette.floorShadow);
  gl.uniform3f(renderer.uniforms.wallLight, ...themePalette.wallLight);
  gl.uniform3f(renderer.uniforms.wallShadow, ...themePalette.wallShadow);
  gl.uniform3f(renderer.uniforms.outlineColor, ...themePalette.outlineColor);
  gl.uniform3f(renderer.uniforms.propColor, ...themePalette.propColor);
  gl.uniform3f(renderer.uniforms.itemColor, ...themePalette.itemColor);
  gl.uniform3f(renderer.uniforms.characterColor, ...themePalette.characterColor);

  gl.drawArrays(gl.TRIANGLES, 0, 6);

  gl.bindVertexArray(null);
  gl.bindTexture(gl.TEXTURE_2D, null);
}

function getPointDistance(first: PointerSample, second: PointerSample) {
  return Math.hypot(second.x - first.x, second.y - first.y);
}

function getPointMidpoint(first: PointerSample, second: PointerSample) {
  return {
    x: (first.x + second.x) / 2,
    y: (first.y + second.y) / 2,
  };
}

type PointerSample = {
  x: number;
  y: number;
};

type InteractionState =
  | {
      mode: "idle";
    }
  | {
      mode: "pan";
      lastPoint: PointerSample;
    }
  | {
      mode: "pinch";
      lastMidpoint: PointerSample;
      lastDistance: number;
    };

function getInitialOptionsState() {
  return GENERATOR_DEFINITIONS.reduce<Record<GeneratorId, Record<string, number>>>(
    (accumulator, definition) => {
      accumulator[definition.id] = { ...definition.defaults };
      return accumulator;
    },
    {
      apartment: {},
      "apartment-floor": {},
      "apartment-building": {},
    },
  );
}

function InfoTooltip({
  tip,
  className = "",
}: {
  tip: string;
  className?: string;
}) {
  const triggerRef = useRef<HTMLButtonElement | null>(null);
  const [isOpen, setIsOpen] = useState(false);
  const [position, setPosition] = useState({ left: 0, top: 0 });

  function updatePosition() {
    const trigger = triggerRef.current;
    if (!trigger) {
      return;
    }

    const rect = trigger.getBoundingClientRect();
    const tooltipWidth = 224;
    const preferredLeft = rect.right + 12;
    const fallbackLeft = rect.left - tooltipWidth - 12;
    const left = preferredLeft + tooltipWidth > window.innerWidth - 12
      ? Math.max(12, fallbackLeft)
      : preferredLeft;
    const top = Math.min(
      window.innerHeight - 12,
      Math.max(12, rect.top + rect.height / 2),
    );

    setPosition({ left, top });
  }

  useEffect(() => {
    if (!isOpen) {
      return;
    }

    updatePosition();

    const handleViewportChange = () => {
      updatePosition();
    };

    window.addEventListener("scroll", handleViewportChange, true);
    window.addEventListener("resize", handleViewportChange);

    return () => {
      window.removeEventListener("scroll", handleViewportChange, true);
      window.removeEventListener("resize", handleViewportChange);
    };
  }, [isOpen]);

  return (
    <>
      <button
        ref={triggerRef}
        type="button"
        aria-label={tip}
        onMouseEnter={() => {
          updatePosition();
          setIsOpen(true);
        }}
        onMouseLeave={() => setIsOpen(false)}
        onFocus={() => {
          updatePosition();
          setIsOpen(true);
        }}
        onBlur={() => setIsOpen(false)}
        onClick={() => {
          updatePosition();
          setIsOpen((current) => !current);
        }}
        className={`btn btn-ghost btn-xs btn-circle h-5 min-h-5 w-5 cursor-help border border-base-300 p-0 text-[10px] text-base-content/60 ${className}`.trim()}
      >
        ?
      </button>
      {isOpen && typeof document !== "undefined"
        ? createPortal(
          <div
            className="pointer-events-none fixed z-[80] max-w-56 -translate-y-1/2"
            style={{ left: `${position.left}px`, top: `${position.top}px` }}
          >
            <div className="relative">
              <div className="absolute left-0 top-1/2 h-3 w-3 -translate-x-1/2 -translate-y-1/2 rotate-45 rounded-[2px] bg-neutral shadow-sm" />
              <div className="rounded-lg bg-neutral px-3 py-2 text-xs leading-5 text-neutral-content shadow-xl ring-1 ring-black/10">
                {tip}
              </div>
            </div>
          </div>,
          document.body,
        )
        : null}
    </>
  );
}

export default function ProcGenBrowserDemo() {
  const canvasRef = useRef<HTMLCanvasElement | null>(null);
  const viewportRef = useRef<HTMLDivElement | null>(null);
  const engineRef = useRef<ProcGenEngineModule | null>(null);
  const rendererRef = useRef<ShaderRenderer | null>(null);
  const textureRef = useRef<MapTexture | null>(null);
  const cameraRef = useRef<CameraState | null>(null);
  const pointersRef = useRef(new Map<number, PointerSample>());
  const interactionRef = useRef<InteractionState>({ mode: "idle" });
  const [selectedGenerator, setSelectedGenerator] =
    useState<GeneratorId>("apartment-building");
  const [optionsByGenerator, setOptionsByGenerator] = useState(getInitialOptionsState);
  const [result, setResult] = useState<EngineMap | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [engineReady, setEngineReady] = useState(false);
  const [isGenerating, setIsGenerating] = useState(false);
  const [viewportSize, setViewportSize] = useState<ViewportSize>({ width: 0, height: 0 });
  const [themePalette, setThemePalette] = useState<ThemePalette>(DEFAULT_THEME_PALETTE);

  const activeDefinition = GENERATOR_BY_ID[selectedGenerator];
  const activeOptions = optionsByGenerator[selectedGenerator];
  const deferredRequest = useDeferredValue(
    JSON.stringify({
      generatorId: selectedGenerator,
      options: activeOptions,
    }),
  );

  useEffect(() => {
    let cancelled = false;

    async function boot() {
      try {
        const engine = await loadEngineModule();
        if (cancelled) {
          return;
        }

        engineRef.current = engine;
        setEngineReady(true);
      } catch (error) {
        if (!cancelled) {
          setErrorMessage(
            error instanceof Error ? error.message : "Failed to load the browser-local proc-gen engine.",
          );
        }
      }
    }

    void boot();

    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    const viewport = viewportRef.current;
    if (!viewport) {
      return;
    }

    const observer = new ResizeObserver((entries) => {
      const entry = entries[0];
      if (!entry) {
        return;
      }

      const nextWidth = Math.floor(entry.contentRect.width);
      const nextHeight = Math.floor(entry.contentRect.height);

      setViewportSize((current) => {
        if (current.width === nextWidth && current.height === nextHeight) {
          return current;
        }

        return {
          width: nextWidth,
          height: nextHeight,
        };
      });
    });

    observer.observe(viewport);

    return () => {
      observer.disconnect();
    };
  }, []);

  useEffect(() => {
    setThemePalette(readThemePalette());

    const observer = new MutationObserver(() => {
      setThemePalette(readThemePalette());
    });

    observer.observe(document.documentElement, {
      attributes: true,
      attributeFilter: ["data-theme", "class"],
    });

    return () => {
      observer.disconnect();
    };
  }, []);

  useEffect(() => {
    const viewport = viewportRef.current;
    if (!viewport) {
      return;
    }

    const handleWheel = (event: globalThis.WheelEvent) => {
      if (!textureRef.current || !cameraRef.current) {
        return;
      }

      if (event.cancelable) {
        event.preventDefault();
      }

      const zoomFactor = Math.exp(-event.deltaY * 0.0015);
      zoomAtClientPoint(
        event.clientX,
        event.clientY,
        clamp(cameraRef.current.zoom * zoomFactor, MIN_CAMERA_ZOOM, MAX_CAMERA_ZOOM),
      );
    };

    viewport.addEventListener("wheel", handleWheel, { passive: false });

    return () => {
      viewport.removeEventListener("wheel", handleWheel);
    };
  }, [viewportSize]);

  function redraw() {
    const canvas = canvasRef.current;
    const texture = textureRef.current;
    const camera = cameraRef.current;

    if (!canvas || !texture || !camera) {
      return;
    }
    let renderer = rendererRef.current;
    if (renderer && renderer.gl.canvas !== canvas) {
      renderer = null;
      rendererRef.current = null;
    }

    if (!renderer) {
      renderer = ensureShaderRenderer(canvas);
      rendererRef.current = renderer;

      if (!renderer) {
        setErrorMessage("WebGL2 is unavailable, so the Godot shader renderer could not start.");
        return;
      }

      uploadMaskTexture(renderer, texture);
    }

    drawViewport(canvas, renderer, texture, viewportSize, camera, themePalette);
  }

  function resetCamera() {
    const texture = textureRef.current;
    if (!texture) {
      cameraRef.current = null;
      return;
    }

    const camera = createDefaultCamera(texture);
    clampCamera(camera, texture, viewportSize);
    cameraRef.current = camera;
  }

  function zoomAtClientPoint(clientX: number, clientY: number, nextZoom: number) {
    const viewport = viewportRef.current;
    const texture = textureRef.current;
    const camera = cameraRef.current;
    if (!viewport || !texture || !camera) {
      return;
    }

    const rect = viewport.getBoundingClientRect();
    const baseScale = getBaseScale(texture, viewportSize);
    const currentScale = baseScale * camera.zoom;
    const nextScale = baseScale * nextZoom;
    const pointX = clientX - rect.left;
    const pointY = clientY - rect.top;
    const mapX = camera.centerX + (pointX - viewportSize.width / 2) / currentScale;
    const mapY = camera.centerY + (pointY - viewportSize.height / 2) / currentScale;

    camera.zoom = clamp(nextZoom, MIN_CAMERA_ZOOM, MAX_CAMERA_ZOOM);
    camera.centerX = mapX - (pointX - viewportSize.width / 2) / nextScale;
    camera.centerY = mapY - (pointY - viewportSize.height / 2) / nextScale;
    clampCamera(camera, texture, viewportSize);
    redraw();
  }

  useEffect(() => {
    if (!result) {
      textureRef.current = null;
      cameraRef.current = null;
      redraw();
      return;
    }

    const previousTexture = textureRef.current;
    const previousCamera = cameraRef.current;
    const nextTexture = createMapTexture(result);
    textureRef.current = nextTexture;

    if (nextTexture && rendererRef.current) {
      uploadMaskTexture(rendererRef.current, nextTexture);
    }

    if (!nextTexture) {
      cameraRef.current = null;
    } else if (previousCamera && previousTexture) {
      const nextCamera = {
        centerX: previousCamera.centerX * (nextTexture.width / previousTexture.width),
        centerY: previousCamera.centerY * (nextTexture.height / previousTexture.height),
        zoom: previousCamera.zoom,
      };
      clampCamera(nextCamera, nextTexture, viewportSize);
      cameraRef.current = nextCamera;
    } else {
      resetCamera();
    }

    redraw();
  }, [result]);

  useEffect(() => {
    const texture = textureRef.current;
    const camera = cameraRef.current;

    if (!texture) {
      return;
    }

    if (!camera) {
      resetCamera();
    } else {
      clampCamera(camera, texture, viewportSize);
    }

    redraw();
  }, [viewportSize, themePalette]);

  useEffect(() => {
    if (!engineReady || !engineRef.current) {
      return;
    }

    const request = JSON.parse(deferredRequest) as {
      generatorId: GeneratorId;
      options: Record<string, number>;
    };

    const timerId = window.setTimeout(() => {
      setIsGenerating(true);

      void engineRef.current!
        .generateMap(request)
        .then((envelope) => {
          startTransition(() => {
            setErrorMessage(
              envelope.ok === false
                ? envelope.errorMessage || "Generation failed."
                : null,
            );
            setResult(envelope.ok === false ? null : envelope.map ?? null);
          });
        })
        .catch((error: unknown) => {
          startTransition(() => {
            setErrorMessage(
              error instanceof Error ? error.message : "Generation failed.",
            );
            setResult(null);
          });
        })
        .finally(() => {
          setIsGenerating(false);
        });
    }, 140);

    return () => {
      window.clearTimeout(timerId);
    };
  }, [deferredRequest, engineReady]);

  function updateOption(control: ControlDefinition, nextValue: number) {
    setOptionsByGenerator((current) => {
      const definition = GENERATOR_BY_ID[selectedGenerator];
      const bounds = getControlBounds(selectedGenerator, control, current[selectedGenerator]);
      const nextOptions = {
        ...current[selectedGenerator],
        [control.key]: clamp(nextValue, bounds.min, bounds.max),
      };

      if (selectedGenerator === "apartment-building") {
        const requiredDimension = alignToStep(
          Math.max(96, getMinimumBuildingDimension(nextOptions)),
          96,
          8,
        );
        nextOptions.width = Math.max(nextOptions.width, requiredDimension);
        nextOptions.height = Math.max(nextOptions.height, requiredDimension);
      }

      return {
        ...current,
        [definition.id]: nextOptions,
      };
    });
  }

  function resetActiveGenerator() {
    setOptionsByGenerator((current) => ({
      ...current,
      [selectedGenerator]: { ...activeDefinition.defaults },
    }));
  }

  function syncPointerInteraction() {
    const pointers = [...pointersRef.current.values()];

    if (pointers.length === 0) {
      interactionRef.current = { mode: "idle" };
      return;
    }

    if (pointers.length === 1) {
      const [pointer] = pointers;
      interactionRef.current = {
        mode: "pan",
        lastPoint: pointer,
      };
      return;
    }

    const [first, second] = pointers;
    interactionRef.current = {
      mode: "pinch",
      lastMidpoint: getPointMidpoint(first, second),
      lastDistance: getPointDistance(first, second),
    };
  }

  function handlePointerDown(event: PointerEvent<HTMLDivElement>) {
    event.currentTarget.setPointerCapture(event.pointerId);
    pointersRef.current.set(event.pointerId, {
      x: event.clientX,
      y: event.clientY,
    });
    syncPointerInteraction();
  }

  function handlePointerMove(event: PointerEvent<HTMLDivElement>) {
    if (!pointersRef.current.has(event.pointerId)) {
      return;
    }

    pointersRef.current.set(event.pointerId, {
      x: event.clientX,
      y: event.clientY,
    });

    const texture = textureRef.current;
    const camera = cameraRef.current;
    if (!texture || !camera) {
      syncPointerInteraction();
      return;
    }

    const interaction = interactionRef.current;

    if (interaction.mode === "pan") {
      const nextPoint = pointersRef.current.get(event.pointerId);
      if (!nextPoint) {
        return;
      }

      const scale = getBaseScale(texture, viewportSize) * camera.zoom;
      camera.centerX -= (nextPoint.x - interaction.lastPoint.x) / scale;
      camera.centerY -= (nextPoint.y - interaction.lastPoint.y) / scale;
      clampCamera(camera, texture, viewportSize);
      interactionRef.current = {
        mode: "pan",
        lastPoint: nextPoint,
      };
      redraw();
      return;
    }

    if (interaction.mode === "pinch" && pointersRef.current.size >= 2) {
      const [first, second] = [...pointersRef.current.values()];
      const midpoint = getPointMidpoint(first, second);
      const distance = getPointDistance(first, second);
      const scale = getBaseScale(texture, viewportSize) * camera.zoom;

      camera.centerX -= (midpoint.x - interaction.lastMidpoint.x) / scale;
      camera.centerY -= (midpoint.y - interaction.lastMidpoint.y) / scale;
      clampCamera(camera, texture, viewportSize);

      if (interaction.lastDistance > 0 && distance > 0) {
        zoomAtClientPoint(
          midpoint.x,
          midpoint.y,
          clamp(camera.zoom * (distance / interaction.lastDistance), MIN_CAMERA_ZOOM, MAX_CAMERA_ZOOM),
        );
      } else {
        redraw();
      }

      interactionRef.current = {
        mode: "pinch",
        lastMidpoint: midpoint,
        lastDistance: distance,
      };
    }
  }

  function handlePointerEnd(event: PointerEvent<HTMLDivElement>) {
    pointersRef.current.delete(event.pointerId);
    syncPointerInteraction();
  }

  const wallCount = Math.floor((result?.layers?.walls?.length ?? 0) / 2);

  return (
    <div className="grid h-[100dvh] gap-2 overflow-hidden p-2 sm:gap-3 sm:p-3 xl:gap-4 xl:p-4 grid-rows-[minmax(14rem,34dvh)_minmax(0,1fr)] sm:grid-rows-[minmax(16rem,38dvh)_minmax(0,1fr)] xl:grid-cols-[minmax(19rem,22rem)_minmax(0,1fr)] xl:grid-rows-1">
      <aside className="order-2 flex h-full min-h-0 flex-col overflow-hidden border border-base-300/70 bg-transparent shadow-none xl:order-1">
        <div className="card-body flex min-h-0 flex-1 flex-col gap-3 p-3 sm:gap-4 sm:p-4 xl:p-5">
          <div className="hidden xl:block">
            <h2 className="mt-2 text-2xl font-bold leading-tight text-primary">
              Apartment Generator
            </h2>
          </div>

          <div className="grid grid-cols-[minmax(0,1fr)_auto] items-end gap-2 sm:gap-3">
            <label className="form-control min-w-0">
              <div className="label py-1">
                <span className="label-text text-xs font-semibold uppercase tracking-[0.2em] text-base-content/50">
                  Generator Type
                </span>
              </div>
              <select
                value={selectedGenerator}
                onChange={(event) => setSelectedGenerator(event.currentTarget.value as GeneratorId)}
                className="select select-bordered select-sm w-full min-w-0"
              >
                {GENERATOR_DEFINITIONS.map((definition) => (
                  <option key={definition.id} value={definition.id}>
                    {definition.label}
                  </option>
                ))}
              </select>
            </label>

            <button
              type="button"
              onClick={resetActiveGenerator}
              className="btn btn-sm btn-outline whitespace-nowrap"
            >
              Reset
            </button>
          </div>

          <div className="min-h-0 flex-1 pt-1">
            <div className="min-w-0 h-full space-y-2 overflow-y-auto overflow-x-hidden overscroll-contain pr-1 pb-20 sm:pb-24 xl:pb-0">
              {activeDefinition.controls.map((control) => {
                const bounds = getControlBounds(selectedGenerator, control, activeOptions);

                return (
                  <div key={control.key}>
                    <div className="grid grid-cols-[minmax(0,1fr)_5rem] items-center gap-3">
                      <div className="flex min-w-0 items-center gap-2">
                        <div className="truncate text-sm font-semibold leading-5 text-base-content">{control.label}</div>
                        <div className="shrink-0">
                          <InfoTooltip tip={control.description} className="tooltip-right shrink-0" />
                        </div>
                      </div>
                      <input
                        type="number"
                        min={bounds.min}
                        max={bounds.max}
                        step={control.step}
                        value={activeOptions[control.key]}
                        onChange={(event) =>
                          updateOption(
                            control,
                            parseNumericInput(event.currentTarget.value, activeOptions[control.key]),
                          )
                        }
                        className="input input-bordered input-xs h-8 min-h-8 w-full text-right"
                      />
                    </div>

                    <input
                      type="range"
                      min={bounds.min}
                      max={bounds.max}
                      step={control.step}
                      value={activeOptions[control.key]}
                      onChange={(event) =>
                        updateOption(
                          control,
                          parseNumericInput(event.currentTarget.value, activeOptions[control.key]),
                        )
                      }
                      className="range range-primary range-xs mt-2 block w-full [--range-fill:0]"
                    />

                    <div className="mt-1 flex items-center justify-between text-[10px] font-semibold uppercase tracking-[0.18em] text-base-content/40">
                      <span>{bounds.min}</span>
                      <span>{bounds.max}</span>
                    </div>
                  </div>
                );
              })}
            </div>
          </div>
        </div>
      </aside>

      <section className="order-1 flex min-h-[14rem] flex-col gap-2 xl:order-2 xl:h-full xl:min-h-0 xl:gap-3">
        <div className="px-1 xl:hidden">
          <h2 className="text-base font-semibold leading-tight text-primary sm:text-lg">
            Apartment Generator
          </h2>
        </div>
        <div
          ref={viewportRef}
          className="min-h-0 flex-1 overflow-hidden border border-base-300/70 touch-none"
          onPointerDown={handlePointerDown}
          onPointerMove={handlePointerMove}
          onPointerUp={handlePointerEnd}
          onPointerCancel={handlePointerEnd}
        >
          {result ? (
            <canvas
              ref={canvasRef}
              className="block h-full w-full cursor-grab touch-none bg-transparent active:cursor-grabbing"
              aria-label="Procedurally generated apartment map"
            />
          ) : (
            <div className="flex h-full min-h-[12rem] items-center justify-center rounded-xl border border-dashed border-base-300/70 bg-transparent px-6 text-center text-sm leading-6 text-base-content/60">
              {errorMessage || "Loading the browser-local generator..."}
            </div>
          )}
        </div>
      </section>
    </div>
  );
}

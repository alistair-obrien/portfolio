(() => {
const PROCGEN_SHADER_VERTEX_SOURCE = `#version 300 es
in vec2 a_position;
out vec2 v_uv;

void main() {
  gl_Position = vec4(a_position, 0.0, 1.0);
  v_uv = vec2(a_position.x * 0.5 + 0.5, 1.0 - (a_position.y * 0.5 + 0.5));
}
`;

const PROCGEN_SHADER_FRAGMENT_SOURCE = `#version 300 es
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

float sampleFloorMask(vec2 uv) {
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
  float floorMask = sampleFloorMask(uv);
  float sceneGrad = directionalGradient(uv, GRADIENT_DIRECTION);
  float openFloor = floorMask * (1.0 - center) * (1.0 - propMask) * (1.0 - itemMask);

  if (floorMask < 0.5 && center < 0.5 && propMask < 0.5 && itemMask < 0.5) {
    outColor = vec4(backdrop, 1.0);
    return;
  }

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

  vec3 base = backdrop;
  base = mix(base, floorColor, floorMask);
  base = mix(base, wallColor, center);
  base = mix(base, propColor, propMask);
  base = mix(base, itemColor, itemMask);
  base *= 1.0 - wallOcclusion * (1.0 - center);

outColor = vec4(base, 1.0);
}
`;

  window.ProcGenShaders = {
    PROCGEN_SHADER_VERTEX_SOURCE,
    PROCGEN_SHADER_FRAGMENT_SOURCE,
  };
})();

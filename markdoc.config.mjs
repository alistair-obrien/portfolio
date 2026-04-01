import { defineMarkdocConfig, component } from '@astrojs/markdoc/config';
import shiki from '@astrojs/markdoc/shiki';
import horizonLight from './src/themes/horizon-light.json';
import horizonDark from './src/themes/horizon-dark.json';

const codeHighlighting = await shiki({
  themes: {
    light: horizonLight,
    dark: horizonDark,
  },
  defaultColor: 'light',
  langAlias: {
    cs: 'csharp',
  },
});

export default defineMarkdocConfig({
  extends: [codeHighlighting],
  tags: {
    Spotify: {
      render: component('./src/components/Spotify.astro'),
      attributes: {
        url: { type: String, required: true },
      },
    },
    YouTube: {
      render: component('./src/components/YouTube.astro'),
      attributes: {
        id: { type: String },
        url: { type: String },
      },
    },
    Twitter: {
      render: component('./src/components/Twitter.astro'),
      attributes: {
        url: { type: String },
        id: { type: String },
        username: { type: String },
      },
    },
    CopyOnWriteHistory: {
      render: component('./src/components/CopyOnWriteHistory.astro'),
      attributes: {},
    },
    SimulationBranching: {
      render: component('./src/components/SimulationBranching.astro'),
      attributes: {},
    },
    LiveCommandPreview: {
      render: component('./src/components/LiveCommandPreview.astro'),
      attributes: {},
    },
  },
});

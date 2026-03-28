import { defineMarkdocConfig, component } from '@astrojs/markdoc/config';

export default defineMarkdocConfig({
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
  },
});

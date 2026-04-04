import { component, defineMarkdocConfig } from "@astrojs/markdoc/config";

export default defineMarkdocConfig({
  tags: {
    procgenembed: {
      render: component("./src/components/blog/ProcGenEmbed.astro"),
      selfClosing: true,
      attributes: {
        eyebrow: { type: String },
        title: { type: String },
        description: { type: String },
        src: { type: String, required: true },
        height: { type: String },
      },
    },
  },
});

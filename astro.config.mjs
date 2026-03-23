// @ts-check
import { defineConfig } from "astro/config";

import tailwindcss from "@tailwindcss/vite";

import react from "@astrojs/react";
import markdoc from "@astrojs/markdoc";
import keystatic from "@keystatic/astro";
import vercel from "@astrojs/vercel";

import icon from "astro-icon";

// https://astro.build/config
export default defineConfig({
  integrations: [react(), markdoc(), keystatic(), icon()],
  site: 'https://alistair-obrien.github.io',
  vite: {
    plugins: [tailwindcss()],
    optimizeDeps: {
      include: ["@keystatic/core", "@keystatic/astro"],
    },
  },

  output: "server",

  adapter: vercel({
    imageService: true
  }),
});
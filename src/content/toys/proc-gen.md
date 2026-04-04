---
featured: true
title: ProcGen Playground
description: >-
  A live playground for experimenting with staged apartment-building generation,
  corridor shapes, layouts, and shader rendering.
image: '@assets/blog/generating-apartment-complexes/image.png'
startDate: 2026-04-01
platforms:
  - web
skills:
  - C#
  - WebGL
  - Astro
  - Procedural Generation
demoLink: /blog/generating-apartment-complexes
widgetSrc: /proc-gen?embed=1&generator=Apartment_StageOne&lockGenerator=1&hideGenerator=1&hideTitle=1&shape=enclosed&rotation=0deg&horizontalCorridorLength=56&verticalCorridorLength=36&corridorWidth=5
widgetHeight: 42rem
fullscreenLink: /widgets/proc-gen/index.html?generator=Apartment_StageOne
---

This toy is the live version of the procedural generation playground used in the apartment generation write-up.

It sits somewhere between a tool and a demo:

- it is built to explain the generation process visually
- it is interactive enough to explore the parameter space directly
- it doubles as a lightweight development surface for the standalone ProcGen library

The fullscreen mode is meant for deeper exploration, while this page is the more curated "showcase" version.

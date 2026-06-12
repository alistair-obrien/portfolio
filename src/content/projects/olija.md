---
featured: true
title: Olija
roles:
  - Programmer
description: >-
  Olija is a game about Faraday's quest, a man shipwrecked then trapped in the
  mysterious country of Terraphage. Armed with a legendary harpoon, he and other
  castaways try to leave this hostile country to return to their homelands.
image: '@assets/projects/olija/image.jpg'
startDate: 2020-09-01
endDate: 2021-01-28
platforms:
  - windows
  - switch
  - playstation
  - xbox
skills:
  - C#
  - Unity
  - Construct 2
demoLink: https://store.steampowered.com/app/1297330/Olija/
---
I joined this project near the end. The core development was done, but I was added to assist in porting to multiple platforms.

When I joined I found that a lot of the build processes were undocumented and fairly manual. There many steps involved and every step and order simply committed to memory per platform.

Upon starting I spent some time documenting and automating as many of the processes as possible. This involved tasks such as converting video and audio file formats to platform native, swapping input definition assets, setting Scripting Define Symbols and switching to the proper build targets.

I mostly spent time hunting weird bugs, and developed debug overlays for better analysis in builds. An especially pesky bug was a physics box shape bug that was causing incorrect collisions in only one scene of the game.

This was a great project to work on, and I learnt a lot about various platforms!

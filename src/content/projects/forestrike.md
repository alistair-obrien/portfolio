---
featured: true
title: Forestrike
roles:
  - Lead Programmer
  - Producer
description: >-
  Forestrike is a martial arts roguelite where every death brings you closer to
  winning the fight before it begins. Use your ‘Foresight’ ability to experiment
  without consequences before fighting for real until earning the ultimate
  reward - victory without ‘Foresight’.
image: '@assets/projects/forestrike/image.webp'
startDate: 2022-01-21
endDate: 2025-11-21
platforms:
  - windows
  - switch
skills:
  - C#
  - Unity
  - Python
demoLink: https://store.steampowered.com/app/2325920/Forestrike/
---
This project was really special to me. I was involved from the very beginning to the very end and had the pleasure to work alongside some very talented developers. I'm very proud of what we were able to make, and have fond memories of our collaborative development environment.

## Producing / Project Management

Throughout the development span of the project I was responsible for designing the project schedule, the budget and the workflow.

The goals were to ensure developers would have very little friction in tackling tasks in their domain, quick and easy visibility on the state of work and a way to pivot/reassess development directions when need be.

We signed with Devolver Digital as our publisher, and a lot of work was put towards ensuring that we had solid milestones we could gracefully hit, and a budget that was realistic and attractive.

One of the biggest challenges here was that text content of the game grew much bigger than originally anticipated and we found we had to become reactive to external localization deadlines.

In order to solve this the director, writer and myself met and we came up with a solid gameplan and attractive compromises. In the end the story and delivery was mostly positively received by players, so it really felt like we found a good solution without abandoning our creative intentions.

## Tools

### Combat Animation Sequencer

The core tool that the majority of the game is built on.

The games director is a very talented pixel art animator, especially when it comes to action. He works very fast and iterates often, making adjustments to pixels, hitboxes, character states, sounds and more.

Asperite is his tool of choice, so I built an Asperite importer and a custom animation system that presents frames in the same format as Asperite.

On top of the custom animation I built a modular designer that allowed him to adjust frame lengths, add hitboxes/hurtboxes, manipulate physics, set states, transition to other animations, set up bind points for weapons/hats and more.

This tool grew bit by bit over the duration of development, often unlocking new potential and adding even more interesting mechanics to the core gameplay.

It was often surprising to me how expressive he managed to be with the tool!

### Player Techniques

In the game you learn various techniques which modify certain aspects of your moveset or even add new moves.

For this I used an existing node based plugin (similar to Unreal blueprints) and created code generated node wrappers for our game events.

This tool allowed designers to hook into various events, mutate payloads or trigger other behaviour.

Because we had our own wrapper for the actual graph runners, we were able to also reuse these generically outside of just techniques, and also apply optimizations that did not come natively such as pooling.

### Localization

As mentioned above, there was a lot of text content. Far more than we expected initially.

In order to keep iteration fluid and fast, and avoid external spreadsheet changes and reimports, I designed a localization system that could be used in the appropriate context, and still be collated into the appropriate localization kit later for external localization teams.

Using C# attributes for discoverability, and custom Unity Editors, we were able to connect all text in the game late in development and successfully have our game translated into 10 different languages.

### Audio Layer

I found audio in Unity to generally be quite slow to work with from a designer perspective. The director of the game is an extremely skilled sound designer and musician, so it was imperative he had a fast and intuitive way to set up his sounds in the game and adjust with as little friction as possible.

For this purpose I created an audio layer which:

- Removes the need to upkeep Audio Players manually
- Is asset driven so changes during play will persist
- Had various randomization options (Clips, Pitch, Volume)
- Could support more standard operations without much trouble, such as following objects, or looping

### UI Framework

From the beginning we knew this was not a UI centric game and that UI would have to be prioritized low.

Because of this a lot of the framework was simplified and components recycled as much as possible. This resulted in programmers being able to very quickly build debug menus, and quick prototype menus based on game designer's requirements.

## Dev Ops

A huge part of development friction can be in hunting down bugs that only show their heads in builds, or even just wanting to quickly show something to someone for discussion.

Often times in our high velocity team we did not want that process to interrupt our work, so setting up a remote build server was crucial.

Using Jenkins, python and custom Unity editors I set up a build process that could very quickly start and deploy testing builds to steam branches of developers working git copy without them having to commit or manage their own side branches.

## Switch Porting

We released the game on Steam and Nintendo Switch.

For Switch I was involved in graphics, memory and CPU bottleneck optimizations. This involved a lot of diving into the profiler and identifying costly operations.

In the end the game ran smoothly on Switch! (Very important for how timing is such a key factor in the game)

There was also some work involved in properly communicating with an independent server as we have a feature that allows players to share fight codes with each other.

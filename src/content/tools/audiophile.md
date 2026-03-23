---
featured: true
title: Audiophile
description: >-
  A Unity plugin that streamlines Sound Effect implementation so programmers and
  sound designers can elevate your game to the next level!
image: '@assets/tools/audiophile/image.png'
startDate: 2023-01-01
platforms:
  - unity
skills:
  - C#
demoLink: https://pixel-dust-dev.itch.io/audiophile-unity-sfx-tool
sourceLink: https://github.com/alistair-obrien/Unity-Audiophile
---
## Features

- Fast AudioClip variations generation to save time on manually assigning clips
- Weighted randomized clip playback for more detailed randomization
- Randomized pitch and Randomized volume for more variation and control
- Ability to make&nbsp;Spatial Sound and Advanced Settings presets to save time
- In Editor preview exactly as it would playback at runtime
- Streamlined Audio Mixer group assign so you can iterate quickly on mixer setups
- Choose between embedded Sound Events or Preset Asset Sound Events to fit whichever workflow
- Inbuilt Pooled Audio System to help keep your audio playing optimized
- No complicated framework to learn
- One line of code to add a Sound Event, and one line of code to play a sound event

```
public SoundEvent _sound;

public void Start()
{
    _sound.Play();
}
```

# <p align=center> Mouse Aim Flight Redux </p>

<p align=center>
	<img alt="License" src="https://img.shields.io/badge/License-All_Rights_Reserved-red">

	<a href="https://github.com/aspctt/MouseAimFlightRedux"><img src="https://cdn.jsdelivr.net/npm/@intergrav/devins-badges@3/assets/compact-minimal/available/github_vector.svg" alt="Available on GitHub"></a>
</p>

> **Status: in development.** Updated for KSP 1.12.5 with a new flight controller, which is being tested and tuned. There is no release of this version yet.

## Description

Mouse Aim Flight Redux lets you fly aircraft with the mouse. Put the cursor where you want to go and the mod works pitch, roll and yaw to bring the nose onto it, so you aim the aircraft rather than steer it.

The controller works from the craft's own torque and inertia, so it adapts to whatever you build rather than to one tuned airframe. It is meant to hold the craft steady without trimming, keep within G and angle of attack limits, and be precise enough to keep the nose on a target. On screen, a reticle marks the cursor and another marks where the nose is actually pointing. How it all works is set out in [DESIGN.md](https://github.com/aspctt/MouseAimFlightRedux/blob/main/docs/DESIGN.md).

Mouse aim is switched on and off with a hotkey in flight, and a second hotkey cycles between flight modes: Normal, Cruise and Aggressive, which trade smoothness for agility. The modes live in `FlightModes.cfg`, where they can be tuned or added to. A toolbar button opens the settings: hotkeys, mouse sensitivity, axis inversion, and the style, size and opacity of the reticle.

Mouse Aim Flight Redux is an independent continuation of the original Mouse Aim Flight by tetryds and ferram4, last released for KSP 1.9, and is not endorsed by them. See [Licensing](#licensing) for how the two relate.

## Installation

To install, place the GameData folder inside your Kerbal Space Program folder. If asked to overwrite files, please do so.

**REMOVE ANY OLD VERSIONS BEFORE INSTALLING**.

## Dependencies

None.

Supported where installed, no patching needed:

* [Ferram Aerospace Research](https://github.com/dkavolis/Ferram-Aerospace-Research) - the stock control surface adjustments are left off, so FAR's own handling applies

## Licensing

Mouse Aim Flight Redux is **All Rights Reserved**. The full terms are in [LICENSE](./LICENSE). The short version:

* You may download it and play with it.
* **Your own mods and configs are yours.** Separate mods, plugins and tools that work with Mouse Aim Flight Redux through its public API, and ModuleManager patches or config files that change its settings, may be written and distributed on any terms you choose, commercial ones included. No attribution is required, and this grant is irrevocable: it cannot be withdrawn from work already published, and it survives any future change to the license.
* Mod managers and modpacks may include it **by reference**, the way CKAN metadata does, so the download comes from an official page. Re-hosting, bundling, or altering the mod is not permitted.
* The mod's own source code and assets stay reserved. The source is public so it can be read and checked, not reused.

Mouse Aim Flight Redux continues the original by tetryds and ferram4. The parts of the original released under the BSD 2-Clause License stay under it, with its notice kept in [NOTICE](./NOTICE). The rest of the original was never licensed; it has been replaced with new code and is not included in any release. Releases of the original keep their own terms.

Please note the copyrights and trademarks in [NOTICE](./NOTICE).

## Credits

### This continuation

* aspctt - continuation, rewrite, maintenance

### Original Mouse Aim Flight

* tetryds - original author
* ferram4 - original author
* BahamutoD - BDArmory, which parts of the aim handling derive from
* yut23 - bug fixes

### Change Log

Full release history is in [CHANGELOG](./CHANGELOG.md).

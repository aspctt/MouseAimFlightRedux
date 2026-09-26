# <p align=center> Mouse Aim Flight Redux </p>

<p align=center>
	<img alt="Version" src="https://img.shields.io/badge/Version-1.5.1-orange">
	<img alt="Available for" src="https://img.shields.io/badge/Available_for-KSP_1.12.x-blue">
	<img alt="License" src="https://img.shields.io/badge/License-All_Rights_Reserved-red">
</p>

<p align=center>
	<a href="https://forum.kerbalspaceprogram.com/topic/231839-1125-mouse-aim-flight-redux-fly-planes-with-your-mouse/"><img src="https://cdn.jsdelivr.net/gh/aspctt/MouseAimFlightRedux@main/docs/badges/ksp-forum.svg" alt="Available on the KSP Forums"></a>
	<a href="https://spacedock.info/mod/4609/Mouse%20Aim%20Flight%20Redux"><img src="https://cdn.jsdelivr.net/gh/aspctt/MouseAimFlightRedux@main/docs/badges/spacedock.svg" alt="Available on SpaceDock"></a>
	<a href="https://github.com/aspctt/MouseAimFlightRedux"><img src="https://cdn.jsdelivr.net/npm/@intergrav/devins-badges@3/assets/compact-minimal/available/github_vector.svg" alt="Available on GitHub"></a>
</p>

## Description

Fly planes with your mouse. Point where you want to go, and Mouse Aim Flight Redux works the pitch, roll and yaw to get you there.

It reads your craft's torque and inertia, so it adapts to whatever you build. It turns hard without overshooting, stays within G and angle of attack limits, and never needs trimming.

* **Y** turns mouse aim on and off.
* **O** switches flight mode while mouse aim is on: Normal, Aggressive, Unlimited or Cruise. Each game starts in Normal.
* Terrain avoidance, switched on in the settings, pulls up at the last moment if you're about to fly into the ground or a mountain, then hands back. It stays out of the way with the gear down.
* "Camera Follows Aim" in the settings swings the camera round behind your aim, War Thunder style. Hold the right mouse button to look around.
* The toolbar button opens the settings: hotkeys, sensitivity, axis inversion, the on-screen markers, terrain avoidance and the camera.

Mouse Aim Flight Redux continues the original Mouse Aim Flight by tetryds and ferram4. It is an independent project, not endorsed by them.

## Installation

To install, place the GameData folder inside your Kerbal Space Program folder. If asked to overwrite files, please do so.

**REMOVE ANY OLD VERSIONS BEFORE INSTALLING**, including the original Mouse Aim Flight.

## Dependencies

None. Works with [Ferram Aerospace Research](https://github.com/dkavolis/Ferram-Aerospace-Research) out of the box.

SAS and [Atmosphere Autopilot](https://github.com/Boris-Barboris/AtmosphereAutopilot) are kept off while mouse aim is on, and come back on afterwards. To fly with Atmosphere Autopilot's fly-by-wire on, untick "Keep Atmosphere Autopilot Off" in the settings.

## Tuning

Flight modes live in `GameData/MouseAimFlightRedux/FlightModes.cfg`. Edit them, then press "Reload flight modes from disk" in the settings window to try your changes without restarting. Tick "Show Tuning Overlay" there to watch what the controller asks for and what the craft does, live. How it all works is in [DESIGN.md](https://github.com/aspctt/MouseAimFlightRedux/blob/main/docs/DESIGN.md).

## Licensing

Mouse Aim Flight Redux is **All Rights Reserved**. The full terms are in [LICENSE](./LICENSE). In short:

* Download it and play.
* Your own mods, tools, configs and ModuleManager patches that work with it are yours, on any terms you like, with no credit needed.
* Modpacks and mod managers like CKAN may link to it, with the download coming from an official page. Re-hosting or altering it is not allowed.
* The code and assets stay reserved. The source is public to read, not to reuse.

The parts that come from the original under the BSD 2-Clause License keep that license. See [NOTICE](./NOTICE) for those terms, and for copyrights and trademarks.

## Credits

* aspctt - Redux, rewrite, maintenance
* tetryds, ferram4 - the original Mouse Aim Flight
* BahamutoD - BDArmory, which parts of the aim handling come from
* yut23 - fixes to the original

Full history is in [CHANGELOG](./CHANGELOG.md).

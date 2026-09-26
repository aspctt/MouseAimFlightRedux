# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.4.1] - 2026-09-26

### Changed

- Terrain avoidance pulls up by Unlimited's limits, whatever the flight mode, until it's climbing clear. It takes over no earlier than before, but pulls away from mountains much harder

## [1.4.0] - 2026-09-25

### Added

- Terrain avoidance, switched on in the settings: just before you'd fly into the ground or a mountain, it levels the wings, pulls up and shows PULL UP, then hands back. It stays out of the way with the gear down

## [1.3.0] - 2026-09-25

### Added

- A tuning overlay, switched on in the settings, with live numbers and graphs of the rates asked for and flown, the limits and the inputs

### Changed

- Supports every KSP 1.12 release, not just 1.12.5
- Mouse aim now turns on and off with Y instead of P, which Atmosphere Autopilot uses. Saved settings still on P move to Y once
- O only switches flight mode while mouse aim is on, so it no longer also toggles Atmosphere Autopilot's moderation
- Turns with less than 90° of bank yaw with the flight path, so they're coordinated instead of skidding

### Fixed

- Hard pulls no longer run a few degrees past the angle of attack limit and pulse
- Pointing straight up or down no longer rocks the wings from side to side
- Cruise stays within its 45° bank limit, and turns round level instead of climbing steeply and getting stuck

## [1.2.0] - 2026-09-25

### Added

- A Crosshair nose marker, outlined in black so it shows against sky and ground
- A button to reset the settings to their defaults

### Changed

- Crosshair is the default nose marker. Existing players keep the one in their saved settings
- Nose markers are half their old size, so they sit inside the aim ring

### Fixed

- Binding the mouse aim key in the settings could switch mouse aim on straight away

## [1.1.0] - 2026-09-25

### Added

- SAS and Atmosphere Autopilot are kept off while mouse aim is on, and switched back on afterwards if they were on
- A setting to let Atmosphere Autopilot fly together with mouse aim
- An Unlimited flight mode, with no G limit and as much turning as stock wings can give
- `maxG = 0` turns a flight mode's G limit off
- A `brakingShare` flight mode key, for how hard it counts on stopping a rotation on target

### Changed

- Each game starts in Normal instead of the last flight mode used
- Flight modes cycle Normal, Aggressive, Unlimited, Cruise
- The nose marker is picked from a list that shows each marker

### Fixed

- Wobble with Atmosphere Autopilot installed. The control surface speed-up leaves other mods' control surfaces alone, and the controller allows for Atmosphere Autopilot's surfaces moving at a fixed speed

## [1.0.0] - 2026-09-25

First release. Mouse Aim Flight Redux continues Mouse Aim Flight 1.1.3 by tetryds and ferram4, and the changes below are relative to it. Earlier history is in [the original repository](https://github.com/tetryds/MouseAimFlight).

### Added

- Flight modes live in `FlightModes.cfg`, can be patched with ModuleManager, and reload in game without a restart
- The flight mode can be switched from the settings window as well as by hotkey
- Hotkeys are bound by pressing any key
- In space, mouse aim points the craft straight at the aim instead of banking
- A KSP-AVC version file
- The plugin registers with KSP as `MouseAimFlightRedux`, so other mods can depend on it

### Changed

- Renamed to Mouse Aim Flight Redux. It installs to `GameData/MouseAimFlightRedux`, and its config nodes are now `MOUSE_AIM_FLIGHT_REDUX_MODE` and `MOUSE_AIM_FLIGHT_REDUX_SETTINGS`
- Built for KSP 1.12.5
- A new flight controller that adapts to each craft's torque and inertia, turns without overshooting, and stays within G and angle of attack limits
- Normal, Cruise and Aggressive are retuned for the new controller
- Settings are saved in `PluginData`, so updates keep them. Settings from the original are not carried over
- Mouse sensitivity is now set in degrees per mouse step
- The markers and toolbar icon are drawn by the mod instead of loaded from image files
- Licensed All Rights Reserved. The parts from the original under the BSD 2-Clause License keep it, as set out in NOTICE
- Mouse aim runs once for the active vessel instead of on every loaded vessel

### Removed

- All code and textures from the original that were never licensed

### Fixed

- KSP's own startup for the vessel module was skipped
- The toolbar button and its listeners were never cleaned up when leaving flight
- Control surfaces docked or decoupled while mouse aim was on kept the wrong speed until the craft was reloaded

[Unreleased]: https://github.com/aspctt/MouseAimFlightRedux/compare/1.4.1...HEAD
[1.4.1]: https://github.com/aspctt/MouseAimFlightRedux/compare/1.4.0...1.4.1
[1.4.0]: https://github.com/aspctt/MouseAimFlightRedux/compare/1.3.0...1.4.0
[1.3.0]: https://github.com/aspctt/MouseAimFlightRedux/compare/1.2.0...1.3.0
[1.2.0]: https://github.com/aspctt/MouseAimFlightRedux/compare/1.1.0...1.2.0
[1.1.0]: https://github.com/aspctt/MouseAimFlightRedux/compare/1.0.0...1.1.0
[1.0.0]: https://github.com/aspctt/MouseAimFlightRedux/releases/tag/1.0.0

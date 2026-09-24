# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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

[Unreleased]: https://github.com/aspctt/MouseAimFlightRedux/compare/1.0.0...HEAD
[1.0.0]: https://github.com/aspctt/MouseAimFlightRedux/releases/tag/1.0.0

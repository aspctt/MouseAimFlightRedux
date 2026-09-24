# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

Continues Mouse Aim Flight by tetryds and ferram4 from the last commit of the original repository, after its final release, 1.1.3. Earlier history is in [the original repository](https://github.com/tetryds/MouseAimFlight).

### Added

- Flight modes are read from `FlightModes.cfg`, so they can be tuned or added to there or through ModuleManager, and the settings window can reload them from disk without a restart
- The flight mode can be changed from the settings window as well as by hotkey
- Hotkeys are bound by pressing the key itself, so any key works, including numbers and function keys
- Outside the atmosphere, mouse aim points the craft straight at the aim rather than banking into a turn
- A KSP-AVC version file, so version checkers and CKAN can see which KSP it is built for
- The plugin registers with KSP as `MouseAimFlightRedux`, so other mods can declare a dependency on it

### Changed

- Renamed to Mouse Aim Flight Redux. It installs to `GameData/MouseAimFlightRedux`, and its flight modes and settings are `MOUSE_AIM_FLIGHT_REDUX_MODE` and `MOUSE_AIM_FLIGHT_REDUX_SETTINGS` nodes
- Built for KSP 1.12.5
- A new flight controller. It measures the torque and inertia of the craft it is flying and the response it actually gets, closes each axis as fast as that allows without overshooting, and keeps within the mode's G and angle of attack limits
- Normal, Cruise and Aggressive are rebuilt on the new controller and tuned from scratch
- Settings are saved in `PluginData`, so updating the mod keeps them. Settings from earlier versions are not carried over
- Mouse sensitivity is set in degrees of aim per step of mouse movement
- The markers and toolbar icon are drawn by the mod rather than loaded from image files
- Licensed All Rights Reserved. The parts of the original under the BSD 2-Clause License keep it, with its notice in NOTICE. Releases of the original keep their own terms
- Reads whether the camera is in mouse look through KSP's public API, rather than the first private field it happens to find on the camera
- Mouse aim runs once for the active vessel rather than in every loaded vessel

### Removed

- All code and textures from the original that were never licensed, replaced as described above

### Fixed

- The vessel module replaced KSP's own startup for vessel modules instead of extending it, so KSP's part of that startup never ran
- The toolbar button was never removed on leaving flight, and each visit to flight added another listener for it that was never cleaned up
- Control surfaces docked on while mouse aim was on were left 3.5 times too slow, and ones decoupled while it was on 3.5 times too fast, until the craft was reloaded

[Unreleased]: https://github.com/aspctt/MouseAimFlightRedux/commits/main

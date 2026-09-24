# Mouse Aim Flight Redux: Design

How the plugin works and why. The code follows this document.

## Scope

The mouse moves an aim point. Every physics frame, the plugin sets pitch, yaw and roll to bring the nose onto it. It draws a marker at the aim and one at the nose, and offers hotkeys and a settings window.

## Provenance

The original Mouse Aim Flight had two kinds of files:

- **BSD 2-Clause:** the vessel module and the scene addon. Their code is kept, adapted, in the files marked BSD under "Structure", each with that license's header.
- **All Rights Reserved:** everything else, meaning the controller, flight modes, settings and textures. All of it is replaced.

Replacement code is written from this document, never from the unlicensed files: not their structure, names, constants or tuning.

## Structure

One addon per flight scene runs mouse aim for the active vessel, since only that one is ever flown.

| File | Role | |
|---|---|---|
| `MouseAimPilot.cs` | Hotkeys, cursor, on and off, following the active vessel, sending inputs | BSD |
| `AimTracker.cs` | The aim point and free look | BSD |
| `ControlSurfaceBoost.cs` | The control surface speed-up | BSD |
| `UI/Hud.cs` | The on-screen markers | BSD |
| `UI/SettingsWindow.cs` | Toolbar button and settings window | BSD |
| `UI/Reticles.cs` | Marker and icon textures | |
| `Settings.cs` | Player settings | |
| `Control/` | The controller and flight modes | |

Switching vessels, pausing or going on EVA turns mouse aim off. Leaving flight turns it off quietly.

## Frames and conventions

Geometry uses the vessel's `ReferenceTransform`: nose is `up`, canopy is `-forward`, right wing is `right`.

Angles come from `atan2` on those vectors, so signs follow geometry, not a library's handedness:

- **Pitch** is positive with the aim above the nose, and the pitch rate is `Cross(ω, nose) · canopy`.
- **Yaw** is positive with the aim right of the nose, and the yaw rate is `Cross(ω, nose) · right`.
- **Roll** is positive with the right wing down, and the roll rate is `Cross(ω, canopy) · right`.

`ω` is the root part's angular velocity. `FlightCtrlState` uses the same signs, as the kOS docs describe and KSP 1.12.5 confirms.

## Aim point

The aim sits 5000 m ahead of the centre of mass, fixed in world space so it stays put as the craft turns. The mouse moves it in the camera's frame, so right on the mouse is always right on screen. Sensitivity is in degrees per mouse step.

Keyboard pitch or yaw takes over and re-centres the aim on the nose. Keyboard roll takes over roll only. The right mouse button, or KSP's own mouse look, freezes the aim so the camera can move.

## Controller

Three stages, each axis on its own.

### Guidance

Turns the aim into an angle error per axis.

- **Pitch:** the aim's angle above the nose. Once banking toward the aim, it pulls by the whole angle off the nose, scaled by how well the bank has put the aim overhead, and never pushes while rolling.
- **Roll:** wings level below `bankBlendStart` degrees off the nose, fully banked toward the aim above `bankBlendEnd`, blended as directions in between so nothing wraps at ±180°. Limited to `maxBank`.
- **Yaw:** trims the last few degrees, fading as the bank takes over, plus a sideslip term to keep turns coordinated.

All of this fades in between 0.3 and 1.5 kPa of dynamic pressure. Below that, as in space, pitch and yaw point straight at the aim, roll holds still, and the flight limits are off.

### Attitude loop

Turns angle error into a target rate.

- **Lead:** it works on the error left once the controls catch up, `error - rate · (rateResponse + controlLag)`, so it eases off before the target rather than after.
- **Rate:** the smaller of `error / attitudeResponse` and the braking curve `sqrt(2 · a · |error|)`, where `a` is half the available angular acceleration. That's the fastest rate it can still stop from in time.
- **Limits:** `maxPitchRate`, `maxYawRate` and `maxRollRate`. Pitch is also capped at `maxG · g / airspeed`, halved when pushing. Near `maxAoA`, or `maxNegativeAoA` when pushing, pitch rate may only grow by what closes the remaining margin over `attitudeResponse`, so a turn held at the limit keeps turning.

### Rate loop

Turns rate error into stick input, incrementally. It measures the angular acceleration it's actually getting and adds the shortfall over authority. The wanted acceleration is `rateError / rateResponse`.

Measuring beats modelling here. A stable plane needs a lot of held elevator to keep pitching, and a loop that only scales rate error by authority never gets there at speed. Measuring folds stability, trim and damping into the feedback.

- **Acceleration:** change in rate per frame, low-pass filtered.
- **Controls:** modelled as following the inputs with a `controlLag` delay, then passed through the same filter so the two line up.
- **Gain:** `controlGain` scales each step. Too-high authority only slows it; too-low overshoots, which a gain below 1 guards against.
- **Pilot override:** the input the vessel actually got is fed back, so an axis resumes smoothly when the pilot lets go.

### Authority

Torque over moment of inertia, per axis.

- **Torque:** the sum of `ITorqueProvider.GetPotentialTorque` over the vessel, taken as x pitch, y roll and z yaw, with positive and negative averaged. RCS only counts while it's on. Refreshed a few times a second, since it changes with airspeed.
- **Inertia:** summed from the part rigidbodies about each axis through the centre of mass. That's each part's own inertia on the axis, plus mass times distance squared. Refreshed about once a second.
- **Floor:** a minimum keeps the division sane on axes with nothing to steer them.

## Flight modes

A mode is a set of limits and response times for the same controller, read from `MOUSE_AIM_FLIGHT_REDUX_MODE` nodes in `GameData/MouseAimFlightRedux/FlightModes.cfg`. ModuleManager can patch them, and the mode hotkey cycles them in file order. The settings window can reload the file without a restart, though that reload skips ModuleManager.

| Key | Unit | Meaning |
|---|---|---|
| `name` | | shown on screen |
| `maxPitchRate`, `maxYawRate`, `maxRollRate` | °/s | rate limits |
| `maxG` | g | pull limit, halved when pushing |
| `maxAoA`, `maxNegativeAoA` | ° | angle of attack limits |
| `maxBank` | ° | bank limit |
| `bankBlendStart`, `bankBlendEnd` | ° | where the bank starts and finishes committing |
| `attitudeResponse` | s | attitude loop time constant |
| `rateResponse` | s | rate loop time constant |
| `controlLag` | s | how long the controls take to follow |
| `controlGain` | | share of each correction made at once |

Three modes ship: **Normal** for general flying, **Cruise** for gentle, level-seeking long flights, and **Aggressive** for aerobatics and combat.

## Settings

Saved to `GameData/MouseAimFlightRedux/PluginData/Settings.cfg`, which KSP doesn't load as config and updates never overwrite. Anything missing uses the default.

| Key | Default | |
|---|---|---|
| `toggleKey` | P | mouse aim on and off |
| `modeKey` | O | next flight mode |
| `mode` | Normal | last mode selected |
| `mouseSensitivity` | 1 | degrees per mouse step |
| `invertX`, `invertY` | False | |
| `reticle` | Cross | nose marker: Cross, Dot or None |
| `reticleOpacity` | 1 | |
| `reticleSize` | 0.75 | fraction of 1/32 of the screen width |

To bind a hotkey, click its button and press a key. Escape cancels.

## Control surface speed-up

While mouse aim is on, stock control surfaces move 3.5 times faster and ease into position, which suits many small corrections. Each surface's own values are saved and put back exactly. Surfaces docked on while it's on are sped up as they arrive, and decoupled ones get their values back at once. Not applied under Ferram Aerospace Research, which drives its own surfaces.

## Markers and icon

Drawn into textures at startup from signed distance functions, antialiased over one pixel. No image files ship.

## Compatibility

- **KSP:** built against 1.12.5 for .NET Framework 4.7.2. KSP before 1.8 ran .NET 3.5, so it can't load there.
- **Ferram Aerospace Research:** detected by assembly name, and the speed-up is skipped.

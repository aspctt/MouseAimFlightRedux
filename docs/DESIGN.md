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
| `AutopilotLockout.cs` | Keeping SAS and Atmosphere Autopilot off | |
| `AtmosphereAutopilot.cs` | Atmosphere Autopilot's master switch and surface speed, by reflection | |
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
- **Rate:** the smaller of `error / attitudeResponse` and the braking curve `sqrt(2 · a · |error|)`, where `a` is `brakingShare` of the available angular acceleration. That's the fastest rate it can still stop from in time.
- **Limits:** `maxPitchRate`, `maxYawRate` and `maxRollRate`. Pitch is also capped at `maxG · g / airspeed`, halved when pushing, unless `maxG` is 0. Near `maxAoA`, or `maxNegativeAoA` when pushing, pitch rate may only grow by what closes the remaining margin over `attitudeResponse`, so a turn held at the limit keeps turning.

### Rate loop

Turns rate error into stick input, incrementally. It measures the angular acceleration it's actually getting and adds the shortfall over authority. The wanted acceleration is `rateError / rateResponse`.

Measuring beats modelling here. A stable plane needs a lot of held elevator to keep pitching, and a loop that only scales rate error by authority never gets there at speed. Measuring folds stability, trim and damping into the feedback.

- **Acceleration:** change in rate per frame, low-pass filtered.
- **Controls:** modelled as following the inputs with a `controlLag` delay, then passed through the same filter so the two line up. Controls that move at a fixed speed, like Atmosphere Autopilot's surfaces, are modelled at that speed instead, blended by their share of the axis's torque. Otherwise the loop keeps adding input while they catch up on a big change, then overshoots.
- **Gain:** `controlGain` scales each step. Too-high authority only slows it; too-low overshoots, which a gain below 1 guards against.
- **Pilot override:** the input the vessel actually got is fed back, so an axis resumes smoothly when the pilot lets go.

### Authority

Torque over moment of inertia, per axis.

- **Torque:** the sum of `ITorqueProvider.GetPotentialTorque` over the vessel, taken as x pitch, y roll and z yaw, with positive and negative averaged. RCS only counts while it's on. Refreshed a few times a second, since it changes with airspeed.
- **Inertia:** summed from the part rigidbodies about each axis through the centre of mass. That's each part's own inertia on the axis, plus mass times distance squared. Refreshed about once a second.
- **Floor:** a minimum keeps the division sane on axes with nothing to steer them.
- **Fixed-speed share:** the part of each axis's torque from Atmosphere Autopilot's surfaces, and their speed, for the rate loop's controls model.

## Flight modes

A mode is a set of limits and response times for the same controller, read from `MOUSE_AIM_FLIGHT_REDUX_MODE` nodes in `GameData/MouseAimFlightRedux/FlightModes.cfg`. ModuleManager can patch them. Each game starts in the first, and the mode hotkey cycles them in file order. The settings window can reload the file without a restart, though that reload skips ModuleManager.

| Key | Unit | Meaning |
|---|---|---|
| `name` | | shown on screen |
| `maxPitchRate`, `maxYawRate`, `maxRollRate` | °/s | rate limits |
| `maxG` | g | pull limit, halved when pushing, 0 for none |
| `maxAoA`, `maxNegativeAoA` | ° | angle of attack limits |
| `maxBank` | ° | bank limit |
| `bankBlendStart`, `bankBlendEnd` | ° | where the bank starts and finishes committing |
| `attitudeResponse` | s | attitude loop time constant |
| `rateResponse` | s | rate loop time constant |
| `controlLag` | s | how long the controls take to follow |
| `controlGain` | | share of each correction made at once |
| `brakingShare` | | share of the angular acceleration the braking curve counts on, 0.5 by default |

Four modes ship, in this order: **Normal** for general flying, **Aggressive** for aerobatics and combat, **Unlimited**, with no load limit and the angle of attack limit at 30°, where stock wings make the most lift, and **Cruise** for gentle, level-seeking long flights.

## Settings

Saved to `GameData/MouseAimFlightRedux/PluginData/Settings.cfg`, which KSP doesn't load as config and updates never overwrite. Anything missing uses the default.

| Key | Default | |
|---|---|---|
| `toggleKey` | P | mouse aim on and off |
| `modeKey` | O | next flight mode |
| `mouseSensitivity` | 1 | degrees per mouse step |
| `invertX`, `invertY` | False | |
| `reticle` | Crosshair | nose marker: Crosshair, Cross, Dot or None |
| `reticleOpacity` | 1 | |
| `reticleSize` | 0.75 | aim ring size, as a fraction of 1/32 of the screen width. The nose marker is half that |
| `keepAtmosphereAutopilotOff` | True | see "Other autopilots", shown only with Atmosphere Autopilot installed |

To bind a hotkey, click its button and press a key. Escape cancels. "Reset to defaults" asks once more, then puts every setting above back to its default. Flight modes are untouched.

## Control surface speed-up

While mouse aim is on, stock control surfaces move 3.5 times faster and ease into position, which suits many small corrections. Each surface's own values are saved and put back exactly. Surfaces docked on while it's on are sped up as they arrive, and decoupled ones get their values back at once. Not applied under Ferram Aerospace Research, which drives its own surfaces. Only stock modules are sped up: `ModuleControlSurface` and its stock subclasses. Other mods' replacements keep their own movement, since the controller is tuned against stock surfaces. Atmosphere Autopilot replaces every stock control surface with its own at load, even when switched off, and those wobble when sped up.

## Other autopilots

Stock SAS and Atmosphere Autopilot steer through the same pitch, yaw and roll. SAS is always kept off while mouse aim is on. Atmosphere Autopilot is too, unless `keepAtmosphereAutopilotOff` is false:

- **Turning on** records whether each was on, then switches it off.
- **While on,** either one switched back on, by its key or anything else, goes off again in `LateUpdate`, before the next physics step.
- **Turning off** switches back on whichever was on and was switched off. Atmosphere Autopilot goes first, since it switches SAS off as it starts. Nothing is restored on a destroyed vessel.

Mouse aim flies in `OnPreAutopilotUpdate`, the earliest of the vessel's control callbacks going by their names and by how kOS and Atmosphere Autopilot use them. Atmosphere Autopilot flies in `OnAutopilotUpdate`, so when both are on it always runs second, reads mouse aim's output as the pilot's stick and flies that. Callbacks on one delegate run in the order they were added, which changes with vessel switches, so sharing `OnAutopilotUpdate` would not keep that order.

Atmosphere Autopilot is optional and reached by reflection, through public members only: `AtmosphereAutopilot.Instance`, `getVesselModules(Vessel)` and `TopModuleManager.Active`, plus `mainMenuGUIUpdate()` to refresh its toolbar button, and the constant `SyncModuleControlSurface.CSURF_SPD`, the speed its surfaces move at when not set to ease. Checked against 1.6.1. If any is missing or throws, it's left alone for the rest of the session, with one log line.

## Markers and icon

Drawn into textures at startup from signed distance functions, antialiased over one pixel. No image files ship. Each mipmap is drawn from the shapes at its own size, so markers drawn small stay clean. The nose marker is drawn at half the aim ring's size, so it sits inside it.

The Crosshair nose marker has a white centre dot and four arms with a gap between them. The outer half of each arm is a rounded rectangle of half see-through medium grey. The dot and those tips are outlined in black at 75% opacity, and the inner half of each arm is the same black, so it shows against bright sky and dark ground alike.

## Compatibility

- **KSP:** built against 1.12.5 for .NET Framework 4.7.2. KSP before 1.8 ran .NET 3.5, so it can't load there.
- **Ferram Aerospace Research:** detected by assembly name, and the speed-up is skipped.
- **Atmosphere Autopilot:** detected by assembly name and kept off unless the player lets them fly together, see "Other autopilots".

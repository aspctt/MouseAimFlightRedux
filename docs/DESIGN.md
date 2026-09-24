# Mouse Aim Flight Redux: Design

This document is the source of truth for how the plugin works and why. Code follows it, not the other way round.

## Scope

The player points the mouse where the aircraft should go. The plugin keeps an aim direction in world space that the mouse moves, and every physics frame it computes pitch, yaw and roll inputs that bring the nose onto that direction. It draws a marker at the aim direction and another at the nose, and it offers hotkeys and a settings window.

## Provenance

The original Mouse Aim Flight shipped two kinds of files. Those under the BSD 2-Clause License are kept and adapted: the vessel module that read the mouse and drove the inputs, and the scene addon that drew the markers and the settings window. Their code now lives in the files marked BSD under "Structure", each carrying that license's header. Everything else in the original was All Rights Reserved and is replaced: the flight controller, the flight modes, the settings, and the textures.

Replacement code is written from this document. The unlicensed files are not used as a reference for it: not their structure, their names, their constants or their tuning. Where the new code does the same job, it does it its own way, and the reasons are recorded here.

## Structure

One addon per flight scene owns mouse aim for whichever vessel is active, rather than a module on every loaded vessel, since only the active one is ever flown.

| File | Role | |
|---|---|---|
| `MouseAimPilot.cs` | The owner: hotkeys, the cursor, switching on and off, following the active vessel, and the inputs sent each physics frame | BSD |
| `AimTracker.cs` | The aim direction and free look | BSD |
| `ControlSurfaceBoost.cs` | The control surface speed-up | BSD |
| `UI/Hud.cs` | The markers over the flight view | BSD |
| `UI/SettingsWindow.cs` | The toolbar button and settings window | BSD |
| `UI/Reticles.cs` | Marker and icon textures | |
| `Settings.cs` | Player settings | |
| `Control/` | The controller and flight modes | |

Switching vessels, opening the pause menu and going on EVA all switch mouse aim off, and leaving flight lets go quietly.

## Frames and conventions

All geometry uses the vessel's `ReferenceTransform`:

| Direction | Vector | Meaning |
|---|---|---|
| Nose | `up` | where the aircraft points |
| Canopy | `-forward` | "up" for the pilot |
| Right wing | `right` | |

Angles are measured from those vectors with `atan2`, so each sign is defined by geometry rather than by a library's handedness:

- Pitch error is positive when the aim is above the nose.
- Yaw error is positive when the aim is right of the nose.
- Bank is positive when the right wing is down.

Angular rates come from the root part's rigidbody. The velocity of a unit vector `v` rotating with the body is `Cross(ω, v)`, so:

- **Pitch rate** is `Cross(ω, nose) · canopy`, nose-up positive.
- **Yaw rate** is `Cross(ω, nose) · right`, nose-right positive.
- **Roll rate** is `Cross(ω, canopy) · right`, right-wing-down positive.

The outputs go to `FlightCtrlState`. Positive `pitch` is nose up, positive `yaw` is nose right, and positive `roll` is right wing down. This follows the kOS raw control documentation, which drives the same inputs, and is confirmed in game on KSP 1.12.5.

## Aim direction

The aim direction is a point 5000 m ahead of the centre of mass, held in world space so it stays put as the aircraft turns. Mouse movement shifts it in the camera's frame, so moving the mouse right always moves the aim right on screen whatever the aircraft's attitude. The shift is `sensitivity` degrees per unit of raw mouse axis.

Pitch or yaw input from the keyboard overrides the controller and re-centres the aim on the nose, and roll input overrides roll only. Holding the right mouse button, or KSP's own mouse look, freezes the aim so the camera can be moved.

## Controller

A cascade, run independently on each axis.

### Guidance

This stage turns the aim direction into three angle errors.

- **Pitch:** the aim's angle above the nose.
- **Roll:** the aircraft rolls to put the aim above the canopy, then pulls. How far it commits to that depends on how far off the nose the aim is. Below `bankBlendStart` degrees it holds the wings level to the horizon; above `bankBlendEnd` it banks fully toward the aim; in between the two target directions are blended as vectors, which avoids wrapping problems at ±180°. The bank reached is limited to `maxBank`.
- **Yaw:** trims the last few degrees of error, fading out as the bank takes over, plus a sideslip term that keeps turns coordinated.

Aircraft behaviour only makes sense with air to work in. Dynamic pressure scales it in between 0.3 and 1.5 kPa. Below that range, pitch and yaw point straight at the aim and roll holds still, which suits spacecraft, and the flight limits below stop applying.

### Attitude loop

This loop turns an angle error into a commanded rate. It works on the error that will still be left once the rate loop and the controls have caught up with a change of command: the current error minus the current rate times `rateResponse + controlLag`. Commanding on that, rather than on the error as it stands, takes the rate off in time instead of after the target has been passed.

The command is the smaller of a linear response (`error / attitudeResponse`) and a braking curve, `sqrt(2 · a · |error|)`, where `a` is half the angular acceleration available on that axis. The braking curve is the fastest rate the aircraft can still stop from before reaching the target, so large errors are closed quickly without overshooting.

The command is then limited:

- **Rate:** `maxPitchRate`, `maxYawRate`, `maxRollRate`.
- **Load factor:** the pitch rate is capped at `maxG · g / airspeed`, and at half of that when pushing.
- **Angle of attack:** the pitch rate may exceed the current one only by what closes the remaining margin to `maxAoA`, or to `maxNegativeAoA` when pushing, over `attitudeResponse`. A turn held at the limit keeps turning, and one past it backs off.

### Rate loop

This loop is incremental: it measures the angular acceleration actually being achieved, and moves the input by the shortfall against what the rate command needs, divided by the axis's authority. The needed acceleration is `rateError / rateResponse`.

A simpler loop that turns rate error straight into input, scaled by full-deflection authority, fails on stable aircraft. At speed, full deflection is worth a great deal of torque, but holding a pitch rate means holding enough elevator against the aircraft's own stability. That loop only gets there once the rate error is large, so the nose barely moves. Measuring the acceleration puts stability, trim, damping and anything else unmodelled into the feedback, and the input builds to whatever holding the rate takes.

Measurement details:

- **Acceleration:** the change in rate per physics frame, low-pass filtered.
- **Controls:** they do not reach an input at once. They are modelled as following the inputs with a first-order lag of `controlLag`, and that estimate passes through the same filter as the acceleration, so the two are compared at the same delay.
- **Gain:** `controlGain` scales each correction. Authority estimated too high only slows the loop; estimated too low, it overshoots, and a gain below 1 buys margin against that.
- **Pilot override:** the input the vessel actually received is fed back, so an overridden axis carries on smoothly when the pilot lets go.

### Authority

Authority is torque over moment of inertia, per axis.

- **Torque** is the sum of `ITorqueProvider.GetPotentialTorque` over the vessel's parts: control surfaces, reaction wheels, gimbals, and RCS only while RCS is switched on. Components are taken as x pitch, y roll and z yaw, with the positive and negative directions averaged. It is refreshed several times a second, since control surface torque changes with airspeed.
- **Moment of inertia** about each axis through the centre of mass is summed from the part rigidbodies: each one's own inertia tensor projected onto the axis, plus mass times its distance from the axis squared. It is refreshed about once a second.

A floor on authority keeps the division sane on axes with nothing to steer them.

## Flight modes

A flight mode is a set of limits and response times for the same controller. Modes are `MOUSE_AIM_FLIGHT_REDUX_MODE` nodes in `GameData/MouseAimFlightRedux/FlightModes.cfg`, so they can be changed or added by editing that file or through ModuleManager. The mode hotkey cycles them in file order, and the settings window can re-read the file from disk to tune without a restart. That reload bypasses ModuleManager.

| Key | Unit | Meaning |
|---|---|---|
| `name` | | shown on screen |
| `maxPitchRate`, `maxYawRate`, `maxRollRate` | °/s | rate limits |
| `maxG` | g | pull limit; pushing is limited to half |
| `maxAoA`, `maxNegativeAoA` | ° | angle of attack limits |
| `maxBank` | ° | bank limit while steering |
| `bankBlendStart`, `bankBlendEnd` | ° | aim offsets between which the bank commits |
| `attitudeResponse` | s | time constant of the attitude loop |
| `rateResponse` | s | time constant of the rate loop |
| `controlLag` | s | how long the controls take to follow an input |
| `controlGain` | | share of each correction the rate loop makes at once |

Three ship with the mod:

- **Normal:** for general flying.
- **Cruise:** gentle and level-seeking, for long flights.
- **Aggressive:** high rates and limits, for aerobatics and combat.

## Settings

Settings are saved to `GameData/MouseAimFlightRedux/PluginData/Settings.cfg`. KSP does not load config from `PluginData`, and an update of the mod never overwrites it. Defaults live in code, so a missing file or key simply means the default.

| Key | Default | |
|---|---|---|
| `toggleKey` | P | switches mouse aim on and off |
| `modeKey` | O | cycles flight modes |
| `mode` | Normal | the mode selected last |
| `mouseSensitivity` | 1 | degrees of aim per unit of mouse axis |
| `invertX`, `invertY` | False | |
| `reticle` | Cross | nose marker: Cross, Dot or None |
| `reticleOpacity` | 1 | |
| `reticleSize` | 0.75 | marker size as a fraction of 1/32 of the screen width |

Hotkeys are bound by clicking the button in the settings window and pressing a key; Escape cancels.

## Control surface speed-up

While mouse aim is on, stock control surfaces move 3.5 times faster and ease into position, which suits a controller making many small corrections. Each surface's own speed and easing are recorded when it is sped up and put back exactly when mouse aim goes off. Surfaces that join the vessel while it is on, by docking say, are sped up as they arrive. Surfaces that leave, by decoupling, get their own values back at once. Neither is touched under Ferram Aerospace Research, which drives its control surfaces itself.

## Markers and icon

The aim ring, the nose markers and the toolbar icon are drawn into textures at startup from signed distance functions, with one pixel of antialiasing. No image files ship with the mod.

## Compatibility

- **KSP version:** built against KSP 1.12.5 for .NET Framework 4.7.2. KSP before 1.8 ran a .NET 3.5 runtime, so it cannot load there.
- **Ferram Aerospace Research:** detected by its assembly name. The stock control surface speed-up is only applied without it.

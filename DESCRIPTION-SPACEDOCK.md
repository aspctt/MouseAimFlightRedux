<h1 style="text-align: center">Mouse Aim Flight Redux</h1>

<div style="text-align: center">
<img alt="Available for" src="https://img.shields.io/badge/Available_for-KSP_1.12.5-blue">
<img alt="Requires" src="https://img.shields.io/badge/Requires-Nothing-brightgreen">
<img alt="License" src="https://img.shields.io/badge/License-All_Rights_Reserved-red">
<br>
<a href="https://github.com/aspctt/MouseAimFlightRedux"><img alt="GitHub" src="https://cdn.jsdelivr.net/npm/@intergrav/devins-badges@3/assets/compact-minimal/available/github_vector.svg"></a>
</div>

Point your mouse where you want to go, and your plane follows. Smooth, easy flying for anything you build.

Mouse Aim Flight Redux handles the stick for you. It reads each craft's torque and inertia, so it adapts to whatever you bring to the runway: it turns hard without overshooting, stays inside G and angle of attack limits, and never needs trimming.

### Controls

- **P** turns mouse aim on and off.
- **O** switches flight mode. Each game starts in Normal.
- Hold the **right mouse button** to look around. The aim stays put until you let go.
- Your keyboard still works. Press pitch or yaw to take over, and let go to hand control back.
- The toolbar button opens the settings: hotkeys, sensitivity, axis inversion and the on-screen markers.

### Flight modes

**Normal** --> everyday flying

**Aggressive** --> hard turns for dogfights and aerobatics

**Unlimited** --> no G limit, turning as hard as the wings allow. With G-force limits on in the difficulty settings, pilots can black out and parts can break

**Cruise** --> gentle turns and shallow banks

The modes live in a plain config file. Tweak them, add your own, or patch them with ModuleManager, then reload them from the settings window without restarting the game.

### Works with

Stock aerodynamics and Ferram Aerospace Research. In stock, control surfaces move faster while mouse aim is on and go back to normal when you switch it off.

SAS and Atmosphere Autopilot switch off while mouse aim is on, and come back afterwards. To fly with Atmosphere Autopilot's fly-by-wire under mouse aim, untick "Keep Atmosphere Autopilot Off" in the settings.

### Installation

Place the GameData folder inside your Kerbal Space Program folder. Remove any older version first, including the original Mouse Aim Flight.

### Requirements

Kerbal Space Program 1.12.5. No other mods needed.

### Credits

Mouse Aim Flight Redux continues Mouse Aim Flight by tetryds and ferram4, with parts of the aim handling from BahamutoD's BDArmory and fixes by yut23. It is an independent project, not endorsed by the original authors.

### License

Mouse Aim Flight Redux is All Rights Reserved. The full terms are in [LICENSE](https://github.com/aspctt/MouseAimFlightRedux/blob/main/LICENSE), and the short version is:

- Download it and play.
- Your own mods, tools, configs and ModuleManager patches that work with it are yours, on any terms you like, with no credit needed.
- Modpacks and mod managers like CKAN may link to it, with the download coming from an official page. Re-hosting or altering it is not allowed.
- The code and assets stay reserved. The source is public to read, not to reuse.

The parts that come from the original under the BSD 2-Clause License keep that license. Those terms, along with copyrights and trademarks, are in [NOTICE](https://github.com/aspctt/MouseAimFlightRedux/blob/main/NOTICE).

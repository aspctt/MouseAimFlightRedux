/*
Copyright (c) 2016, BahamutoD, ferram4, tetryds
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

* Redistributions of source code must retain the above copyright notice, this
  list of conditions and the following disclaimer.

* Redistributions in binary form must reproduce the above copyright notice,
  this list of conditions and the following disclaimer in the documentation
  and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

using MouseAimFlightRedux.Control;
using MouseAimFlightRedux.UI;
using UnityEngine;

namespace MouseAimFlightRedux;

/// <summary>
/// Runs mouse aim for whichever vessel is active: the hotkeys, the cursor, the aim, and the inputs sent each physics
/// frame. One per flight scene. See docs/DESIGN.md, "Structure".
/// </summary>
[KSPAddon(KSPAddon.Startup.Flight, false)]
sealed class MouseAimPilot : MonoBehaviour
{
	readonly AimTracker aim = new();
	readonly Autopilot autopilot = new();
	readonly ControlSurfaceBoost boost = new();
	readonly AutopilotLockout lockout = new();

	Vessel vessel;
	VesselDynamics dynamics;
	bool active;

	/// <summary>Set in the physics callback while the pilot holds pitch or yaw, read by the aim in Update.</summary>
	bool pilotOverride;

	void Start()
	{
		GameEvents.onVesselWasModified.Add(OnVesselModified);
	}

	void OnDestroy()
	{
		GameEvents.onVesselWasModified.Remove(OnVesselModified);
		Bind(null);
	}

	void Update()
	{
		var current = FlightGlobals.ActiveVessel;
		if (current != vessel)
			Bind(current);
		if (vessel == null)
			return;

		// The pause menu and a kerbal on EVA both need the cursor back.
		if (PauseMenu.isOpen || vessel.isEVA)
		{
			if (active)
				SetActive(false, true);
			return;
		}

		var settings = Settings.Instance;
		var hotkeys = !MapView.MapIsEnabled && !InputLockManager.IsAllLocked(ControlTypes.KEYBOARDINPUT);
		if (hotkeys && Input.GetKeyDown(settings.ToggleKey))
			SetActive(!active, true);

		if (hotkeys && Input.GetKeyDown(settings.ModeKey))
		{
			autopilot.Reset();
			ScreenMessages.PostScreenMessage("Flight mode: " + FlightModes.Next().Name);
		}

		if (!active)
			return;

		if (pilotOverride)
			aim.Recentre(vessel);
		else
			aim.Follow(settings, FlightCamera.fetch.mainCamera.transform);
	}

	void LateUpdate()
	{
		if (vessel == null)
			return;

		// After every Update, so SAS or Atmosphere Autopilot switched on this frame is off again before physics runs.
		if (active)
			lockout.Hold();

		if (MapView.MapIsEnabled || PauseMenu.isOpen)
			return;

		// KSP's own mouse look frees the cursor, so take it back whenever free look starts or ends.
		if (aim.UpdateFreeLook() && active)
			LockCursor(true);
	}

	void OnGUI()
	{
		if (active && vessel != null && !MapView.MapIsEnabled)
			Hud.Draw(vessel, aim.Aim, FlightCamera.fetch.mainCamera);
	}

	void SetActive(bool on, bool announce)
	{
		active = on && vessel != null;
		LockCursor(active);
		autopilot.Reset();
		pilotOverride = false;

		if (active)
		{
			aim.Recentre(vessel);
			dynamics.Invalidate();
			boost.Apply(vessel);
			lockout.Engage(vessel);
			if (announce)
				ScreenMessages.PostScreenMessage("Mouse aim: " + FlightModes.Current.Name);
		}
		else
		{
			boost.Restore();
			lockout.Release();
			if (announce)
				ScreenMessages.PostScreenMessage("Mouse aim: Off");
		}
	}

	/// <summary>Moves mouse aim to another vessel, switching it off first. Null lets go of the current one.</summary>
	void Bind(Vessel next)
	{
		if (active)
			SetActive(false, next != null);
		if (vessel != null)
			vessel.OnPreAutopilotUpdate -= Fly;

		vessel = next;
		dynamics = vessel != null ? new VesselDynamics(vessel) : null;
		// The earliest of the vessel's control callbacks, so autopilots on the later ones, like Atmosphere Autopilot
		// flying together with mouse aim, take its output as their input whichever of them hooked in first.
		if (vessel != null)
			vessel.OnPreAutopilotUpdate += Fly;
	}

	void Fly(FlightCtrlState s)
	{
		if (!active || PauseMenu.isOpen || vessel != FlightGlobals.ActiveVessel)
			return;

		if (s.pitch != s.pitchTrim || s.yaw != s.yawTrim)
		{
			pilotOverride = true;
			autopilot.Reset();
			return;
		}
		pilotOverride = false;

		var dt = TimeWarp.fixedDeltaTime;
		dynamics.Update(dt);
		if (!dynamics.Valid)
			return;

		autopilot.Drive(dynamics, aim.Aim, FlightModes.Current, dt, out var pitch, out var yaw, out var roll);

		s.pitch = pitch;
		s.yaw = yaw;
		if (s.roll == s.rollTrim)
			s.roll = roll;

		autopilot.Applied(s.pitch, s.yaw, s.roll);
	}

	void OnVesselModified(Vessel modified)
	{
		if (!active || modified != vessel)
			return;

		boost.Refresh();
		dynamics.Invalidate();
	}

	static void LockCursor(bool locked)
	{
		Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
		Cursor.visible = !locked;
	}
}

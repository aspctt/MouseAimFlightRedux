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

//// Dependencies

using MouseAimFlightRedux.Control;
using MouseAimFlightRedux.UI;
using UnityEngine;

namespace MouseAimFlightRedux;

/// <summary>
/// Runs mouse aim for whichever vessel is active: the hotkeys, the cursor, the aim, and
/// the inputs sent each physics frame. One per flight scene. See docs/DESIGN.md,
/// "Structure".
/// </summary>
[KSPAddon(KSPAddon.Startup.Flight, false)]
sealed class MouseAimPilot : MonoBehaviour
{
	//// References and State

	readonly AimTracker aim = new();
	readonly AimCamera aimCamera = new();
	readonly Autopilot autopilot = new();
	readonly ControlSurfaceBoost boost = new();
	readonly AutopilotLockout lockout = new();
	readonly TuningOverlay tuning = new();

	Vessel? vessel;
	VesselDynamics? dynamics;
	BodyTerrain? terrain;
	TerrainAvoidance? avoidance;
	bool isActive;

	/// <summary>
	/// Set in the physics callback while the pilot holds pitch or yaw, read by the aim in
	/// Update.
	/// </summary>
	bool isPilotOverriding;

	//// Private Functions

	static void LockCursor(bool isLocked)
	{
		Cursor.lockState = isLocked ? CursorLockMode.Locked : CursorLockMode.None;
		Cursor.visible = !isLocked;
	}

	void SetActive(bool shouldBeActive, bool shouldAnnounce)
	{
		// Sanity check
		if (shouldBeActive == isActive)
			return;

		// Switch over
		isActive = shouldBeActive && vessel != null;
		LockCursor(isActive);
		autopilot.Reset();
		aimCamera.Release();
		isPilotOverriding = false;

		// Turn off, giving back whatever was taken over
		if (!isActive || vessel == null)
		{
			boost.Restore();
			lockout.Release();
			if (shouldAnnounce)
				ScreenMessages.PostScreenMessage("Mouse aim: Off");
			return;
		}

		// Turn on
		aim.Recentre(vessel);
		dynamics?.Invalidate();
		avoidance?.Reset();
		tuning.Clear();
		boost.Apply(vessel);
		lockout.Engage(vessel);
		if (shouldAnnounce)
			ScreenMessages.PostScreenMessage("Mouse aim: " + FlightModes.Current.Name);
	}

	/// <summary>
	/// Moves mouse aim to another vessel, switching it off first. Null lets go of the
	/// current one.
	/// </summary>
	void Bind(Vessel? next)
	{
		// Let go of the current vessel
		SetActive(false, next != null);
		if (vessel != null)
			vessel.OnPreAutopilotUpdate -= OnPreAutopilotUpdate;

		// Take the next one
		// Mouse aim flies in the earliest of the vessel's control callbacks, so
		// autopilots on the later ones, like Atmosphere Autopilot flying together with
		// mouse aim, take its output as their input whichever of them hooked in first.
		vessel = next;
		dynamics = next != null ? new VesselDynamics(next) : null;
		terrain = next != null ? new BodyTerrain(next) : null;
		avoidance = next != null ? new TerrainAvoidance() : null;
		if (next != null)
			next.OnPreAutopilotUpdate += OnPreAutopilotUpdate;
	}

	void OnPreAutopilotUpdate(FlightCtrlState state)
	{
		// Sanity check
		if (!isActive || dynamics == null || terrain == null || avoidance == null || vessel == null || PauseMenu.isOpen || vessel != FlightGlobals.ActiveVessel)
			return;

		// Let the pilot take over pitch and yaw, even from a recovery
		if (state.pitch != state.pitchTrim || state.yaw != state.yawTrim)
		{
			isPilotOverriding = true;
			autopilot.Reset();
			avoidance.Reset();
			return;
		}
		isPilotOverriding = false;

		// Measure the vessel
		var deltaTime = TimeWarp.fixedDeltaTime;
		dynamics.Update(deltaTime);
		if (!dynamics.IsValid)
			return;

		// Keep out of the ground, unless landing or landed
		var mode = FlightModes.Current;
		var isAvoidingTerrain = Settings.Instance.ShouldAvoidTerrain && !vessel.ActionGroups[KSPActionGroup.Gear] && !vessel.LandedOrSplashed;
		var target = avoidance.Update(dynamics, terrain, aim.Aim, mode, autopilot.PitchInput, isAvoidingTerrain, deltaTime);

		// Fly toward the aim, leaving roll to the pilot while they hold it
		autopilot.Drive(dynamics, target, avoidance.FlownMode(mode), deltaTime, out var pitch, out var yaw, out var roll);
		state.pitch = pitch;
		state.yaw = yaw;
		if (state.roll == state.rollTrim)
			state.roll = roll;

		// Feed back what reached the vessel
		autopilot.Applied(state.pitch, state.yaw, state.roll);
		tuning.Record(autopilot);
	}

	void OnVesselModified(Vessel modified)
	{
		// Sanity check
		if (!isActive || modified != vessel)
			return;

		// Measure it again
		boost.Refresh();
		dynamics?.Invalidate();
	}

	//// Event Wiring

	void Start()
	{
		GameEvents.onVesselWasModified.Add(OnVesselModified);
	}

	void OnDestroy()
	{
		GameEvents.onVesselWasModified.Remove(OnVesselModified);
		Bind(null);
		tuning.Destroy();
	}

	void Update()
	{
		// Follow the active vessel
		var activeVessel = FlightGlobals.ActiveVessel;
		if (activeVessel != vessel)
			Bind(activeVessel);
		if (vessel == null)
			return;

		// Give the cursor back for the pause menu and a kerbal on EVA
		if (PauseMenu.isOpen || vessel.isEVA)
		{
			SetActive(false, true);
			return;
		}

		// Read the hotkeys
		// The mode key is only read while on, so the default O never also reaches
		// Atmosphere Autopilot, which reads O while its fly-by-wire is on.
		var settings = Settings.Instance;
		var canUseHotkeys = !MapView.MapIsEnabled && !InputLockManager.IsAllLocked(ControlTypes.KEYBOARDINPUT);
		if (canUseHotkeys && Input.GetKeyDown(settings.ToggleKey))
			SetActive(!isActive, true);
		if (canUseHotkeys && isActive && Input.GetKeyDown(settings.ModeKey))
		{
			autopilot.Reset();
			ScreenMessages.PostScreenMessage("Flight mode: " + FlightModes.Next().Name);
		}

		// Move the aim
		if (!isActive)
			return;
		if (isPilotOverriding)
			aim.Recentre(vessel);
		else
			aim.Follow(settings, FlightCamera.fetch.mainCamera.transform);

		// Swing the camera round behind it, leaving it to the player in free look
		// Real time, so the camera feels the same in physics warp.
		if (settings.ShouldCameraFollowAim && !aim.IsFreeLooking)
			aimCamera.Follow(vessel, aim.Aim, Time.unscaledDeltaTime);
		else
			aimCamera.Release();
	}

	void LateUpdate()
	{
		// Sanity check
		if (vessel == null)
			return;

		// Switch SAS and Atmosphere Autopilot back off
		// After every Update, so either one switched on this frame is off again before
		// physics runs.
		if (isActive)
			lockout.Hold();

		// Take the cursor back from KSP's own mouse look
		// Its free look frees the cursor, so take it back whenever free look starts or
		// ends.
		if (MapView.MapIsEnabled || PauseMenu.isOpen)
			return;
		if (aim.UpdateFreeLook() && isActive)
			LockCursor(true);
	}

	void OnGUI()
	{
		// Sanity check
		if (vessel == null || dynamics == null || avoidance == null || MapView.MapIsEnabled)
			return;

		// Draw the markers and the tuning overlay
		if (isActive)
			Hud.Draw(vessel, aim.Aim, avoidance.IsRecovering, FlightCamera.fetch.mainCamera);
		if (Settings.Instance.ShouldShowTuningOverlay)
			tuning.Draw(isActive, FlightModes.Current, dynamics, autopilot, avoidance, aimCamera, vessel);
	}
}

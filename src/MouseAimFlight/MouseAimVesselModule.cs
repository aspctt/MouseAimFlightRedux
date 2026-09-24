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

using MouseAimFlight.Control;
using UnityEngine;

namespace MouseAimFlight
{
	public class MouseAimVesselModule : VesselModule
	{
		/// <summary>How far ahead of the centre of mass the aim point sits, m.</summary>
		const float AimDistance = 5000f;

		Transform vesselTransform;

		Autopilot autopilot;
		VesselDynamics dynamics;

		static Vessel prevActiveVessel = null;
		bool mouseAimActive = false;
		static bool freeLook = false;
		static bool prevFreeLook = false;
		static bool forceCursorResetNextFrame = false;
		static bool pitchYawOverrideMouseAim = false;

		Vector3 targetPosition;
		Vector3 mouseAimScreenLocation;
		Vector3 vesselForwardScreenLocation;

		void ToggleMouseAim() //Mouse aim must not be toggled by anything other than this function
		{
			mouseAimActive = !mouseAimActive;
			if (mouseAimActive)
			{
				Cursor.lockState = CursorLockMode.Locked;
				Cursor.visible = false;
				autopilot.Reset();
				dynamics.Invalidate();
				ScreenMessages.PostScreenMessage("MAF Enabled: " + FlightModes.Current.Name);
			}
			else
			{
				Cursor.lockState = CursorLockMode.None;
				Cursor.visible = true;
				ScreenMessages.PostScreenMessage("MAF Disabled");
			}
			targetPosition = vesselTransform.up * AimDistance;     //if it's activated, set it to the baseline
			UpdateCursorScreenLocation();
			TweakControlSurfaces(mouseAimActive); //Remove when stock control surfaces are fixed
		}

		protected override void OnStart()
		{
			base.OnStart();

			autopilot = new Autopilot();
			dynamics = new VesselDynamics(vessel);
			vessel.OnAutopilotUpdate += MouseAimPilot;

			vesselTransform = vessel.ReferenceTransform;
			targetPosition = vesselTransform.up * AimDistance;     //if it's activated, set it to the baseline
		}

		void OnGUI()
		{
			if (vessel == FlightGlobals.ActiveVessel && mouseAimActive && !MapView.MapIsEnabled)
			{
				MouseAimFlightSceneGUI.DisplayMouseAimReticles(mouseAimScreenLocation, vesselForwardScreenLocation);
			}
		}

		void Update()
		{
			if ((vessel != FlightGlobals.ActiveVessel) || vessel.isEVA)
			{
				if (mouseAimActive)
					ToggleMouseAim();
				return;
			}

			if (PauseMenu.isOpen)
			{
				if (mouseAimActive)
					ToggleMouseAim();
				return;
			}

			var settings = Settings.Instance;
			bool enableHotkeys = !MapView.MapIsEnabled && !InputLockManager.IsAllLocked(ControlTypes.KEYBOARDINPUT);
			if (vessel == FlightGlobals.ActiveVessel && vessel != prevActiveVessel)
			{
				prevActiveVessel = vessel;
				if (mouseAimActive)
				{
					Cursor.lockState = CursorLockMode.Locked;
					Cursor.visible = false;
				}
				else
				{
					Cursor.lockState = CursorLockMode.None;
					Cursor.visible = true;
				}
			}
			else if (enableHotkeys && Input.GetKeyDown(settings.ToggleKey))
			{
				ToggleMouseAim();
			}

			if (enableHotkeys && Input.GetKeyDown(settings.ModeKey))
			{
				autopilot.Reset();
				ScreenMessages.PostScreenMessage("Flight Mode: " + FlightModes.Next().Name);
			}

			if (!mouseAimActive)
				return;

			UpdateMouseCursorForCameraRotation();
			UpdateVesselScreenLocation();
			UpdateCursorScreenLocation();
		}

		void LateUpdate()
		{
			if (vessel == FlightGlobals.ActiveVessel)
				CheckResetCursor();
		}

		void MouseAimPilot(FlightCtrlState s)
		{
			if (vessel != FlightGlobals.ActiveVessel || !mouseAimActive || PauseMenu.isOpen) //Now this depends only on if mouse aim is active or not, but will leave it this way for now
				return;

			vesselTransform = vessel.ReferenceTransform;

			if (s.pitch != s.pitchTrim || s.yaw != s.yawTrim)
			{
				pitchYawOverrideMouseAim = true;
				autopilot.Reset();
				return;
			}
			else
				pitchYawOverrideMouseAim = false;

			FlyToPosition(s);
		}

		void UpdateMouseCursorForCameraRotation()
		{
			if (pitchYawOverrideMouseAim)
			{
				targetPosition = vesselTransform.up * AimDistance;
			}
			else
			{
				var settings = Settings.Instance;
				Vector3 mouseDelta;

				if (freeLook)
					mouseDelta = Vector3.zero;
				else
					mouseDelta = new Vector3(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * (settings.MouseSensitivity * Mathf.Deg2Rad * AimDistance);

				if (settings.InvertX)
					mouseDelta.x *= -1;
				if (settings.InvertY)
					mouseDelta.y *= -1;

				Transform cameraTransform = FlightCamera.fetch.mainCamera.transform;

				Vector3d localTarget = cameraTransform.InverseTransformDirection(targetPosition);
				localTarget += mouseDelta;
				localTarget.Normalize();
				localTarget *= AimDistance;

				targetPosition = cameraTransform.TransformDirection(localTarget);
			}
		}

		void UpdateCursorScreenLocation()
		{
			mouseAimScreenLocation = FlightCamera.fetch.mainCamera.WorldToScreenPoint(targetPosition + vessel.CoM);
		}

		void UpdateVesselScreenLocation()
		{
			vesselForwardScreenLocation = vesselTransform.up * AimDistance;
			vesselForwardScreenLocation = FlightCamera.fetch.mainCamera.WorldToScreenPoint(vesselForwardScreenLocation + vessel.CoM);
		}

		void CheckResetCursor()
		{
			if (MapView.MapIsEnabled || PauseMenu.isOpen)
				return;

			prevFreeLook = freeLook;

			if (Mouse.Right.GetButton())
				freeLook = true;
			else if (freeLook)
				freeLook = false;

			freeLook |= CameraMouseLook.MouseLocked;

			if ((freeLook != prevFreeLook || forceCursorResetNextFrame) && mouseAimActive)
			{
				Cursor.lockState = CursorLockMode.Locked;
				Cursor.visible = false;

				forceCursorResetNextFrame = false;
			}
		}

		void FlyToPosition(FlightCtrlState s)
		{
			var dt = TimeWarp.fixedDeltaTime;
			dynamics.Update(dt);
			if (!dynamics.Valid)
				return;

			autopilot.Drive(dynamics, targetPosition, FlightModes.Current, dt, out var pitch, out var yaw, out var roll);

			s.pitch = pitch;
			s.yaw = yaw;
			if (s.roll == s.rollTrim)
				s.roll = roll;

			autopilot.Applied(s.pitch, s.yaw, s.roll);
		}

		void TweakControlSurfaces(bool mouseFlightActive) //Tweak stock control surfaces for sane behavior
		{
			if (!Settings.FarLoaded)
			{
				if (mouseFlightActive)
				{
					foreach (var ctrlSurface in vessel.FindPartModulesImplementing<ModuleControlSurface>()) //Only use if not performance critical, really.
					{
						ctrlSurface.useExponentialSpeed = true;
						ctrlSurface.actuatorSpeed *= 3.5f;
					}
					Debug.Log("[MAF]: MAF Enabled, Control Surfaces Tweaked");
				}
				else
				{
					foreach (var ctrlSurface in vessel.FindPartModulesImplementing<ModuleControlSurface>()) //Only use if not performance critical, really.
					{
						ctrlSurface.useExponentialSpeed = false;
						ctrlSurface.actuatorSpeed /= 3.5f;
					}
					Debug.Log("[MAF]: MAF Disabled, Control Surfaces Reverted");
				}
			}
		}

		void OnDestroy()
		{
			if (vessel)
				vessel.OnAutopilotUpdate -= MouseAimPilot;
		}
	}
}

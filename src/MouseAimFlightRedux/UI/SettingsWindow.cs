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

using System;
using KSP.UI.Screens;
using MouseAimFlightRedux.Control;
using UnityEngine;

namespace MouseAimFlightRedux.UI;

/// <summary>The toolbar button and the settings window it opens.</summary>
[KSPAddon(KSPAddon.Startup.Flight, false)]
sealed class SettingsWindow : MonoBehaviour
{
	//// Types

	enum KeyBinding
	{
		None,
		Toggle,
		Mode,
	}

	//// Constants

	const string BINDING_LOCK_IDENTIFIER = "MouseAimFlightReduxKeyBinding";

	const float MARKER_ROW_HEIGHT = 30f;

	//// References and State

	/// <summary>
	/// Kept across flight scenes so the window reopens where it was left.
	/// </summary>
	static Rect windowRect;

	ApplicationLauncherButton? button;
	bool isVisible;
	KeyBinding binding;
	bool isMarkerListOpen;
	GUIStyle? markerRowStyle;
	bool isConfirmingReset;

	/// <summary>
	/// The key that just ended a binding. Unity can hand a key press to the window a
	/// frame before the game sees it, so the keyboard stays locked until the key is let
	/// go, or a new hotkey would fire straight away.
	/// </summary>
	KeyCode pendingReleaseKey;

	/// <summary>
	/// A layout window grows to fit its contents but never shrinks back on its own, so
	/// it's cut down to size after the marker list closes.
	/// </summary>
	bool shouldShrink;

	GUIStyle MarkerRowStyle => markerRowStyle ??= new GUIStyle(HighLogic.Skin.button) { alignment = TextAnchor.MiddleLeft };

	//// Private Functions

	void DrawWindow(int windowIdentifier)
	{
		// Capture what this frame shows
		// Layout and the events after it must see the same controls, so a click only
		// opens or closes the list, or the reset question, from the next frame on.
		var settings = Settings.Instance;
		var isListOpen = isMarkerListOpen;
		var isConfirming = isConfirmingReset;
		GUILayout.BeginVertical(GUILayout.Width(260));

		// Draw the hotkeys
		KeyRow("Toggle mouse aim", KeyBinding.Toggle, settings.ToggleKey);
		KeyRow("Next flight mode", KeyBinding.Mode, settings.ModeKey);
		GUILayout.Space(10);

		// Draw the flight mode
		GUILayout.BeginHorizontal();
		GUILayout.Label("Flight mode: " + FlightModes.Current.Name);
		if (GUILayout.Button("Next", GUILayout.Width(60)))
			FlightModes.Next();
		GUILayout.EndHorizontal();
		if (GUILayout.Button("Reload flight modes from disk"))
			FlightModes.ReloadFromDisk();
		GUILayout.Space(10);

		// Draw the nose marker button, its preview and its list
		GUILayout.BeginHorizontal();
		if (GUILayout.Button("Nose marker: " + settings.Reticle, GUILayout.Width(180)))
			SetMarkerListOpen(!isMarkerListOpen);
		var preview = GUILayoutUtility.GetRect(48, 48, GUILayout.Width(48), GUILayout.Height(48));
		var nose = Reticles.Nose(settings.Reticle);
		if (nose != null)
		{
			var oldColor = GUI.color;
			GUI.color = new Color(1f, 1f, 1f, settings.ReticleOpacity);
			GUI.DrawTexture(preview, nose);
			GUI.color = oldColor;
		}
		GUILayout.EndHorizontal();
		if (isListOpen)
			MarkerList(settings);
		GUILayout.Space(10);

		// Draw the sliders and toggles
		GUILayout.Label("Mouse Sensitivity: " + settings.MouseSensitivity.ToString("0.00"));
		settings.MouseSensitivity = GUILayout.HorizontalSlider(settings.MouseSensitivity, 0.25f, 5f);
		GUILayout.Label("Marker Opacity: " + settings.ReticleOpacity.ToString("0.00"));
		settings.ReticleOpacity = GUILayout.HorizontalSlider(settings.ReticleOpacity, 0f, 1f);
		GUILayout.Label("Marker Size: " + settings.ReticleSize.ToString("0.00"));
		settings.ReticleSize = GUILayout.HorizontalSlider(settings.ReticleSize, 0.4f, 1f);
		settings.ShouldInvertX = GUILayout.Toggle(settings.ShouldInvertX, "Invert X Axis");
		settings.ShouldInvertY = GUILayout.Toggle(settings.ShouldInvertY, "Invert Y Axis");
		if (AtmosphereAutopilot.IsAvailable)
			settings.ShouldKeepAtmosphereAutopilotOff = GUILayout.Toggle(settings.ShouldKeepAtmosphereAutopilotOff, "Keep Atmosphere Autopilot Off");
		settings.ShouldShowTuningOverlay = GUILayout.Toggle(settings.ShouldShowTuningOverlay, "Show Tuning Overlay");
		GUILayout.Space(10);

		// Draw the reset button
		// Asks once more first, since there's no undo.
		if (!isConfirming)
		{
			if (GUILayout.Button("Reset to defaults"))
				isConfirmingReset = true;
		}
		else
		{
			GUILayout.BeginHorizontal();
			GUILayout.Label("Reset every setting?");
			if (GUILayout.Button("Yes", GUILayout.Width(50)))
				ResetToDefaults();
			if (GUILayout.Button("No", GUILayout.Width(50)))
				isConfirmingReset = false;
			GUILayout.EndHorizontal();
		}

		GUILayout.EndVertical();
		GUI.DragWindow();
	}

	/// <summary>
	/// Opens under the nose marker button: one row per marker, its name on the left and
	/// the marker on the right.
	/// </summary>
	void MarkerList(Settings settings)
	{
		foreach (ReticleStyle style in Enum.GetValues(typeof(ReticleStyle)))
		{
			// Draw the row as a button
			var row = GUILayoutUtility.GetRect(180f, MARKER_ROW_HEIGHT, GUILayout.Width(180f), GUILayout.Height(MARKER_ROW_HEIGHT));
			var isChosen = style == settings.Reticle;
			if (GUI.Toggle(row, isChosen, style.ToString(), MarkerRowStyle) != isChosen)
			{
				settings.Reticle = style;
				SetMarkerListOpen(false);
			}

			// Draw its marker at the right end
			var marker = Reticles.Nose(style);
			if (marker != null)
			{
				var size = MARKER_ROW_HEIGHT - 6f;
				GUI.DrawTexture(new Rect(row.xMax - size - 6f, row.y + 3f, size, size), marker);
			}
		}
	}

	void ResetToDefaults()
	{
		EndBinding();
		SetMarkerListOpen(false);
		isConfirmingReset = false;
		Settings.ResetToDefaults();
	}

	void SetMarkerListOpen(bool isOpen)
	{
		if (isMarkerListOpen && !isOpen)
			shouldShrink = true;
		isMarkerListOpen = isOpen;
	}

	void KeyRow(string label, KeyBinding which, KeyCode key)
	{
		GUILayout.BeginHorizontal();
		GUILayout.Label(label);
		if (GUILayout.Button(binding == which ? "Press a key..." : key.ToString(), GUILayout.Width(110)))
		{
			if (binding == which)
				EndBinding();
			else
				BeginBinding(which);
		}
		GUILayout.EndHorizontal();
	}

	void BeginBinding(KeyBinding which)
	{
		// Keep the key being bound from also flying the vessel or firing a hotkey
		binding = which;
		pendingReleaseKey = KeyCode.None;
		InputLockManager.SetControlLock(ControlTypes.KEYBOARDINPUT, BINDING_LOCK_IDENTIFIER);
	}

	void EndBinding()
	{
		binding = KeyBinding.None;
		pendingReleaseKey = KeyCode.None;
		InputLockManager.RemoveControlLock(BINDING_LOCK_IDENTIFIER);
	}

	void CaptureKey()
	{
		// Sanity check
		var currentEvent = Event.current;
		if (currentEvent.type != EventType.KeyDown || currentEvent.keyCode == KeyCode.None)
			return;

		// Bind the key, unless it's Escape
		if (currentEvent.keyCode != KeyCode.Escape)
		{
			var settings = Settings.Instance;
			if (binding == KeyBinding.Toggle)
				settings.ToggleKey = currentEvent.keyCode;
			else
				settings.ModeKey = currentEvent.keyCode;
			settings.Save();
		}

		// Hold the lock until Update sees the key let go
		pendingReleaseKey = currentEvent.keyCode;
		binding = KeyBinding.None;
		currentEvent.Use();
	}

	void OnApplicationLauncherReady()
	{
		// Sanity check
		if (button != null || !ApplicationLauncher.Ready)
			return;

		// Add the button
		button = ApplicationLauncher.Instance.AddModApplication(OnToolbarButtonOn, OnToolbarButtonOff, null, null, null, null, ApplicationLauncher.AppScenes.FLIGHT, Reticles.Icon);
	}

	void OnApplicationLauncherDestroyed()
	{
		// Sanity check
		if (button == null)
			return;

		// Remove the button
		if (ApplicationLauncher.Instance != null)
			ApplicationLauncher.Instance.RemoveModApplication(button);
		button = null;
	}

	void OnToolbarButtonOn()
	{
		isVisible = true;
	}

	void OnToolbarButtonOff()
	{
		// Sanity check
		if (!isVisible)
			return;

		// Close the window and save
		isVisible = false;
		EndBinding();
		SetMarkerListOpen(false);
		isConfirmingReset = false;
		Settings.Instance.Save();
	}

	//// Event Wiring

	void Start()
	{
		if (windowRect.width <= 0f)
			windowRect = new Rect(Screen.width - 340f, 100f, 280f, 0f);

		GameEvents.onGUIApplicationLauncherReady.Add(OnApplicationLauncherReady);
		GameEvents.onGUIApplicationLauncherDestroyed.Add(OnApplicationLauncherDestroyed);
		if (ApplicationLauncher.Ready)
			OnApplicationLauncherReady();
	}

	void OnDestroy()
	{
		GameEvents.onGUIApplicationLauncherReady.Remove(OnApplicationLauncherReady);
		GameEvents.onGUIApplicationLauncherDestroyed.Remove(OnApplicationLauncherDestroyed);
		OnApplicationLauncherDestroyed();
		EndBinding();
		Settings.Instance.Save();
	}

	void Update()
	{
		// Sanity check
		if (binding != KeyBinding.None || pendingReleaseKey == KeyCode.None)
			return;
		if (Input.GetKey(pendingReleaseKey) || Input.GetKeyDown(pendingReleaseKey))
			return;

		// Let go of the keyboard once the bound key is up
		pendingReleaseKey = KeyCode.None;
		InputLockManager.RemoveControlLock(BINDING_LOCK_IDENTIFIER);
	}

	void OnGUI()
	{
		// Sanity check
		if (!isVisible)
			return;

		// Take a key being bound
		if (binding != KeyBinding.None)
			CaptureKey();

		// Draw the window, cut down to size if something closed
		GUI.skin = HighLogic.Skin;
		if (shouldShrink)
		{
			windowRect.height = 0f;
			shouldShrink = false;
		}
		windowRect = GUILayout.Window(GetHashCode(), windowRect, DrawWindow, "Mouse Aim Flight Redux");
	}
}

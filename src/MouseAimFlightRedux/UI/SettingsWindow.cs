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

using System;
using KSP.UI.Screens;
using MouseAimFlightRedux.Control;
using UnityEngine;

namespace MouseAimFlightRedux.UI;

/// <summary>The toolbar button and the settings window it opens.</summary>
[KSPAddon(KSPAddon.Startup.Flight, false)]
sealed class SettingsWindow : MonoBehaviour
{
	enum KeyBinding
	{
		None,
		Toggle,
		Mode,
	}

	const string BindingLockId = "MouseAimFlightReduxKeyBinding";

	const float MarkerRowHeight = 30f;

	/// <summary>Kept across flight scenes so the window reopens where it was left.</summary>
	static Rect windowRect;

	ApplicationLauncherButton button;
	bool visible;
	KeyBinding binding;
	bool markerListOpen;
	GUIStyle markerRowStyle;
	bool confirmingReset;

	/// <summary>
	/// The key that just ended a binding. Unity can hand a key press to the window a frame before the game sees it, so
	/// the keyboard stays locked until the key is let go, or a new hotkey would fire straight away.
	/// </summary>
	KeyCode releasePending;

	/// <summary>
	/// A layout window grows to fit its contents but never shrinks back on its own, so it's cut down to size after the
	/// marker list closes.
	/// </summary>
	bool shrink;

	void Start()
	{
		Reticles.Build();
		if (windowRect.width <= 0f)
			windowRect = new Rect(Screen.width - 340f, 100f, 280f, 0f);

		GameEvents.onGUIApplicationLauncherReady.Add(AddButton);
		GameEvents.onGUIApplicationLauncherDestroyed.Add(RemoveButton);
		if (ApplicationLauncher.Ready)
			AddButton();
	}

	void OnDestroy()
	{
		GameEvents.onGUIApplicationLauncherReady.Remove(AddButton);
		GameEvents.onGUIApplicationLauncherDestroyed.Remove(RemoveButton);
		RemoveButton();
		EndBinding();
		Settings.Instance.Save();
	}

	void Update()
	{
		if (binding != KeyBinding.None || releasePending == KeyCode.None)
			return;
		if (Input.GetKey(releasePending) || Input.GetKeyDown(releasePending))
			return;

		releasePending = KeyCode.None;
		InputLockManager.RemoveControlLock(BindingLockId);
	}

	void OnGUI()
	{
		if (!visible)
			return;

		if (binding != KeyBinding.None)
			CaptureKey();

		GUI.skin = HighLogic.Skin;
		markerRowStyle ??= new GUIStyle(HighLogic.Skin.button) { alignment = TextAnchor.MiddleLeft };
		if (shrink)
		{
			windowRect.height = 0f;
			shrink = false;
		}
		windowRect = GUILayout.Window(GetHashCode(), windowRect, DrawWindow, "Mouse Aim Flight Redux");
	}

	void DrawWindow(int windowId)
	{
		var settings = Settings.Instance;

		GUILayout.BeginVertical(GUILayout.Width(260));

		KeyRow("Toggle mouse aim", KeyBinding.Toggle, settings.ToggleKey);
		KeyRow("Next flight mode", KeyBinding.Mode, settings.ModeKey);

		GUILayout.Space(10);

		GUILayout.BeginHorizontal();
		GUILayout.Label("Flight mode: " + FlightModes.Current.Name);
		if (GUILayout.Button("Next", GUILayout.Width(60)))
			FlightModes.Next();
		GUILayout.EndHorizontal();
		if (GUILayout.Button("Reload flight modes from disk"))
			FlightModes.ReloadFromDisk();

		GUILayout.Space(10);

		// Layout and the events after it must see the same controls, so a click only opens or closes the list, or the
		// reset question, from the next frame on.
		var listOpen = markerListOpen;
		var confirming = confirmingReset;
		GUILayout.BeginHorizontal();
		if (GUILayout.Button("Nose marker: " + settings.Reticle, GUILayout.Width(180)))
			SetMarkerListOpen(!markerListOpen);
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

		if (listOpen)
			MarkerList(settings);

		GUILayout.Space(10);

		GUILayout.Label("Mouse Sensitivity: " + settings.MouseSensitivity.ToString("0.00"));
		settings.MouseSensitivity = GUILayout.HorizontalSlider(settings.MouseSensitivity, 0.25f, 5f);
		GUILayout.Label("Marker Opacity: " + settings.ReticleOpacity.ToString("0.00"));
		settings.ReticleOpacity = GUILayout.HorizontalSlider(settings.ReticleOpacity, 0f, 1f);
		GUILayout.Label("Marker Size: " + settings.ReticleSize.ToString("0.00"));
		settings.ReticleSize = GUILayout.HorizontalSlider(settings.ReticleSize, 0.4f, 1f);
		settings.InvertX = GUILayout.Toggle(settings.InvertX, "Invert X Axis");
		settings.InvertY = GUILayout.Toggle(settings.InvertY, "Invert Y Axis");
		if (AtmosphereAutopilot.Available)
			settings.KeepAtmosphereAutopilotOff = GUILayout.Toggle(settings.KeepAtmosphereAutopilotOff, "Keep Atmosphere Autopilot Off");

		GUILayout.Space(10);

		// Asks once more first, since there's no undo.
		if (!confirming)
		{
			if (GUILayout.Button("Reset to defaults"))
				confirmingReset = true;
		}
		else
		{
			GUILayout.BeginHorizontal();
			GUILayout.Label("Reset every setting?");
			if (GUILayout.Button("Yes", GUILayout.Width(50)))
				ResetToDefaults();
			if (GUILayout.Button("No", GUILayout.Width(50)))
				confirmingReset = false;
			GUILayout.EndHorizontal();
		}

		GUILayout.EndVertical();

		GUI.DragWindow();
	}

	/// <summary>Opens under the nose marker button: one row per marker, its name on the left and the marker on the right.</summary>
	void MarkerList(Settings settings)
	{
		foreach (ReticleStyle style in Enum.GetValues(typeof(ReticleStyle)))
		{
			var row = GUILayoutUtility.GetRect(180f, MarkerRowHeight, GUILayout.Width(180f), GUILayout.Height(MarkerRowHeight));
			var chosen = style == settings.Reticle;
			if (GUI.Toggle(row, chosen, style.ToString(), markerRowStyle) != chosen)
			{
				settings.Reticle = style;
				SetMarkerListOpen(false);
			}

			var marker = Reticles.Nose(style);
			if (marker != null)
			{
				var size = MarkerRowHeight - 6f;
				GUI.DrawTexture(new Rect(row.xMax - size - 6f, row.y + 3f, size, size), marker);
			}
		}
	}

	void ResetToDefaults()
	{
		EndBinding();
		SetMarkerListOpen(false);
		confirmingReset = false;
		Settings.ResetToDefaults();
	}

	void SetMarkerListOpen(bool open)
	{
		if (markerListOpen && !open)
			shrink = true;
		markerListOpen = open;
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
		binding = which;
		releasePending = KeyCode.None;
		// Keep the key being bound from also flying the vessel or firing a hotkey.
		InputLockManager.SetControlLock(ControlTypes.KEYBOARDINPUT, BindingLockId);
	}

	void EndBinding()
	{
		binding = KeyBinding.None;
		releasePending = KeyCode.None;
		InputLockManager.RemoveControlLock(BindingLockId);
	}

	void CaptureKey()
	{
		var current = Event.current;
		if (current.type != EventType.KeyDown || current.keyCode == KeyCode.None)
			return;

		if (current.keyCode != KeyCode.Escape)
		{
			var settings = Settings.Instance;
			if (binding == KeyBinding.Toggle)
				settings.ToggleKey = current.keyCode;
			else
				settings.ModeKey = current.keyCode;
			settings.Save();
		}

		// The lock stays on until Update sees the key let go.
		releasePending = current.keyCode;
		binding = KeyBinding.None;
		current.Use();
	}

	void AddButton()
	{
		if (button != null || !ApplicationLauncher.Ready)
			return;

		button = ApplicationLauncher.Instance.AddModApplication(
			Show,
			Hide,
			null,
			null,
			null,
			null,
			ApplicationLauncher.AppScenes.FLIGHT,
			Reticles.Icon);
	}

	void RemoveButton()
	{
		if (button == null)
			return;

		if (ApplicationLauncher.Instance != null)
			ApplicationLauncher.Instance.RemoveModApplication(button);
		button = null;
	}

	void Show()
	{
		visible = true;
	}

	void Hide()
	{
		visible = false;
		EndBinding();
		SetMarkerListOpen(false);
		confirmingReset = false;
		Settings.Instance.Save();
	}
}

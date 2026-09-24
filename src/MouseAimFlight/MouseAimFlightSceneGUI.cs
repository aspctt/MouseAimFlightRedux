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

using KSP.UI.Screens;
using MouseAimFlight.Control;
using UnityEngine;

namespace MouseAimFlight
{
	[KSPAddon(KSPAddon.Startup.Flight, false)]
	class MouseAimFlightSceneGUI : MonoBehaviour
	{
		enum KeyBinding
		{
			None,
			Toggle,
			Mode,
		}

		const string BindingLockId = "MouseAimFlightKeyBinding";

		static Rect guiRect;

		ApplicationLauncherButton button;
		bool showGUI;
		KeyBinding binding;

		void Start()
		{
			Reticles.Build();
			if (guiRect.width <= 0f)
				guiRect = new Rect(Screen.width - 340f, 100f, 280f, 0f);

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

		public static void DisplayMouseAimReticles(Vector3 mouseAimScreenLocation, Vector3 vesselForwardScreenLocation)
		{
			Reticles.Build();
			var settings = Settings.Instance;
			float size = settings.ReticleSize * Screen.width / 32;
			Color oldcolor = GUI.color;
			GUI.color = new Color(1, 1, 1, settings.ReticleOpacity);

			if (mouseAimScreenLocation.z > 0)
				GUI.DrawTexture(ScreenRect(mouseAimScreenLocation, size), Reticles.Aim);

			var nose = Reticles.Nose(settings.Reticle);
			if (nose != null && vesselForwardScreenLocation.z > 0)
				GUI.DrawTexture(ScreenRect(vesselForwardScreenLocation, size), nose);

			GUI.color = oldcolor;
		}

		static Rect ScreenRect(Vector3 screenLocation, float size)
		{
			return new Rect(screenLocation.x - (0.5f * size), (Screen.height - screenLocation.y) - (0.5f * size), size, size);
		}

		void OnGUI()
		{
			if (!showGUI)
				return;

			if (binding != KeyBinding.None)
				CaptureKey();

			GUI.skin = HighLogic.Skin;
			guiRect = GUILayout.Window(GetHashCode(), guiRect, GUIWindow, "Mouse Aim Flight");
		}

		void GUIWindow(int windowID)
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

			GUILayout.BeginHorizontal();
			if (GUILayout.Button("Nose marker: " + settings.Reticle, GUILayout.Width(180)))
				settings.Reticle = (ReticleStyle)(((int)settings.Reticle + 1) % 3);
			var preview = GUILayoutUtility.GetRect(48, 48, GUILayout.Width(48), GUILayout.Height(48));
			var nose = Reticles.Nose(settings.Reticle);
			if (nose != null)
			{
				Color oldcolor = GUI.color;
				GUI.color = new Color(1, 1, 1, settings.ReticleOpacity);
				GUI.DrawTexture(preview, nose);
				GUI.color = oldcolor;
			}
			GUILayout.EndHorizontal();

			GUILayout.Space(10);

			GUILayout.Label("Mouse Sensitivity: " + settings.MouseSensitivity.ToString("0.00"));
			settings.MouseSensitivity = GUILayout.HorizontalSlider(settings.MouseSensitivity, 0.25f, 5f);
			GUILayout.Label("Marker Opacity: " + settings.ReticleOpacity.ToString("0.00"));
			settings.ReticleOpacity = GUILayout.HorizontalSlider(settings.ReticleOpacity, 0f, 1f);
			GUILayout.Label("Marker Size: " + settings.ReticleSize.ToString("0.00"));
			settings.ReticleSize = GUILayout.HorizontalSlider(settings.ReticleSize, 0.4f, 1f);
			settings.InvertX = GUILayout.Toggle(settings.InvertX, "Invert X Axis");
			settings.InvertY = GUILayout.Toggle(settings.InvertY, "Invert Y Axis");

			GUILayout.EndVertical();

			GUI.DragWindow();
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
			// Keep the key being bound from also flying the vessel or firing a hotkey.
			InputLockManager.SetControlLock(ControlTypes.KEYBOARDINPUT, BindingLockId);
		}

		void EndBinding()
		{
			binding = KeyBinding.None;
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

			current.Use();
			EndBinding();
		}

		#region AppLauncher
		void AddButton()
		{
			if (button != null || !ApplicationLauncher.Ready)
				return;

			button = ApplicationLauncher.Instance.AddModApplication(
				onAppLaunchToggleOn,
				onAppLaunchToggleOff,
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

		void onAppLaunchToggleOn()
		{
			showGUI = true;
		}

		void onAppLaunchToggleOff()
		{
			showGUI = false;
			EndBinding();
			Settings.Instance.Save();
		}
		#endregion
	}
}

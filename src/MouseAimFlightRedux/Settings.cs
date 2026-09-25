//// Dependencies

using System;
using System.IO;
using UnityEngine;

namespace MouseAimFlightRedux;

//// Types

public enum ReticleStyle
{
	Crosshair,
	Cross,
	Dot,
	None,
}

/// <summary>
/// The player's settings. Saved under PluginData, which KSP does not load as config and
/// an update of the mod never overwrites. Anything missing from the file keeps its
/// default.
/// </summary>
public sealed class Settings
{
	//// Constants

	const string NODE_NAME = "MOUSE_AIM_FLIGHT_REDUX_SETTINGS";

	/// <summary>
	/// The settings file format. 2 moved the default toggle key from P, which Atmosphere
	/// Autopilot also uses, to Y.
	/// </summary>
	const int FILE_VERSION = 2;

	//// References and State

	static Settings? instance;
	static bool? isFerramAerospaceResearchLoaded;

	public KeyCode ToggleKey = KeyCode.Y;
	public KeyCode ModeKey = KeyCode.O;
	public float MouseSensitivity = 1f;
	public bool ShouldInvertX;
	public bool ShouldInvertY;
	public ReticleStyle Reticle = ReticleStyle.Crosshair;
	public float ReticleOpacity = 1f;
	public float ReticleSize = 0.75f;
	public bool ShouldKeepAtmosphereAutopilotOff = true;
	public bool ShouldAvoidTerrain;
	public bool ShouldShowTuningOverlay;

	static string FilePath => Path.Combine(KSPUtil.ApplicationRootPath, "GameData", "MouseAimFlightRedux", "PluginData", "Settings.cfg");

	//// Private Functions

	static Settings Load()
	{
		// Find the settings node
		var settings = new Settings();
		var path = FilePath;
		if (!File.Exists(path))
			return settings;

		var node = ConfigNode.Load(path)?.GetNode(NODE_NAME);
		if (node == null)
		{
			Debug.LogWarning($"[MouseAimFlightRedux] {path} has no {NODE_NAME} node, using defaults");
			return settings;
		}

		// Read the keys, moving the old default toggle key
		// Every file is saved in full on leaving flight, so an older one holds the old
		// default whether or not it was chosen.
		var version = 1;
		node.TryGetValue("version", ref version);
		ReadEnum(node, "toggleKey", ref settings.ToggleKey);
		if (version < 2 && settings.ToggleKey == KeyCode.P)
			settings.ToggleKey = KeyCode.Y;
		ReadEnum(node, "modeKey", ref settings.ModeKey);

		// Read everything else
		node.TryGetValue("mouseSensitivity", ref settings.MouseSensitivity);
		node.TryGetValue("invertX", ref settings.ShouldInvertX);
		node.TryGetValue("invertY", ref settings.ShouldInvertY);
		ReadEnum(node, "reticle", ref settings.Reticle);
		node.TryGetValue("reticleOpacity", ref settings.ReticleOpacity);
		node.TryGetValue("reticleSize", ref settings.ReticleSize);
		node.TryGetValue("keepAtmosphereAutopilotOff", ref settings.ShouldKeepAtmosphereAutopilotOff);
		node.TryGetValue("avoidTerrain", ref settings.ShouldAvoidTerrain);
		node.TryGetValue("tuningOverlay", ref settings.ShouldShowTuningOverlay);
		return settings;
	}

	static void ReadEnum<T>(ConfigNode node, string key, ref T value) where T : struct
	{
		// Sanity check
		var text = node.GetValue(key);
		if (text == null)
			return;

		// Parse it, keeping the default for anything unknown
		if (Enum.TryParse(text, true, out T parsed) && Enum.IsDefined(typeof(T), parsed))
			value = parsed;
		else
			Debug.LogWarning($"[MouseAimFlightRedux] Ignoring {key} = {text} in settings, not a valid value");
	}

	/// <summary>Whether KSP loaded a plugin assembly with this name.</summary>
	static bool IsAssemblyLoaded(string name)
	{
		foreach (var loaded in AssemblyLoader.loadedAssemblies)
		{
			if (loaded.assembly.GetName().Name == name)
				return true;
		}
		return false;
	}

	//// Public API

	public static Settings Instance => instance ??= Load();

	/// <summary>
	/// Ferram Aerospace Research replaces stock aerodynamics, including how control
	/// surfaces respond.
	/// </summary>
	public static bool IsFerramAerospaceResearchLoaded => isFerramAerospaceResearchLoaded ??= IsAssemblyLoaded("FerramAerospaceResearch");

	/// <summary>Puts every setting back to its default and saves.</summary>
	public static void ResetToDefaults()
	{
		instance = new Settings();
		instance.Save();
	}

	public void Save()
	{
		// Write every setting
		var node = new ConfigNode(NODE_NAME);
		node.AddValue("version", FILE_VERSION);
		node.AddValue("toggleKey", ToggleKey.ToString());
		node.AddValue("modeKey", ModeKey.ToString());
		node.AddValue("mouseSensitivity", MouseSensitivity);
		node.AddValue("invertX", ShouldInvertX);
		node.AddValue("invertY", ShouldInvertY);
		node.AddValue("reticle", Reticle.ToString());
		node.AddValue("reticleOpacity", ReticleOpacity);
		node.AddValue("reticleSize", ReticleSize);
		node.AddValue("keepAtmosphereAutopilotOff", ShouldKeepAtmosphereAutopilotOff);
		node.AddValue("avoidTerrain", ShouldAvoidTerrain);
		node.AddValue("tuningOverlay", ShouldShowTuningOverlay);

		var root = new ConfigNode();
		root.AddNode(node);

		// Save it to disk
		var path = FilePath;
		Directory.CreateDirectory(Path.GetDirectoryName(path));
		if (!root.Save(path))
			Debug.LogError($"[MouseAimFlightRedux] Could not save settings to {path}");
	}
}

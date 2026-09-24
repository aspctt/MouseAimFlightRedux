using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MouseAimFlight;

public enum ReticleStyle
{
	Cross,
	Dot,
	None,
}

/// <summary>
/// The player's settings. Saved under PluginData, which KSP does not load as config and an update of the mod never
/// overwrites. Anything missing from the file keeps its default.
/// </summary>
public sealed class Settings
{
	const string NodeName = "MOUSE_AIM_FLIGHT_SETTINGS";

	static Settings instance;
	static bool? farLoaded;

	public static Settings Instance => instance ??= Load();

	/// <summary>Ferram Aerospace Research replaces stock aerodynamics, including how control surfaces respond.</summary>
	public static bool FarLoaded => farLoaded ??= AssemblyLoader.loadedAssemblies.Any(a => a.assembly.GetName().Name == "FerramAerospaceResearch");

	public KeyCode ToggleKey = KeyCode.P;
	public KeyCode ModeKey = KeyCode.O;
	public string Mode = "Normal";
	public float MouseSensitivity = 1f;
	public bool InvertX;
	public bool InvertY;
	public ReticleStyle Reticle = ReticleStyle.Cross;
	public float ReticleOpacity = 1f;
	public float ReticleSize = 0.75f;

	static string FilePath => Path.Combine(KSPUtil.ApplicationRootPath, "GameData", "MouseAimFlight", "PluginData", "Settings.cfg");

	static Settings Load()
	{
		var settings = new Settings();
		var path = FilePath;
		if (!File.Exists(path))
			return settings;

		var node = ConfigNode.Load(path)?.GetNode(NodeName);
		if (node == null)
		{
			Debug.LogWarning($"[MouseAimFlight] {path} has no {NodeName} node, using defaults");
			return settings;
		}

		ReadEnum(node, "toggleKey", ref settings.ToggleKey);
		ReadEnum(node, "modeKey", ref settings.ModeKey);
		node.TryGetValue("mode", ref settings.Mode);
		node.TryGetValue("mouseSensitivity", ref settings.MouseSensitivity);
		node.TryGetValue("invertX", ref settings.InvertX);
		node.TryGetValue("invertY", ref settings.InvertY);
		ReadEnum(node, "reticle", ref settings.Reticle);
		node.TryGetValue("reticleOpacity", ref settings.ReticleOpacity);
		node.TryGetValue("reticleSize", ref settings.ReticleSize);
		return settings;
	}

	public void Save()
	{
		var node = new ConfigNode(NodeName);
		node.AddValue("toggleKey", ToggleKey.ToString());
		node.AddValue("modeKey", ModeKey.ToString());
		node.AddValue("mode", Mode);
		node.AddValue("mouseSensitivity", MouseSensitivity);
		node.AddValue("invertX", InvertX);
		node.AddValue("invertY", InvertY);
		node.AddValue("reticle", Reticle.ToString());
		node.AddValue("reticleOpacity", ReticleOpacity);
		node.AddValue("reticleSize", ReticleSize);

		var root = new ConfigNode();
		root.AddNode(node);

		var path = FilePath;
		Directory.CreateDirectory(Path.GetDirectoryName(path));
		if (!root.Save(path))
			Debug.LogError($"[MouseAimFlight] Could not save settings to {path}");
	}

	static void ReadEnum<T>(ConfigNode node, string key, ref T value) where T : struct
	{
		var text = node.GetValue(key);
		if (text == null)
			return;
		if (Enum.TryParse(text, true, out T parsed) && Enum.IsDefined(typeof(T), parsed))
			value = parsed;
		else
			Debug.LogWarning($"[MouseAimFlight] Ignoring {key} = {text} in settings, not a valid value");
	}
}

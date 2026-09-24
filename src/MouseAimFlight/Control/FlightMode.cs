using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MouseAimFlight.Control;

/// <summary>
/// Limits and response times for the controller. Angles in degrees, rates in degrees per second, times in seconds.
/// See docs/DESIGN.md, "Flight modes".
/// </summary>
public sealed class FlightMode
{
	public const string NodeName = "MOUSE_AIM_FLIGHT_MODE";

	public string Name = "Normal";
	public float MaxPitchRate = 25f;
	public float MaxYawRate = 10f;
	public float MaxRollRate = 120f;
	public float MaxG = 6f;
	public float MaxAoA = 18f;
	public float MaxNegativeAoA = 8f;
	public float MaxBank = 180f;
	public float BankBlendStart = 3f;
	public float BankBlendEnd = 20f;
	public float AttitudeResponse = 0.4f;
	public float RateResponse = 0.15f;
	public float ControlLag = 0.12f;
	public float ControlGain = 0.7f;

	static FlightMode FromNode(ConfigNode node)
	{
		var mode = new FlightMode();
		node.TryGetValue("name", ref mode.Name);
		node.TryGetValue("maxPitchRate", ref mode.MaxPitchRate);
		node.TryGetValue("maxYawRate", ref mode.MaxYawRate);
		node.TryGetValue("maxRollRate", ref mode.MaxRollRate);
		node.TryGetValue("maxG", ref mode.MaxG);
		node.TryGetValue("maxAoA", ref mode.MaxAoA);
		node.TryGetValue("maxNegativeAoA", ref mode.MaxNegativeAoA);
		node.TryGetValue("maxBank", ref mode.MaxBank);
		node.TryGetValue("bankBlendStart", ref mode.BankBlendStart);
		node.TryGetValue("bankBlendEnd", ref mode.BankBlendEnd);
		node.TryGetValue("attitudeResponse", ref mode.AttitudeResponse);
		node.TryGetValue("rateResponse", ref mode.RateResponse);
		node.TryGetValue("controlLag", ref mode.ControlLag);
		node.TryGetValue("controlGain", ref mode.ControlGain);

		// Response times divide, the gain has to move the input the right way, and the bank blend needs a width.
		mode.AttitudeResponse = Mathf.Max(mode.AttitudeResponse, 0.05f);
		mode.RateResponse = Mathf.Max(mode.RateResponse, 0.02f);
		mode.ControlLag = Mathf.Max(mode.ControlLag, 0.01f);
		mode.ControlGain = Mathf.Clamp(mode.ControlGain, 0.05f, 1.5f);
		mode.BankBlendEnd = Mathf.Max(mode.BankBlendEnd, mode.BankBlendStart + 0.1f);
		return mode;
	}

	/// <summary>The modes in file order, read from the game database so ModuleManager patches apply.</summary>
	public static List<FlightMode> LoadFromDatabase() => Build(GameDatabase.Instance.GetConfigNodes(NodeName));

	/// <summary>
	/// The modes read straight from FlightModes.cfg on disk, for tuning without a restart. ModuleManager patches do not
	/// apply to this.
	/// </summary>
	public static List<FlightMode> LoadFromDisk()
	{
		var path = Path.Combine(KSPUtil.ApplicationRootPath, "GameData", "MouseAimFlight", "FlightModes.cfg");
		var root = File.Exists(path) ? ConfigNode.Load(path) : null;
		if (root == null)
			Debug.LogWarning($"[MouseAimFlight] Could not read {path}");
		return Build(root?.GetNodes(NodeName));
	}

	static List<FlightMode> Build(ConfigNode[] nodes)
	{
		var modes = new List<FlightMode>();
		if (nodes != null)
		{
			foreach (var node in nodes)
				modes.Add(FromNode(node));
		}

		if (modes.Count == 0)
		{
			Debug.LogWarning($"[MouseAimFlight] No {NodeName} nodes found, using a built-in mode");
			modes.Add(new FlightMode());
		}
		return modes;
	}
}

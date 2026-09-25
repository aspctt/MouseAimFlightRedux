//// Dependencies

using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MouseAimFlightRedux.Control;

/// <summary>
/// Limits and response times for the controller. Angles in degrees, rates in degrees per
/// second, times in seconds. See docs/DESIGN.md, "Flight modes".
/// </summary>
public sealed class FlightMode
{
	//// Constants

	public const string NODE_NAME = "MOUSE_AIM_FLIGHT_REDUX_MODE";

	//// References and State

	public string Name = "Normal";
	public float MaximumPitchRate = 25f;
	public float MaximumYawRate = 10f;
	public float MaximumRollRate = 120f;

	/// <summary>Load factor limit, g. Zero or less for none.</summary>
	public float MaximumLoadFactor = 6f;

	public float MaximumAngleOfAttack = 18f;
	public float MaximumNegativeAngleOfAttack = 8f;
	public float MaximumBank = 180f;
	public float BankBlendStart = 3f;
	public float BankBlendEnd = 20f;
	public float AttitudeResponse = 0.4f;
	public float RateResponse = 0.15f;
	public float ControlLag = 0.12f;
	public float ControlGain = 0.7f;
	public float BrakingShare = 0.5f;

	//// Private Functions

	static FlightMode FromNode(ConfigNode node)
	{
		// Read the keys
		var mode = new FlightMode();
		node.TryGetValue("name", ref mode.Name);
		node.TryGetValue("maxPitchRate", ref mode.MaximumPitchRate);
		node.TryGetValue("maxYawRate", ref mode.MaximumYawRate);
		node.TryGetValue("maxRollRate", ref mode.MaximumRollRate);
		node.TryGetValue("maxG", ref mode.MaximumLoadFactor);
		node.TryGetValue("maxAoA", ref mode.MaximumAngleOfAttack);
		node.TryGetValue("maxNegativeAoA", ref mode.MaximumNegativeAngleOfAttack);
		node.TryGetValue("maxBank", ref mode.MaximumBank);
		node.TryGetValue("bankBlendStart", ref mode.BankBlendStart);
		node.TryGetValue("bankBlendEnd", ref mode.BankBlendEnd);
		node.TryGetValue("attitudeResponse", ref mode.AttitudeResponse);
		node.TryGetValue("rateResponse", ref mode.RateResponse);
		node.TryGetValue("controlLag", ref mode.ControlLag);
		node.TryGetValue("controlGain", ref mode.ControlGain);
		node.TryGetValue("brakingShare", ref mode.BrakingShare);

		// Keep the values workable
		// Response times divide, the gain has to move the input the right way, braking
		// can't plan on more than all of the authority, and the bank blend needs a width.
		mode.AttitudeResponse = Mathf.Max(mode.AttitudeResponse, 0.05f);
		mode.RateResponse = Mathf.Max(mode.RateResponse, 0.02f);
		mode.ControlLag = Mathf.Max(mode.ControlLag, 0.01f);
		mode.ControlGain = Mathf.Clamp(mode.ControlGain, 0.05f, 1.5f);
		mode.BrakingShare = Mathf.Clamp(mode.BrakingShare, 0.1f, 1f);
		mode.BankBlendEnd = Mathf.Max(mode.BankBlendEnd, mode.BankBlendStart + 0.1f);
		return mode;
	}

	static List<FlightMode> Build(ConfigNode[]? nodes)
	{
		// Read every mode
		var modes = new List<FlightMode>();
		if (nodes != null)
		{
			foreach (var node in nodes)
				modes.Add(FromNode(node));
		}

		// Fall back on the defaults
		if (modes.Count == 0)
		{
			Debug.LogWarning($"[MouseAimFlightRedux] No {NODE_NAME} nodes found, using a built-in mode");
			modes.Add(new FlightMode());
		}
		return modes;
	}

	//// Public API

	/// <summary>
	/// The modes in file order, read from the game database so ModuleManager patches
	/// apply.
	/// </summary>
	public static List<FlightMode> LoadFromDatabase() => Build(GameDatabase.Instance.GetConfigNodes(NODE_NAME));

	/// <summary>
	/// The modes read straight from FlightModes.cfg on disk, for tuning without a
	/// restart. ModuleManager patches do not apply to this.
	/// </summary>
	public static List<FlightMode> LoadFromDisk()
	{
		// Read the file
		var path = Path.Combine(KSPUtil.ApplicationRootPath, "GameData", "MouseAimFlightRedux", "FlightModes.cfg");
		var root = File.Exists(path) ? ConfigNode.Load(path) : null;
		if (root == null)
			Debug.LogWarning($"[MouseAimFlightRedux] Could not read {path}");

		// Build the modes from it
		return Build(root?.GetNodes(NODE_NAME));
	}
}

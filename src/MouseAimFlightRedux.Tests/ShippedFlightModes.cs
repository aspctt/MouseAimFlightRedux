//// Dependencies

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using MouseAimFlightRedux.Control;

namespace MouseAimFlightRedux.Tests;

/// <summary>
/// The flight modes in the shipped FlightModes.cfg, read without KSP. The keys match
/// FlightMode's own reader, and a key this doesn't know fails the load, so a key renamed
/// in the file can't slip past the tests.
/// </summary>
public static class ShippedFlightModes
{
	//// Constants

	const string FILE_NAME = "FlightModes.cfg";

	//// References and State

	static List<FlightMode>? modes;

	//// Private Functions

	static float Number(string key, string value)
	{
		// Sanity check
		if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
			throw new FormatException($"{FILE_NAME}: {key} = {value} is not a number");

		return number;
	}

	static void Set(FlightMode mode, string key, string value)
	{
		switch (key)
		{
			case "name": mode.Name = value; break;
			case "maxPitchRate": mode.MaximumPitchRate = Number(key, value); break;
			case "maxYawRate": mode.MaximumYawRate = Number(key, value); break;
			case "maxRollRate": mode.MaximumRollRate = Number(key, value); break;
			case "maxG": mode.MaximumLoadFactor = Number(key, value); break;
			case "maxAoA": mode.MaximumAngleOfAttack = Number(key, value); break;
			case "maxNegativeAoA": mode.MaximumNegativeAngleOfAttack = Number(key, value); break;
			case "maxBank": mode.MaximumBank = Number(key, value); break;
			case "bankBlendStart": mode.BankBlendStart = Number(key, value); break;
			case "bankBlendEnd": mode.BankBlendEnd = Number(key, value); break;
			case "attitudeResponse": mode.AttitudeResponse = Number(key, value); break;
			case "rateResponse": mode.RateResponse = Number(key, value); break;
			case "controlLag": mode.ControlLag = Number(key, value); break;
			case "controlGain": mode.ControlGain = Number(key, value); break;
			case "brakingShare": mode.BrakingShare = Number(key, value); break;
			default: throw new FormatException($"{FILE_NAME}: unknown key {key}");
		}
	}

	static List<FlightMode> Load()
	{
		// Read the copy beside the tests
		var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FILE_NAME);
		var loaded = new List<FlightMode>();
		FlightMode? mode = null;
		var isOpening = false;

		// Build a mode from each node
		// Only the layout the shipped file uses is understood. Anything else fails,
		// rather than being read differently from how KSP would read it.
		foreach (var rawLine in File.ReadAllLines(path))
		{
			var comment = rawLine.IndexOf("//", StringComparison.Ordinal);
			var line = (comment >= 0 ? rawLine.Substring(0, comment) : rawLine).Trim();
			if (line.Length == 0)
				continue;

			if (mode == null && line == FlightMode.NODE_NAME)
				isOpening = true;
			else if (isOpening && line == "{")
			{
				mode = new FlightMode();
				isOpening = false;
			}
			else if (mode != null && line == "}")
			{
				loaded.Add(mode);
				mode = null;
			}
			else if (mode != null && line.IndexOf('=') > 0)
			{
				var equals = line.IndexOf('=');
				Set(mode, line.Substring(0, equals).Trim(), line.Substring(equals + 1).Trim());
			}
			else
				throw new FormatException($"{FILE_NAME}: can't read \"{line}\"");
		}
		return loaded;
	}

	//// Public API

	public static IReadOnlyList<FlightMode> All => modes ??= Load();
}

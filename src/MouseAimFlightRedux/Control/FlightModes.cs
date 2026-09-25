using System;
using System.Collections.Generic;

namespace MouseAimFlightRedux.Control;

/// <summary>The loaded flight modes and which one is selected, shared by every vessel.</summary>
public static class FlightModes
{
	static List<FlightMode> modes;
	static int index;

	public static FlightMode Current
	{
		get
		{
			if (modes == null)
				Use(FlightMode.LoadFromDatabase());
			return modes[index];
		}
	}

	public static FlightMode Next()
	{
		_ = Current;
		index = (index + 1) % modes.Count;
		return modes[index];
	}

	/// <summary>Re-reads the modes from disk, keeping the selection by name where it still exists.</summary>
	public static void ReloadFromDisk() => Use(FlightMode.LoadFromDisk());

	/// <summary>Starts on the first mode, so each game begins in the same one.</summary>
	static void Use(List<FlightMode> loaded)
	{
		var selected = modes?[index].Name;
		modes = loaded;
		index = Math.Max(modes.FindIndex(m => m.Name == selected), 0);
	}
}

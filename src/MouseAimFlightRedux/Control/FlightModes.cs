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
		Remember();
		return modes[index];
	}

	/// <summary>Re-reads the modes from disk, keeping the selection by name where it still exists.</summary>
	public static void ReloadFromDisk() => Use(FlightMode.LoadFromDisk());

	static void Use(List<FlightMode> loaded)
	{
		modes = loaded;
		index = modes.FindIndex(m => m.Name == Settings.Instance.Mode);
		if (index < 0)
			index = 0;
	}

	static void Remember()
	{
		Settings.Instance.Mode = modes[index].Name;
		Settings.Instance.Save();
	}
}

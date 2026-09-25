//// Dependencies

using System.Collections.Generic;

namespace MouseAimFlightRedux.Control;

/// <summary>
/// The loaded flight modes and which one is selected, shared by every vessel. Each game
/// starts on the first.
/// </summary>
public static class FlightModes
{
	//// References and State

	static List<FlightMode>? modes;
	static int selectedIndex;

	/// <summary>
	/// The modes, read from the game database the first time they're needed.
	/// </summary>
	static List<FlightMode> Modes => modes ??= FlightMode.LoadFromDatabase();

	//// Private Functions

	/// <summary>
	/// Swaps in newly loaded modes, keeping the selection by name where it still exists.
	/// </summary>
	static void Use(List<FlightMode> loaded)
	{
		// Remember the selection
		var selectedName = modes?[selectedIndex].Name;
		modes = loaded;

		// Find it again, or start on the first mode
		selectedIndex = 0;
		for (var index = 0; index < loaded.Count; index++)
		{
			if (loaded[index].Name == selectedName)
			{
				selectedIndex = index;
				break;
			}
		}
	}

	//// Public API

	public static FlightMode Current => Modes[selectedIndex];

	public static FlightMode Next()
	{
		selectedIndex = (selectedIndex + 1) % Modes.Count;
		return Modes[selectedIndex];
	}

	/// <summary>
	/// Re-reads the modes from disk, keeping the selection by name where it still exists.
	/// </summary>
	public static void ReloadFromDisk() => Use(FlightMode.LoadFromDisk());
}

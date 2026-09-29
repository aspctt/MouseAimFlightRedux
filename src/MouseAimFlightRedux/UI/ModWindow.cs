//// Dependencies

using ClickThroughFix;
using UnityEngine;

namespace MouseAimFlightRedux.UI;

/// <summary>
/// Draws the mod's windows through ClickThroughBlocker, so a click on one doesn't also
/// reach the game behind it. See docs/DESIGN.md, "Windows".
/// </summary>
static class ModWindow
{
	//// Public API

	/// <summary>Call from OnGUI in place of GUILayout.Window.</summary>
	public static Rect Draw(int identifier, Rect rect, GUI.WindowFunction drawContents, string title)
	{
		// Draw it plainly while mouse aim holds the cursor
		// Nothing can be clicked then, but ClickThroughBlocker still reads the pointer the
		// lock holds in place, and would lock the controls, keyboard flying, throttle and
		// staging included, for as long as a window sat under it.
		if (Cursor.lockState == CursorLockMode.Locked)
			return GUILayout.Window(identifier, rect, drawContents, title);

		// Keep clicks on it from reaching the game
		return ClickThruBlocker.GUILayoutWindow(identifier, rect, drawContents, title);
	}
}

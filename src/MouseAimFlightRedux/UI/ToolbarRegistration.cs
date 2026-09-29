//// Dependencies

using ToolbarControl_NS;
using UnityEngine;

namespace MouseAimFlightRedux.UI;

/// <summary>
/// Registers the toolbar button with Toolbar Controller at the main menu, so players can
/// pick which toolbar it goes on in its settings. Toolbar Controller only adds buttons it
/// knows about, so this has to happen before the first flight.
/// </summary>
[KSPAddon(KSPAddon.Startup.MainMenu, true)]
sealed class ToolbarRegistration : MonoBehaviour
{
	//// Event Wiring

	void Start()
	{
		ToolbarControl.RegisterMod(SettingsWindow.TOOLBAR_NAMESPACE, SettingsWindow.TITLE);
	}
}

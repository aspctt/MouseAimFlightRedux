namespace MouseAimFlightRedux;

/// <summary>
/// Keeps stock SAS off while mouse aim flies, since it steers through the same controls
/// and would fight it, and Atmosphere Autopilot too unless the player lets them fly
/// together. Whatever was switched off is switched back on afterwards. See
/// docs/DESIGN.md, "Other autopilots".
/// </summary>
sealed class AutopilotLockout
{
	//// References and State

	Vessel? vessel;
	bool isSasOnAtEngage;
	bool isAtmosphereAutopilotOnAtEngage;
	bool hasSwitchedOffAtmosphereAutopilot;

	//// Public API

	public void Engage(Vessel target)
	{
		// Sanity check
		if (target == vessel)
			return;

		// Let go of any earlier vessel
		Release();
		if (target == null)
			return;

		// Record what was on, then switch it off
		vessel = target;
		isSasOnAtEngage = target.ActionGroups[KSPActionGroup.SAS];
		isAtmosphereAutopilotOnAtEngage = AtmosphereAutopilot.IsOn(target);
		hasSwitchedOffAtmosphereAutopilot = false;
		Hold();
	}

	/// <summary>
	/// Switches them back off if anything turned them on, such as the pilot pressing a
	/// key.
	/// </summary>
	public void Hold()
	{
		// Sanity check
		if (vessel == null)
			return;

		// Switch SAS off
		if (vessel.ActionGroups[KSPActionGroup.SAS])
			vessel.ActionGroups.SetGroup(KSPActionGroup.SAS, false);

		// Switch Atmosphere Autopilot off, unless they fly together
		if (Settings.Instance.ShouldKeepAtmosphereAutopilotOff && AtmosphereAutopilot.IsOn(vessel))
		{
			AtmosphereAutopilot.Set(vessel, false);
			hasSwitchedOffAtmosphereAutopilot = true;
		}
	}

	public void Release()
	{
		// Sanity check
		var target = vessel;
		vessel = null;
		if (target == null || target.state == Vessel.State.DEAD)
			return;

		// Switch back on whatever was on
		// Atmosphere Autopilot switches SAS off as it comes on, so SAS goes last to end
		// up as it was.
		if (isAtmosphereAutopilotOnAtEngage && hasSwitchedOffAtmosphereAutopilot)
			AtmosphereAutopilot.Set(target, true);
		if (isSasOnAtEngage)
			target.ActionGroups.SetGroup(KSPActionGroup.SAS, true);
	}
}

namespace MouseAimFlightRedux;

/// <summary>
/// Keeps stock SAS off while mouse aim flies, since it steers through the same controls and would fight it, and
/// Atmosphere Autopilot too unless the player lets them fly together. Whatever was switched off is switched back on
/// afterwards. See docs/DESIGN.md, "Other autopilots".
/// </summary>
sealed class AutopilotLockout
{
	Vessel vessel;
	bool sasWasOn;
	bool atmosphereAutopilotWasOn;
	bool atmosphereAutopilotSwitchedOff;

	public void Engage(Vessel target)
	{
		Release();
		if (target == null)
			return;

		vessel = target;
		sasWasOn = vessel.ActionGroups[KSPActionGroup.SAS];
		atmosphereAutopilotWasOn = AtmosphereAutopilot.IsOn(vessel);
		atmosphereAutopilotSwitchedOff = false;
		Hold();
	}

	/// <summary>Switches them back off if anything turned them on, such as the pilot pressing a key.</summary>
	public void Hold()
	{
		if (vessel == null)
			return;

		if (vessel.ActionGroups[KSPActionGroup.SAS])
			vessel.ActionGroups.SetGroup(KSPActionGroup.SAS, false);

		if (Settings.Instance.KeepAtmosphereAutopilotOff && AtmosphereAutopilot.IsOn(vessel))
		{
			AtmosphereAutopilot.Set(vessel, false);
			atmosphereAutopilotSwitchedOff = true;
		}
	}

	public void Release()
	{
		var target = vessel;
		vessel = null;
		if (target == null || target.state == Vessel.State.DEAD)
			return;

		// Atmosphere Autopilot switches SAS off as it comes on, so SAS goes last to end up as it was.
		if (atmosphereAutopilotWasOn && atmosphereAutopilotSwitchedOff)
			AtmosphereAutopilot.Set(target, true);
		if (sasWasOn)
			target.ActionGroups.SetGroup(KSPActionGroup.SAS, true);
	}
}

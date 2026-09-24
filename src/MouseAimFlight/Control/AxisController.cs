using UnityEngine;

namespace MouseAimFlight.Control;

/// <summary>
/// One axis of the cascade: an angle error becomes a rate command, and the rate loop changes the input until the
/// measured angular acceleration matches what that command needs. See docs/DESIGN.md, "Attitude loop" and "Rate loop".
/// </summary>
sealed class AxisController
{
	/// <summary>Share of the available angular acceleration the braking curve plans to stop with.</summary>
	const float BrakingShare = 0.5f;

	/// <summary>Time constant of the low-pass filter on measured angular acceleration, s.</summary>
	const float FilterTime = 0.05f;

	bool primed;
	float lastRate;
	float lastInput;

	/// <summary>Measured angular acceleration after the filter, rad/s².</summary>
	float acceleration;

	/// <summary>Where the controls are estimated to have got to, following the inputs with the mode's control lag.</summary>
	float controls;

	/// <summary>The same estimate through the acceleration filter, so the two are compared at the same delay.</summary>
	float controlsFiltered;

	public void Reset()
	{
		primed = false;
		lastInput = 0f;
		acceleration = 0f;
		controls = 0f;
		controlsFiltered = 0f;
	}

	/// <summary>The input that actually reached the vessel, which differs from the one returned when the pilot overrides it.</summary>
	public void Applied(float input) => lastInput = Mathf.Clamp(input, -1f, 1f);

	/// <param name="error">Angle to close, rad.</param>
	/// <param name="rate">Current rate on this axis, rad/s.</param>
	/// <param name="minRate">Lowest rate the command may take, rad/s.</param>
	/// <param name="maxRate">Highest rate the command may take, rad/s.</param>
	/// <param name="authority">Angular acceleration a full input produces, rad/s².</param>
	/// <returns>Input from -1 to 1.</returns>
	public float Step(float error, float rate, float minRate, float maxRate, float authority, FlightMode mode, float dt)
	{
		if (dt <= 0f)
			return lastInput;

		// Attitude loop, on the error that will be left once the rate loop and the controls have caught up with a
		// change of command, so the rate comes off in time instead of after the target has been passed.
		var lead = mode.RateResponse + mode.ControlLag;
		var ahead = error - rate * lead;
		var magnitude = Mathf.Abs(ahead);
		var linear = magnitude / mode.AttitudeResponse;
		var braking = Mathf.Sqrt(2f * BrakingShare * authority * magnitude);
		var rateCommand = Mathf.Sign(ahead) * Mathf.Min(linear, braking);

		// Limits can cross, an angle of attack limit against a load limit say. Split the difference rather than let
		// the order of the clamp decide.
		if (minRate > maxRate)
			minRate = maxRate = 0.5f * (minRate + maxRate);
		rateCommand = Mathf.Clamp(rateCommand, minRate, maxRate);

		// Rate loop, incremental: measure the angular acceleration actually being achieved and move the input by the
		// shortfall over authority. Stability, trim and damping all show up in the measurement, so an aircraft that
		// needs a lot of held elevator to keep pitching gets it.
		if (!primed)
		{
			lastRate = rate;
			controls = lastInput;
			controlsFiltered = lastInput;
			primed = true;
		}

		var measured = (rate - lastRate) / dt;
		lastRate = rate;

		var filter = 1f - Mathf.Exp(-dt / FilterTime);
		controls += (lastInput - controls) * (1f - Mathf.Exp(-dt / mode.ControlLag));
		acceleration += (measured - acceleration) * filter;
		controlsFiltered += (controls - controlsFiltered) * filter;

		var wanted = (rateCommand - rate) / mode.RateResponse;
		var input = controlsFiltered + mode.ControlGain * (wanted - acceleration) / authority;

		lastInput = Mathf.Clamp(input, -1f, 1f);
		return lastInput;
	}
}

//// Dependencies

using UnityEngine;

namespace MouseAimFlightRedux.Control;

/// <summary>
/// One axis of the cascade: an angle error becomes a rate command, and the rate loop
/// changes the input until the measured angular acceleration matches what that command
/// needs. See docs/DESIGN.md, "Attitude loop" and "Rate loop".
/// </summary>
sealed class AxisController
{
	//// Constants

	/// <summary>
	/// Time constant of the low-pass filter on measured angular acceleration, s.
	/// </summary>
	const float FILTER_TIME = 0.05f;

	//// References and State

	bool isPrimed;
	float lastRate;
	float lastInput;

	/// <summary>Measured angular acceleration after the filter, rad/s².</summary>
	float acceleration;

	/// <summary>
	/// Where controls that ease into position are estimated to have got to, following the
	/// inputs with the mode's control lag.
	/// </summary>
	float easing;

	/// <summary>
	/// Where controls that move at a fixed speed are estimated to have got to.
	/// </summary>
	float slewing;

	/// <summary>
	/// The same estimate through the acceleration filter, so the two are compared at the
	/// same delay.
	/// </summary>
	float controlsFiltered;

	/// <summary>Angle error on the last step, rad.</summary>
	public float Error { get; private set; }

	/// <summary>Rate asked for on the last step, after the limits, rad/s.</summary>
	public float RateCommand { get; private set; }

	/// <summary>Rate measured on the last step, rad/s.</summary>
	public float Rate { get; private set; }

	/// <summary>
	/// The limits the rate command was held to on the last step, rad/s.
	/// </summary>
	public float MinimumRate { get; private set; }

	public float MaximumRate { get; private set; }

	/// <summary>
	/// 1 when the last rate command was held at <see cref="MaximumRate"/>, -1 at
	/// <see cref="MinimumRate"/>, 0 when neither.
	/// </summary>
	public int HeldLimit { get; private set; }

	//// Public API

	/// <summary>The last input, as it reached the vessel.</summary>
	public float Input => lastInput;

	public void Reset()
	{
		isPrimed = false;
		lastInput = 0f;
		acceleration = 0f;
		easing = 0f;
		slewing = 0f;
		controlsFiltered = 0f;
	}

	/// <summary>
	/// The input that actually reached the vessel, which differs from the one returned
	/// when the pilot overrides it.
	/// </summary>
	public void Applied(float input) => lastInput = Mathf.Clamp(input, -1f, 1f);

	/// <param name="error">Angle to close, rad.</param>
	/// <param name="targetRate">How fast the target itself is moving on this axis,
	/// rad/s. The rate asked for is this plus whatever closes the error.</param>
	/// <param name="rate">Current rate on this axis, rad/s.</param>
	/// <param name="minimumRate">Lowest rate the command may take, rad/s.</param>
	/// <param name="maximumRate">Highest rate the command may take, rad/s.</param>
	/// <param name="authority">Angular acceleration a full input produces,
	/// rad/s².</param>
	/// <param name="slewShare">Share of the authority from controls that move at a fixed
	/// speed, 0 to 1.</param>
	/// <param name="slewSpeed">That fixed speed, full inputs per second.</param>
	/// <returns>Input from -1 to 1.</returns>
	public float Step(float error, float targetRate, float rate, float minimumRate, float maximumRate, float authority, float slewShare, float slewSpeed, FlightMode mode, float deltaTime)
	{
		// Sanity check
		if (deltaTime <= 0f)
			return lastInput;

		// Turn the error into a rate
		// The attitude loop works on the error that will be left once the rate loop and
		// the controls have caught up with a change of command, so the rate comes off in
		// time instead of after the target has been passed. A moving target is followed
		// at its own rate, so holding onto it doesn't need a standing error.
		var lead = mode.RateResponse + mode.ControlLag;
		var ahead = error - (rate - targetRate) * lead;
		var magnitude = Mathf.Abs(ahead);
		var linear = magnitude / mode.AttitudeResponse;
		var braking = Mathf.Sqrt(2f * mode.BrakingShare * authority * magnitude);
		var rateCommand = targetRate + Mathf.Sign(ahead) * Mathf.Min(linear, braking);

		// Hold the rate within the limits
		// Limits can cross, an angle of attack limit against a load limit say. Split the
		// difference rather than let the order of the clamp decide.
		if (minimumRate > maximumRate)
			minimumRate = maximumRate = 0.5f * (minimumRate + maximumRate);
		HeldLimit = rateCommand > maximumRate ? 1 : rateCommand < minimumRate ? -1 : 0;
		rateCommand = Mathf.Clamp(rateCommand, minimumRate, maximumRate);

		// Record the step for the tuning overlay
		Error = error;
		RateCommand = rateCommand;
		Rate = rate;
		MinimumRate = minimumRate;
		MaximumRate = maximumRate;

		// Start the estimates from where the controls are
		if (!isPrimed)
		{
			lastRate = rate;
			easing = lastInput;
			slewing = lastInput;
			controlsFiltered = lastInput;
			isPrimed = true;
		}

		// Measure the angular acceleration
		var measured = (rate - lastRate) / deltaTime;
		lastRate = rate;

		// Estimate where the controls have got to
		// Controls that move at a fixed speed fall behind on big changes, and a loop that
		// assumed otherwise would keep adding input while they caught up, then overshoot.
		var filter = 1f - Mathf.Exp(-deltaTime / FILTER_TIME);
		easing += (lastInput - easing) * (1f - Mathf.Exp(-deltaTime / mode.ControlLag));
		var slew = slewSpeed * deltaTime;
		slewing += Mathf.Clamp(lastInput - slewing, -slew, slew);
		var controls = Mathf.Lerp(easing, slewing, slewShare);
		acceleration += (measured - acceleration) * filter;
		controlsFiltered += (controls - controlsFiltered) * filter;

		// Move the input by the shortfall over authority
		// The rate loop is incremental: stability, trim and damping all show up in the
		// measured acceleration, so an aircraft that needs a lot of held elevator to keep
		// pitching gets it.
		var wanted = (rateCommand - rate) / mode.RateResponse;
		var input = controlsFiltered + mode.ControlGain * (wanted - acceleration) / authority;
		lastInput = Mathf.Clamp(input, -1f, 1f);
		return lastInput;
	}
}

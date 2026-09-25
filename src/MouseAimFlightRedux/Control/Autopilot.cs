//// Dependencies

using UnityEngine;

namespace MouseAimFlightRedux.Control;

//// Types

/// <summary>
/// What set a rate limit: the mode's rate limit, its load limit or its angle of attack
/// limit.
/// </summary>
public enum RateLimit
{
	Rate,
	LoadFactor,
	AngleOfAttack,
}

/// <summary>
/// Turns an aim direction into pitch, yaw and roll inputs. Guidance picks an angle error
/// per axis, then each axis runs an attitude loop and a rate loop. See docs/DESIGN.md,
/// "Controller".
/// </summary>
public sealed class Autopilot
{
	//// Constants

	/// <summary>One standard g, m/s².</summary>
	const float GRAVITY = 9.80665f;

	/// <summary>Dynamic pressure, kPa, over which aircraft behaviour fades in.</summary>
	const float AERODYNAMIC_BLEND_START = 0.3f;

	const float AERODYNAMIC_BLEND_FULL = 1.5f;

	/// <summary>
	/// How strongly yaw works to keep the nose on the flight path while turning.
	/// </summary>
	const float SIDESLIP_GAIN = 0.5f;

	//// References and State

	readonly AxisController pitch = new();
	readonly AxisController yaw = new();
	readonly AxisController roll = new();

	/// <summary>
	/// How far aircraft behaviour had faded in on the last step, 0 to 1.
	/// </summary>
	public float AerodynamicBlend { get; private set; }

	/// <summary>
	/// How far the last step had committed to banking toward the aim rather than wings
	/// level, 0 to 1.
	/// </summary>
	public float BankCommitment { get; private set; }

	/// <summary>What set the highest pitch rate on the last step.</summary>
	public RateLimit PitchUpLimit { get; private set; }

	/// <summary>What set the lowest pitch rate on the last step.</summary>
	public RateLimit PitchDownLimit { get; private set; }

	//// Private Functions

	static float SmoothStep(float from, float to, float value) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, value));

	static Vector2 Normalized(Vector2 vector) => vector.sqrMagnitude > 1e-8f ? vector.normalized : Vector2.zero;

	static float WrapAngle(float radians) => Mathf.Repeat(radians + Mathf.PI, 2f * Mathf.PI) - Mathf.PI;

	//// Public API

	internal AxisController Pitch => pitch;
	internal AxisController Yaw => yaw;
	internal AxisController Roll => roll;

	public void Reset()
	{
		pitch.Reset();
		yaw.Reset();
		roll.Reset();
	}

	/// <summary>
	/// The inputs that actually reached the vessel, after any the pilot overrode.
	/// </summary>
	public void Applied(float pitchInput, float yawInput, float rollInput)
	{
		pitch.Applied(pitchInput);
		yaw.Applied(yawInput);
		roll.Applied(rollInput);
	}

	/// <summary>
	/// Inputs follow FlightCtrlState: positive pitch is nose up, positive yaw is nose
	/// right, positive roll is right wing down.
	/// </summary>
	public void Drive(VesselDynamics vessel, Vector3 aimDirection, FlightMode mode, float deltaTime, out float pitchInput, out float yawInput, out float rollInput)
	{
		// Measure the aim against the nose
		var aim = aimDirection.normalized;
		var along = Vector3.Dot(aim, vessel.Nose);
		var above = Vector3.Dot(aim, vessel.Canopy);
		var right = Vector3.Dot(aim, vessel.Right);
		var pitchError = Mathf.Atan2(above, along);
		var yawError = Mathf.Atan2(right, along);
		var offNose = Mathf.Acos(Mathf.Clamp(along, -1f, 1f));

		// Fade in aircraft behaviour and the commitment to bank
		var aerodynamicBlend = SmoothStep(AERODYNAMIC_BLEND_START, AERODYNAMIC_BLEND_FULL, vessel.DynamicPressure);
		var bankCommitment = SmoothStep(mode.BankBlendStart, mode.BankBlendEnd, offNose * Mathf.Rad2Deg);
		AerodynamicBlend = aerodynamicBlend;
		BankCommitment = bankCommitment;

		// Bank toward the aim or wings level
		// Blended as directions in the plane across the nose, so the blend never wraps
		// through ±180°.
		var aimAround = new Vector2(right, above);
		var levelAround = new Vector2(Vector3.Dot(vessel.Up, vessel.Right), Vector3.Dot(vessel.Up, vessel.Canopy));
		var wantedDirection = bankCommitment * Normalized(aimAround) + (1f - bankCommitment) * Normalized(levelAround);
		var bankError = wantedDirection.sqrMagnitude > 1e-6f ? Mathf.Atan2(wantedDirection.x, wantedDirection.y) : 0f;

		// Hold the bank within its limit
		if (mode.MaximumBank < 180f && levelAround.sqrMagnitude > 1e-4f)
		{
			var bank = Mathf.Atan2(-levelAround.x, levelAround.y);
			var bankLimit = mode.MaximumBank * Mathf.Deg2Rad;
			var targetBank = Mathf.Clamp(WrapAngle(bank + bankError), -bankLimit, bankLimit);
			bankError = targetBank - bank;
		}

		// Pull toward the aim
		// Once committed to the bank, pull by the whole angle off the nose, scaled by how
		// well the bank has put the aim overhead. Never push while rolling toward it.
		var aimRollAngle = Mathf.Atan2(right, above);
		var pull = offNose * Mathf.Max(0f, Mathf.Cos(aimRollAngle));
		var aerodynamicPitch = Mathf.Lerp(pitchError, pull, bankCommitment);

		// Trim with yaw
		// Yaw trims the last few degrees, fading out as the bank takes over, and keeps
		// the turn coordinated.
		var aerodynamicYaw = (1f - bankCommitment) * yawError + SIDESLIP_GAIN * vessel.Sideslip;

		// Blend the commands by how aircraft-like the flight is
		var pitchCommand = Mathf.Lerp(pitchError, aerodynamicPitch, aerodynamicBlend);
		var yawCommand = Mathf.Lerp(yawError, aerodynamicYaw, aerodynamicBlend);
		var rollCommand = aerodynamicBlend * bankError;

		// Limit the pitch rate by the mode
		var maximumPitch = mode.MaximumPitchRate * Mathf.Deg2Rad;
		var pitchUp = maximumPitch;
		var pitchDown = -maximumPitch;
		PitchUpLimit = RateLimit.Rate;
		PitchDownLimit = RateLimit.Rate;

		// Limit it further by load factor and angle of attack
		// In a turn the flight path rotates at n·g/V, and a maximum load factor of zero
		// or less means no limit. Near the angle of attack limit, allow the current pitch
		// rate plus whatever closes the remaining margin, so a turn held at the limit
		// keeps turning and one past it backs off.
		if (aerodynamicBlend > 0f)
		{
			var loadFactorRate = mode.MaximumLoadFactor > 0f ? mode.MaximumLoadFactor * GRAVITY / Mathf.Max(vessel.Airspeed, 1f) : float.PositiveInfinity;
			var upMargin = mode.MaximumAngleOfAttack * Mathf.Deg2Rad - vessel.AngleOfAttack;
			var downMargin = mode.MaximumNegativeAngleOfAttack * Mathf.Deg2Rad + vessel.AngleOfAttack;
			var angleOfAttackUp = vessel.PitchRate + upMargin / mode.AttitudeResponse;
			var angleOfAttackDown = vessel.PitchRate - downMargin / mode.AttitudeResponse;

			var pullLimit = Mathf.Min(loadFactorRate, angleOfAttackUp);
			var pushLimit = Mathf.Max(-0.5f * loadFactorRate, angleOfAttackDown);
			if (pullLimit < maximumPitch)
				PitchUpLimit = loadFactorRate <= angleOfAttackUp ? RateLimit.LoadFactor : RateLimit.AngleOfAttack;
			if (pushLimit > -maximumPitch)
				PitchDownLimit = -0.5f * loadFactorRate >= angleOfAttackDown ? RateLimit.LoadFactor : RateLimit.AngleOfAttack;

			pitchUp = Mathf.Lerp(pitchUp, Mathf.Min(maximumPitch, pullLimit), aerodynamicBlend);
			pitchDown = Mathf.Lerp(pitchDown, Mathf.Max(-maximumPitch, pushLimit), aerodynamicBlend);
		}

		// Step each axis
		var maximumYaw = mode.MaximumYawRate * Mathf.Deg2Rad;
		var maximumRoll = mode.MaximumRollRate * Mathf.Deg2Rad;
		pitchInput = pitch.Step(pitchCommand, vessel.PitchRate, pitchDown, pitchUp, vessel.PitchAuthority, vessel.PitchSlewShare, vessel.SlewSpeed, mode, deltaTime);
		yawInput = yaw.Step(yawCommand, vessel.YawRate, -maximumYaw, maximumYaw, vessel.YawAuthority, vessel.YawSlewShare, vessel.SlewSpeed, mode, deltaTime);
		rollInput = roll.Step(rollCommand, vessel.RollRate, -maximumRoll, maximumRoll, vessel.RollAuthority, vessel.RollSlewShare, vessel.SlewSpeed, mode, deltaTime);
	}
}

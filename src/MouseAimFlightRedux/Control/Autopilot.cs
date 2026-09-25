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

	/// <summary>
	/// Time constant of the low-pass filter on the measured rates of change of angle of
	/// attack and sideslip, s.
	/// </summary>
	const float AIRFLOW_RATE_FILTER_TIME = 0.1f;

	/// <summary>
	/// Where wings level starts to fade out and where it's gone, as the sine of the
	/// nose's angle from straight up or down: 30° and about 10°.
	/// </summary>
	const float LEVEL_FADE_START = 0.5f;

	const float LEVEL_FADE_END = 0.17f;

	/// <summary>
	/// How far past the bank limit the bank toward the aim has to be before a turn seeks
	/// the aim's height outright, degrees.
	/// </summary>
	const float HEIGHT_SEEKING_BLEND = 15f;

	/// <summary>
	/// Floor on the cosine of the bank when turning a difference in height into pitch.
	/// </summary>
	const float MINIMUM_BANK_COSINE = 0.25f;

	//// References and State

	readonly AxisController pitch = new();
	readonly AxisController yaw = new();
	readonly AxisController roll = new();
	bool isAirflowPrimed;
	float lastAngleOfAttack;
	float lastSideslip;

	/// <summary>
	/// Rates of change of angle of attack and sideslip after the filter, rad/s.
	/// </summary>
	float angleOfAttackRate;

	float sideslipRate;

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
		isAirflowPrimed = false;
		angleOfAttackRate = 0f;
		sideslipRate = 0f;
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
	public void Drive(IVesselDynamics vessel, Vector3 aimDirection, FlightMode mode, float deltaTime, out float pitchInput, out float yawInput, out float rollInput)
	{
		// Measure the aim against the nose
		var aim = aimDirection.normalized;
		var along = Vector3.Dot(aim, vessel.Nose);
		var above = Vector3.Dot(aim, vessel.Canopy);
		var right = Vector3.Dot(aim, vessel.Right);
		var pitchError = Mathf.Atan2(above, along);
		var yawError = Mathf.Atan2(right, along);
		var offNose = Mathf.Acos(Mathf.Clamp(along, -1f, 1f));
		var heightError = Mathf.Asin(Mathf.Clamp(Vector3.Dot(aim, vessel.Up), -1f, 1f)) - Mathf.Asin(Mathf.Clamp(Vector3.Dot(vessel.Nose, vessel.Up), -1f, 1f));

		// Fade in aircraft behaviour and the commitment to bank
		var aerodynamicBlend = SmoothStep(AERODYNAMIC_BLEND_START, AERODYNAMIC_BLEND_FULL, vessel.DynamicPressure);
		var bankCommitment = SmoothStep(mode.BankBlendStart, mode.BankBlendEnd, offNose * Mathf.Rad2Deg);
		AerodynamicBlend = aerodynamicBlend;
		BankCommitment = bankCommitment;

		// Find which way is up across the nose
		// Wings level means nothing with the nose straight up or down, where the
		// slightest movement swings it round, so it fades out there. Chasing it rocked
		// the wings from side to side at full roll.
		var levelAround = new Vector2(Vector3.Dot(vessel.Up, vessel.Right), Vector3.Dot(vessel.Up, vessel.Canopy));
		var levelWeight = SmoothStep(LEVEL_FADE_END, LEVEL_FADE_START, levelAround.magnitude);

		// Bank toward the aim or wings level
		// Blended as directions in the plane across the nose, so the blend never wraps
		// through ±180°. Where wings level has faded, roll holds still.
		var aimAround = new Vector2(right, above);
		var wantedDirection = bankCommitment * Normalized(aimAround) + (1f - bankCommitment) * levelWeight * Normalized(levelAround);
		var bankError = wantedDirection.sqrMagnitude > 1e-6f ? Mathf.Atan2(wantedDirection.x, wantedDirection.y) : 0f;
		var rollStrength = bankCommitment + (1f - bankCommitment) * levelWeight;

		// Hold the bank within its limit
		// Past the limit the aim can't be put overhead, and pulling at it anyway climbs
		// or dives as much as it turns. So the further past the limit the bank toward the
		// aim would go, the more the turn seeks the aim's height instead, banked toward
		// its side. Pulling at it had Cruise climb steeply turning round, then stick with
		// the aim below the nose.
		var bank = 0f;
		var heightSeeking = 0f;
		if (mode.MaximumBank < 180f && levelAround.sqrMagnitude > 1e-4f)
		{
			// Find how far past the limit the bank would go
			bank = Mathf.Atan2(-levelAround.x, levelAround.y);
			var bankLimit = mode.MaximumBank * Mathf.Deg2Rad;
			var wantedBank = WrapAngle(bank + bankError);
			heightSeeking = levelWeight * SmoothStep(0f, HEIGHT_SEEKING_BLEND, (Mathf.Abs(wantedBank) - bankLimit) * Mathf.Rad2Deg);

			// Bank toward the aim, or toward its side once seeking its height
			var levelRight = new Vector2(levelAround.y, -levelAround.x).normalized;
			var aside = Mathf.Atan2(Vector2.Dot(aimAround, levelRight), along);
			var sideBank = bankLimit * Mathf.Clamp(aside / (mode.BankBlendEnd * Mathf.Deg2Rad), -1f, 1f);
			var targetBank = Mathf.Lerp(Mathf.Clamp(wantedBank, -bankLimit, bankLimit), sideBank, heightSeeking);
			bankError = Mathf.Lerp(bankError, targetBank - bank, levelWeight);
		}

		// Pull toward the aim
		// Once committed to the bank, pull by the whole angle off the nose, scaled by how
		// well the bank has put the aim overhead. Never push while rolling toward it.
		var aimRollAngle = Mathf.Atan2(right, above);
		var pull = offNose * Mathf.Max(0f, Mathf.Cos(aimRollAngle));

		// Or seek the aim's height
		// Pitch moves the nose up by the cosine of the bank, so the difference in height
		// is scaled up by it, keeping its sign past 90°.
		var bankCosine = Mathf.Cos(bank);
		var heightPull = heightError / (Mathf.Sign(bankCosine) * Mathf.Max(Mathf.Abs(bankCosine), MINIMUM_BANK_COSINE));
		var aerodynamicPitch = Mathf.Lerp(pitchError, Mathf.Lerp(pull, heightPull, heightSeeking), bankCommitment);

		// Measure how fast the airflow is changing
		// The flight path turns at the nose's rate plus the rate the airflow moves across
		// it: pitch rate less the change in angle of attack, yaw rate plus the change in
		// sideslip.
		var angleOfAttack = vessel.AngleOfAttack;
		var sideslip = vessel.Sideslip;
		if (!isAirflowPrimed)
		{
			lastAngleOfAttack = angleOfAttack;
			lastSideslip = sideslip;
			isAirflowPrimed = true;
		}
		if (deltaTime > 0f)
		{
			var filter = 1f - Mathf.Exp(-deltaTime / AIRFLOW_RATE_FILTER_TIME);
			angleOfAttackRate += (WrapAngle(angleOfAttack - lastAngleOfAttack) / deltaTime - angleOfAttackRate) * filter;
			sideslipRate += (WrapAngle(sideslip - lastSideslip) / deltaTime - sideslipRate) * filter;
		}
		lastAngleOfAttack = angleOfAttack;
		lastSideslip = sideslip;

		// Trim with yaw
		// Yaw trims the last few degrees, fading out as the bank takes over, and keeps
		// the turn coordinated. Once committed to the bank it follows the flight path's
		// own yaw rate: a turn short of 90° of bank needs the nose to yaw as well as
		// pitch, and holding yaw to what the sideslip alone asked for skidded it.
		var aerodynamicYaw = (1f - bankCommitment) * yawError + SIDESLIP_GAIN * sideslip;
		var yawTargetRate = aerodynamicBlend * bankCommitment * (vessel.YawRate + sideslipRate);

		// Blend the commands by how aircraft-like the flight is
		var pitchCommand = Mathf.Lerp(pitchError, aerodynamicPitch, aerodynamicBlend);
		var yawCommand = Mathf.Lerp(yawError, aerodynamicYaw, aerodynamicBlend);
		var rollCommand = aerodynamicBlend * rollStrength * bankError;

		// Limit the pitch rate by the mode
		var maximumPitch = mode.MaximumPitchRate * Mathf.Deg2Rad;
		var pitchUp = maximumPitch;
		var pitchDown = -maximumPitch;
		PitchUpLimit = RateLimit.Rate;
		PitchDownLimit = RateLimit.Rate;

		// Limit it further by load factor and angle of attack
		// In a turn the flight path rotates at n·g/V, and a maximum load factor of zero
		// or less means no limit. The angle of attack only grows while the nose turns
		// faster than the flight path, so near its limit allow the flight path's turn
		// rate plus whatever closes the remaining margin, looking ahead by the time a
		// change of pitch rate takes. A turn held at the limit keeps turning, and one
		// past it backs off. Allowing the nose's own rate instead ran 3 to 6° past the
		// limit and pulsed.
		if (aerodynamicBlend > 0f)
		{
			var loadFactorRate = mode.MaximumLoadFactor > 0f ? mode.MaximumLoadFactor * GRAVITY / Mathf.Max(vessel.Airspeed, 1f) : float.PositiveInfinity;
			var flightPathRate = vessel.PitchRate - angleOfAttackRate;
			var comingAngleOfAttack = angleOfAttack + angleOfAttackRate * (mode.RateResponse + mode.ControlLag);
			var upMargin = mode.MaximumAngleOfAttack * Mathf.Deg2Rad - comingAngleOfAttack;
			var downMargin = mode.MaximumNegativeAngleOfAttack * Mathf.Deg2Rad + comingAngleOfAttack;
			var angleOfAttackUp = flightPathRate + upMargin / mode.AttitudeResponse;
			var angleOfAttackDown = flightPathRate - downMargin / mode.AttitudeResponse;

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
		pitchInput = pitch.Step(pitchCommand, 0f, vessel.PitchRate, pitchDown, pitchUp, vessel.PitchAuthority, vessel.PitchSlewShare, vessel.SlewSpeed, mode, deltaTime);
		yawInput = yaw.Step(yawCommand, yawTargetRate, vessel.YawRate, -maximumYaw, maximumYaw, vessel.YawAuthority, vessel.YawSlewShare, vessel.SlewSpeed, mode, deltaTime);
		rollInput = roll.Step(rollCommand, 0f, vessel.RollRate, -maximumRoll, maximumRoll, vessel.RollAuthority, vessel.RollSlewShare, vessel.SlewSpeed, mode, deltaTime);
	}
}

using UnityEngine;

namespace MouseAimFlightRedux.Control;

/// <summary>
/// Turns an aim direction into pitch, yaw and roll inputs. Guidance picks an angle error per axis, then each axis runs
/// an attitude loop and a rate loop. See docs/DESIGN.md, "Controller".
/// </summary>
public sealed class Autopilot
{
	/// <summary>One standard g, m/s².</summary>
	const float Gravity = 9.80665f;

	/// <summary>Dynamic pressure, kPa, over which aircraft behaviour fades in.</summary>
	const float AeroStart = 0.3f;

	const float AeroFull = 1.5f;

	/// <summary>How strongly yaw works to keep the nose on the flight path while turning.</summary>
	const float SideslipGain = 0.5f;

	readonly AxisController pitch = new();
	readonly AxisController yaw = new();
	readonly AxisController roll = new();

	public void Reset()
	{
		pitch.Reset();
		yaw.Reset();
		roll.Reset();
	}

	/// <summary>The inputs that actually reached the vessel, after any the pilot overrode.</summary>
	public void Applied(float pitchInput, float yawInput, float rollInput)
	{
		pitch.Applied(pitchInput);
		yaw.Applied(yawInput);
		roll.Applied(rollInput);
	}

	/// <summary>
	/// Inputs follow FlightCtrlState: positive pitch is nose up, positive yaw is nose right, positive roll is right wing
	/// down.
	/// </summary>
	public void Drive(VesselDynamics vessel, Vector3 aimDirection, FlightMode mode, float dt, out float pitchInput, out float yawInput, out float rollInput)
	{
		var aim = aimDirection.normalized;
		var along = Vector3.Dot(aim, vessel.Nose);
		var above = Vector3.Dot(aim, vessel.Canopy);
		var right = Vector3.Dot(aim, vessel.Right);

		var pitchError = Mathf.Atan2(above, along);
		var yawError = Mathf.Atan2(right, along);
		var offNose = Mathf.Acos(Mathf.Clamp(along, -1f, 1f));

		var aero = SmoothStep(AeroStart, AeroFull, vessel.DynamicPressure);
		var commit = SmoothStep(mode.BankBlendStart, mode.BankBlendEnd, offNose * Mathf.Rad2Deg);

		// Bank: toward putting the aim above the canopy, or toward wings level, blended as directions in the plane
		// across the nose so the blend never wraps through ±180°.
		var aimAround = new Vector2(right, above);
		var levelAround = new Vector2(Vector3.Dot(vessel.Up, vessel.Right), Vector3.Dot(vessel.Up, vessel.Canopy));
		var wanted = commit * Normalized(aimAround) + (1f - commit) * Normalized(levelAround);
		var bankError = wanted.sqrMagnitude > 1e-6f ? Mathf.Atan2(wanted.x, wanted.y) : 0f;

		if (mode.MaxBank < 180f && levelAround.sqrMagnitude > 1e-4f)
		{
			var bank = Mathf.Atan2(-levelAround.x, levelAround.y);
			var limit = mode.MaxBank * Mathf.Deg2Rad;
			var target = Mathf.Clamp(WrapAngle(bank + bankError), -limit, limit);
			bankError = target - bank;
		}

		// Pitch: once committed to the bank, pull by the whole angle off the nose, scaled by how well the bank has put
		// the aim overhead. Never push while rolling toward it.
		var aimRoll = Mathf.Atan2(right, above);
		var pull = offNose * Mathf.Max(0f, Mathf.Cos(aimRoll));
		var aeroPitch = Mathf.Lerp(pitchError, pull, commit);

		// Yaw: trims the last few degrees, fading out as the bank takes over, and keeps the turn coordinated.
		var aeroYaw = (1f - commit) * yawError + SideslipGain * vessel.Sideslip;

		var pitchCommand = Mathf.Lerp(pitchError, aeroPitch, aero);
		var yawCommand = Mathf.Lerp(yawError, aeroYaw, aero);
		var rollCommand = aero * bankError;

		var maxPitch = mode.MaxPitchRate * Mathf.Deg2Rad;
		var pitchUp = maxPitch;
		var pitchDown = -maxPitch;
		if (aero > 0f)
		{
			// Load factor: in a turn the flight path rotates at n·g/V.
			var gRate = mode.MaxG * Gravity / Mathf.Max(vessel.Airspeed, 1f);

			// Angle of attack: allow the current pitch rate plus whatever closes the remaining margin, so a turn held
			// at the limit keeps turning and one past it backs off.
			var upMargin = mode.MaxAoA * Mathf.Deg2Rad - vessel.AngleOfAttack;
			var downMargin = mode.MaxNegativeAoA * Mathf.Deg2Rad + vessel.AngleOfAttack;
			var aoaUp = vessel.PitchRate + upMargin / mode.AttitudeResponse;
			var aoaDown = vessel.PitchRate - downMargin / mode.AttitudeResponse;

			pitchUp = Mathf.Lerp(pitchUp, Mathf.Min(maxPitch, Mathf.Min(gRate, aoaUp)), aero);
			pitchDown = Mathf.Lerp(pitchDown, Mathf.Max(-maxPitch, Mathf.Max(-0.5f * gRate, aoaDown)), aero);
		}

		var maxYaw = mode.MaxYawRate * Mathf.Deg2Rad;
		var maxRoll = mode.MaxRollRate * Mathf.Deg2Rad;

		pitchInput = pitch.Step(pitchCommand, vessel.PitchRate, pitchDown, pitchUp, vessel.PitchAuthority, mode, dt);
		yawInput = yaw.Step(yawCommand, vessel.YawRate, -maxYaw, maxYaw, vessel.YawAuthority, mode, dt);
		rollInput = roll.Step(rollCommand, vessel.RollRate, -maxRoll, maxRoll, vessel.RollAuthority, mode, dt);
	}

	static float SmoothStep(float from, float to, float value) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, value));

	static Vector2 Normalized(Vector2 v) => v.sqrMagnitude > 1e-8f ? v.normalized : Vector2.zero;

	static float WrapAngle(float radians) => Mathf.Repeat(radians + Mathf.PI, 2f * Mathf.PI) - Mathf.PI;
}

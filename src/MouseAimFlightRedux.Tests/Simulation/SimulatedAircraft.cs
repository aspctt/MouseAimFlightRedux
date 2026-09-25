//// Dependencies

using System;
using MouseAimFlightRedux.Control;
using UnityEngine;

namespace MouseAimFlightRedux.Tests.Simulation;

/// <summary>
/// A rigid aircraft flown one physics step at a time and measured the way
/// <see cref="VesselDynamics"/> measures a vessel. Each axis turns on its own under its
/// control surfaces, reaction wheels, aerodynamic stability and damping, and the flight
/// path turns under lift, side force and gravity at a constant airspeed. Nothing couples
/// the axes but geometry, which is where the guidance does its work. The world is flat,
/// with up along y. The controller sees the airframe's measurement noise; the public
/// members are the true values.
/// </summary>
public sealed class SimulatedAircraft : IVesselDynamics
{
	//// Constants

	/// <summary>One g for reporting load factor, m/s².</summary>
	const float STANDARD_GRAVITY = 9.81f;

	/// <summary>
	/// Seed for the measurement noise, so every run of a test flies the same flight.
	/// </summary>
	const int NOISE_SEED = 1;

	//// References and State

	readonly Airframe airframe;
	Vector3 nose;
	Vector3 canopy;
	Vector3 right;
	Vector3 velocity;
	float pitchDeflection;
	float yawDeflection;
	float rollDeflection;
	readonly System.Random random = new(NOISE_SEED);

	/// <summary>
	/// The errors in this step's measurements, rad and rad/s.
	/// </summary>
	float angleOfAttackNoise;

	float sideslipNoise;
	float pitchRateNoise;
	float yawRateNoise;
	float rollRateNoise;

	public float PitchRate { get; private set; }
	public float YawRate { get; private set; }
	public float RollRate { get; private set; }

	/// <summary>
	/// Acceleration from lift and side force along the canopy on the last step, g. What
	/// the pilot feels, and what KSP's G meter shows in level flight.
	/// </summary>
	public float LoadFactor { get; private set; }

	//// Private Functions

	static bool IsSlewing(Airframe airframe) => !float.IsPositiveInfinity(airframe.SlewSpeed);

	/// <summary>
	/// Turns a pair of the airframe's axes in their own plane, the first toward the
	/// second.
	/// </summary>
	static void Turn(ref Vector3 from, ref Vector3 toward, float angle)
	{
		var cosine = Mathf.Cos(angle);
		var sine = Mathf.Sin(angle);
		var turnedFrom = from * cosine + toward * sine;
		toward = toward * cosine - from * sine;
		from = turnedFrom;
	}

	/// <summary>
	/// The part of a direction square to the flight path, as a unit vector, or zero along
	/// it.
	/// </summary>
	static Vector3 Across(Vector3 direction, Vector3 flightPath)
	{
		var across = direction - Vector3.Dot(direction, flightPath) * flightPath;
		return across.sqrMagnitude > 1e-8f ? across.normalized : Vector3.zero;
	}

	float Authority(float surfaceAuthority) => (surfaceAuthority * DynamicPressure + airframe.WheelAuthority) * airframe.AuthorityEstimate;

	float SlewShare(float surfaceAuthority)
	{
		// Sanity check
		var surfaceTorque = surfaceAuthority * DynamicPressure;
		var torque = surfaceTorque + airframe.WheelAuthority;
		if (!IsSlewing(airframe) || torque <= 0f)
			return 0f;

		return surfaceTorque / torque;
	}

	/// <summary>
	/// A normally distributed random error with this standard deviation, by the
	/// Box-Muller transform.
	/// </summary>
	float Noise(float deviation)
	{
		// Sanity check
		if (deviation <= 0f)
			return 0f;

		// Draw it
		// System.Random works in doubles and the simulation in floats, as Unity does. The
		// error never needs more precision than a float holds.
		var uniform = 1.0 - random.NextDouble();
		var angle = 2.0 * Math.PI * random.NextDouble();
		return deviation * (float)(Math.Sqrt(-2.0 * Math.Log(uniform)) * Math.Cos(angle));
	}

	/// <summary>Moves a control surface toward its input for one step.</summary>
	float MoveSurface(float deflection, float input, float deltaTime)
	{
		// Ease toward the input
		input = Mathf.Clamp(input, -1f, 1f);
		if (!IsSlewing(airframe))
			return deflection + (input - deflection) * (1f - Mathf.Exp(-deltaTime / airframe.SurfaceLag));

		// Or move toward it at a fixed speed
		var slew = airframe.SlewSpeed * deltaTime;
		return deflection + Mathf.Clamp(input - deflection, -slew, slew);
	}

	//// Public API

	/// <summary>
	/// Starts in level flight along z with the wings level, at the angle of attack that
	/// holds 1 g, and with the controls centred.
	/// </summary>
	public SimulatedAircraft(Airframe airframe)
	{
		// Find the angle of attack for level flight
		this.airframe = airframe;
		var liftPerRadian = airframe.LiftSlope * DynamicPressure;
		var trim = liftPerRadian > 0f ? Mathf.Min(airframe.Gravity / liftPerRadian, 0.3f) : 0f;

		// Pitch the nose up by it
		nose = Vector3.forward * Mathf.Cos(trim) + Vector3.up * Mathf.Sin(trim);
		canopy = Vector3.up * Mathf.Cos(trim) - Vector3.forward * Mathf.Sin(trim);
		right = Vector3.right;
		velocity = Vector3.forward * airframe.Airspeed;
	}

	public Airframe Airframe => airframe;

	public Vector3 Nose => nose;
	public Vector3 Canopy => canopy;
	public Vector3 Right => right;
	public Vector3 Up => Vector3.up;

	public float PitchAuthority => Authority(airframe.PitchSurfaceAuthority);
	public float YawAuthority => Authority(airframe.YawSurfaceAuthority);
	public float RollAuthority => Authority(airframe.RollSurfaceAuthority);

	public float SlewSpeed => airframe.SlewSpeed;
	public float PitchSlewShare => SlewShare(airframe.PitchSurfaceAuthority);
	public float YawSlewShare => SlewShare(airframe.YawSurfaceAuthority);
	public float RollSlewShare => SlewShare(airframe.RollSurfaceAuthority);

	public float DynamicPressure => 0.5f * airframe.AirDensity * airframe.Airspeed * airframe.Airspeed / 1000f;
	public float Airspeed => airframe.Airspeed;
	public float AngleOfAttack => Mathf.Atan2(-Vector3.Dot(velocity, canopy), Vector3.Dot(velocity, nose));
	public float Sideslip => Mathf.Atan2(Vector3.Dot(velocity, right), Vector3.Dot(velocity, nose));

	/// <summary>
	/// Right wing down positive, rad. Undefined with the nose straight up or down.
	/// </summary>
	public float Bank => Mathf.Atan2(-Vector3.Dot(Up, right), Vector3.Dot(Up, canopy));

	float IVesselDynamics.PitchRate => PitchRate + pitchRateNoise;
	float IVesselDynamics.YawRate => YawRate + yawRateNoise;
	float IVesselDynamics.RollRate => RollRate + rollRateNoise;
	float IVesselDynamics.AngleOfAttack => AngleOfAttack + angleOfAttackNoise;
	float IVesselDynamics.Sideslip => Sideslip + sideslipNoise;

	/// <summary>Flies one physics step with these inputs.</summary>
	public void Step(float pitchInput, float yawInput, float rollInput, float deltaTime)
	{
		// Move the control surfaces
		pitchDeflection = MoveSurface(pitchDeflection, pitchInput, deltaTime);
		yawDeflection = MoveSurface(yawDeflection, yawInput, deltaTime);
		rollDeflection = MoveSurface(rollDeflection, rollInput, deltaTime);

		// Speed up or slow down each axis
		// Wheels answer the input at once. Surfaces answer where they've got to.
		var dynamicPressure = DynamicPressure;
		var angleOfAttack = AngleOfAttack;
		var sideslip = Sideslip;
		var pitchAcceleration = (airframe.PitchSurfaceAuthority * pitchDeflection + airframe.PitchStability * angleOfAttack - airframe.PitchDamping * PitchRate) * dynamicPressure + airframe.WheelAuthority * Mathf.Clamp(pitchInput, -1f, 1f);
		var yawAcceleration = (airframe.YawSurfaceAuthority * yawDeflection + airframe.YawStability * sideslip - airframe.YawDamping * YawRate) * dynamicPressure + airframe.WheelAuthority * Mathf.Clamp(yawInput, -1f, 1f);
		var rollAcceleration = (airframe.RollSurfaceAuthority * rollDeflection - airframe.RollDamping * RollRate) * dynamicPressure + airframe.WheelAuthority * Mathf.Clamp(rollInput, -1f, 1f);
		PitchRate += pitchAcceleration * deltaTime;
		YawRate += yawAcceleration * deltaTime;
		RollRate += rollAcceleration * deltaTime;

		// Turn the airframe by its rates
		// Pitch turns the nose toward the canopy, yaw the nose toward the right wing, and
		// roll the canopy toward the right wing, matching the signs in docs/DESIGN.md.
		Turn(ref nose, ref canopy, PitchRate * deltaTime);
		Turn(ref nose, ref right, YawRate * deltaTime);
		Turn(ref canopy, ref right, RollRate * deltaTime);
		nose = nose.normalized;
		canopy = (canopy - Vector3.Dot(canopy, nose) * nose).normalized;
		right = (right - Vector3.Dot(right, nose) * nose - Vector3.Dot(right, canopy) * canopy).normalized;

		// Turn the flight path
		// Lift works across the path toward the canopy side, side force against the
		// sideslip, and the airspeed stays put.
		var flightPath = velocity.normalized;
		var lift = airframe.LiftSlope * dynamicPressure * angleOfAttack * Across(canopy, flightPath);
		var sideForce = -airframe.SideForceSlope * dynamicPressure * sideslip * Across(right, flightPath);
		var acceleration = lift + sideForce - airframe.Gravity * Up;
		var turning = acceleration - Vector3.Dot(acceleration, flightPath) * flightPath;
		velocity = (velocity + turning * deltaTime).normalized * airframe.Airspeed;
		LoadFactor = Vector3.Dot(lift + sideForce, canopy) / STANDARD_GRAVITY;

		// Take the errors for the next measurements
		angleOfAttackNoise = Noise(airframe.AirflowNoise * Mathf.Deg2Rad);
		sideslipNoise = Noise(airframe.AirflowNoise * Mathf.Deg2Rad);
		pitchRateNoise = Noise(airframe.RateNoise * Mathf.Deg2Rad);
		yawRateNoise = Noise(airframe.RateNoise * Mathf.Deg2Rad);
		rollRateNoise = Noise(airframe.RateNoise * Mathf.Deg2Rad);
	}
}

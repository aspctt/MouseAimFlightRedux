//// Dependencies

using System;

namespace MouseAimFlightRedux.Tests.Simulation;

/// <summary>
/// The numbers a <see cref="SimulatedAircraft"/> flies by. Aerodynamic terms are given
/// per kPa of dynamic pressure, so an airframe flown faster gets stiffer and more
/// responsive the way a real one does. The presets are made up to span what KSP planes
/// do, not measured from any craft.
/// </summary>
public sealed class Airframe
{
	//// References and State

	public string Name = "";

	/// <summary>Held constant, m/s, as if the throttle always matched the drag.</summary>
	public float Airspeed = 150f;

	/// <summary>kg/m³. Sea level air by default, zero for space.</summary>
	public float AirDensity = 1.225f;

	/// <summary>m/s².</summary>
	public float Gravity = 9.81f;

	/// <summary>
	/// Lift acceleration per radian of angle of attack, (m/s²)/(rad·kPa). Linear, with no
	/// stall, so the controller's own limits are all that stop a pull.
	/// </summary>
	public float LiftSlope;

	/// <summary>
	/// Side force acceleration per radian of sideslip, (m/s²)/(rad·kPa).
	/// </summary>
	public float SideForceSlope;

	/// <summary>
	/// Angular acceleration from a full control surface input, rad/s² per kPa.
	/// </summary>
	public float PitchSurfaceAuthority;

	public float YawSurfaceAuthority;
	public float RollSurfaceAuthority;

	/// <summary>
	/// Angular acceleration from full reaction wheel input on each axis, rad/s². Wheels
	/// act at once and don't care about the air.
	/// </summary>
	public float WheelAuthority;

	/// <summary>
	/// Pitch acceleration per radian of angle of attack, rad/s² per kPa. Negative is
	/// stable: the nose swings back toward the flight path.
	/// </summary>
	public float PitchStability;

	/// <summary>
	/// Yaw acceleration per radian of sideslip, rad/s² per kPa. Positive is stable: the
	/// nose swings into the airflow like a weathervane.
	/// </summary>
	public float YawStability;

	/// <summary>Aerodynamic damping on each axis, 1/s per kPa.</summary>
	public float PitchDamping;

	public float YawDamping;
	public float RollDamping;

	/// <summary>
	/// Time constant of control surfaces that ease into position, s. Only used when
	/// <see cref="SlewSpeed"/> is infinite.
	/// </summary>
	public float SurfaceLag = 0.1f;

	/// <summary>
	/// Speed of control surfaces that move at a fixed speed, full inputs per second, or
	/// infinity for surfaces that ease into position.
	/// </summary>
	public float SlewSpeed = float.PositiveInfinity;

	/// <summary>
	/// How the authority told to the controller compares with the real one. KSP's figure
	/// comes from each part's reported torque, which is never exact.
	/// </summary>
	public float AuthorityEstimate = 1f;

	/// <summary>
	/// Random error on each measurement of angle of attack and sideslip, degrees, as a
	/// standard deviation. Craft in KSP flex and jitter a little from step to step.
	/// </summary>
	public float AirflowNoise;

	/// <summary>Random error on each measured rate, degrees per second.</summary>
	public float RateNoise;

	//// Public API

	/// <summary>
	/// A light, stable jet at 150 m/s. It pulls 1 g at about 3° angle of attack and 6 g
	/// at 18°, rolls at up to about 170°/s, and holds full elevator at about 38°.
	/// </summary>
	public static Airframe Fighter() => new()
	{
		Name = "Fighter",
		Airspeed = 150f,
		LiftSlope = 14.2f,
		SideForceSlope = 3f,
		PitchSurfaceAuthority = 0.4f,
		YawSurfaceAuthority = 0.15f,
		RollSurfaceAuthority = 1.2f,
		WheelAuthority = 0.3f,
		PitchStability = -0.6f,
		YawStability = 0.35f,
		PitchDamping = 0.12f,
		YawDamping = 0.08f,
		RollDamping = 0.4f,
		SurfaceLag = 0.08f,
	};

	/// <summary>
	/// A heavy, sluggish cargo plane at 120 m/s, rolling at up to about 57°/s. Full
	/// elevator only holds about 23° angle of attack.
	/// </summary>
	public static Airframe Cargo() => new()
	{
		Name = "Cargo",
		Airspeed = 120f,
		LiftSlope = 20f,
		SideForceSlope = 4f,
		PitchSurfaceAuthority = 0.1f,
		YawSurfaceAuthority = 0.05f,
		RollSurfaceAuthority = 0.25f,
		WheelAuthority = 0.05f,
		PitchStability = -0.25f,
		YawStability = 0.15f,
		PitchDamping = 0.15f,
		YawDamping = 0.1f,
		RollDamping = 0.25f,
		SurfaceLag = 0.12f,
	};

	/// <summary>
	/// The fighter with a slightly unstable pitch, as some KSP builds are.
	/// </summary>
	public static Airframe Unstable()
	{
		var airframe = Fighter();
		airframe.Name = "Unstable";
		airframe.PitchStability = 0.05f;
		return airframe;
	}

	/// <summary>
	/// The fighter with Atmosphere Autopilot's control surfaces, which move at 2 full
	/// inputs per second (its <c>SyncModuleControlSurface.CSURF_SPD</c> in 1.6.1).
	/// </summary>
	public static Airframe AtmosphereAutopilotSurfaces()
	{
		var airframe = Fighter();
		airframe.Name = "AA surfaces";
		airframe.SlewSpeed = 2f;
		return airframe;
	}

	/// <summary>
	/// The fighter slowed to 100 m/s, where it runs out of angle of attack well before it
	/// reaches any mode's load limit.
	/// </summary>
	public static Airframe SlowFighter()
	{
		var airframe = Fighter();
		airframe.Name = "Slow fighter";
		airframe.Airspeed = 100f;
		return airframe;
	}

	/// <summary>
	/// The fighter sped up to 250 m/s, where it reaches any mode's load limit well before
	/// its angle of attack limit.
	/// </summary>
	public static Airframe FastFighter()
	{
		var airframe = Fighter();
		airframe.Name = "Fast fighter";
		airframe.Airspeed = 250f;
		return airframe;
	}

	/// <summary>
	/// The fighter with its authority told to the controller as double or half.
	/// </summary>
	public static Airframe MisjudgedFighter(float authorityEstimate)
	{
		var airframe = Fighter();
		airframe.Name = FormattableString.Invariant($"Fighter, authority told ×{authorityEstimate}");
		airframe.AuthorityEstimate = authorityEstimate;
		return airframe;
	}

	/// <summary>
	/// The fighter measured with random errors of 0.2° on angle of attack and sideslip
	/// and 0.5°/s on each rate, fresh every step. Harsher than a rigid craft in KSP, to
	/// show whether the controller turns jitter into stick movement.
	/// </summary>
	public static Airframe NoisyFighter()
	{
		var airframe = Fighter();
		airframe.Name = "Noisy fighter";
		airframe.AirflowNoise = 0.2f;
		airframe.RateNoise = 0.5f;
		return airframe;
	}

	/// <summary>A probe in space, turned by reaction wheels alone.</summary>
	public static Airframe Probe() => new()
	{
		Name = "Probe",
		Airspeed = 2000f,
		AirDensity = 0f,
		Gravity = 0f,
		WheelAuthority = 0.5f,
	};
}

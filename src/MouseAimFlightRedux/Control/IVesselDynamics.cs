//// Dependencies

using UnityEngine;

namespace MouseAimFlightRedux.Control;

/// <summary>
/// What the controller needs to know about a vessel, measured in its reference frame. See
/// docs/DESIGN.md, "Frames and conventions" and "Authority". In game this comes from
/// <see cref="VesselDynamics"/>; the tests fly a simulated aircraft through it instead.
/// </summary>
public interface IVesselDynamics
{
	//// Public API

	Vector3 Nose { get; }
	Vector3 Canopy { get; }
	Vector3 Right { get; }

	/// <summary>Away from the centre of the body being orbited.</summary>
	Vector3 Up { get; }

	/// <summary>Nose up positive, rad/s.</summary>
	float PitchRate { get; }

	/// <summary>Nose right positive, rad/s.</summary>
	float YawRate { get; }

	/// <summary>Right wing down positive, rad/s.</summary>
	float RollRate { get; }

	/// <summary>
	/// Angular acceleration a full input produces on each axis, rad/s².
	/// </summary>
	float PitchAuthority { get; }

	float YawAuthority { get; }
	float RollAuthority { get; }

	/// <summary>
	/// How fast the slowest fixed-speed controls move, in full inputs per second, or
	/// infinity if there are none. Stock surfaces ease into position instead; Atmosphere
	/// Autopilot's move at a fixed speed.
	/// </summary>
	float SlewSpeed { get; }

	/// <summary>
	/// Share of each axis's torque that comes from fixed-speed controls, 0 to 1.
	/// </summary>
	float PitchSlewShare { get; }

	float YawSlewShare { get; }
	float RollSlewShare { get; }

	/// <summary>kPa.</summary>
	float DynamicPressure { get; }

	/// <summary>Surface speed, m/s.</summary>
	float Airspeed { get; }

	/// <summary>Nose above the flight path positive, rad.</summary>
	float AngleOfAttack { get; }

	/// <summary>Flight path right of the nose positive, rad.</summary>
	float Sideslip { get; }
}

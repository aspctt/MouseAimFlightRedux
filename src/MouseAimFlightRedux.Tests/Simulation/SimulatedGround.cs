//// Dependencies

using MouseAimFlightRedux.Control;
using UnityEngine;

namespace MouseAimFlightRedux.Tests.Simulation;

/// <summary>
/// Flat ground at y = 0 under a simulated aircraft, with an optional ridge across the
/// starting course: a straight-sided rise to a peak, the same both ways.
/// </summary>
public sealed class SimulatedGround : ITerrain
{
	//// References and State

	readonly SimulatedAircraft aircraft;

	/// <summary>How far along the starting course the ridge peaks, m.</summary>
	public float RidgeDistance;

	/// <summary>How high the ridge peaks, m. Zero for none.</summary>
	public float RidgeHeight;

	/// <summary>
	/// How far either side of its peak the ridge reaches down to flat ground, m.
	/// </summary>
	public float RidgeHalfWidth = 1f;

	//// Public API

	public SimulatedGround(SimulatedAircraft aircraft)
	{
		this.aircraft = aircraft;
	}

	/// <summary>The height of the ground under a point, m.</summary>
	public float HeightAt(Vector3 point) => RidgeHeight * Mathf.Max(0f, 1f - Mathf.Abs(point.z - RidgeDistance) / RidgeHalfWidth);

	public float HeightAbove(Vector3 offset)
	{
		var point = aircraft.Position + offset;
		return point.y - HeightAt(point);
	}
}

//// Dependencies

using UnityEngine;

namespace MouseAimFlightRedux.Control;

/// <summary>
/// The terrain and sea of the body a vessel is flying over, read from the body's own
/// height map. Buildings, such as those at the space center, aren't part of it.
/// </summary>
public sealed class BodyTerrain : ITerrain
{
	//// References and State

	readonly Vessel vessel;

	//// Public API

	public BodyTerrain(Vessel vessel)
	{
		this.vessel = vessel;
	}

	public float HeightAbove(Vector3 offset)
	{
		// Find the point over the body
		var body = vessel.mainBody;
		var position = vessel.CoMD + new Vector3d(offset.x, offset.y, offset.z);
		var latitude = body.GetLatitude(position);
		var longitude = body.GetLongitude(position);

		// Measure it against the ground, or the sea where the ground is below it
		// Positions and heights are doubles in KSP. The difference fits a float easily.
		var ground = body.TerrainAltitude(latitude, longitude, !body.ocean);
		return (float)(body.GetAltitude(position) - ground);
	}
}

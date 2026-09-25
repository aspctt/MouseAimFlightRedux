//// Dependencies

using UnityEngine;

namespace MouseAimFlightRedux.Control;

/// <summary>
/// The ground around a vessel, for terrain avoidance. In game this is the body's terrain
/// and sea; the tests fly over made-up ground instead.
/// </summary>
public interface ITerrain
{
	//// Public API

	/// <summary>
	/// How high a point is above the ground or sea directly beneath it, m. The point is
	/// an offset from the vessel's centre of mass, in world space.
	/// </summary>
	float HeightAbove(Vector3 offset);
}

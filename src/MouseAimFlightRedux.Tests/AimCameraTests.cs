//// Dependencies

using NUnit.Framework;
using UnityEngine;

namespace MouseAimFlightRedux.Tests;

/// <summary>
/// Checks where the camera sits against the aim, per docs/DESIGN.md, "Camera".
/// </summary>
[TestFixture]
public sealed class AimCameraTests
{
	//// Constants

	/// <summary>Angles are good to this, degrees.</summary>
	const float TOLERANCE = 0.01f;

	//// Private Functions

	/// <summary>
	/// The camera sits ELEVATION above the line through the aim, behind it, on the side
	/// the given up points to.
	/// </summary>
	static void AssertBehindAndAbove(Vector3 offset, Vector3 aimDirection, Vector3 up)
	{
		using (Assert.EnterMultipleScope())
		{
			Assert.That(offset.magnitude, Is.EqualTo(1f).Within(1e-5f), "length");
			Assert.That(Vector3.Angle(offset, -aimDirection), Is.EqualTo(AimCamera.ELEVATION).Within(TOLERANCE), "elevation");
			Assert.That(Vector3.Dot(offset, Vector3.Cross(aimDirection, up)), Is.EqualTo(0f).Within(1e-5f), "off to the side");
			Assert.That(Vector3.Dot(offset, up), Is.GreaterThan(0f), "below the aim");
		}
	}

	//// Public API

	[Test]
	public void SitsBehindAndAboveALevelAim()
	{
		var offset = AimCamera.OffsetDirection(Vector3.forward, Vector3.up, Vector3.forward);
		AssertBehindAndAbove(offset, Vector3.forward, Vector3.up);
	}

	[Test]
	public void LiftsTowardTheCameraUpInAClimb()
	{
		// Aim 70° up with the camera still level
		// Built by hand, since Unity's Quaternion needs the engine to run.
		var climb = 70f * Mathf.Deg2Rad;
		var aimDirection = new Vector3(0f, Mathf.Sin(climb), Mathf.Cos(climb));
		var offset = AimCamera.OffsetDirection(aimDirection, Vector3.up, Vector3.forward);

		// Above the aim means toward the camera's up, across the aim
		AssertBehindAndAbove(offset, aimDirection, Vector3.ProjectOnPlane(Vector3.up, aimDirection).normalized);
	}

	/// <summary>
	/// Aiming along the camera's up leaves no up across the aim, so the camera's back
	/// takes its place, as it would once the camera swings round.
	/// </summary>
	[Test]
	public void UsesTheCameraBackWithTheAimAlongItsUp()
	{
		var offset = AimCamera.OffsetDirection(Vector3.up, Vector3.up, Vector3.forward);
		AssertBehindAndAbove(offset, Vector3.up, Vector3.back);
	}
}

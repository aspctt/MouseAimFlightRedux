//// Dependencies

using UnityEngine;

namespace MouseAimFlightRedux;

/// <summary>
/// Swings KSP's flight camera round behind the aim, War Thunder style, so the mouse turns
/// the view and the aircraft follows. It only moves the camera, and KSP still runs it, so
/// zoom, camera modes and keeping clear of the ground work as before. See docs/DESIGN.md,
/// "Camera".
/// </summary>
public sealed class AimCamera
{
	//// Constants

	/// <summary>
	/// How far above the line through the aim the camera sits, degrees. KSP's camera always
	/// centres the vessel, so this lifts the aim that far above it on screen, clear of the
	/// aircraft.
	/// </summary>
	public const float ELEVATION = 8f;

	/// <summary>Time constant of the camera catching up with the aim, s.</summary>
	const float RESPONSE = 0.08f;

	/// <summary>
	/// Below this squared length, the camera's up lies too close along the aim to say
	/// which way is up across it.
	/// </summary>
	const float MINIMUM_UP_SQUARED = 1e-4f;

	//// References and State

	/// <summary>
	/// The way from the camera's pivot to where it sits, in world space. Kept here rather
	/// than read back each frame, so a frame of reference KSP turns with the vessel can't
	/// drag the camera off the aim over time.
	/// </summary>
	Vector3 offsetDirection;

	public bool IsFollowing { get; private set; }

	//// Private Functions

	/// <summary>
	/// Whether KSP's flight camera is showing and looking at this vessel, and not handed
	/// to another mod.
	/// </summary>
	static bool CanMove(FlightCamera camera, Vessel vessel)
	{
		var manager = CameraManager.Instance;
		return manager != null && manager.currentCameraMode == CameraManager.CameraMode.Flight && camera.updateActive && camera.targetMode == FlightCamera.TargetMode.Vessel && camera.vesselTarget == vessel;
	}

	//// Public API

	/// <summary>
	/// Where the camera sits from its pivot to look along the aim from ELEVATION above it:
	/// behind the aim, lifted toward the camera's up.
	/// </summary>
	public static Vector3 OffsetDirection(Vector3 aimDirection, Vector3 cameraUp, Vector3 cameraForward)
	{
		// Find up across the aim
		// With the aim along the camera's up, as after looking away with free look, the
		// camera's back is what turns up once it swings round.
		var up = Vector3.ProjectOnPlane(cameraUp, aimDirection);
		if (up.sqrMagnitude < MINIMUM_UP_SQUARED)
			up = Vector3.ProjectOnPlane(-cameraForward, aimDirection);
		up.Normalize();

		// Lift the camera above the line through the aim
		var elevation = ELEVATION * Mathf.Deg2Rad;
		return -aimDirection * Mathf.Cos(elevation) + up * Mathf.Sin(elevation);
	}

	/// <summary>
	/// Moves the camera toward its place behind the aim. Call from Update, so KSP's camera
	/// applies it in the same frame.
	/// </summary>
	public void Follow(Vessel vessel, Vector3 aim, float deltaTime)
	{
		// Sanity check
		var camera = FlightCamera.fetch;
		if (camera == null || !CanMove(camera, vessel))
		{
			IsFollowing = false;
			return;
		}

		// Start from wherever the camera is
		var pivot = camera.GetPivot().position;
		var cameraTransform = camera.transform;
		if (!IsFollowing)
		{
			offsetDirection = (cameraTransform.position - pivot).normalized;
			IsFollowing = true;
		}

		// Swing toward the aim
		var target = OffsetDirection(aim.normalized, cameraTransform.up, cameraTransform.forward);
		offsetDirection = Vector3.Slerp(offsetDirection, target, 1f - Mathf.Exp(-deltaTime / RESPONSE));

		// Place the camera, keeping its distance so zoom still works
		// KSP works out the camera's heading, pitch and distance from the position, in
		// whichever frame of reference its camera mode uses.
		camera.SetCamCoordsFromPosition(pivot + offsetDirection * camera.Distance);
	}

	/// <summary>
	/// Leaves the camera where it is. The next Follow starts from there.
	/// </summary>
	public void Release() => IsFollowing = false;
}

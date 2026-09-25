//// Dependencies

using System;
using System.Collections.Generic;
using UnityEngine;

namespace MouseAimFlightRedux.Control;

/// <summary>
/// Measures a KSP vessel for the controller. The members it reads are described on
/// <see cref="IVesselDynamics"/>.
/// </summary>
public sealed class VesselDynamics : IVesselDynamics
{
	//// Constants

	const float TORQUE_REFRESH_INTERVAL = 0.2f;
	const float INERTIA_REFRESH_INTERVAL = 1f;

	/// <summary>
	/// Floor on angular acceleration per full input, in rad/s², so axes with nothing to
	/// steer them stay finite.
	/// </summary>
	const float MINIMUM_AUTHORITY = 0.05f;

	//// References and State

	readonly Vessel vessel;
	readonly List<ITorqueProvider> torqueProviders = new();
	int providerPartCount = -1;
	float torqueTimer;
	float inertiaTimer;

	/// <summary>Available torque in kN·m: x pitch, y roll, z yaw.</summary>
	Vector3 torque;

	/// <summary>
	/// The part of <see cref="torque"/> from controls that move at a fixed speed.
	/// </summary>
	Vector3 slewTorque;

	/// <summary>
	/// Moment of inertia in t·m² about the pitch (x), roll (y) and yaw (z) axes through
	/// the centre of mass.
	/// </summary>
	Vector3 inertia = Vector3.one;

	public bool IsValid { get; private set; }

	public Vector3 Nose { get; private set; }
	public Vector3 Canopy { get; private set; }
	public Vector3 Right { get; private set; }
	public Vector3 Up { get; private set; }

	public float PitchRate { get; private set; }
	public float YawRate { get; private set; }
	public float RollRate { get; private set; }

	public float PitchAuthority { get; private set; }
	public float YawAuthority { get; private set; }
	public float RollAuthority { get; private set; }

	public float SlewSpeed { get; private set; } = float.PositiveInfinity;
	public float PitchSlewShare { get; private set; }
	public float YawSlewShare { get; private set; }
	public float RollSlewShare { get; private set; }

	public float DynamicPressure { get; private set; }
	public float Airspeed { get; private set; }
	public float AngleOfAttack { get; private set; }
	public float Sideslip { get; private set; }
	public Vector3 Velocity { get; private set; }
	public float Gravity { get; private set; }

	//// Private Functions

	static float Share(float part, float whole) => whole > 1e-6f ? Mathf.Clamp01(part / whole) : 0f;

	void CollectTorqueProviders()
	{
		torqueProviders.Clear();
		torqueProviders.AddRange(vessel.FindPartModulesImplementing<ITorqueProvider>());
		providerPartCount = vessel.parts.Count;
		torqueTimer = 0f;
		inertiaTimer = 0f;
	}

	void MeasureTorque()
	{
		// Add up every provider's torque
		var isRcsOn = vessel.ActionGroups[KSPActionGroup.RCS];
		var sum = Vector3.zero;
		var slewSum = Vector3.zero;
		var slewSpeed = float.PositiveInfinity;
		for (var index = torqueProviders.Count - 1; index >= 0; index--)
		{
			var provider = torqueProviders[index];
			if (!isRcsOn && provider is ModuleRCS)
				continue;

			try
			{
				provider.GetPotentialTorque(out var positive, out var negative);
				var average = new Vector3((Mathf.Abs(positive.x) + Mathf.Abs(negative.x)) * 0.5f, (Mathf.Abs(positive.y) + Mathf.Abs(negative.y)) * 0.5f, (Mathf.Abs(positive.z) + Mathf.Abs(negative.z)) * 0.5f);
				sum += average;

				var speed = provider is ModuleControlSurface surface ? AtmosphereAutopilot.SurfaceSpeed(surface) : float.PositiveInfinity;
				if (!float.IsPositiveInfinity(speed))
				{
					slewSum += average;
					slewSpeed = Mathf.Min(slewSpeed, speed);
				}
			}
			catch (Exception exception)
			{
				// One bad module should cost its own torque, not every frame's log.
				Debug.LogWarning($"[MouseAimFlightRedux] Ignoring torque from {provider.GetType().Name}: {exception.Message}");
				torqueProviders.RemoveAt(index);
			}
		}

		// Keep the totals
		torque = sum;
		slewTorque = slewSum;
		SlewSpeed = slewSpeed;
	}

	void MeasureInertia()
	{
		// Add up every part about each axis
		var centreOfMass = vessel.CoM;
		var sum = Vector3.zero;
		foreach (var part in vessel.parts)
		{
			var rigidbody = part.rb;
			if (rigidbody == null)
				continue;

			var offset = rigidbody.worldCenterOfMass - centreOfMass;
			var principalRotation = rigidbody.rotation * rigidbody.inertiaTensorRotation;
			sum.x += AxisInertia(rigidbody, principalRotation, offset, Right);
			sum.y += AxisInertia(rigidbody, principalRotation, offset, Nose);
			sum.z += AxisInertia(rigidbody, principalRotation, offset, Canopy);
		}

		// Keep the totals, never zero
		inertia = Vector3.Max(sum, new Vector3(1e-3f, 1e-3f, 1e-3f));
	}

	/// <summary>
	/// One rigidbody's moment of inertia about a world axis through the vessel's centre
	/// of mass: its own principal moments projected onto the axis, plus mass times its
	/// distance from the axis squared.
	/// </summary>
	static float AxisInertia(Rigidbody rigidbody, Quaternion principalRotation, Vector3 offset, Vector3 axis)
	{
		var localAxis = Quaternion.Inverse(principalRotation) * axis;
		var moments = rigidbody.inertiaTensor;
		var ownInertia = moments.x * localAxis.x * localAxis.x + moments.y * localAxis.y * localAxis.y + moments.z * localAxis.z * localAxis.z;
		var offsetFromAxis = offset - Vector3.Dot(offset, axis) * axis;
		return ownInertia + rigidbody.mass * offsetFromAxis.sqrMagnitude;
	}

	//// Public API

	public VesselDynamics(Vessel vessel)
	{
		this.vessel = vessel;
	}

	/// <summary>
	/// Forces torque, inertia and the list of torque providers to be measured again on
	/// the next update.
	/// </summary>
	public void Invalidate()
	{
		providerPartCount = -1;
		torqueTimer = 0f;
		inertiaTimer = 0f;
	}

	public void Update(float deltaTime)
	{
		// Sanity check
		var rootPart = vessel.rootPart;
		var rigidbody = rootPart != null ? rootPart.rb : null;
		var referenceTransform = vessel.ReferenceTransform;
		IsValid = rigidbody != null && referenceTransform != null;
		if (rigidbody == null || referenceTransform == null)
			return;

		// Find the axes
		Nose = referenceTransform.up;
		Canopy = -referenceTransform.forward;
		Right = referenceTransform.right;
		Up = (vessel.CoMD - vessel.mainBody.position).normalized;

		// Measure the rates
		var angularVelocity = rigidbody.angularVelocity;
		var noseMotion = Vector3.Cross(angularVelocity, Nose);
		PitchRate = Vector3.Dot(noseMotion, Canopy);
		YawRate = Vector3.Dot(noseMotion, Right);
		RollRate = Vector3.Dot(Vector3.Cross(angularVelocity, Canopy), Right);

		// Measure the airflow
		// KSP keeps these as doubles. The controller works in floats, as Unity does, and
		// they never need more precision than that.
		DynamicPressure = (float)vessel.dynamicPressurekPa;
		Airspeed = (float)vessel.srfSpeed;
		Velocity = (Vector3)vessel.srf_velocity;
		var fromCentre = vessel.CoMD - vessel.mainBody.position;
		Gravity = (float)(vessel.mainBody.gravParameter / fromCentre.sqrMagnitude);
		if (Airspeed > 1f)
		{
			var velocity = vessel.GetSrfVelocity();
			var along = Vector3.Dot(velocity, Nose);
			AngleOfAttack = Mathf.Atan2(-Vector3.Dot(velocity, Canopy), along);
			Sideslip = Mathf.Atan2(Vector3.Dot(velocity, Right), along);
		}
		else
		{
			AngleOfAttack = 0f;
			Sideslip = 0f;
		}

		// Refresh torque and inertia now and then
		if (vessel.parts.Count != providerPartCount)
			CollectTorqueProviders();
		torqueTimer -= deltaTime;
		if (torqueTimer <= 0f)
		{
			torqueTimer = TORQUE_REFRESH_INTERVAL;
			MeasureTorque();
		}
		inertiaTimer -= deltaTime;
		if (inertiaTimer <= 0f)
		{
			inertiaTimer = INERTIA_REFRESH_INTERVAL;
			MeasureInertia();
		}

		// Work out the authority on each axis
		PitchAuthority = Mathf.Max(torque.x / inertia.x, MINIMUM_AUTHORITY);
		RollAuthority = Mathf.Max(torque.y / inertia.y, MINIMUM_AUTHORITY);
		YawAuthority = Mathf.Max(torque.z / inertia.z, MINIMUM_AUTHORITY);
		PitchSlewShare = Share(slewTorque.x, torque.x);
		RollSlewShare = Share(slewTorque.y, torque.y);
		YawSlewShare = Share(slewTorque.z, torque.z);
	}
}

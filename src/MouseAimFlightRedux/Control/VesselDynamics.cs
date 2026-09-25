using System;
using System.Collections.Generic;
using UnityEngine;

namespace MouseAimFlightRedux.Control;

/// <summary>
/// What the controller needs to know about a vessel, measured in its reference frame. See docs/DESIGN.md, "Frames and
/// conventions" and "Authority".
/// </summary>
public sealed class VesselDynamics
{
	const float TorqueRefreshInterval = 0.2f;
	const float InertiaRefreshInterval = 1f;

	/// <summary>Floor on angular acceleration per full input, in rad/s², so axes with nothing to steer them stay finite.</summary>
	const float MinAuthority = 0.05f;

	readonly Vessel vessel;
	readonly List<ITorqueProvider> torqueProviders = new();
	int providerPartCount = -1;
	float torqueTimer;
	float inertiaTimer;

	/// <summary>Available torque in kN·m: x pitch, y roll, z yaw.</summary>
	Vector3 torque;

	/// <summary>The part of <see cref="torque"/> from controls that move at a fixed speed.</summary>
	Vector3 slewTorque;

	/// <summary>Moment of inertia in t·m² about the pitch (x), roll (y) and yaw (z) axes through the centre of mass.</summary>
	Vector3 inertia = Vector3.one;

	public bool Valid { get; private set; }

	public Vector3 Nose { get; private set; }
	public Vector3 Canopy { get; private set; }
	public Vector3 Right { get; private set; }

	/// <summary>Away from the centre of the body being orbited.</summary>
	public Vector3 Up { get; private set; }

	/// <summary>Nose up positive, rad/s.</summary>
	public float PitchRate { get; private set; }

	/// <summary>Nose right positive, rad/s.</summary>
	public float YawRate { get; private set; }

	/// <summary>Right wing down positive, rad/s.</summary>
	public float RollRate { get; private set; }

	/// <summary>Angular acceleration a full input produces on each axis, rad/s².</summary>
	public float PitchAuthority { get; private set; }

	public float YawAuthority { get; private set; }
	public float RollAuthority { get; private set; }

	/// <summary>
	/// How fast the slowest fixed-speed controls move, in full inputs per second, or infinity if there are none. Stock
	/// surfaces ease into position instead; Atmosphere Autopilot's move at a fixed speed.
	/// </summary>
	public float SlewSpeed { get; private set; } = float.PositiveInfinity;

	/// <summary>Share of each axis's torque that comes from fixed-speed controls, 0 to 1.</summary>
	public float PitchSlewShare { get; private set; }

	public float YawSlewShare { get; private set; }
	public float RollSlewShare { get; private set; }

	/// <summary>kPa.</summary>
	public float DynamicPressure { get; private set; }

	/// <summary>Surface speed, m/s.</summary>
	public float Airspeed { get; private set; }

	/// <summary>Nose above the flight path positive, rad.</summary>
	public float AngleOfAttack { get; private set; }

	/// <summary>Flight path right of the nose positive, rad.</summary>
	public float Sideslip { get; private set; }

	public VesselDynamics(Vessel vessel)
	{
		this.vessel = vessel;
	}

	/// <summary>Forces torque, inertia and the list of torque providers to be measured again on the next update.</summary>
	public void Invalidate()
	{
		providerPartCount = -1;
		torqueTimer = 0f;
		inertiaTimer = 0f;
	}

	public void Update(float dt)
	{
		var root = vessel.rootPart;
		var body = root != null ? root.rb : null;
		var reference = vessel.ReferenceTransform;
		Valid = body != null && reference != null;
		if (!Valid)
			return;

		Nose = reference.up;
		Canopy = -reference.forward;
		Right = reference.right;
		Up = (vessel.CoMD - vessel.mainBody.position).normalized;

		var omega = body.angularVelocity;
		var noseMotion = Vector3.Cross(omega, Nose);
		PitchRate = Vector3.Dot(noseMotion, Canopy);
		YawRate = Vector3.Dot(noseMotion, Right);
		RollRate = Vector3.Dot(Vector3.Cross(omega, Canopy), Right);

		DynamicPressure = (float)vessel.dynamicPressurekPa;
		Airspeed = (float)vessel.srfSpeed;
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

		if (vessel.parts.Count != providerPartCount)
			CollectTorqueProviders();

		torqueTimer -= dt;
		if (torqueTimer <= 0f)
		{
			torqueTimer = TorqueRefreshInterval;
			MeasureTorque();
		}

		inertiaTimer -= dt;
		if (inertiaTimer <= 0f)
		{
			inertiaTimer = InertiaRefreshInterval;
			MeasureInertia();
		}

		PitchAuthority = Mathf.Max(torque.x / inertia.x, MinAuthority);
		RollAuthority = Mathf.Max(torque.y / inertia.y, MinAuthority);
		YawAuthority = Mathf.Max(torque.z / inertia.z, MinAuthority);

		PitchSlewShare = Share(slewTorque.x, torque.x);
		RollSlewShare = Share(slewTorque.y, torque.y);
		YawSlewShare = Share(slewTorque.z, torque.z);
	}

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
		var rcsOn = vessel.ActionGroups[KSPActionGroup.RCS];
		var sum = Vector3.zero;
		var slewSum = Vector3.zero;
		var slewSpeed = float.PositiveInfinity;
		for (var i = torqueProviders.Count - 1; i >= 0; i--)
		{
			var provider = torqueProviders[i];
			if (!rcsOn && provider is ModuleRCS)
				continue;

			try
			{
				provider.GetPotentialTorque(out var positive, out var negative);
				var average = new Vector3(
					(Mathf.Abs(positive.x) + Mathf.Abs(negative.x)) * 0.5f,
					(Mathf.Abs(positive.y) + Mathf.Abs(negative.y)) * 0.5f,
					(Mathf.Abs(positive.z) + Mathf.Abs(negative.z)) * 0.5f);
				sum += average;

				var speed = provider is ModuleControlSurface surface ? AtmosphereAutopilot.SurfaceSpeed(surface) : float.PositiveInfinity;
				if (!float.IsPositiveInfinity(speed))
				{
					slewSum += average;
					slewSpeed = Mathf.Min(slewSpeed, speed);
				}
			}
			catch (Exception e)
			{
				// One bad module should cost its own torque, not every frame's log.
				Debug.LogWarning($"[MouseAimFlightRedux] Ignoring torque from {provider.GetType().Name}: {e.Message}");
				torqueProviders.RemoveAt(i);
			}
		}
		torque = sum;
		slewTorque = slewSum;
		SlewSpeed = slewSpeed;
	}

	void MeasureInertia()
	{
		var centre = vessel.CoM;
		var right = Right;
		var nose = Nose;
		var canopy = Canopy;
		var sum = Vector3.zero;

		foreach (var part in vessel.parts)
		{
			var body = part.rb;
			if (body == null)
				continue;

			var offset = body.worldCenterOfMass - centre;
			var principal = body.rotation * body.inertiaTensorRotation;
			sum.x += AxisInertia(body, principal, offset, right);
			sum.y += AxisInertia(body, principal, offset, nose);
			sum.z += AxisInertia(body, principal, offset, canopy);
		}

		inertia = Vector3.Max(sum, new Vector3(1e-3f, 1e-3f, 1e-3f));
	}

	/// <summary>
	/// One rigidbody's moment of inertia about a world axis through the vessel's centre of mass: its own principal
	/// moments projected onto the axis, plus mass times its distance from the axis squared.
	/// </summary>
	static float AxisInertia(Rigidbody body, Quaternion principal, Vector3 offset, Vector3 axis)
	{
		var local = Quaternion.Inverse(principal) * axis;
		var moments = body.inertiaTensor;
		var own = moments.x * local.x * local.x + moments.y * local.y * local.y + moments.z * local.z * local.z;
		var fromAxis = offset - Vector3.Dot(offset, axis) * axis;
		return own + body.mass * fromAxis.sqrMagnitude;
	}
}

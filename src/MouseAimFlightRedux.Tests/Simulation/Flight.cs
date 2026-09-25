//// Dependencies

using System.Collections.Generic;
using System.Globalization;
using MouseAimFlightRedux.Control;
using UnityEngine;

namespace MouseAimFlightRedux.Tests.Simulation;

/// <summary>
/// The controller flying a simulated aircraft toward an aim, one physics step at a time,
/// in the order MouseAimPilot uses in game. Records every step for the tests to check.
/// </summary>
public sealed class Flight
{
	//// Types

	public readonly struct Sample
	{
		public readonly float Time;

		/// <summary>Angle between the nose and the aim, degrees.</summary>
		public readonly float Error;

		/// <summary>Degrees.</summary>
		public readonly float AngleOfAttack;

		/// <summary>g.</summary>
		public readonly float LoadFactor;

		/// <summary>Degrees.</summary>
		public readonly float Bank;

		/// <summary>Degrees per second.</summary>
		public readonly float RollRate;

		public readonly float PitchInput;
		public readonly float YawInput;
		public readonly float RollInput;

		/// <summary>Height above the ground, m.</summary>
		public readonly float Height;

		/// <summary>True while terrain avoidance flew a recovery.</summary>
		public readonly bool IsRecovering;

		public Sample(float time, float error, float angleOfAttack, float loadFactor, float bank, float rollRate, float pitchInput, float yawInput, float rollInput, float height, bool isRecovering)
		{
			Time = time;
			Error = error;
			AngleOfAttack = angleOfAttack;
			LoadFactor = loadFactor;
			Bank = bank;
			RollRate = rollRate;
			PitchInput = pitchInput;
			YawInput = yawInput;
			RollInput = rollInput;
			Height = height;
			IsRecovering = isRecovering;
		}
	}

	//// Constants

	/// <summary>
	/// The physics step, s. Unity's default fixed step, assumed to be what KSP runs at
	/// normal speed.
	/// </summary>
	public const float DELTA_TIME = 0.02f;

	/// <summary>
	/// Starting height unless a test picks one, m: far enough above the ground for
	/// terrain avoidance never to look at it.
	/// </summary>
	public const float FAR_ABOVE_GROUND = 100000f;

	//// References and State

	readonly List<Sample> samples = new();
	float time;

	/// <summary>
	/// Where the mouse has put the aim, as a direction from the aircraft. Fixed in world
	/// space, as in game.
	/// </summary>
	public Vector3 Aim;

	/// <summary>
	/// The roll input the pilot is holding, if any. As in game, the controller keeps
	/// flying pitch and yaw and is told the roll that really went in.
	/// </summary>
	public float? PilotRoll;

	/// <summary>With the gear down, terrain avoidance stands down, as in game.</summary>
	public bool IsGearDown;

	/// <summary>The setting that switches terrain avoidance on, as in game.</summary>
	public bool ShouldAvoidTerrain = true;

	//// Private Functions

	static float DegreesBetween(Vector3 from, Vector3 to) => Mathf.Acos(Mathf.Clamp(Vector3.Dot(from.normalized, to.normalized), -1f, 1f)) * Mathf.Rad2Deg;

	//// Public API

	public Flight(Airframe airframe, FlightMode mode, Vector3 aim, float height = FAR_ABOVE_GROUND)
	{
		Aircraft = new SimulatedAircraft(airframe, height);
		Ground = new SimulatedGround(Aircraft);
		Mode = mode;
		Aim = aim;
	}

	public SimulatedAircraft Aircraft { get; }
	public SimulatedGround Ground { get; }
	public FlightMode Mode { get; }
	public Autopilot Autopilot { get; } = new();
	public TerrainAvoidance Avoidance { get; } = new();
	public IReadOnlyList<Sample> Samples => samples;

	/// <summary>
	/// A direction by its heading right of the starting course and its elevation above
	/// the horizon, both in degrees.
	/// </summary>
	public static Vector3 Direction(float heading, float elevation)
	{
		var headingRadians = heading * Mathf.Deg2Rad;
		var elevationRadians = elevation * Mathf.Deg2Rad;
		var level = Vector3.forward * Mathf.Cos(headingRadians) + Vector3.right * Mathf.Sin(headingRadians);
		return level * Mathf.Cos(elevationRadians) + Vector3.up * Mathf.Sin(elevationRadians);
	}

	/// <summary>Flies one physics step.</summary>
	public void Step()
	{
		// Keep out of the ground, unless landing
		var target = Avoidance.Update(Aircraft, Ground, Aim, Mode, Autopilot.PitchInput, ShouldAvoidTerrain && !IsGearDown, DELTA_TIME);

		// Fly toward the aim, leaving roll to the pilot while they hold it
		Autopilot.Drive(Aircraft, target, Avoidance.FlownMode(Mode), DELTA_TIME, out var pitch, out var yaw, out var roll);
		if (PilotRoll is float pilotRoll)
			roll = pilotRoll;

		// Feed back what reached the aircraft and move it
		Autopilot.Applied(pitch, yaw, roll);
		Aircraft.Step(pitch, yaw, roll, DELTA_TIME);
		time += DELTA_TIME;

		// Record the step
		var error = DegreesBetween(Aircraft.Nose, Aim);
		samples.Add(new Sample(time, error, Aircraft.AngleOfAttack * Mathf.Rad2Deg, Aircraft.LoadFactor, Aircraft.Bank * Mathf.Rad2Deg, Aircraft.RollRate * Mathf.Rad2Deg, pitch, yaw, roll, Ground.HeightAbove(Vector3.zero), Avoidance.IsRecovering));
	}

	public Flight Fly(float duration)
	{
		var steps = Mathf.RoundToInt(duration / DELTA_TIME);
		for (var step = 0; step < steps; step++)
			Step();
		return this;
	}

	/// <summary>
	/// When the nose came within the tolerance of the aim for good, s, or infinity if it
	/// ended outside it.
	/// </summary>
	public float SettleTime(float tolerance)
	{
		// Sanity check
		if (samples.Count == 0 || samples[samples.Count - 1].Error > tolerance)
			return float.PositiveInfinity;

		// Find the last step outside the tolerance
		for (var index = samples.Count - 1; index >= 0; index--)
		{
			if (samples[index].Error > tolerance)
				return samples[index].Time;
		}
		return 0f;
	}

	public float FinalError => samples.Count > 0 ? samples[samples.Count - 1].Error : float.PositiveInfinity;

	/// <summary>Degrees.</summary>
	public float HighestAngleOfAttack
	{
		get
		{
			var highest = float.NegativeInfinity;
			foreach (var sample in samples)
				highest = Mathf.Max(highest, sample.AngleOfAttack);
			return highest;
		}
	}

	/// <summary>Degrees.</summary>
	public float LowestAngleOfAttack
	{
		get
		{
			var lowest = float.PositiveInfinity;
			foreach (var sample in samples)
				lowest = Mathf.Min(lowest, sample.AngleOfAttack);
			return lowest;
		}
	}

	/// <summary>g.</summary>
	public float HighestLoadFactor
	{
		get
		{
			var highest = float.NegativeInfinity;
			foreach (var sample in samples)
				highest = Mathf.Max(highest, sample.LoadFactor);
			return highest;
		}
	}

	/// <summary>g.</summary>
	public float LowestLoadFactor
	{
		get
		{
			var lowest = float.PositiveInfinity;
			foreach (var sample in samples)
				lowest = Mathf.Min(lowest, sample.LoadFactor);
			return lowest;
		}
	}

	/// <summary>Lowest height above the ground, m. Below zero, it hit.</summary>
	public float LowestHeight
	{
		get
		{
			var lowest = float.PositiveInfinity;
			foreach (var sample in samples)
				lowest = Mathf.Min(lowest, sample.Height);
			return lowest;
		}
	}

	/// <summary>How many times terrain avoidance took over.</summary>
	public int RecoveryCount
	{
		get
		{
			var count = 0;
			for (var index = 0; index < samples.Count; index++)
			{
				if (samples[index].IsRecovering && (index == 0 || !samples[index - 1].IsRecovering))
					count++;
			}
			return count;
		}
	}

	/// <summary>Degrees either way.</summary>
	public float SteepestBank
	{
		get
		{
			var steepest = 0f;
			foreach (var sample in samples)
				steepest = Mathf.Max(steepest, Mathf.Abs(sample.Bank));
			return steepest;
		}
	}

	/// <summary>Degrees per second either way.</summary>
	public float FastestRoll
	{
		get
		{
			var fastest = 0f;
			foreach (var sample in samples)
				fastest = Mathf.Max(fastest, Mathf.Abs(sample.RollRate));
			return fastest;
		}
	}

	/// <summary>
	/// How much the inputs moved from a time on, in full inputs per second over all three
	/// axes. Near zero in steady flight; a wobble or a buzz shows up here.
	/// </summary>
	public float InputActivity(float from)
	{
		// Add up every change after the time
		var total = 0f;
		var start = float.NaN;
		for (var index = 1; index < samples.Count; index++)
		{
			var sample = samples[index];
			if (sample.Time <= from)
				continue;

			if (float.IsNaN(start))
				start = samples[index - 1].Time;
			var previous = samples[index - 1];
			total += Mathf.Abs(sample.PitchInput - previous.PitchInput) + Mathf.Abs(sample.YawInput - previous.YawInput) + Mathf.Abs(sample.RollInput - previous.RollInput);
		}

		// Turn it into a rate
		var duration = samples.Count > 0 ? samples[samples.Count - 1].Time - start : 0f;
		return duration > 0f ? total / duration : 0f;
	}

	/// <summary>
	/// One line of what the flight did, for the test output. Input activity is counted
	/// from the grace after settling.
	/// </summary>
	public string Summary(float tolerance, float grace) => string.Format(CultureInfo.InvariantCulture, "on the aim within {0}° at {1:0.00} s, final error {2:0.00}°, AoA {3:0.0}° to {4:0.0}°, {5:0.00} g to {6:0.00} g, bank up to {7:0}°, roll up to {8:0}°/s, input activity once settled {9:0.000}/s", tolerance, SettleTime(tolerance), FinalError, LowestAngleOfAttack, HighestAngleOfAttack, LowestLoadFactor, HighestLoadFactor, SteepestBank, FastestRoll, InputActivity(SettleTime(tolerance) + grace));
}

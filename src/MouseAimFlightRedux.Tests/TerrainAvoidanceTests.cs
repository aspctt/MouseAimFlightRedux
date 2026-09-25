//// Dependencies

using System;
using System.Collections.Generic;
using MouseAimFlightRedux.Control;
using MouseAimFlightRedux.Tests.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace MouseAimFlightRedux.Tests;

/// <summary>
/// Flies simulated aircraft at the ground and checks terrain avoidance does what
/// docs/DESIGN.md says: it pulls out of dives and over ridges at the last moment, leaves
/// low flying alone, stands down with the gear down, and learns how hard the wings pull.
/// </summary>
[TestFixture]
public sealed class TerrainAvoidanceTests
{
	//// Constants

	const float DIVE_START_HEIGHT = 1500f;
	const float DIVE_DURATION = 60f;

	/// <summary>
	/// Lowest a recovery may bring the craft, m. Recoveries aim to clear by 20 m.
	/// </summary>
	const float SAFE_HEIGHT = 5f;

	/// <summary>
	/// How close the same flight without terrain avoidance must have come to the ground
	/// for a takeover to count as needed, m. Twice the 20 m a recovery keeps clear by,
	/// since predictions err on the safe side: one caught a turn over-banked to 128°,
	/// pulling 8 g toward the ground at 113 m, that the controller would have saved at
	/// 38 m by itself.
	/// </summary>
	const float NEEDED_TAKEOVER_HEIGHT = 40f;

	const float LOW_LEVEL_HEIGHT = 60f;
	const float LOW_TURN_HEIGHT = 100f;
	const float LOW_FLIGHT_DURATION = 30f;

	/// <summary>
	/// How close the learned pull has to come to the airframe's real one, as a share.
	/// </summary>
	const float LIFT_TOLERANCE = 0.1f;

	/// <summary>
	/// How long thrust and drag take to bring a craft back to cruise speed, s, so dives
	/// speed up and climbs slow down as they do in KSP.
	/// </summary>
	const float SPEED_RECOVERY_TIME = 10f;

	static readonly float[] DIVES = { 30f, 60f, 90f };
	static readonly float[] MOUNTAIN_SLOPES = { 30f, 45f };

	//// Private Functions

	static Airframe TradingSpeed(Airframe airframe)
	{
		airframe.SpeedRecoveryTime = SPEED_RECOVERY_TIME;
		return airframe;
	}

	static Airframe[] Airframes() => new[]
	{
		TradingSpeed(Airframe.Fighter()),
		TradingSpeed(Airframe.SlowFighter()),
		TradingSpeed(Airframe.FastFighter()),
		TradingSpeed(Airframe.Cargo()),
		TradingSpeed(Airframe.InclinedWingFighter()),
		TradingSpeed(Airframe.AtmosphereAutopilotSurfaces()),
	};

	static IEnumerable<TestCaseData> DiveCases()
	{
		foreach (var mode in ShippedFlightModes.All)
		{
			foreach (var airframe in Airframes())
			{
				foreach (var dive in DIVES)
					yield return new TestCaseData(mode, airframe, dive).SetArgDisplayNames(mode.Name, airframe.Name, FormattableString.Invariant($"Dive {dive}°"));
			}
		}
	}

	static IEnumerable<TestCaseData> ModeAndAirframeCases()
	{
		foreach (var mode in ShippedFlightModes.All)
		{
			foreach (var airframe in Airframes())
				yield return new TestCaseData(mode, airframe).SetArgDisplayNames(mode.Name, airframe.Name);
		}
	}

	static IEnumerable<TestCaseData> MountainCases()
	{
		foreach (var mode in ShippedFlightModes.All)
		{
			foreach (var airframe in Airframes())
			{
				foreach (var slope in MOUNTAIN_SLOPES)
					yield return new TestCaseData(mode, airframe, slope).SetArgDisplayNames(mode.Name, airframe.Name, FormattableString.Invariant($"Slope {slope}°"));
			}
		}
	}

	static IEnumerable<TestCaseData> ModeCases()
	{
		foreach (var mode in ShippedFlightModes.All)
			yield return new TestCaseData(mode).SetArgDisplayNames(mode.Name);
	}

	static IEnumerable<TestCaseData> LearningCases()
	{
		yield return new TestCaseData(Airframe.Fighter()).SetArgDisplayNames("Fighter");
		yield return new TestCaseData(Airframe.SlowFighter()).SetArgDisplayNames("Slow fighter");
		yield return new TestCaseData(Airframe.InclinedWingFighter()).SetArgDisplayNames("Inclined wing fighter");
		yield return new TestCaseData(Airframe.NoisyFighter()).SetArgDisplayNames("Noisy fighter");
	}

	static FlightMode Named(string name)
	{
		foreach (var mode in ShippedFlightModes.All)
		{
			if (mode.Name == name)
				return mode;
		}
		throw new ArgumentException($"No shipped mode named {name}");
	}

	static void Report(string label, Flight flight)
	{
		var pull = flight.Avoidance.HasLearnedLift ? "learned" : "assumed";
		TestContext.Out.WriteLine(FormattableString.Invariant($"{flight.Mode.Name} / {flight.Aircraft.Airframe.Name} / {label}: lowest {flight.LowestHeight:0} m, {flight.RecoveryCount} recoveries, pull {flight.Avoidance.AvailableLoadFactor:0.0} g {pull}"));
	}

	//// Public API

	[TestCaseSource(nameof(DiveCases))]
	public void PullsOutBeforeTheGround(FlightMode mode, Airframe airframe, float dive)
	{
		// Dive at the ground and keep aiming into it
		var flight = new Flight(airframe, mode, Flight.Direction(0f, -dive), DIVE_START_HEIGHT).Fly(DIVE_DURATION);
		Report(FormattableString.Invariant($"Dive {dive}°"), flight);

		// Check it pulled out every time
		Assert.That(flight.RecoveryCount, Is.GreaterThan(0), "recoveries");
		Assert.That(flight.LowestHeight, Is.GreaterThan(SAFE_HEIGHT), "lowest height, m");
	}

	/// <summary>
	/// With the nose on the aim, level flight means aiming a little above the horizon, at
	/// the angle of attack that holds the craft up. On the horizon it slowly sinks.
	/// </summary>
	[TestCaseSource(nameof(ModeAndAirframeCases))]
	public void LeavesLowFlyingAlone(FlightMode mode, Airframe airframe)
	{
		// Fly level low down
		var flight = new Flight(airframe, mode, Vector3.forward, LOW_LEVEL_HEIGHT);
		flight.Aim = flight.Aircraft.Nose;
		flight.Fly(LOW_FLIGHT_DURATION);
		Report("Low level", flight);

		// Check it never took over
		Assert.That(flight.RecoveryCount, Is.Zero, "recoveries");
	}

	/// <summary>
	/// A turn aimed for level flight still sinks a little in some modes, since the nose
	/// holds the aim while the flight path drops below it. Taking over is only right when
	/// the same turn without terrain avoidance would have come close to the ground.
	/// </summary>
	[TestCaseSource(nameof(ModeAndAirframeCases))]
	public void OnlyTakesOverTurningWhenHeadingForTheGround(FlightMode mode, Airframe airframe)
	{
		// Turn low down, with and without terrain avoidance
		var guarded = new Flight(airframe, mode, Vector3.forward, LOW_TURN_HEIGHT);
		var unguarded = new Flight(airframe, mode, Vector3.forward, LOW_TURN_HEIGHT) { ShouldAvoidTerrain = false };
		var aim = Flight.Direction(-90f, Mathf.Asin(guarded.Aircraft.Nose.y) * Mathf.Rad2Deg);
		guarded.Aim = aim;
		unguarded.Aim = aim;
		guarded.Fly(LOW_FLIGHT_DURATION);
		unguarded.Fly(LOW_FLIGHT_DURATION);
		Report("Low turn", guarded);
		Report("Low turn unguarded", unguarded);

		// Check any takeover was needed, and that it kept clear
		if (guarded.RecoveryCount > 0)
			Assert.That(unguarded.LowestHeight, Is.LessThan(NEEDED_TAKEOVER_HEIGHT), "lowest height without terrain avoidance, m");
		Assert.That(guarded.LowestHeight, Is.GreaterThan(SAFE_HEIGHT), "lowest height, m");
	}

	[TestCaseSource(nameof(ModeAndAirframeCases))]
	public void ClimbsOverARidge(FlightMode mode, Airframe airframe)
	{
		// Fly level at a ridge rising about 9° to 400 m
		var flight = new Flight(airframe, mode, Flight.Direction(0f, 0f), 150f);
		flight.Ground.RidgeDistance = 5000f;
		flight.Ground.RidgeHeight = 400f;
		flight.Ground.RidgeHalfWidth = 2500f;
		flight.Fly(60f);
		Report("Ridge", flight);

		// Check it climbed over
		Assert.That(flight.RecoveryCount, Is.GreaterThan(0), "recoveries");
		Assert.That(flight.LowestHeight, Is.GreaterThan(SAFE_HEIGHT), "lowest height, m");
	}

	/// <summary>
	/// Flying level at a mountain face steeper than a gentle climb can clear, it has to
	/// climb steeply, and it has to look ahead from well above the valley floor.
	/// </summary>
	[TestCaseSource(nameof(MountainCases))]
	public void ClimbsOverAMountain(FlightMode mode, Airframe airframe, float slope)
	{
		// Fly level at a mountain rising at the slope to 800 m, from 3 km out
		// Any higher and the slow craft can't climb over it at all: at 45°, it bleeds
		// speed until it can't hold the climb.
		var flight = new Flight(airframe, mode, Vector3.forward, 300f);
		flight.Aim = flight.Aircraft.Nose;
		flight.Ground.RidgeHalfWidth = 800f / Mathf.Tan(slope * Mathf.Deg2Rad);
		flight.Ground.RidgeDistance = 3000f + flight.Ground.RidgeHalfWidth;
		flight.Ground.RidgeHeight = 800f;
		flight.Fly(80f);
		Report(FormattableString.Invariant($"Mountain {slope}°"), flight);

		// Check it climbed over
		Assert.That(flight.RecoveryCount, Is.GreaterThan(0), "recoveries");
		Assert.That(flight.LowestHeight, Is.GreaterThan(SAFE_HEIGHT), "lowest height, m");
	}

	[TestCaseSource(nameof(ModeCases))]
	public void StandsDownWithTheGearDown(FlightMode mode)
	{
		// Descend into the ground with the gear down, as if landing
		var flight = new Flight(Airframe.Fighter(), mode, Flight.Direction(0f, -10f), 300f) { IsGearDown = true };
		flight.Fly(LOW_FLIGHT_DURATION);
		Report("Gear down", flight);

		// Check it let it
		Assert.That(flight.RecoveryCount, Is.Zero, "recoveries");
		Assert.That(flight.LowestHeight, Is.LessThan(0f), "lowest height, m");
	}

	/// <summary>
	/// The pull a recovery counts on is learned from how the wings lift in normal flying,
	/// here a climbing turn in Unlimited, which has no load limit to hide it.
	/// </summary>
	[TestCaseSource(nameof(LearningCases))]
	public void LearnsHowHardTheWingsPull(Airframe airframe)
	{
		// Turn about to give it something to learn from
		var mode = Named("Unlimited");
		var flight = new Flight(airframe, mode, Flight.Direction(120f, 30f)).Fly(20f);
		Report("Learning", flight);

		// Compare with the airframe's real lift at the mode's angle of attack limit
		var angle = (mode.MaximumAngleOfAttack + airframe.WingIncidence) * Mathf.Deg2Rad;
		var real = airframe.LiftSlope * flight.Aircraft.DynamicPressure * angle / airframe.Gravity;
		Assert.That(flight.Avoidance.HasLearnedLift, Is.True, "learned");
		Assert.That(flight.Avoidance.AvailableLoadFactor, Is.EqualTo(real).Within(LIFT_TOLERANCE * 100f).Percent, "pull, g");
	}
}

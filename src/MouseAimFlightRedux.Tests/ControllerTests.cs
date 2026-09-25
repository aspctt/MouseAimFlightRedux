//// Dependencies

using System;
using System.Collections.Generic;
using MouseAimFlightRedux.Control;
using MouseAimFlightRedux.Tests.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace MouseAimFlightRedux.Tests;

/// <summary>
/// Flies every shipped flight mode against simulated aircraft and checks the controller
/// does what docs/DESIGN.md says: it settles on the aim, keeps to the mode's limits,
/// holds still in steady flight, and points straight at the aim in space. Each test
/// prints what its flight did, for comparing tuning changes.
/// </summary>
[TestFixture]
public sealed class ControllerTests
{
	//// Types

	public sealed class Maneuver
	{
		public readonly string Name;

		/// <summary>Right of the starting course, degrees.</summary>
		public readonly float Heading;

		/// <summary>Above the horizon, degrees.</summary>
		public readonly float Elevation;

		public Maneuver(string name, float heading, float elevation)
		{
			Name = name;
			Heading = heading;
			Elevation = elevation;
		}

		public Vector3 Aim => Flight.Direction(Heading, Elevation);

		public override string ToString() => Name;
	}

	//// Constants

	/// <summary>How close to the aim counts as on it, degrees.</summary>
	const float SETTLED = 1f;

	/// <summary>Long enough for the slowest mode to turn right round, s.</summary>
	const float FLIGHT_DURATION = 90f;

	/// <summary>Long enough to reach any limit and hold it, s.</summary>
	const float SHORT_FLIGHT_DURATION = 20f;

	/// <summary>
	/// The slowest turn is Cruise's, whose 45° bank limit takes about 40 s to turn round
	/// at 150 m/s on its own, s.
	/// </summary>
	const float SETTLE_TIME_LIMIT = 60f;

	/// <summary>How close level flight should hold the aim, degrees.</summary>
	const float LEVEL_ERROR = 0.2f;

	/// <summary>
	/// Input movement allowed once settled, in full inputs per second over all axes. A
	/// wobble or a buzz moves them far more.
	/// </summary>
	const float STEADY_ACTIVITY = 0.05f;

	/// <summary>
	/// Time after coming onto the aim for the wings to finish levelling, s. Stillness is
	/// checked from then on.
	/// </summary>
	const float SETTLE_GRACE = 3f;

	/// <summary>
	/// How far past an angle of attack limit a pull may carry, degrees.
	/// </summary>
	const float ANGLE_OF_ATTACK_MARGIN = 2f;

	/// <summary>
	/// How far past a load limit a pull may carry, g. The limit caps how fast the flight
	/// path turns, so gravity can add up to another 1 g on top of it.
	/// </summary>
	const float LOAD_FACTOR_MARGIN = 1.5f;

	/// <summary>
	/// How far past a bank limit a turn may carry, degrees. Pulling in a climbing turn
	/// rolls the wings further as the nose rises, and the roll loop takes a moment to
	/// catch it.
	/// </summary>
	const float BANK_MARGIN = 5f;

	/// <summary>
	/// How much more the inputs may move with noisy measurements in a turn than in level
	/// flight, as a ratio. Following the flight path's yaw rate in a turn picks up a
	/// little of the noise in the measured sideslip.
	/// </summary>
	const float NOISY_TURN_ACTIVITY_RATIO = 1.15f;

	/// <summary>
	/// Roll allowed in space, where the controller shouldn't roll at all, °/s.
	/// </summary>
	const float SPACE_ROLL_RATE = 1f;

	/// <summary>
	/// How far past level the wings may swing once the pilot lets go of roll, degrees.
	/// </summary>
	const float PILOT_ROLL_SWING = 5f;

	static readonly Maneuver LEVEL = new("Level", 0f, 0f);
	static readonly Maneuver STRAIGHT_UP = new("Straight up", 0f, 90f);
	static readonly Maneuver SHALLOW_DIVE = new("Dive 15°", 0f, -15f);
	static readonly Maneuver REVERSAL = new("Reverse 150°", 150f, 0f);
	static readonly Maneuver LEFT_TURN = new("Turn left 90°", -90f, 0f);
	static readonly Maneuver CLIMBING_TURN = new("Climbing turn", 120f, 30f);

	static readonly Maneuver[] MANEUVERS =
	{
		new("Climb 20°", 0f, 20f),
		new("Dive 30°", 0f, -30f),
		new("Turn right 45°", 45f, 0f),
		LEFT_TURN,
		REVERSAL,
		STRAIGHT_UP,
		CLIMBING_TURN,
	};

	//// Private Functions

	static Airframe[] Airframes() => new[]
	{
		Airframe.Fighter(),
		Airframe.SlowFighter(),
		Airframe.Cargo(),
		Airframe.Unstable(),
		Airframe.AtmosphereAutopilotSurfaces(),
		Airframe.MisjudgedFighter(2f),
		Airframe.MisjudgedFighter(0.5f),
	};

	static TestCaseData Case(FlightMode mode, Airframe airframe, Maneuver maneuver) => new TestCaseData(mode, airframe, maneuver).SetArgDisplayNames(mode.Name, airframe.Name, maneuver.Name);

	static IEnumerable<TestCaseData> ManeuverCases()
	{
		foreach (var mode in ShippedFlightModes.All)
		{
			foreach (var airframe in Airframes())
			{
				foreach (var maneuver in MANEUVERS)
					yield return Case(mode, airframe, maneuver);
			}
		}
	}

	static IEnumerable<TestCaseData> LevelCases()
	{
		foreach (var mode in ShippedFlightModes.All)
		{
			foreach (var airframe in Airframes())
				yield return Case(mode, airframe, LEVEL);
		}
	}

	static IEnumerable<TestCaseData> AngleOfAttackCases()
	{
		foreach (var mode in ShippedFlightModes.All)
		{
			yield return Case(mode, Airframe.SlowFighter(), STRAIGHT_UP);
			yield return Case(mode, Airframe.SlowFighter(), SHALLOW_DIVE);
		}
	}

	static IEnumerable<TestCaseData> LoadFactorCases()
	{
		foreach (var mode in ShippedFlightModes.All)
		{
			if (mode.MaximumLoadFactor <= 0f)
				continue;

			yield return Case(mode, Airframe.FastFighter(), STRAIGHT_UP);
			yield return Case(mode, Airframe.FastFighter(), SHALLOW_DIVE);
			yield return Case(mode, Airframe.FastFighter(), REVERSAL);
		}
	}

	static IEnumerable<TestCaseData> BankLimitCases()
	{
		foreach (var mode in ShippedFlightModes.All)
		{
			if (mode.MaximumBank >= 180f)
				continue;

			yield return Case(mode, Airframe.Fighter(), LEFT_TURN);
			yield return Case(mode, Airframe.Fighter(), REVERSAL);
			yield return Case(mode, Airframe.Fighter(), CLIMBING_TURN);
		}
	}

	static IEnumerable<TestCaseData> SpaceCases()
	{
		foreach (var mode in ShippedFlightModes.All)
		{
			foreach (var maneuver in MANEUVERS)
				yield return Case(mode, Airframe.Probe(), maneuver);
		}
	}

	static IEnumerable<TestCaseData> ModeCases()
	{
		foreach (var mode in ShippedFlightModes.All)
			yield return new TestCaseData(mode).SetArgDisplayNames(mode.Name);
	}

	static Flight Fly(FlightMode mode, Airframe airframe, Maneuver maneuver, float duration)
	{
		var flight = new Flight(airframe, mode, maneuver.Aim).Fly(duration);
		TestContext.Out.WriteLine($"{mode.Name} / {airframe.Name} / {maneuver.Name}: {flight.Summary(SETTLED, SETTLE_GRACE)}");
		return flight;
	}

	//// Public API

	[TestCaseSource(nameof(ManeuverCases))]
	public void SettlesOnTheAim(FlightMode mode, Airframe airframe, Maneuver maneuver)
	{
		// Fly it
		var flight = Fly(mode, airframe, maneuver, FLIGHT_DURATION);

		// Check it got there and stayed there, still
		var settleTime = flight.SettleTime(SETTLED);
		Assert.That(settleTime, Is.LessThan(SETTLE_TIME_LIMIT), "settle time, s");
		Assert.That(flight.InputActivity(settleTime + SETTLE_GRACE), Is.LessThan(STEADY_ACTIVITY), "input activity once settled");
	}

	[TestCaseSource(nameof(LevelCases))]
	public void HoldsLevelFlightStill(FlightMode mode, Airframe airframe, Maneuver maneuver)
	{
		// Fly it
		var flight = Fly(mode, airframe, maneuver, SHORT_FLIGHT_DURATION);

		// Check it held the aim without wobbling
		Assert.That(flight.FinalError, Is.LessThan(LEVEL_ERROR), "final error, degrees");
		Assert.That(flight.InputActivity(5f), Is.LessThan(STEADY_ACTIVITY), "input activity after 5 s");
	}

	[TestCaseSource(nameof(AngleOfAttackCases))]
	public void KeepsToTheAngleOfAttackLimits(FlightMode mode, Airframe airframe, Maneuver maneuver)
	{
		// Fly it
		var flight = Fly(mode, airframe, maneuver, SHORT_FLIGHT_DURATION);

		// Check both limits
		Assert.That(flight.HighestAngleOfAttack, Is.LessThanOrEqualTo(mode.MaximumAngleOfAttack + ANGLE_OF_ATTACK_MARGIN), "highest angle of attack, degrees");
		Assert.That(flight.LowestAngleOfAttack, Is.GreaterThanOrEqualTo(-mode.MaximumNegativeAngleOfAttack - ANGLE_OF_ATTACK_MARGIN), "lowest angle of attack, degrees");
	}

	[TestCaseSource(nameof(LoadFactorCases))]
	public void KeepsToTheLoadLimits(FlightMode mode, Airframe airframe, Maneuver maneuver)
	{
		// Fly it
		var flight = Fly(mode, airframe, maneuver, SHORT_FLIGHT_DURATION);

		// Check the pull and the push, which gets half
		Assert.That(flight.HighestLoadFactor, Is.LessThanOrEqualTo(mode.MaximumLoadFactor + LOAD_FACTOR_MARGIN), "highest load factor, g");
		Assert.That(flight.LowestLoadFactor, Is.GreaterThanOrEqualTo(-0.5f * mode.MaximumLoadFactor - LOAD_FACTOR_MARGIN), "lowest load factor, g");
	}

	[TestCaseSource(nameof(BankLimitCases))]
	public void KeepsToTheBankLimit(FlightMode mode, Airframe airframe, Maneuver maneuver)
	{
		var flight = Fly(mode, airframe, maneuver, FLIGHT_DURATION);
		Assert.That(flight.SteepestBank, Is.LessThanOrEqualTo(mode.MaximumBank + BANK_MARGIN), "steepest bank, degrees");
	}

	/// <summary>
	/// Craft in KSP jitter a little from step to step. The controller should still
	/// settle, and turning shouldn't make it much more jittery than holding level.
	/// </summary>
	[TestCaseSource(nameof(ModeCases))]
	public void ShrugsOffMeasurementNoise(FlightMode mode)
	{
		// Fly level and turn round, both with noisy measurements
		var level = Fly(mode, Airframe.NoisyFighter(), LEVEL, SHORT_FLIGHT_DURATION);
		var turn = Fly(mode, Airframe.NoisyFighter(), REVERSAL, FLIGHT_DURATION);

		// Check the turn got there without much more jitter
		Assert.That(turn.SettleTime(SETTLED), Is.LessThan(SETTLE_TIME_LIMIT), "settle time, s");
		Assert.That(turn.InputActivity(SETTLE_GRACE), Is.LessThan(level.InputActivity(SETTLE_GRACE) * NOISY_TURN_ACTIVITY_RATIO), "input activity turning against level, full inputs per second");
	}

	[TestCaseSource(nameof(SpaceCases))]
	public void PointsStraightAtTheAimInSpace(FlightMode mode, Airframe airframe, Maneuver maneuver)
	{
		// Fly it
		var flight = Fly(mode, airframe, maneuver, FLIGHT_DURATION);

		// Check it got there without rolling
		Assert.That(flight.SettleTime(SETTLED), Is.LessThan(SETTLE_TIME_LIMIT), "settle time, s");
		Assert.That(flight.FastestRoll, Is.LessThan(SPACE_ROLL_RATE), "fastest roll, °/s");
	}

	/// <summary>
	/// While the pilot holds roll, the controller is told the roll that really went in,
	/// so it knows where the ailerons are when they let go and levels out cleanly.
	/// </summary>
	[TestCaseSource(nameof(ModeCases))]
	public void LevelsOutAfterThePilotLetsGoOfRoll(FlightMode mode)
	{
		// Settle into level flight
		var flight = new Flight(Airframe.Fighter(), mode, LEVEL.Aim).Fly(5f);

		// Roll right for a second, then let go
		flight.PilotRoll = 0.5f;
		flight.Fly(1f);
		var released = flight.Samples.Count;
		flight.PilotRoll = null;
		flight.Fly(SHORT_FLIGHT_DURATION);

		// Find the furthest it swung past level the other way
		var swing = 0f;
		for (var index = released; index < flight.Samples.Count; index++)
			swing = Mathf.Max(swing, -flight.Samples[index].Bank);
		TestContext.Out.WriteLine(FormattableString.Invariant($"{mode.Name} / Fighter / Pilot roll: rolled to {flight.Samples[released - 1].Bank:0}°, swung {swing:0.0}° past level, {flight.Summary(SETTLED, SETTLE_GRACE)}"));

		// Check it came back level and on the aim
		Assert.That(Mathf.Abs(flight.Aircraft.Bank * Mathf.Rad2Deg), Is.LessThan(SETTLED), "final bank, degrees");
		Assert.That(flight.FinalError, Is.LessThan(SETTLED), "final error, degrees");
		Assert.That(swing, Is.LessThan(PILOT_ROLL_SWING), "swing past level, degrees");
	}
}

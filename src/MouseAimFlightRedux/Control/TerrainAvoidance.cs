//// Dependencies

using System.Diagnostics;
using UnityEngine;

namespace MouseAimFlightRedux.Control;

/// <summary>
/// Keeps the craft out of the ground at the last moment. Every so often it predicts the
/// recoveries the controller could fly if it took over now: unload, roll the wings level,
/// then a pull building up to the most the craft allows, into climbs from gentle to
/// steep. When even the best would only just clear the ground, it takes over and flies
/// the gentlest that clears, and hands back once a dive toward the pilot's aim could
/// itself be recovered from. See docs/DESIGN.md, "Terrain avoidance".
/// </summary>
public sealed class TerrainAvoidance
{
	//// Constants

	/// <summary>
	/// How close a predicted recovery may come to the ground before taking over, m.
	/// </summary>
	const float CLEARANCE = 20f;

	/// <summary>
	/// How far a recovery has to clear the ground before handing back, m. Twice the
	/// trigger, so it doesn't flick back and forth.
	/// </summary>
	const float RELEASE_CLEARANCE = 40f;

	/// <summary>
	/// Time for the controller to take over and unload whatever pull it had, s.
	/// </summary>
	const float UNLOAD_TIME = 0.3f;

	/// <summary>
	/// The roll rate a recovery counts on until the craft has been seen rolling faster,
	/// °/s.
	/// </summary>
	const float ASSUMED_ROLL_RATE = 60f;

	/// <summary>Share of the fastest roll seen that a recovery counts on.</summary>
	const float ROLL_RATE_SHARE = 0.8f;

	/// <summary>Time over which the fastest roll seen is forgotten, s.</summary>
	const float ROLL_RATE_MEMORY = 60f;

	/// <summary>Share of the predicted turn rate a recovery counts on.</summary>
	const float TURN_SAFETY = 0.95f;

	/// <summary>
	/// Share of the angle of attack the elevator can reach that a recovery counts on.
	/// </summary>
	const float REACH_SAFETY = 0.9f;

	/// <summary>
	/// How long a prediction follows the climb after the pull, s, so a slope rising
	/// ahead shows up.
	/// </summary>
	const float LOOKAHEAD = 5f;

	/// <summary>Step along a predicted path, s.</summary>
	const float PREDICTION_STEP = 0.25f;

	/// <summary>Longest a prediction runs, s.</summary>
	const float PREDICTION_LIMIT = 20f;

	/// <summary>
	/// Time between predictions, s. Each reads the height map a few dozen times.
	/// </summary>
	const float PREDICTION_INTERVAL = 0.1f;

	/// <summary>
	/// Predicted clearance under which it predicts every physics step instead, m. Heading
	/// into a steep slope the clearance can shrink by 20 m between predictions, enough to
	/// take over well past the last moment.
	/// </summary>
	const float CLOSE_CLEARANCE = 60f;

	/// <summary>
	/// Dynamic pressure the controller needs to fly as an aircraft, kPa. Below it there's
	/// no lift worth counting on.
	/// </summary>
	const float MINIMUM_DYNAMIC_PRESSURE = 1.5f;

	/// <summary>Lift assumed before the wings have been seen working, g.</summary>
	const float ASSUMED_LOAD_FACTOR = 2f;

	/// <summary>Time over which lift is learned, s.</summary>
	const float LIFT_MEMORY = 5f;

	/// <summary>
	/// How fast the angle of attack may be changing for the pitch input to count as
	/// holding it, rad/s.
	/// </summary>
	const float STEADY_ANGLE_RATE = 0.05f;

	/// <summary>
	/// Spread of angle of attack needed to learn from, as a standard deviation in
	/// degrees. Steady flight says nothing about how lift grows.
	/// </summary>
	const float MINIMUM_ANGLE_OF_ATTACK_SPREAD = 1f;

	/// <summary>
	/// How far above the flight path the lift has to point to be turning it upward, rad.
	/// </summary>
	const float LIFT_LEAD = 0.35f;

	/// <summary>
	/// Flight path angles a recovery can climb to, degrees, gentlest first. Flat ground
	/// only needs the first. A mountain face needs one steeper than its slope.
	/// </summary>
	static readonly float[] RECOVERY_CLIMBS = { 15f, 30f, 45f, 60f };

	/// <summary>
	/// The built-in mode, Normal's, whose limits a recovery flies by at least.
	/// </summary>
	static readonly FlightMode ESCAPE_FLOOR = new();

	//// References and State

	readonly Stopwatch stopwatch = new();
	bool isPrimed;
	Vector3 lastVelocity;
	float predictionTimer;

	/// <summary>
	/// Running averages of angle of attack, rad, lift over dynamic pressure,
	/// (m/s²)/kPa, and their products, for fitting a straight line to lift.
	/// </summary>
	float meanAngle;

	float meanLift;
	float meanAngleSquared;
	float meanAngleLift;
	bool hasLiftModel;

	/// <summary>
	/// The same for the pitch input that holds each angle, from steady moments only.
	/// </summary>
	float meanSteadyAngle;

	float meanInput;
	float meanSteadyAngleSquared;
	float meanAngleInput;
	float lastAngle;

	/// <summary>Lift over dynamic pressure at zero angle of attack, (m/s²)/kPa.</summary>
	float liftAtZero;

	/// <summary>
	/// How lift over dynamic pressure grows with angle of attack, per rad.
	/// </summary>
	float liftSlope;

	/// <summary>
	/// The highest angle of attack full pitch input holds, rad, or infinity when the
	/// elevator has never looked short of reaching the mode's limit.
	/// </summary>
	float reachableAngle = float.PositiveInfinity;

	/// <summary>
	/// Lift on the last step, m/s², positive toward the canopy.
	/// </summary>
	float currentLift;

	/// <summary>The fastest roll seen lately, rad/s, fading over time.</summary>
	float peakRollRate;

	/// <summary>The climb the current recovery flies to, rad.</summary>
	float recoveryClimb;

	/// <summary>The level direction the current recovery climbs along.</summary>
	Vector3 recoveryHeading;

	/// <summary>
	/// The mode a recovery flies by, and the mode it was made from.
	/// </summary>
	FlightMode? escapeMode;

	FlightMode? escapeSource;

	/// <summary>True while flying a recovery.</summary>
	public bool IsRecovering { get; private set; }

	/// <summary>
	/// True while watching the ground: switched on, gear up, airborne and fast enough to
	/// fly as an aircraft.
	/// </summary>
	public bool IsWatching { get; private set; }

	/// <summary>Height above the ground at the last prediction, m.</summary>
	public float Height { get; private set; } = float.PositiveInfinity;

	/// <summary>
	/// How close the last prediction's recovery came to the ground, m. Infinity when the
	/// ground was too far away to check.
	/// </summary>
	public float PredictedClearance { get; private set; } = float.PositiveInfinity;

	/// <summary>
	/// The pull a recovery counts on at the current speed, g, before the safety share.
	/// </summary>
	public float AvailableLoadFactor { get; private set; }

	/// <summary>True once lift has been learned from the craft's own flying.</summary>
	public bool HasLearnedLift => hasLiftModel;

	/// <summary>
	/// How long the last round of predictions took, ms, for keeping an eye on the cost of
	/// reading the height map.
	/// </summary>
	public float PredictionTime { get; private set; }

	//// Private Functions

	static Vector3 Across(Vector3 direction, Vector3 axis)
	{
		var across = direction - Vector3.Dot(direction, axis) * axis;
		return across.sqrMagnitude > 1e-8f ? across.normalized : Vector3.zero;
	}

	/// <summary>
	/// Fits lift, and the pitch input that holds each angle, against angle of attack from
	/// how the craft actually flies. Lift is whatever acceleration across the flight path
	/// gravity doesn't explain. Dividing by dynamic pressure makes the fit hold at any
	/// speed, and fitting lines rather than ratios copes with wings set at an angle to
	/// the fuselage and with trim.
	/// </summary>
	void Learn(IVesselDynamics vessel, float pitchInput, float deltaTime)
	{
		// Measure the lift
		var velocity = vessel.Velocity;
		var acceleration = (velocity - lastVelocity) / deltaTime;
		lastVelocity = velocity;
		var liftDirection = Across(vessel.Canopy, velocity.normalized);
		var lift = Vector3.Dot(acceleration + vessel.Gravity * vessel.Up, liftDirection);
		currentLift = lift;

		// Remember the fastest roll
		peakRollRate = Mathf.Max(Mathf.Abs(vessel.RollRate), peakRollRate * Mathf.Exp(-deltaTime / ROLL_RATE_MEMORY));

		// Sanity check
		var dynamicPressure = vessel.DynamicPressure;
		var angle = vessel.AngleOfAttack;
		if (dynamicPressure < MINIMUM_DYNAMIC_PRESSURE || liftDirection == Vector3.zero || Mathf.Abs(angle) > 0.6f)
			return;

		// Add it to the running averages
		var liftPerPressure = lift / dynamicPressure;
		var blend = 1f - Mathf.Exp(-deltaTime / LIFT_MEMORY);
		meanAngle += (angle - meanAngle) * blend;
		meanLift += (liftPerPressure - meanLift) * blend;
		meanAngleSquared += (angle * angle - meanAngleSquared) * blend;
		meanAngleLift += (angle * liftPerPressure - meanAngleLift) * blend;

		// Add the input too while the angle holds steady
		// While the angle is still building, full input says nothing about where it'll
		// stop: counting those moments had the elevator look short of 7° on a craft
		// reaching 12.
		var angleRate = Mathf.Abs(angle - lastAngle) / deltaTime;
		lastAngle = angle;
		if (angleRate < STEADY_ANGLE_RATE)
		{
			meanSteadyAngle += (angle - meanSteadyAngle) * blend;
			meanInput += (pitchInput - meanInput) * blend;
			meanSteadyAngleSquared += (angle * angle - meanSteadyAngleSquared) * blend;
			meanAngleInput += (angle * pitchInput - meanAngleInput) * blend;
		}

		// Fit the lift line when there's enough spread to fit it on
		var minimumVariance = MINIMUM_ANGLE_OF_ATTACK_SPREAD * Mathf.Deg2Rad * MINIMUM_ANGLE_OF_ATTACK_SPREAD * Mathf.Deg2Rad;
		var variance = meanAngleSquared - meanAngle * meanAngle;
		var slope = variance >= minimumVariance ? (meanAngleLift - meanAngle * meanLift) / variance : 0f;
		if (slope > 0f)
		{
			liftSlope = slope;
			liftAtZero = meanLift - slope * meanAngle;
			hasLiftModel = true;
		}

		// Find where full pitch input runs out
		// A craft needing more input for more angle reaches its limit where the line
		// meets full input. One needing less is unstable in pitch and isn't held back.
		var steadyVariance = meanSteadyAngleSquared - meanSteadyAngle * meanSteadyAngle;
		if (steadyVariance < minimumVariance)
			return;

		var inputSlope = (meanAngleInput - meanSteadyAngle * meanInput) / steadyVariance;
		reachableAngle = inputSlope > 0f ? meanSteadyAngle + (1f - meanInput) / inputSlope : float.PositiveInfinity;
	}

	/// <summary>
	/// The most lift at this speed, at the lower of the mode's angle of attack limit and
	/// what the elevator can reach, m/s². Lift is only ever scaled down with speed: a
	/// dive with airbrakes out, or a craft with a lot of drag, may not speed up.
	/// </summary>
	float AvailableLift(IVesselDynamics vessel, FlightMode mode, float speed)
	{
		// Sanity check
		if (!hasLiftModel)
			return ASSUMED_LOAD_FACTOR * vessel.Gravity;

		// Scale dynamic pressure to the speed
		var speedRatio = Mathf.Min(speed / Mathf.Max(vessel.Airspeed, 1f), 1f);
		var dynamicPressure = vessel.DynamicPressure * speedRatio * speedRatio;
		var angle = Mathf.Min(mode.MaximumAngleOfAttack * Mathf.Deg2Rad, reachableAngle * REACH_SAFETY);
		return dynamicPressure * (liftAtZero + liftSlope * angle);
	}

	/// <summary>
	/// How fast a recovery could turn the flight path upward at this speed and climb,
	/// rad/s, held to the same limits the controller flies by.
	/// </summary>
	float TurnRate(IVesselDynamics vessel, FlightMode mode, float speed, float climb)
	{
		var gravity = vessel.Gravity;
		var rate = (AvailableLift(vessel, mode, speed) - gravity * Mathf.Cos(climb)) / speed;
		if (mode.MaximumLoadFactor > 0f)
			rate = Mathf.Min(rate, mode.MaximumLoadFactor * gravity / speed);
		rate = Mathf.Min(rate, mode.MaximumPitchRate * Mathf.Deg2Rad);
		return rate * TURN_SAFETY;
	}

	/// <summary>
	/// Time to roll through an angle, s, speeding up at half the roll authority to the
	/// mode's roll rate, or as fast as the craft has been seen rolling if that's slower.
	/// </summary>
	float RollTime(IVesselDynamics vessel, FlightMode mode, float angle)
	{
		var craftRate = Mathf.Max(ASSUMED_ROLL_RATE * Mathf.Deg2Rad, ROLL_RATE_SHARE * peakRollRate);
		var rate = Mathf.Min(mode.MaximumRollRate * Mathf.Deg2Rad, craftRate);
		var acceleration = Mathf.Max(0.5f * vessel.RollAuthority, 0.01f);
		return angle > rate * rate / acceleration ? angle / rate + rate / acceleration : 2f * Mathf.Sqrt(angle / acceleration);
	}

	/// <summary>
	/// How far the craft has to roll, rad, 0 to π, for its lift to turn the flight path
	/// toward a climb along a heading. Level, that's the bank. Diving steeply, the bank
	/// means little: it can read as upside down when pulling toward the canopy is already
	/// the way out. Lift works across the flight path, so it's measured there rather than
	/// across the nose, which in a steep turn sits well inside the path.
	/// </summary>
	static float RollNeeded(IVesselDynamics vessel, Vector3 heading, float targetClimb)
	{
		// Aim the lift at turning the path up toward the climb
		// Always at least somewhat above the path, since holding a climb takes lift too:
		// aimed at a climb the path had already passed, it read as needing a half roll.
		var flightPath = vessel.Velocity.normalized;
		var up = vessel.Up;
		var climbNow = Mathf.Atan2(Vector3.Dot(flightPath, up), Vector3.Dot(flightPath, heading));
		var climb = Mathf.Min(Mathf.Max(targetClimb, climbNow + LIFT_LEAD), 0.5f * Mathf.PI);
		var wanted = Across(heading * Mathf.Cos(climb) + up * Mathf.Sin(climb), flightPath);
		var lift = Across(vessel.Canopy, flightPath);

		// Sanity check
		if (wanted == Vector3.zero || lift == Vector3.zero)
			return 0f;

		// Measure it from the way the lift points
		return Mathf.Acos(Mathf.Clamp(Vector3.Dot(wanted, lift), -1f, 1f));
	}

	/// <summary>
	/// Follows a recovery from a flight direction, with a roll to make, rad, and lift,
	/// into a climb along a heading, rad, and returns how close it comes to the ground,
	/// m.
	/// </summary>
	float PredictClearance(IVesselDynamics vessel, ITerrain terrain, FlightMode mode, Vector3 direction, Vector3 heading, float roll, float startingLift, float targetClimb)
	{
		// Start from the flight path
		var up = vessel.Up;
		var speed = Mathf.Max(vessel.Velocity.magnitude, 1f);
		var climb = Mathf.Atan2(Vector3.Dot(direction, up), Vector3.Dot(direction, heading));

		// Time the phases
		// The pull builds over the controller's own response times, and over however long
		// the pitch authority takes to reach the rate. Only half the authority counts: by
		// the time the angle of attack is reached, a stable craft's own stiffness takes
		// the rest, and counting all of it had a heavy craft scrape a steep slope.
		var pullStart = UNLOAD_TIME + RollTime(vessel, mode, roll);
		var rateEstimate = Mathf.Max(TurnRate(vessel, mode, speed, 0f), 0f);
		var buildTime = mode.AttitudeResponse + mode.RateResponse + mode.ControlLag + rateEstimate / Mathf.Max(0.5f * vessel.PitchAuthority, 0.01f);

		// Fly it
		// With less than a quarter turn to roll, the controller keeps pulling as it
		// rolls, so more of the lift goes the right way as the roll comes off. With more,
		// the pull it had fades as it unloads and it coasts while rolling. Then the pull
		// builds up to the recovery climb and holds it. Speed trades with height.
		var rollTime = pullStart - UNLOAD_TIME;
		var isInverted = roll > 0.5f * Mathf.PI;
		var position = Vector3.zero;
		var lowest = terrain.HeightAbove(position);
		var climbingTime = 0f;
		for (var time = 0f; time < PREDICTION_LIMIT && climbingTime < LOOKAHEAD; time += PREDICTION_STEP)
		{
			var gravity = vessel.Gravity;
			var rolled = rollTime > 0f ? Mathf.Clamp01((time - UNLOAD_TIME) / rollTime) : 1f;
			var keptLift = isInverted ? startingLift * Mathf.Max(0f, 1f - time / UNLOAD_TIME) : startingLift;
			var coasting = (keptLift * Mathf.Cos(roll * (1f - rolled)) - gravity * Mathf.Cos(climb)) / speed;
			var climbRate = coasting;
			if (time >= pullStart)
			{
				var built = 1f - Mathf.Exp(-(time - pullStart) / buildTime);
				climbRate = Mathf.Lerp(coasting, TurnRate(vessel, mode, speed, climb), built);
				if (climb >= targetClimb)
				{
					climbRate = 0f;
					climbingTime += PREDICTION_STEP;
				}
			}

			// Move along the path at the step's middle angle
			// Using the angle at the end of each step credited a pull-up with a metre or
			// so of extra height every step, enough to clip a steep slope.
			var nextClimb = Mathf.Min(climb + climbRate * PREDICTION_STEP, Mathf.Max(climb, targetClimb));
			var middleClimb = 0.5f * (climb + nextClimb);
			climb = nextClimb;
			speed = Mathf.Max(speed - gravity * Mathf.Sin(middleClimb) * PREDICTION_STEP, 1f);
			position += (heading * Mathf.Cos(middleClimb) + up * Mathf.Sin(middleClimb)) * (speed * PREDICTION_STEP);
			lowest = Mathf.Min(lowest, terrain.HeightAbove(position));
		}
		return lowest;
	}

	/// <summary>
	/// Tries the recovery climbs gentlest first and returns the clearance of the gentlest
	/// that clears the ground by the hand-back margin, or of the best if none does, with
	/// its climb, rad. Wanting the wider margin picks a steeper climb while there's still
	/// room, rather than one that only just scrapes over.
	/// </summary>
	float BestRecovery(IVesselDynamics vessel, ITerrain terrain, FlightMode mode, Vector3 direction, Vector3 heading, bool isRolledOver, float startingLift, out float climb)
	{
		// Try each climb until one clears
		var best = float.NegativeInfinity;
		climb = RECOVERY_CLIMBS[0] * Mathf.Deg2Rad;
		foreach (var candidate in RECOVERY_CLIMBS)
		{
			var candidateClimb = candidate * Mathf.Deg2Rad;
			var roll = isRolledOver ? Mathf.PI : RollNeeded(vessel, heading, candidateClimb);
			var clearance = PredictClearance(vessel, terrain, mode, direction, heading, roll, startingLift, candidateClimb);
			if (clearance >= RELEASE_CLEARANCE)
			{
				climb = candidateClimb;
				return clearance;
			}

			if (clearance > best)
			{
				best = clearance;
				climb = candidateClimb;
			}
		}
		return best;
	}

	/// <summary>
	/// The mode a recovery flies by: the current one, with its pitch, load and angle of
	/// attack limits and response times at least as strong as Normal's. A gentle mode's
	/// own limits would make every recovery start early.
	/// </summary>
	FlightMode EscapeMode(FlightMode mode)
	{
		// Sanity check
		if (escapeMode != null && escapeSource == mode)
			return escapeMode;

		// Strengthen the mode
		escapeSource = mode;
		escapeMode = new FlightMode
		{
			Name = mode.Name,
			MaximumPitchRate = Mathf.Max(mode.MaximumPitchRate, ESCAPE_FLOOR.MaximumPitchRate),
			MaximumYawRate = Mathf.Max(mode.MaximumYawRate, ESCAPE_FLOOR.MaximumYawRate),
			MaximumRollRate = Mathf.Max(mode.MaximumRollRate, ESCAPE_FLOOR.MaximumRollRate),
			MaximumLoadFactor = mode.MaximumLoadFactor > 0f ? Mathf.Max(mode.MaximumLoadFactor, ESCAPE_FLOOR.MaximumLoadFactor) : 0f,
			MaximumAngleOfAttack = Mathf.Max(mode.MaximumAngleOfAttack, ESCAPE_FLOOR.MaximumAngleOfAttack),
			MaximumNegativeAngleOfAttack = mode.MaximumNegativeAngleOfAttack,
			MaximumBank = Mathf.Max(mode.MaximumBank, ESCAPE_FLOOR.MaximumBank),
			BankBlendStart = mode.BankBlendStart,
			BankBlendEnd = mode.BankBlendEnd,
			AttitudeResponse = Mathf.Min(mode.AttitudeResponse, ESCAPE_FLOOR.AttitudeResponse),
			RateResponse = Mathf.Min(mode.RateResponse, ESCAPE_FLOOR.RateResponse),
			ControlLag = mode.ControlLag,
			ControlGain = mode.ControlGain,
			BrakingShare = mode.BrakingShare,
		};
		return escapeMode;
	}

	/// <summary>
	/// Whether a dive toward the pilot's aim could still be recovered from, taken as the
	/// craft rolled right over and heading that way, and whether it's climbing now.
	/// </summary>
	bool CanHandBack(IVesselDynamics vessel, ITerrain terrain, FlightMode mode, Vector3 pilotAim)
	{
		// Sanity check
		var up = vessel.Up;
		var velocity = vessel.Velocity;
		if (Vector3.Dot(velocity, up) <= 0f)
			return false;

		// Check the worse of the flight path and the aim
		var flightDirection = velocity.normalized;
		var aim = pilotAim.normalized;
		var isAimLower = Vector3.Dot(aim, up) < Vector3.Dot(flightDirection, up);
		var direction = isAimLower ? aim : flightDirection;
		var heading = Across(direction, up);
		if (heading == Vector3.zero)
			heading = Across(vessel.Canopy, up);
		if (heading == Vector3.zero)
			return false;

		var clearance = BestRecovery(vessel, terrain, mode, direction, heading, isAimLower, 0f, out _);
		return clearance >= RELEASE_CLEARANCE;
	}

	//// Public API

	/// <summary>
	/// The mode for the controller to fly by: during a recovery, the escape mode.
	/// </summary>
	public FlightMode FlownMode(FlightMode mode) => IsRecovering ? EscapeMode(mode) : mode;

	/// <summary>
	/// Lets go of any recovery and starts measuring afresh. What has been learned about
	/// the craft is kept.
	/// </summary>
	public void Reset()
	{
		isPrimed = false;
		IsRecovering = false;
		IsWatching = false;
		recoveryHeading = Vector3.zero;
		predictionTimer = 0f;
		PredictedClearance = float.PositiveInfinity;
		Height = float.PositiveInfinity;
	}

	/// <summary>
	/// Call every physics step before the controller. Returns the aim to fly: the
	/// pilot's, or a recovery climb along the current heading.
	/// </summary>
	/// <param name="pitchInput">The pitch input that reached the craft last step.</param>
	/// <param name="isEnabled">False when switched off, with the gear down, or landed.
	/// The craft is still learned.</param>
	public Vector3 Update(IVesselDynamics vessel, ITerrain terrain, Vector3 pilotAim, FlightMode mode, float pitchInput, bool isEnabled, float deltaTime)
	{
		// Sanity check
		if (deltaTime <= 0f)
			return pilotAim;

		// Learn how the craft lifts
		if (isPrimed)
			Learn(vessel, pitchInput, deltaTime);
		else
			lastVelocity = vessel.Velocity;
		isPrimed = true;
		var escape = EscapeMode(mode);
		var speed = Mathf.Max(vessel.Velocity.magnitude, 1f);
		var loadFactor = AvailableLift(vessel, escape, speed) / Mathf.Max(vessel.Gravity, 0.01f);
		AvailableLoadFactor = escape.MaximumLoadFactor > 0f ? Mathf.Min(loadFactor, escape.MaximumLoadFactor) : loadFactor;

		// Stand down where there's nothing to do
		IsWatching = isEnabled && vessel.DynamicPressure >= MINIMUM_DYNAMIC_PRESSURE;
		if (!IsWatching)
		{
			IsRecovering = false;
			PredictedClearance = float.PositiveInfinity;
			return pilotAim;
		}

		// Find the headings a recovery could climb along
		// Ahead, or where the canopy faces, pulling through without a roll. Straight
		// down, the canopy is the only way out.
		var up = vessel.Up;
		var aheadHeading = Across(vessel.Velocity, up);
		var canopyHeading = Across(vessel.Canopy, up);
		var heading = aheadHeading != Vector3.zero ? aheadHeading : canopyHeading;
		if (heading == Vector3.zero)
			return pilotAim;

		// Predict the recovery now and then, or every step when it's getting close
		predictionTimer -= deltaTime;
		if (predictionTimer <= 0f || PredictedClearance < CLOSE_CLEARANCE)
		{
			predictionTimer = PREDICTION_INTERVAL;
			stopwatch.Restart();
			Height = terrain.HeightAbove(Vector3.zero);
			var direction = vessel.Velocity.normalized;
			PredictedClearance = BestRecovery(vessel, terrain, escape, direction, heading, false, currentLift, out recoveryClimb);
			recoveryHeading = heading;

			// Diving steeply, see whether pulling through toward the canopy does better
			var isSteep = Vector3.Dot(direction, up) < -Mathf.Sin(0.25f * Mathf.PI);
			if (isSteep && canopyHeading != Vector3.zero && Vector3.Dot(canopyHeading, heading) < 0.5f)
			{
				var throughClearance = BestRecovery(vessel, terrain, escape, direction, canopyHeading, false, currentLift, out var throughClimb);
				if (throughClearance > PredictedClearance)
				{
					PredictedClearance = throughClearance;
					recoveryClimb = throughClimb;
					recoveryHeading = canopyHeading;
				}
			}

			// Take over when even the best would only just clear, hand back once the aim
			// is safe
			// It always looks, however high above the ground below: flying over a valley
			// toward a mountain, it used to skip looking until the mountain was close.
			if (PredictedClearance < CLEARANCE)
				IsRecovering = true;
			else if (IsRecovering && CanHandBack(vessel, terrain, escape, pilotAim))
				IsRecovering = false;

			// Time it
			// The stopwatch reports a double. A float holds any time this could take.
			PredictionTime = (float)stopwatch.Elapsed.TotalMilliseconds;
		}

		// Fly the recovery climb, or the pilot's aim
		// Never below the climb the flight path already has, as the predictions assume:
		// easing off a steep climb for a gentler one flew back into the slope. The nose
		// sits above the flight path by the angle of attack, so it's aimed that much
		// higher for the path itself to climb at the angle. Aiming the nose there instead
		// had the path run a few degrees shallow, into a slope it could have cleared.
		if (!IsRecovering)
			return pilotAim;

		var pathClimb = Mathf.Asin(Mathf.Clamp(Vector3.Dot(vessel.Velocity.normalized, up), -1f, 1f));
		var noseAbovePath = Mathf.Asin(Mathf.Clamp(Vector3.Dot(vessel.Nose, up), -1f, 1f)) - pathClimb;
		var aimClimb = Mathf.Clamp(Mathf.Max(recoveryClimb, pathClimb) + Mathf.Max(noseAbovePath, 0f), 0f, 0.45f * Mathf.PI);
		var recoveryDirection = recoveryHeading != Vector3.zero ? recoveryHeading : heading;
		return recoveryDirection * Mathf.Cos(aimClimb) + up * Mathf.Sin(aimClimb);
	}
}

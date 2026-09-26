//// Dependencies

using MouseAimFlightRedux.Control;
using UnityEngine;

namespace MouseAimFlightRedux.UI;

/// <summary>
/// A window of what the controller is doing, for tuning flight modes: per axis, the rate
/// asked for against the rate flown, the limits and what set them, the input and the
/// authority, with the last few seconds of each as a graph. See docs/DESIGN.md, "Tuning
/// overlay".
/// </summary>
sealed class TuningOverlay
{
	//// Types

	/// <summary>
	/// One axis's history, in degrees per second for rates, and its graph.
	/// </summary>
	sealed class Graph
	{
		public readonly string Name;

		readonly float[] command = new float[HISTORY_LENGTH];
		readonly float[] actual = new float[HISTORY_LENGTH];
		readonly float[] input = new float[HISTORY_LENGTH];
		readonly float[] minimum = new float[HISTORY_LENGTH];
		readonly float[] maximum = new float[HISTORY_LENGTH];
		readonly Color32[] pixels = new Color32[HISTORY_LENGTH * GRAPH_HEIGHT];

		public Texture2D? Texture { get; private set; }

		public Graph(string name)
		{
			Name = name;
		}

		public void Record(int index, AxisController axis)
		{
			command[index] = axis.RateCommand * Mathf.Rad2Deg;
			actual[index] = axis.Rate * Mathf.Rad2Deg;
			input[index] = axis.Input;
			minimum[index] = axis.MinimumRate * Mathf.Rad2Deg;
			maximum[index] = axis.MaximumRate * Mathf.Rad2Deg;
		}

		/// <summary>
		/// Draws the newest sample at the right edge, with rates scaled to
		/// <paramref name="scale"/> and inputs to 1.
		/// </summary>
		public Texture2D Render(int nextSample, int sampleCount, float scale)
		{
			// Make the texture
			Texture ??= new Texture2D(HISTORY_LENGTH, GRAPH_HEIGHT, TextureFormat.RGBA32, false)
			{
				filterMode = FilterMode.Point,
				wrapMode = TextureWrapMode.Clamp,
			};

			// Clear it to the background and the zero line
			for (var index = 0; index < pixels.Length; index++)
				pixels[index] = BACKGROUND;
			var middle = GRAPH_HEIGHT / 2;
			for (var x = 0; x < HISTORY_LENGTH; x++)
				pixels[middle * HISTORY_LENGTH + x] = ZERO_COLOR;

			// Plot each series, the ones to read most on top
			Plot(minimum, nextSample, sampleCount, scale, LIMIT_COLOR);
			Plot(maximum, nextSample, sampleCount, scale, LIMIT_COLOR);
			Plot(input, nextSample, sampleCount, 1f, INPUT_COLOR);
			Plot(command, nextSample, sampleCount, scale, COMMAND_COLOR);
			Plot(actual, nextSample, sampleCount, scale, ACTUAL_COLOR);

			// Upload it
			Texture.SetPixels32(pixels);
			Texture.Apply(false);
			return Texture;
		}

		public void Destroy()
		{
			if (Texture != null)
				Object.Destroy(Texture);
			Texture = null;
		}

		void Plot(float[] values, int nextSample, int sampleCount, float scale, Color32 color)
		{
			var previousX = -1;
			var previousY = 0f;
			for (var age = sampleCount - 1; age >= 0; age--)
			{
				// Anything off the scale runs along the edge, so it still shows
				var index = (nextSample - 1 - age + HISTORY_LENGTH) % HISTORY_LENGTH;
				var x = HISTORY_LENGTH - 1 - age;
				var y = (GRAPH_HEIGHT - 1) * 0.5f * (1f + Mathf.Clamp(values[index] / scale, -1f, 1f));
				if (previousX >= 0)
					Line(previousX, previousY, x, y, color);
				previousX = x;
				previousY = y;
			}
		}

		void Line(int fromX, float fromY, int toX, float toY, Color32 color)
		{
			int steps = Mathf.Max(Mathf.Abs(toX - fromX), Mathf.CeilToInt(Mathf.Abs(toY - fromY)), 1);
			float stepCount = steps;
			for (var step = 0; step <= steps; step++)
			{
				var progress = step / stepCount;
				var x = Mathf.RoundToInt(Mathf.Lerp(fromX, toX, progress));
				var y = Mathf.RoundToInt(Mathf.Lerp(fromY, toY, progress));
				pixels[y * HISTORY_LENGTH + x] = color;
			}
		}
	}

	//// Constants

	/// <summary>
	/// Physics frames kept, 5.6 s at KSP's default step of 0.02 s. One texture column
	/// each.
	/// </summary>
	const int HISTORY_LENGTH = 280;

	const int GRAPH_HEIGHT = 64;

	/// <summary>Graphs are drawn this wide on screen, two pixels a sample.</summary>
	const float GRAPH_DRAW_WIDTH = 560f;

	/// <summary>
	/// Graphs run from minus to plus this much of the mode's rate limit, so overshoot
	/// past it still shows.
	/// </summary>
	const float GRAPH_HEADROOM = 1.25f;

	static readonly Color32 BACKGROUND = new(0, 0, 0, 120);
	static readonly Color32 ZERO_COLOR = new(255, 255, 255, 50);
	static readonly Color32 LIMIT_COLOR = new(255, 96, 96, 160);
	static readonly Color32 INPUT_COLOR = new(255, 255, 255, 150);
	static readonly Color32 COMMAND_COLOR = new(255, 200, 60, 255);
	static readonly Color32 ACTUAL_COLOR = new(80, 220, 255, 255);

	const string LEGEND = "<color=#ffc83c>Command</color>    <color=#50dcff>Actual</color>    <color=#ffffff96>Input</color>    <color=#ff6060a0>Limits</color>";

	static readonly string[] HEADERS = { "", "Error", "Command", "Actual", "Limits", "Held by", "Input", "Authority", "Fixed speed" };

	/// <summary>
	/// Narrowest each column may be, in pixels, for the values under a header shorter
	/// than them.
	/// </summary>
	static readonly float[] MINIMUM_COLUMN_WIDTHS = { 44f, 52f, 60f, 56f, 104f, 58f, 48f, 60f, 60f };

	/// <summary>Space between columns, in pixels.</summary>
	const float COLUMN_GAP = 8f;

	//// References and State

	/// <summary>
	/// Kept across flight scenes so the window reopens where it was left.
	/// </summary>
	static Rect windowRect = new(20f, 80f, 0f, 0f);

	readonly Graph pitchGraph = new("Pitch");
	readonly Graph yawGraph = new("Yaw");
	readonly Graph rollGraph = new("Roll");
	readonly GUI.WindowFunction drawWindow;

	/// <summary>Where the next sample goes.</summary>
	int nextSample;

	int sampleCount;

	/// <summary>
	/// Set when a sample arrives, so the graphs are only redrawn when there's something
	/// new.
	/// </summary>
	bool hasNewSamples;

	/// <summary>
	/// Set when the line saying mouse aim is off comes or goes. A layout window grows to
	/// fit but never shrinks on its own, so it's cut down to size on the next layout
	/// pass. Doing that on every event instead hands the drag events a window of no size.
	/// </summary>
	bool shouldShrink;

	GUIStyle? cellStyle;
	GUIStyle? rowNameStyle;
	GUIStyle? richStyle;

	/// <summary>Column widths, sized to the headers in the game's font.</summary>
	float[]? columnWidths;

	bool isActive;
	FlightMode? mode;
	VesselDynamics? dynamics;
	Autopilot? autopilot;
	TerrainAvoidance? avoidance;
	AimCamera? aimCamera;
	Vessel? vessel;

	GUIStyle CellStyle => cellStyle ??= new GUIStyle(HighLogic.Skin.label) { alignment = TextAnchor.MiddleRight, wordWrap = false };
	GUIStyle RowNameStyle => rowNameStyle ??= new GUIStyle(CellStyle) { alignment = TextAnchor.MiddleLeft };
	GUIStyle RichStyle => richStyle ??= new GUIStyle(HighLogic.Skin.label) { richText = true, wordWrap = false };
	float[] ColumnWidths => columnWidths ??= SizeColumns(CellStyle);

	//// Private Functions

	static float[] SizeColumns(GUIStyle style)
	{
		var widths = new float[HEADERS.Length];
		for (var index = 0; index < widths.Length; index++)
			widths[index] = Mathf.Max(MINIMUM_COLUMN_WIDTHS[index], style.CalcSize(new GUIContent(HEADERS[index])).x) + COLUMN_GAP;
		return widths;
	}

	/// <summary>How the table names a limit, short enough for its column.</summary>
	static string LimitName(RateLimit limit) => limit switch
	{
		RateLimit.LoadFactor => "G",
		RateLimit.AngleOfAttack => "AoA",
		_ => "Rate",
	};

	/// <summary>
	/// Terrain avoidance's state, the recovery it last predicted, and its height next to
	/// KSP's own radar altitude, which should roughly agree.
	/// </summary>
	static string TerrainLine(TerrainAvoidance avoidance, Vessel vessel)
	{
		// Sanity check
		var state = avoidance.IsPullingUp ? "PULLING UP (Unlimited)" : avoidance.IsRecovering ? "PULLING UP" : avoidance.IsWatching ? "watching" : "off";
		if (!avoidance.IsWatching)
			return $"Terrain {state}";

		// Describe the prediction
		var clearance = float.IsPositiveInfinity(avoidance.PredictedClearance) ? "far" : $"{avoidance.PredictedClearance:0} m";
		var pull = avoidance.HasLearnedLift ? $"{avoidance.AvailableLoadFactor:0.0} g" : $"{avoidance.AvailableLoadFactor:0.0} g assumed";
		var height = float.IsPositiveInfinity(avoidance.Height) ? "-" : $"{avoidance.Height:0} m";
		return $"Terrain {state}    recovery clears {clearance}    pull {pull}    height {height} (KSP radar {vessel.radarAltitude:0} m)    {avoidance.PredictionTime:0.00} ms";
	}

	/// <summary>
	/// KSP's flight camera: its mode, whether it follows the aim, and its pitch against
	/// the limits KSP holds it to.
	/// </summary>
	static string CameraLine(AimCamera aimCamera)
	{
		// Sanity check
		var camera = FlightCamera.fetch;
		if (camera == null)
			return "Camera -";

		// Describe it
		// KSP keeps the camera's angles in radians, and the pitch limits are assumed to be
		// too, since it holds the pitch to them.
		const float DEGREES = Mathf.Rad2Deg;
		var mode = camera.mode == FlightCamera.Modes.AUTO ? $"AUTO ({camera.autoMode})" : camera.mode.ToString();
		var state = aimCamera.IsFollowing ? "following the aim" : "free";
		return $"Camera {mode}    {state}    pitch {camera.camPitch * DEGREES:0}° (limits {camera.minPitch * DEGREES:0}° to {camera.maxPitch * DEGREES:0}°)    distance {camera.Distance:0} m";
	}

	static string HeldBy(AxisController axis, RateLimit upLimit, RateLimit downLimit) => axis.HeldLimit switch
	{
		1 => LimitName(upLimit),
		-1 => LimitName(downLimit),
		_ => "",
	};

	void DrawWindow(int windowIdentifier)
	{
		// Sanity check
		// Draw always sets these before the window runs.
		if (mode == null || dynamics == null || autopilot == null || avoidance == null || aimCamera == null || vessel == null)
			return;

		// Draw the flight
		if (!isActive)
			GUILayout.Label("Mouse aim is off. Showing the last flight.");
		GUILayout.Label($"{mode.Name}    {dynamics.Airspeed:0} m/s    q {dynamics.DynamicPressure:0.0} kPa    AoA {dynamics.AngleOfAttack * Mathf.Rad2Deg:0.0}°    Sideslip {dynamics.Sideslip * Mathf.Rad2Deg:0.0}°    {vessel.geeForce:0.0} g");
		GUILayout.Label($"Aero {autopilot.AerodynamicBlend:0%}    Bank commit {autopilot.BankCommitment:0%}");
		GUILayout.Label(TerrainLine(avoidance, vessel));
		GUILayout.Label(CameraLine(aimCamera));
		GUILayout.Space(6);

		// Draw the table
		Row(HEADERS);
		AxisRow(pitchGraph.Name, autopilot.Pitch, dynamics.PitchAuthority, dynamics.PitchSlewShare, HeldBy(autopilot.Pitch, autopilot.PitchUpLimit, autopilot.PitchDownLimit));
		AxisRow(yawGraph.Name, autopilot.Yaw, dynamics.YawAuthority, dynamics.YawSlewShare, HeldBy(autopilot.Yaw, RateLimit.Rate, RateLimit.Rate));
		AxisRow(rollGraph.Name, autopilot.Roll, dynamics.RollAuthority, dynamics.RollSlewShare, HeldBy(autopilot.Roll, RateLimit.Rate, RateLimit.Rate));
		GUILayout.Label("Angles in °, rates in °/s, authority in °/s² at full input.");

		// Draw the graphs, redrawing them once per new sample
		var shouldRedraw = Event.current.type == EventType.Repaint && hasNewSamples;
		if (shouldRedraw)
			hasNewSamples = false;
		DrawGraph(pitchGraph, mode.MaximumPitchRate, shouldRedraw);
		DrawGraph(yawGraph, mode.MaximumYawRate, shouldRedraw);
		DrawGraph(rollGraph, mode.MaximumRollRate, shouldRedraw);
		GUILayout.Label(LEGEND, RichStyle);

		GUI.DragWindow();
	}

	void AxisRow(string name, AxisController axis, float authority, float fixedShare, string heldBy)
	{
		const float DEGREES = Mathf.Rad2Deg;
		Row(name, (axis.Error * DEGREES).ToString("0.0"), (axis.RateCommand * DEGREES).ToString("0.0"), (axis.Rate * DEGREES).ToString("0.0"), $"{axis.MinimumRate * DEGREES:0.0} to {axis.MaximumRate * DEGREES:0.0}", heldBy, axis.Input.ToString("0.00"), (authority * DEGREES).ToString("0"), fixedShare.ToString("0%"));
	}

	void Row(params string[] values)
	{
		GUILayout.BeginHorizontal();
		for (var index = 0; index < values.Length; index++)
			GUILayout.Label(values[index], index == 0 ? RowNameStyle : CellStyle, GUILayout.Width(ColumnWidths[index]));
		GUILayout.EndHorizontal();
	}

	void DrawGraph(Graph graph, float maximumRate, bool shouldRedraw)
	{
		// Label it above the graph
		// On the graph, the limit lines would run through it.
		var scale = Mathf.Max(maximumRate, 1f) * GRAPH_HEADROOM;
		GUILayout.Label($"{graph.Name}  ±{scale:0} °/s");

		// Draw it
		var rect = GUILayoutUtility.GetRect(GRAPH_DRAW_WIDTH, GRAPH_HEIGHT, GUILayout.Width(GRAPH_DRAW_WIDTH), GUILayout.Height(GRAPH_HEIGHT));
		if (Event.current.type != EventType.Repaint)
			return;
		var texture = shouldRedraw || graph.Texture == null ? graph.Render(nextSample, sampleCount, scale) : graph.Texture;
		GUI.DrawTexture(rect, texture);
	}

	//// Public API

	public TuningOverlay()
	{
		drawWindow = DrawWindow;
	}

	/// <summary>
	/// Call after each step the controller flew, once its inputs have been applied.
	/// </summary>
	public void Record(Autopilot source)
	{
		pitchGraph.Record(nextSample, source.Pitch);
		yawGraph.Record(nextSample, source.Yaw);
		rollGraph.Record(nextSample, source.Roll);
		nextSample = (nextSample + 1) % HISTORY_LENGTH;
		sampleCount = Mathf.Min(sampleCount + 1, HISTORY_LENGTH);
		hasNewSamples = true;
	}

	/// <summary>
	/// Empties the graphs, so a new flight doesn't join onto the last one.
	/// </summary>
	public void Clear()
	{
		nextSample = 0;
		sampleCount = 0;
		hasNewSamples = true;
	}

	public void Destroy()
	{
		pitchGraph.Destroy();
		yawGraph.Destroy();
		rollGraph.Destroy();
	}

	/// <summary>Call from OnGUI.</summary>
	public void Draw(bool isFlying, FlightMode mode, VesselDynamics dynamics, Autopilot autopilot, TerrainAvoidance avoidance, AimCamera aimCamera, Vessel vessel)
	{
		// Take this frame's flight
		if (isFlying != isActive)
			shouldShrink = true;
		isActive = isFlying;
		this.mode = mode;
		this.dynamics = dynamics;
		this.autopilot = autopilot;
		this.avoidance = avoidance;
		this.aimCamera = aimCamera;
		this.vessel = vessel;

		// Cut the window down to size on a layout pass
		GUI.skin = HighLogic.Skin;
		if (shouldShrink && Event.current.type == EventType.Layout)
		{
			windowRect.width = 0f;
			windowRect.height = 0f;
			shouldShrink = false;
		}

		// Draw it
		windowRect = GUILayout.Window(GetHashCode(), windowRect, drawWindow, "Mouse Aim Tuning");
	}
}

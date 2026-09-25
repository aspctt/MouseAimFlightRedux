using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace MouseAimFlightRedux;

/// <summary>
/// Atmosphere Autopilot, reached by reflection so that mod stays optional. Uses only its public members: for the master
/// switch <c>AtmosphereAutopilot.Instance</c>, <c>getVesselModules</c>, <c>TopModuleManager.Active</c> and
/// <c>mainMenuGUIUpdate</c>, and for its control surfaces <c>SyncModuleControlSurface.CSURF_SPD</c>. If they aren't
/// there or anything throws, it reads as off with no surfaces of its own, and is left alone. See docs/DESIGN.md, "Other
/// autopilots".
/// </summary>
static class AtmosphereAutopilot
{
	const string AssemblyName = "AtmosphereAutopilot";

	static bool resolved;
	static bool available;
	static PropertyInfo instance;
	static MethodInfo getVesselModules;
	static MethodInfo refreshMenu;
	static Type managerType;
	static PropertyInfo active;
	static Type surfaceType;
	static float surfaceSpeed = float.PositiveInfinity;

	/// <summary>Installed, with the members the master switch needs.</summary>
	public static bool Available => Resolve();

	public static bool IsOn(Vessel vessel)
	{
		var manager = Manager(vessel);
		if (manager == null)
			return false;

		try
		{
			return (bool)active.GetValue(manager, null);
		}
		catch (Exception e)
		{
			Fail(e);
			return false;
		}
	}

	public static void Set(Vessel vessel, bool on)
	{
		var manager = Manager(vessel);
		if (manager == null)
			return;

		try
		{
			active.SetValue(manager, on, null);
		}
		catch (Exception e)
		{
			Fail(e);
			return;
		}

		// Only updates its toolbar button, so a failure here is harmless.
		try
		{
			refreshMenu?.Invoke(instance.GetValue(null, null), null);
		}
		catch (Exception) { }
	}

	/// <summary>
	/// How fast a control surface moves in full inputs per second if it's one of Atmosphere Autopilot's, which it swaps
	/// in for every stock one under stock aerodynamics. Infinity for any other surface, and for its surfaces set to ease
	/// into position rather than move at a fixed speed.
	/// </summary>
	public static float SurfaceSpeed(ModuleControlSurface surface)
	{
		Resolve();
		if (surfaceType == null || surface.GetType() != surfaceType || surface.useExponentialSpeed)
			return float.PositiveInfinity;
		return surfaceSpeed;
	}

	/// <summary>The vessel's Atmosphere Autopilot manager, or null if the mod isn't installed or hasn't set one up yet.</summary>
	static object Manager(Vessel vessel)
	{
		if (vessel == null || !Resolve())
			return null;

		try
		{
			var main = instance.GetValue(null, null);
			if (main == null)
				return null;
			var modules = getVesselModules.Invoke(main, new object[] { vessel }) as IDictionary;
			return modules != null && modules.Contains(managerType) ? modules[managerType] : null;
		}
		catch (Exception e)
		{
			Fail(e);
			return null;
		}
	}

	static bool Resolve()
	{
		if (resolved)
			return available;
		resolved = true;

		var assembly = AssemblyLoader.loadedAssemblies.FirstOrDefault(a => a.assembly.GetName().Name == AssemblyName)?.assembly;
		if (assembly == null)
			return false;

		var main = assembly.GetType("AtmosphereAutopilot.AtmosphereAutopilot");
		managerType = assembly.GetType("AtmosphereAutopilot.TopModuleManager");
		instance = main?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
		getVesselModules = main?.GetMethod("getVesselModules", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(Vessel) }, null);
		refreshMenu = main?.GetMethod("mainMenuGUIUpdate", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
		active = managerType?.GetProperty("Active", BindingFlags.Public | BindingFlags.Instance);

		available = instance != null && getVesselModules != null && active != null && active.PropertyType == typeof(bool) && active.CanWrite;
		if (available)
			Debug.Log("[MouseAimFlightRedux] Atmosphere Autopilot found, it will be switched off while mouse aim is on");
		else
			Debug.LogWarning("[MouseAimFlightRedux] Atmosphere Autopilot found, but not its master switch, so it will be left alone");

		var surface = assembly.GetType("AtmosphereAutopilot.SyncModuleControlSurface");
		var speed = surface?.GetField("CSURF_SPD", BindingFlags.Public | BindingFlags.Static);
		if (surface != null && typeof(ModuleControlSurface).IsAssignableFrom(surface) && speed is { IsLiteral: true } && speed.GetRawConstantValue() is float value && value > 0f)
		{
			surfaceType = surface;
			surfaceSpeed = value;
			Debug.Log($"[MouseAimFlightRedux] Atmosphere Autopilot's control surfaces move at {value} full inputs per second");
		}
		else
		{
			Debug.LogWarning("[MouseAimFlightRedux] Atmosphere Autopilot found, but not its control surface speed, so its surfaces are treated as stock");
		}

		return available;
	}

	/// <summary>Stops using the master switch for the rest of the session rather than failing every frame.</summary>
	static void Fail(Exception e)
	{
		available = false;
		Debug.LogError($"[MouseAimFlightRedux] Atmosphere Autopilot's master switch failed, leaving it alone from now on: {(e as TargetInvocationException)?.InnerException ?? e}");
	}
}

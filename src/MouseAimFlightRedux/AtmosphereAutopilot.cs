//// Dependencies

using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace MouseAimFlightRedux;

/// <summary>
/// Atmosphere Autopilot, reached by reflection so that mod stays optional. Uses only its
/// public members: for the master switch <c>AtmosphereAutopilot.Instance</c>,
/// <c>getVesselModules</c>, <c>TopModuleManager.Active</c> and <c>mainMenuGUIUpdate</c>,
/// and for its control surfaces <c>SyncModuleControlSurface.CSURF_SPD</c>. If they aren't
/// there or anything throws, it reads as off with no surfaces of its own, and is left
/// alone. See docs/DESIGN.md, "Other autopilots".
/// </summary>
static class AtmosphereAutopilot
{
	//// Types

	/// <summary>
	/// The master switch's members, kept only once every one of them has been found.
	/// </summary>
	sealed class SwitchMembers
	{
		public readonly PropertyInfo Instance;
		public readonly MethodInfo GetVesselModules;
		public readonly MethodInfo? RefreshMenu;
		public readonly Type ManagerType;
		public readonly PropertyInfo Active;

		public SwitchMembers(PropertyInfo instance, MethodInfo getVesselModules, MethodInfo? refreshMenu, Type managerType, PropertyInfo active)
		{
			Instance = instance;
			GetVesselModules = getVesselModules;
			RefreshMenu = refreshMenu;
			ManagerType = managerType;
			Active = active;
		}
	}

	//// Constants

	const string ASSEMBLY_NAME = "AtmosphereAutopilot";

	//// References and State

	static bool isResolved;

	/// <summary>
	/// Null when the mod isn't installed, its switch wasn't found, or using it failed.
	/// </summary>
	static SwitchMembers? switchMembers;

	static Type? surfaceType;
	static float surfaceSpeed = float.PositiveInfinity;

	//// Private Functions

	static Assembly? FindLoadedAssembly(string name)
	{
		foreach (var loaded in AssemblyLoader.loadedAssemblies)
		{
			if (loaded.assembly.GetName().Name == name)
				return loaded.assembly;
		}
		return null;
	}

	/// <summary>
	/// The vessel's Atmosphere Autopilot manager, or null if the mod isn't installed or
	/// hasn't set one up yet.
	/// </summary>
	static object? FindManager(Vessel vessel)
	{
		// Sanity check
		Resolve();
		var members = switchMembers;
		if (vessel == null || members == null)
			return null;

		// Look it up among the vessel's modules
		try
		{
			var main = members.Instance.GetValue(null, null);
			if (main == null)
				return null;
			if (members.GetVesselModules.Invoke(main, new object[] { vessel }) is not IDictionary modules || !modules.Contains(members.ManagerType))
				return null;
			return modules[members.ManagerType];
		}
		catch (Exception exception)
		{
			Fail(exception);
			return null;
		}
	}

	static void Resolve()
	{
		// Sanity check
		if (isResolved)
			return;
		isResolved = true;

		// Find the mod
		var assembly = FindLoadedAssembly(ASSEMBLY_NAME);
		if (assembly == null)
			return;

		// Find the master switch
		var main = assembly.GetType("AtmosphereAutopilot.AtmosphereAutopilot");
		var managerType = assembly.GetType("AtmosphereAutopilot.TopModuleManager");
		var instance = main?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
		var getVesselModules = main?.GetMethod("getVesselModules", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(Vessel) }, null);
		var refreshMenu = main?.GetMethod("mainMenuGUIUpdate", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
		var active = managerType?.GetProperty("Active", BindingFlags.Public | BindingFlags.Instance);
		if (instance != null && getVesselModules != null && managerType != null && active != null && active.PropertyType == typeof(bool) && active.CanWrite)
		{
			switchMembers = new SwitchMembers(instance, getVesselModules, refreshMenu, managerType, active);
			Debug.Log("[MouseAimFlightRedux] Atmosphere Autopilot found, it will be switched off while mouse aim is on");
		}
		else
		{
			Debug.LogWarning("[MouseAimFlightRedux] Atmosphere Autopilot found, but not its master switch, so it will be left alone");
		}

		// Find how fast its control surfaces move
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
	}

	/// <summary>
	/// Stops using the master switch for the rest of the session rather than failing
	/// every frame.
	/// </summary>
	static void Fail(Exception exception)
	{
		switchMembers = null;
		var cause = exception is TargetInvocationException { InnerException: { } inner } ? inner : exception;
		Debug.LogError($"[MouseAimFlightRedux] Atmosphere Autopilot's master switch failed, leaving it alone from now on: {cause}");
	}

	//// Public API

	/// <summary>Installed, with the members the master switch needs.</summary>
	public static bool IsAvailable
	{
		get
		{
			Resolve();
			return switchMembers != null;
		}
	}

	public static bool IsOn(Vessel vessel)
	{
		// Sanity check
		var manager = FindManager(vessel);
		var members = switchMembers;
		if (manager == null || members == null)
			return false;

		// Read the switch
		try
		{
			return members.Active.GetValue(manager, null) is true;
		}
		catch (Exception exception)
		{
			Fail(exception);
			return false;
		}
	}

	public static void Set(Vessel vessel, bool isOn)
	{
		// Sanity check
		var manager = FindManager(vessel);
		var members = switchMembers;
		if (manager == null || members == null)
			return;

		// Flip the switch
		try
		{
			members.Active.SetValue(manager, isOn, null);
		}
		catch (Exception exception)
		{
			Fail(exception);
			return;
		}

		// Refresh its toolbar button
		// That's all this does, so a failure here is harmless.
		try
		{
			members.RefreshMenu?.Invoke(members.Instance.GetValue(null, null), null);
		}
		catch (Exception)
		{
		}
	}

	/// <summary>
	/// How fast a control surface moves in full inputs per second if it's one of
	/// Atmosphere Autopilot's, which it swaps in for every stock one under stock
	/// aerodynamics. Infinity for any other surface, and for its surfaces set to ease
	/// into position rather than move at a fixed speed.
	/// </summary>
	public static float SurfaceSpeed(ModuleControlSurface surface)
	{
		Resolve();
		if (surfaceType == null || surface.GetType() != surfaceType || surface.useExponentialSpeed)
			return float.PositiveInfinity;

		return surfaceSpeed;
	}
}

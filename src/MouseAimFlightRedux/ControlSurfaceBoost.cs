/*
Copyright (c) 2016, BahamutoD, ferram4, tetryds
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

* Redistributions of source code must retain the above copyright notice, this
  list of conditions and the following disclaimer.

* Redistributions in binary form must reproduce the above copyright notice,
  this list of conditions and the following disclaimer in the documentation
  and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MouseAimFlightRedux;

/// <summary>
/// While mouse aim is on, stock control surfaces move faster and ease into position, which suits a controller making
/// many small corrections. Each surface's own values are recorded and put back exactly, including surfaces that join
/// or leave the vessel in between. Not applied under FAR, which drives its control surfaces itself, or to other mods'
/// replacements for the stock modules, which move their surfaces their own way. See docs/DESIGN.md, "Control surface
/// speed-up".
/// </summary>
sealed class ControlSurfaceBoost
{
	const float SpeedFactor = 3.5f;

	struct Original
	{
		public float ActuatorSpeed;
		public bool UseExponentialSpeed;
	}

	readonly Dictionary<ModuleControlSurface, Original> originals = new();
	Vessel vessel;

	public void Apply(Vessel target)
	{
		Restore();
		if (target == null || Settings.FarLoaded)
			return;

		vessel = target;
		Refresh();
		Debug.Log($"[MouseAimFlightRedux] Sped up {originals.Count} control surfaces");
	}

	/// <summary>Speeds up surfaces that have joined the vessel, and puts back those that have left it.</summary>
	public void Refresh()
	{
		if (vessel == null)
			return;

		// A surface decoupled onto another vessel still exists and keeps flying, so it gets its own values back.
		var gone = originals.Keys.Where(surface => surface == null || surface.vessel != vessel).ToList();
		foreach (var surface in gone)
		{
			if (surface != null)
				Put(surface, originals[surface]);
			originals.Remove(surface);
		}

		foreach (var surface in vessel.FindPartModulesImplementing<ModuleControlSurface>())
		{
			if (originals.ContainsKey(surface) || !IsStock(surface))
				continue;

			originals[surface] = new Original { ActuatorSpeed = surface.actuatorSpeed, UseExponentialSpeed = surface.useExponentialSpeed };
			surface.actuatorSpeed *= SpeedFactor;
			surface.useExponentialSpeed = true;
		}
	}

	public void Restore()
	{
		if (vessel == null)
			return;

		foreach (var pair in originals)
		{
			if (pair.Key != null)
				Put(pair.Key, pair.Value);
		}
		Debug.Log($"[MouseAimFlightRedux] Restored {originals.Count} control surfaces");

		originals.Clear();
		vessel = null;
	}

	/// <summary>
	/// ModuleControlSurface or a stock subclass such as ModuleAeroSurface. Atmosphere Autopilot, for one, swaps in its
	/// own subclass whose surfaces follow these settings differently.
	/// </summary>
	static bool IsStock(ModuleControlSurface surface) => surface.GetType().Assembly == typeof(ModuleControlSurface).Assembly;

	static void Put(ModuleControlSurface surface, Original original)
	{
		surface.actuatorSpeed = original.ActuatorSpeed;
		surface.useExponentialSpeed = original.UseExponentialSpeed;
	}
}

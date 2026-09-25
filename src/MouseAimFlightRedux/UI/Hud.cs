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

//// Dependencies

using UnityEngine;

namespace MouseAimFlightRedux.UI;

/// <summary>
/// The markers drawn over the flight view: a ring at the aim, and the chosen marker at
/// the nose.
/// </summary>
static class Hud
{
	//// Constants

	/// <summary>
	/// The nose marker's size against the aim ring's, so it fits inside the ring.
	/// </summary>
	const float NOSE_SCALE = 0.5f;

	//// Private Functions

	static void DrawAt(Vector3 screenPoint, float size, Texture2D texture)
	{
		// Sanity check
		// A point behind the camera has nothing to show.
		if (screenPoint.z <= 0f)
			return;

		// Draw it centred on the point
		GUI.DrawTexture(new Rect(screenPoint.x - 0.5f * size, Screen.height - screenPoint.y - 0.5f * size, size, size), texture);
	}

	//// Public API

	/// <summary>
	/// Call from OnGUI. Positions are taken at repaint, after the camera has moved for
	/// the frame.
	/// </summary>
	public static void Draw(Vessel vessel, Vector3 aim, Camera camera)
	{
		// Sanity check
		if (Event.current.type != EventType.Repaint)
			return;

		// Size and tint the markers
		var settings = Settings.Instance;
		var size = settings.ReticleSize * Screen.width / 32f;
		var centreOfMass = vessel.CoM;
		var oldColor = GUI.color;
		GUI.color = new Color(1f, 1f, 1f, settings.ReticleOpacity);

		// Draw the aim ring
		DrawAt(camera.WorldToScreenPoint(centreOfMass + aim), size, Reticles.Aim);

		// Draw the nose marker
		var nose = Reticles.Nose(settings.Reticle);
		if (nose != null)
			DrawAt(camera.WorldToScreenPoint(centreOfMass + vessel.ReferenceTransform.up * AimTracker.DISTANCE), size * NOSE_SCALE, nose);

		GUI.color = oldColor;
	}
}

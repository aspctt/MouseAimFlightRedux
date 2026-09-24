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

using UnityEngine;

namespace MouseAimFlightRedux;

/// <summary>
/// The aim the mouse moves. Held in world space relative to the centre of mass, so it stays put as the aircraft turns,
/// and moved in the camera's frame, so moving the mouse right always moves it right on screen. See docs/DESIGN.md,
/// "Aim direction".
/// </summary>
sealed class AimTracker
{
	/// <summary>How far ahead of the centre of mass the aim point sits, m.</summary>
	public const float Distance = 5000f;

	/// <summary>Aim point relative to the vessel's centre of mass, in world space.</summary>
	public Vector3 Aim { get; private set; }

	/// <summary>True while the mouse is moving the camera instead, which freezes the aim.</summary>
	public bool FreeLook { get; private set; }

	public void Recentre(Vessel vessel) => Aim = vessel.ReferenceTransform.up * Distance;

	/// <summary>Moves the aim by this frame's mouse movement, unless free look has the mouse.</summary>
	public void Follow(Settings settings, Transform camera)
	{
		if (FreeLook)
			return;

		var delta = new Vector3(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * (settings.MouseSensitivity * Mathf.Deg2Rad * Distance);
		if (settings.InvertX)
			delta.x = -delta.x;
		if (settings.InvertY)
			delta.y = -delta.y;

		var local = camera.InverseTransformDirection(Aim) + delta;
		Aim = camera.TransformDirection(local.normalized * Distance);
	}

	/// <summary>
	/// Free look is the right mouse button held, or KSP's own mouse look. Returns true on the frame it starts or ends.
	/// </summary>
	public bool UpdateFreeLook()
	{
		var was = FreeLook;
		FreeLook = Mouse.Right.GetButton() || CameraMouseLook.MouseLocked;
		return FreeLook != was;
	}
}

using System;
using UnityEngine;

namespace MouseAimFlightRedux.UI;

/// <summary>
/// Marker and icon textures, drawn at startup from signed distance functions so no image files ship with the mod.
/// Shapes are white on transparent and tinted when drawn.
/// </summary>
static class Reticles
{
	const int MarkerSize = 128;
	const int IconSize = 38;

	public static Texture2D Aim { get; private set; }
	public static Texture2D Cross { get; private set; }
	public static Texture2D Dot { get; private set; }
	public static Texture2D Icon { get; private set; }

	public static void Build()
	{
		if (Aim != null)
			return;

		Aim = Render(MarkerSize, p => Ring(p, 0.8f, 0.07f));
		Cross = Render(MarkerSize, CrossShape);
		Dot = Render(MarkerSize, p => p.magnitude - 0.14f);
		Icon = Render(IconSize, p => Mathf.Min(Ring(p, 0.72f, 0.14f), p.magnitude - 0.2f));
	}

	public static Texture2D Nose(ReticleStyle style) => style switch
	{
		ReticleStyle.Cross => Cross,
		ReticleStyle.Dot => Dot,
		_ => null,
	};

	/// <summary>Distance to the edge of a ring of the given radius and width; negative inside.</summary>
	static float Ring(Vector2 p, float radius, float width) => Mathf.Abs(p.magnitude - radius) - width / 2f;

	/// <summary>Four arms with a gap at the centre, so the nose marker never hides what it sits on.</summary>
	static float CrossShape(Vector2 p)
	{
		var a = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y));
		var horizontal = Box(a - new Vector2(0.6f, 0f), new Vector2(0.3f, 0.035f));
		var vertical = Box(a - new Vector2(0f, 0.6f), new Vector2(0.035f, 0.3f));
		return Mathf.Min(horizontal, vertical);
	}

	/// <summary>Distance to an axis-aligned box centred on the origin with the given half extents.</summary>
	static float Box(Vector2 p, Vector2 halfExtents)
	{
		var d = new Vector2(Mathf.Abs(p.x) - halfExtents.x, Mathf.Abs(p.y) - halfExtents.y);
		var outside = new Vector2(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f)).magnitude;
		var inside = Mathf.Min(Mathf.Max(d.x, d.y), 0f);
		return outside + inside;
	}

	/// <summary>Fills a square texture from a distance function over [-1, 1], antialiased across one pixel.</summary>
	static Texture2D Render(int size, Func<Vector2, float> distance)
	{
		var pixels = new Color32[size * size];
		var pixel = 2f / size;
		for (var y = 0; y < size; y++)
		{
			for (var x = 0; x < size; x++)
			{
				var p = new Vector2((x + 0.5f) * pixel - 1f, (y + 0.5f) * pixel - 1f);
				var coverage = Mathf.Clamp01(0.5f - distance(p) / pixel);
				pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(coverage * 255f));
			}
		}

		var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
		{
			filterMode = FilterMode.Bilinear,
			wrapMode = TextureWrapMode.Clamp,
		};
		texture.SetPixels32(pixels);
		texture.Apply(false, true);
		return texture;
	}
}

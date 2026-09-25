using System;
using UnityEngine;

namespace MouseAimFlightRedux.UI;

/// <summary>
/// Marker and icon textures, drawn at startup from signed distance functions so no image files ship with the mod.
/// Shapes are shades of grey on transparent, and tinted when drawn.
/// </summary>
static class Reticles
{
	const int MarkerSize = 128;
	const int IconSize = 38;

	/// <summary>The crosshair's black parts: its outline and the inner half of each arm.</summary>
	const float OutlineOpacity = 0.75f;

	/// <summary>The inside of the crosshair's arm tips, medium grey and half see-through.</summary>
	const float TipShade = 0.5f;

	const float TipOpacity = 0.5f;

	public static Texture2D Aim { get; private set; }
	public static Texture2D Crosshair { get; private set; }
	public static Texture2D Cross { get; private set; }
	public static Texture2D Dot { get; private set; }
	public static Texture2D Icon { get; private set; }

	public static void Build()
	{
		if (Aim != null)
			return;

		Aim = Render(MarkerSize, new Layer(p => Ring(p, 0.8f, 0.07f)));
		Crosshair = Render(MarkerSize,
			new Layer(CrosshairOutline, 0f, OutlineOpacity),
			new Layer(CrosshairTips, TipShade, TipOpacity),
			new Layer(CrosshairDot));
		Cross = Render(MarkerSize, new Layer(p => Arms(p, 0.3f, 0.9f, 0.035f)));
		Dot = Render(MarkerSize, new Layer(p => p.magnitude - 0.14f));
		Icon = Render(IconSize, new Layer(p => Mathf.Min(Ring(p, 0.72f, 0.14f), p.magnitude - 0.2f)));
	}

	public static Texture2D Nose(ReticleStyle style) => style switch
	{
		ReticleStyle.Crosshair => Crosshair,
		ReticleStyle.Cross => Cross,
		ReticleStyle.Dot => Dot,
		_ => null,
	};

	/// <summary>One shape of a marker, filled with a grey level at an opacity where its distance is negative.</summary>
	readonly struct Layer
	{
		public readonly Func<Vector2, float> Distance;
		public readonly float Shade;
		public readonly float Opacity;

		public Layer(Func<Vector2, float> distance, float shade = 1f, float opacity = 1f)
		{
			Distance = distance;
			Shade = shade;
			Opacity = opacity;
		}
	}

	/// <summary>Distance to the edge of a ring of the given radius and width; negative inside.</summary>
	static float Ring(Vector2 p, float radius, float width) => Mathf.Abs(p.magnitude - radius) - width / 2f;

	/// <summary>The outer half of each crosshair arm, a rounded rectangle.</summary>
	static float CrosshairTips(Vector2 p) => Arms(p, 0.63f, 0.9f, 0.07f, 0.05f);

	static float CrosshairDot(Vector2 p) => p.magnitude - 0.14f;

	/// <summary>
	/// A band around the tips and the dot, and the inner half of each arm, so the crosshair shows against bright sky and
	/// dark ground alike. The band leaves the tips' insides clear for their own see-through grey.
	/// </summary>
	static float CrosshairOutline(Vector2 p)
	{
		var shapes = Mathf.Min(CrosshairTips(p), CrosshairDot(p));
		var band = Mathf.Max(shapes - 0.07f, -shapes);
		return Mathf.Min(band, Arms(p, 0.36f, 0.63f, 0.07f));
	}

	/// <summary>
	/// Four arms along the axes, each running between two distances from the centre with the given half width and
	/// corner radius. The gap at the centre keeps a nose marker from hiding what it sits on.
	/// </summary>
	static float Arms(Vector2 p, float from, float to, float halfWidth, float cornerRadius = 0f)
	{
		var a = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y));
		var middle = (from + to) / 2f;
		var halfLength = (to - from) / 2f;
		var inset = new Vector2(cornerRadius, cornerRadius);
		var horizontal = Box(a - new Vector2(middle, 0f), new Vector2(halfLength, halfWidth) - inset);
		var vertical = Box(a - new Vector2(0f, middle), new Vector2(halfWidth, halfLength) - inset);
		return Mathf.Min(horizontal, vertical) - cornerRadius;
	}

	/// <summary>Distance to an axis-aligned box centred on the origin with the given half extents.</summary>
	static float Box(Vector2 p, Vector2 halfExtents)
	{
		var d = new Vector2(Mathf.Abs(p.x) - halfExtents.x, Mathf.Abs(p.y) - halfExtents.y);
		var outside = new Vector2(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f)).magnitude;
		var inside = Mathf.Min(Mathf.Max(d.x, d.y), 0f);
		return outside + inside;
	}

	/// <summary>
	/// Fills a square texture from layers of distance functions over [-1, 1], bottom layer first, antialiased across one
	/// pixel. Every mipmap is drawn from the shapes at its own size, so markers drawn small stay clean instead of
	/// shimmering or picking up light fringes from averaged see-through pixels.
	/// </summary>
	static Texture2D Render(int size, params Layer[] layers)
	{
		var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
		{
			filterMode = FilterMode.Trilinear,
			wrapMode = TextureWrapMode.Clamp,
		};

		for (var level = 0; level < texture.mipmapCount; level++)
		{
			var levelSize = Mathf.Max(1, size >> level);
			texture.SetPixels32(RenderLevel(levelSize, layers), level);
		}

		texture.Apply(false, true);
		return texture;
	}

	static Color32[] RenderLevel(int size, Layer[] layers)
	{
		var pixels = new Color32[size * size];
		var pixel = 2f / size;

		// Clear pixels take the bottom layer's shade, so filtering across an edge doesn't blend in a different one.
		var clearShade = layers[0].Shade;

		for (var y = 0; y < size; y++)
		{
			for (var x = 0; x < size; x++)
			{
				var p = new Vector2((x + 0.5f) * pixel - 1f, (y + 0.5f) * pixel - 1f);
				var shade = 0f;
				var alpha = 0f;
				foreach (var layer in layers)
				{
					var coverage = Mathf.Clamp01(0.5f - layer.Distance(p) / pixel) * layer.Opacity;
					shade = layer.Shade * coverage + shade * (1f - coverage);
					alpha = coverage + alpha * (1f - coverage);
				}

				var grey = (byte)Mathf.RoundToInt((alpha > 0f ? shade / alpha : clearShade) * 255f);
				pixels[y * size + x] = new Color32(grey, grey, grey, (byte)Mathf.RoundToInt(alpha * 255f));
			}
		}
		return pixels;
	}
}

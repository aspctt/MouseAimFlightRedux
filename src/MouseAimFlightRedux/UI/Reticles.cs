//// Dependencies

using System;
using UnityEngine;

namespace MouseAimFlightRedux.UI;

/// <summary>
/// Marker and icon textures, drawn the first time they're needed from signed distance
/// functions, so no image files ship with the mod. Shapes are shades of grey on
/// transparent, and tinted when drawn.
/// </summary>
static class Reticles
{
	//// Types

	/// <summary>
	/// One shape of a marker, filled with a grey level at an opacity where its distance
	/// is negative.
	/// </summary>
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

	/// <summary>Every texture, drawn together.</summary>
	sealed class Textures
	{
		public readonly Texture2D Aim = Render(MARKER_SIZE, new Layer(AimShape));
		public readonly Texture2D Crosshair = Render(MARKER_SIZE, new Layer(CrosshairOutline, 0f, OUTLINE_OPACITY), new Layer(CrosshairTips, TIP_SHADE, TIP_OPACITY), new Layer(CrosshairDot));
		public readonly Texture2D Cross = Render(MARKER_SIZE, new Layer(CrossShape));
		public readonly Texture2D Dot = Render(MARKER_SIZE, new Layer(DotShape));
		public readonly Texture2D Icon = Render(ICON_SIZE, new Layer(IconShape));
	}

	//// Constants

	const int MARKER_SIZE = 128;
	const int ICON_SIZE = 38;

	/// <summary>
	/// The crosshair's black parts: its outline and the inner half of each arm.
	/// </summary>
	const float OUTLINE_OPACITY = 0.75f;

	/// <summary>
	/// The inside of the crosshair's arm tips, medium grey and half see-through.
	/// </summary>
	const float TIP_SHADE = 0.5f;

	const float TIP_OPACITY = 0.5f;

	//// References and State

	static Textures? textures;

	static Textures All => textures ??= new Textures();

	//// Private Functions

	static float AimShape(Vector2 point) => Ring(point, 0.8f, 0.07f);

	static float CrossShape(Vector2 point) => Arms(point, 0.3f, 0.9f, 0.035f);

	static float DotShape(Vector2 point) => point.magnitude - 0.14f;

	/// <summary>The toolbar icon: a ring with a dot in it.</summary>
	static float IconShape(Vector2 point) => Mathf.Min(Ring(point, 0.72f, 0.14f), point.magnitude - 0.2f);

	/// <summary>
	/// Distance to the edge of a ring of the given radius and width; negative inside.
	/// </summary>
	static float Ring(Vector2 point, float radius, float width) => Mathf.Abs(point.magnitude - radius) - width / 2f;

	/// <summary>The outer half of each crosshair arm, a rounded rectangle.</summary>
	static float CrosshairTips(Vector2 point) => Arms(point, 0.63f, 0.9f, 0.07f, 0.05f);

	static float CrosshairDot(Vector2 point) => point.magnitude - 0.14f;

	/// <summary>
	/// A band around the tips and the dot, and the inner half of each arm, so the
	/// crosshair shows against bright sky and dark ground alike. The band leaves the
	/// tips' insides clear for their own see-through grey.
	/// </summary>
	static float CrosshairOutline(Vector2 point)
	{
		var shapes = Mathf.Min(CrosshairTips(point), CrosshairDot(point));
		var band = Mathf.Max(shapes - 0.07f, -shapes);
		return Mathf.Min(band, Arms(point, 0.36f, 0.63f, 0.07f));
	}

	/// <summary>
	/// Four arms along the axes, each running between two distances from the centre with
	/// the given half width and corner radius. The gap at the centre keeps a nose marker
	/// from hiding what it sits on.
	/// </summary>
	static float Arms(Vector2 point, float from, float to, float halfWidth, float cornerRadius = 0f)
	{
		var folded = new Vector2(Mathf.Abs(point.x), Mathf.Abs(point.y));
		var middle = (from + to) / 2f;
		var halfLength = (to - from) / 2f;
		var inset = new Vector2(cornerRadius, cornerRadius);
		var horizontal = Box(folded - new Vector2(middle, 0f), new Vector2(halfLength, halfWidth) - inset);
		var vertical = Box(folded - new Vector2(0f, middle), new Vector2(halfWidth, halfLength) - inset);
		return Mathf.Min(horizontal, vertical) - cornerRadius;
	}

	/// <summary>
	/// Distance to an axis-aligned box centred on the origin with the given half extents.
	/// </summary>
	static float Box(Vector2 point, Vector2 halfExtents)
	{
		var excess = new Vector2(Mathf.Abs(point.x) - halfExtents.x, Mathf.Abs(point.y) - halfExtents.y);
		var outside = new Vector2(Mathf.Max(excess.x, 0f), Mathf.Max(excess.y, 0f)).magnitude;
		var inside = Mathf.Min(Mathf.Max(excess.x, excess.y), 0f);
		return outside + inside;
	}

	/// <summary>
	/// Fills a square texture from layers of distance functions over [-1, 1], bottom
	/// layer first, antialiased across one pixel. Every mipmap is drawn from the shapes
	/// at its own size, so markers drawn small stay clean instead of shimmering or
	/// picking up light fringes from averaged see-through pixels.
	/// </summary>
	static Texture2D Render(int size, params Layer[] layers)
	{
		// Make the texture
		var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
		{
			filterMode = FilterMode.Trilinear,
			wrapMode = TextureWrapMode.Clamp,
		};

		// Draw every mipmap
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
		// Pick the shade for clear pixels
		// Clear pixels take the bottom layer's shade, so filtering across an edge doesn't
		// blend in a different one.
		var pixels = new Color32[size * size];
		var pixelSize = 2f / size;
		var clearShade = layers[0].Shade;

		// Stack the layers in every pixel
		for (var y = 0; y < size; y++)
		{
			for (var x = 0; x < size; x++)
			{
				var point = new Vector2((x + 0.5f) * pixelSize - 1f, (y + 0.5f) * pixelSize - 1f);
				var shade = 0f;
				var alpha = 0f;
				foreach (var layer in layers)
				{
					var coverage = Mathf.Clamp01(0.5f - layer.Distance(point) / pixelSize) * layer.Opacity;
					shade = layer.Shade * coverage + shade * (1f - coverage);
					alpha = coverage + alpha * (1f - coverage);
				}

				// Color32 takes bytes, and both values are already rounded to 0 to 255.
				var grey = (byte)Mathf.RoundToInt((alpha > 0f ? shade / alpha : clearShade) * 255f);
				pixels[y * size + x] = new Color32(grey, grey, grey, (byte)Mathf.RoundToInt(alpha * 255f));
			}
		}
		return pixels;
	}

	//// Public API

	public static Texture2D Aim => All.Aim;
	public static Texture2D Crosshair => All.Crosshair;
	public static Texture2D Cross => All.Cross;
	public static Texture2D Dot => All.Dot;
	public static Texture2D Icon => All.Icon;

	public static Texture2D? Nose(ReticleStyle style) => style switch
	{
		ReticleStyle.Crosshair => Crosshair,
		ReticleStyle.Cross => Cross,
		ReticleStyle.Dot => Dot,
		_ => null,
	};
}

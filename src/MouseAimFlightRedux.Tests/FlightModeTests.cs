//// Dependencies

using System.Reflection;
using MouseAimFlightRedux.Control;
using NUnit.Framework;

namespace MouseAimFlightRedux.Tests;

/// <summary>
/// Checks the shipped FlightModes.cfg against docs/DESIGN.md and the code.
/// </summary>
[TestFixture]
public sealed class FlightModeTests
{
	//// Public API

	[Test]
	public void ShipsTheDocumentedModesInOrder()
	{
		// Collect the names
		var names = new string[ShippedFlightModes.All.Count];
		for (var index = 0; index < names.Length; index++)
			names[index] = ShippedFlightModes.All[index].Name;

		// Compare them with "Flight modes"
		Assert.That(names, Is.EqualTo(new[] { "Normal", "Aggressive", "Unlimited", "Cruise" }));
	}

	/// <summary>
	/// The mode used when FlightModes.cfg has none should fly like the shipped Normal.
	/// </summary>
	[Test]
	public void BuiltInModeMatchesNormal()
	{
		// Find both
		var builtIn = new FlightMode();
		var normal = ShippedFlightModes.All[0];

		// Compare every setting
		using (Assert.EnterMultipleScope())
		{
			foreach (var field in typeof(FlightMode).GetFields(BindingFlags.Public | BindingFlags.Instance))
				Assert.That(field.GetValue(builtIn), Is.EqualTo(field.GetValue(normal)), field.Name);
		}
	}
}

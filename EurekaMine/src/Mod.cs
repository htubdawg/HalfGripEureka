using BepInEx.Configuration;
using BepInEx.Logging;
using HalfGripTools;

namespace EurekaMine
{
	/// <summary>
	/// Eureka Mine: a Pennsylvania coal mine as a race map. The race starts in the pithead yard, runs into the drift mouth and down the
	/// slope, and every lap after that is underground: the haulage, the plane, the lower gangway with the old workings above it, the
	/// spiral, the upper gangway over the overcast and the switchbacks, with the coal chute beside them. It has no scene of its own: it is
	/// built in code inside the Stadium scene when one of its races loads (as Highway and Temple of Drift do), and dealt into the run map.
	/// </summary>
	public class Mod : HalfGripMod
	{
		internal static ManualLogSource Log;
		internal static ModContext Ctx;

		internal static ConfigEntry<bool> TestMode;
		internal static ConfigEntry<float> BenchmarkFactor;
		internal static ConfigEntry<float> HaulageGrip, LampBrightness, HeadlightBoost, HeadlightAngle, CabinLight;
		internal static ConfigEntry<float> RoughGrip, RoughDrag, CoalGrip, CoalDrag, YardGrip, YardDrag;
		internal static ConfigEntry<int> Lights;
		internal static ConfigEntry<bool> DawnSky;
		internal static ConfigEntry<bool> RunawayCars, CoalChutes, VentDoors;
		internal static ConfigEntry<float> DoorSpeedKept;
		internal static ConfigEntry<bool> ShowPath, VerboseLog;

		/// <summary>The map entry the mod adds to the run config, once it exists.</summary>
		internal static MapDefinition Map;

		public override void Load(ModContext ctx)
		{
			Log = ctx.Log;
			Ctx = ctx;

			TestMode = ctx.Config.Bind("Map", "TestMode", false, "Every race you start is in Eureka Mine, whatever the run map says. For trying the map out.");
			BenchmarkFactor = ctx.Config.Bind("Map", "BenchmarkFactor", 1.0f, "Time objectives (\"finish under X s\") are the Stadium's times scaled by the race's length times this. Lower is harder.");
			HaulageGrip = ctx.Config.Bind("Surfaces", "HaulageGrip", 1.3f, "Grip on the haulage and the slope, the mine's main road, 1 being the game's plain tarmac. The old workings' and the chute's grip are shares of this.");
			RoughGrip = ctx.Config.Bind("Surfaces", "OldWorkingsGrip", 0.85f, "Grip on the broken floor of the old workings (the upper seam), 1 being the haulage.");
			RoughDrag = ctx.Config.Bind("Surfaces", "OldWorkingsDrag", 1.4f, "Rolling resistance in the old workings, 1 being the haulage.");
			CoalGrip = ctx.Config.Bind("Surfaces", "ChuteGrip", 0.68f, "Grip on the loose coal of the chute. The chute is far shorter than the switchbacks: this is what it costs.");
			CoalDrag = ctx.Config.Bind("Surfaces", "ChuteDrag", 1.35f, "Rolling resistance on the chute's loose coal.");
			YardGrip = ctx.Config.Bind("Surfaces", "YardGrip", 0.92f, "Grip on the pithead yard's gravel.");
			YardDrag = ctx.Config.Bind("Surfaces", "YardDrag", 1.15f, "Rolling resistance on the yard's gravel.");
			DawnSky = ctx.Config.Bind("Map", "DawnSky", true, "The pithead before sunrise: a painted dawn sky with the ridges on the horizon, and its light. Off: the overcast grey of 0.4.");
			LampBrightness = ctx.Config.Bind("Mine", "LampBrightness", 1f, "Brightness of the light the bulbs paint on the rock, 1 being the mod's own. Read when the map is built.");
			HeadlightBoost = ctx.Config.Bind("Mine", "HeadlightBoost", 8f, "Underground, the cars' headlights are this many times as bright as the game makes them (1: the game's own, made for open tarmac at night).");
			HeadlightAngle = ctx.Config.Bind("Mine", "HeadlightAngle", 110f, "Underground, the headlights' cone in degrees, at least (the game's own is kept if wider).");
			CabinLight = ctx.Config.Bind("Mine", "CabinLight", 0.35f, "Underground, a soft dash lamp lights the cockpit (gauges, shifter, wheel) and nothing else: how bright, 0 for none.");
			Lights = ctx.Config.Bind("Mine", "Lights", 8, "Only used if the lamps' light cannot be painted on the rock (the log warns): real lights moved to the mine lamps nearest the camera. Lower is cheaper.");
			RunawayCars = ctx.Config.Bind("Hazards", "RunawayCars", true, "Strings of loaded mine cars break loose down the plane now and then.");
			CoalChutes = ctx.Config.Bind("Hazards", "CoalChutes", true, "Chutes over the lower gangway dump lump coal onto the road.");
			VentDoors = ctx.Config.Bind("Hazards", "VentDoors", true, "Ventilation doors across the coal chute swing shut and open again.");
			DoorSpeedKept = ctx.Config.Bind("Hazards", "DoorSpeedKept", 0.8f, "Share of its speed a car keeps after bursting through a shut ventilation door (the doors are never solid).");
			ShowPath = ctx.Config.Bind("Debug", "ShowPath", false, "Draw the lap counter's line and the bots' paths.");
			VerboseLog = ctx.Config.Bind("Debug", "VerboseLog", false, "Write the detailed build log.");

			Route.Load();
			MapEntry.Ensure();
			ctx.OnSceneLoaded(scene =>
			{
				Backdrop.Capture();
				MapEntry.Ensure();
				SceneBuilder.OnSceneLoaded(scene);
			});
			// the main menu may be up already when the mod loads: its trees are copied now, else at the next scene
			Backdrop.Capture();
			ctx.OnUnload(MapEntry.Remove);
			ctx.PatchAll();
		}

		public override void Unload()
		{
			SceneBuilder.Unload();
		}

		internal static void Info(string message)
		{
			if (VerboseLog != null && VerboseLog.Value)
			{
				Log.LogInfo(message);
			}
		}
	}
}

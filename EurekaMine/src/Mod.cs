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
		internal static ConfigEntry<float> RoughGrip, RoughDrag, CoalGrip, CoalDrag, YardGrip, YardDrag;
		internal static ConfigEntry<int> Lights;
		internal static ConfigEntry<bool> RunawayCars, CoalChutes, VentDoors;
		internal static ConfigEntry<float> DoorBreakSpeed, DoorSpeedKept;
		internal static ConfigEntry<bool> ShowPath, VerboseLog;

		/// <summary>The map entry the mod adds to the run config, once it exists.</summary>
		internal static MapDefinition Map;

		public override void Load(ModContext ctx)
		{
			Log = ctx.Log;
			Ctx = ctx;

			TestMode = ctx.Config.Bind("Map", "TestMode", false, "Every race you start is in Eureka Mine, whatever the run map says. For trying the map out.");
			BenchmarkFactor = ctx.Config.Bind("Map", "BenchmarkFactor", 1.0f, "Time objectives (\"finish under X s\") are the Stadium's times scaled by the race's length times this. Lower is harder.");
			RoughGrip = ctx.Config.Bind("Surfaces", "OldWorkingsGrip", 0.85f, "Grip on the broken floor of the old workings (the upper seam), 1 being the haulage.");
			RoughDrag = ctx.Config.Bind("Surfaces", "OldWorkingsDrag", 1.4f, "Rolling resistance in the old workings, 1 being the haulage.");
			CoalGrip = ctx.Config.Bind("Surfaces", "ChuteGrip", 0.68f, "Grip on the loose coal of the chute. The chute is far shorter than the switchbacks: this is what it costs.");
			CoalDrag = ctx.Config.Bind("Surfaces", "ChuteDrag", 1.35f, "Rolling resistance on the chute's loose coal.");
			YardGrip = ctx.Config.Bind("Surfaces", "YardGrip", 0.92f, "Grip on the pithead yard's gravel.");
			YardDrag = ctx.Config.Bind("Surfaces", "YardDrag", 1.15f, "Rolling resistance on the yard's gravel.");
			Lights = ctx.Config.Bind("Mine", "Lights", 8, "Real lights moved to the mine lamps nearest the camera (the rest glow without lighting anything). Lower is cheaper.");
			RunawayCars = ctx.Config.Bind("Hazards", "RunawayCars", true, "Strings of loaded mine cars break loose down the plane now and then.");
			CoalChutes = ctx.Config.Bind("Hazards", "CoalChutes", true, "Chutes over the lower gangway dump lump coal onto the road.");
			VentDoors = ctx.Config.Bind("Hazards", "VentDoors", true, "Ventilation doors across the coal chute swing shut and open again.");
			DoorBreakSpeed = ctx.Config.Bind("Hazards", "DoorBreakSpeed", 70f, "Dashboard speed from which a car smashes through a shut ventilation door. Slower, the door holds.");
			DoorSpeedKept = ctx.Config.Bind("Hazards", "DoorSpeedKept", 0.85f, "Share of its speed a car keeps after smashing a door.");
			ShowPath = ctx.Config.Bind("Debug", "ShowPath", false, "Draw the lap counter's line and the bots' paths.");
			VerboseLog = ctx.Config.Bind("Debug", "VerboseLog", false, "Write the detailed build log.");

			Route.Load();
			MapEntry.Ensure();
			ctx.OnSceneLoaded(scene =>
			{
				MapEntry.Ensure();
				SceneBuilder.OnSceneLoaded(scene);
			});
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

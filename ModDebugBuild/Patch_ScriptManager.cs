using HarmonyLib;
using Sandbox.Game.World;
using System.IO;
using VRage.Utils;

namespace avaness.ModDebugBuild
{
    [HarmonyPatch(typeof(MyScriptManager), "UpdateCompatibility")]
    public static class Patch_ScriptManager
    {
		public static bool Prefix(string filename, ref string __result)
		{
			if(File.Exists(filename) && Main.IsLocalMod(filename))
            {
				__result = File.ReadAllText(filename);
                return false;
            }
			return true;
        }

	}

}

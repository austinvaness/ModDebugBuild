using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using VRage.Scripting;
using VRage.Utils;

namespace avaness.ModDebugBuild
{
    [HarmonyPatch("VRage.Scripting.MyVRageScriptingInternal", "CompileAsync")]
    public static class Patch_ScriptCompiler
    {

		public static bool Prefix(MyApiTarget target, string assemblyName, IEnumerable<Script> scripts, out List<Message> diagnostics, string friendlyName, ref Task<Assembly> __result)
		{
			diagnostics = new List<Message>();

			if (target != MyApiTarget.Mod || MyScriptCompiler.Static == null)
				return true;


			foreach (Script s in scripts)
            {
				if (!File.Exists(s.Name) || !Main.IsLocalMod(s.Name))
					return true;
            }

			MyLog.Default.WriteLine("Debug compilation of " + assemblyName + " starts...");
			__result = new MyDebugScriptCompiler(MyScriptCompiler.Static).Compile(target, assemblyName, scripts, diagnostics, friendlyName);
			MyLog.Default.WriteLine("... debug compilation finished.");
			return false;
        }
	}

}

using HarmonyLib;
using Sandbox.Game.World;
using System.Reflection;
using VRage.FileSystem;
using VRage.Game.Models;
using VRage.Plugins;
using VRage.Utils;

namespace avaness.ModDebugBuild
{
    public class Main : IPlugin
    {
        private static string modPath;

        public void Dispose()
        {
            MySession.OnUnloaded -= MySession_OnUnloaded;
        }

        public void Init(object gameInstance)
        {
            modPath = MyFileSystem.ModsPath.Replace('/', '\\');

            Harmony harmony = new Harmony("avaness.ModDebugBuild");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            MySession.OnUnloaded += MySession_OnUnloaded;
        }

        private void MySession_OnUnloaded()
        {
            MyLog.Default.WriteLine("Unloading modded models.");
            MyModels.UnloadModdedModels();
        }

        public void Update()
        {

        }

        public static bool IsLocalMod(string filename)
        {
            return filename.Replace('/', '\\').StartsWith(modPath);
        }
    }
}

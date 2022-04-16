using HarmonyLib;
using System.Reflection;
using VRage.FileSystem;
using VRage.Plugins;

namespace avaness.ModDebugBuild
{
    public class Main : IPlugin
    {
        private static string modPath;

        public void Dispose()
        {

        }

        public void Init(object gameInstance)
        {
            modPath = MyFileSystem.ModsPath.Replace('/', '\\');

            Harmony harmony = new Harmony("avaness.ModDebugBuild");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
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

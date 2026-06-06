using HarmonyLib;
using System;
using TaleWorlds.Engine; // Found in TaleWorlds.Engine.dll -> Contains the native Utilities class
using TaleWorlds.Library; // Found in TaleWorlds.Library.dll -> Contains Debug and ResourceDepot
using TaleWorlds.MountAndBlade; // Found in TaleWorlds.MountAndBlade.dll -> Contains MBSubModuleBase

namespace CleanLogsWarSails
{
    public class SubModule : MBSubModuleBase
    {
        private static bool _isWarSailsMissing = false;
        private static bool _hasLoggedSilence = false;

        protected override void OnSubModuleStart()
        {
            base.OnSubModuleStart();

            bool dlcActive = false;

            // Grabs the clean string array of all loaded module folder names directly from the engine cache
            string[] activeModules = Utilities.GetModulesNames();

            if (activeModules != null)
            {
                foreach (string module in activeModules)
                {
                    // Check for common variants of the War Sails module name
                    if (string.Equals(module, "WS", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(module, "WarSails", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(module, "War_Sails", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(module, "War Sails", StringComparison.OrdinalIgnoreCase))
                    {
                        dlcActive = true;
                        break;
                    }
                }
            }

            // Engage interceptor if the expansion string is missing from the active matrix
            if (!dlcActive)
            {
                _isWarSailsMissing = true;

                var harmony = new Harmony("com.auralynn.cleanlogs.warsails");
                harmony.PatchAll();
            }
        }

        // The gate intercepting the intake thread before missing assets can throw an exception
        [HarmonyPatch(typeof(TaleWorlds.Library.ResourceDepot), "AddPackageString")]
        public static class BlockMissingDlcAssets
        {
            public static bool Prefix(string p)
            {
                if (_isWarSailsMissing && p != null &&
                    (p.IndexOf("warsails", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     p.IndexOf("war_sails", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     p.IndexOf("war sails", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     p.IndexOf("ws", StringComparison.OrdinalIgnoreCase) == 0)) // Check if it starts with 'ws' in case of short prefixes
                {
                    if (!_hasLoggedSilence)
                    {
                        TaleWorlds.Library.Debug.PrintLine("[CleanLogs] Module 'War Sails' not found. Safely blocking asset load requests to prevent log flooding.", Debug.DebugColor.Yellow);
                        _hasLoggedSilence = true;
                    }

                    return false; // Safely drop this specific load thread immediately
                }
                return true;
            }
        }
    }
}

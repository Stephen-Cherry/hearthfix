namespace HearthFix;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
[BepInDependency("TastyChickenLegs.NoSmokeSimplified", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("TastyChickenLegs.NoSmokeStayLit", BepInDependency.DependencyFlags.SoftDependency)]
public class Plugin : BaseUnityPlugin
{
    internal static ManualLogSource Log = null!;
    private static Harmony? _harmony;

    private void Awake()
    {
        Log = Logger;
        Log.LogInfo($"HearthFix v{PluginInfo.PLUGIN_VERSION} loading...");
        _harmony = Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
        Log.LogInfo("HearthFix loaded.");
    }

    private void OnDestroy()
    {
        _harmony?.UnpatchSelf();
    }

    [HarmonyPatch(typeof(Fireplace), "IsBurning")]
    [HarmonyPriority(Priority.Low)]
    static class FireplaceIsBurning_Fix
    {
        static void Postfix(Fireplace __instance, ref bool __result)
        {
            if (__result) return;
            if (__instance.m_nview?.GetZDO() == null) return;

            // NoSmokeSimplified overwrites IsBurning with a raw ZDO fuel check.
            // Newly placed fireplaces have no "fuel" key in the ZDO yet, so GetFloat
            // returns the default (0), causing IsBurning to return false incorrectly.
            // Use -1 as a sentinel: fuel is always >= 0 for existing pieces.
            float fuel = __instance.m_nview.GetZDO().GetFloat("fuel", -1f);
            if (fuel < 0f && __instance.m_startFuel > 0f)
                __result = true;
        }
    }
}

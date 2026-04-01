namespace HearthFix;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
[BepInDependency("TastyChickenLegs.NoSmokeSimplified", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("TastyChickenLegs.NoSmokeStayLit", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("org.bepinex.plugins.valheim_plus", BepInDependency.DependencyFlags.SoftDependency)]
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
        // Cached reflection handles for V+ config — resolved once on first use.
        static PropertyInfo? _vplusCurrentProp;
        static PropertyInfo? _vplusFireSourceProp;
        static MemberInfo? _vplusFireSourceEnabledMember;
        static PropertyInfo? _vplusFireSourceFiresProp;
        static bool _vplusReflectionAttempted;

        static void Postfix(Fireplace __instance, ref bool __result)
        {
            if (__result) return;
            if (__instance.m_nview?.GetZDO() == null) return;

            float fuel = __instance.m_nview.GetZDO().GetFloat("fuel", -1f);

            // Case 1: Newly placed piece whose fuel key has not been written yet.
            // NoSmokeSimplified uses GetFloat("fuel", 0f) so the absent key looks like
            // empty fuel. Detect absence via sentinel -1 and restore true if the piece
            // has start fuel.
            if (fuel < 0f && __instance.m_startFuel > 0f)
            {
                __result = true;
                return;
            }

            // Case 2: Valheim Plus FireSource infinite fires.
            // V+ stops fuel from being consumed but doesn't write a positive fuel value,
            // so fuel stays at 0 and NoSmokeSimplified incorrectly returns false.
            if (IsVPlusInfiniteFires())
                __result = true;
        }

        static bool IsVPlusInfiniteFires()
        {
            if (!_vplusReflectionAttempted)
            {
                _vplusReflectionAttempted = true;
                try
                {
                    // Search all loaded assemblies for the V+ configuration type.
                    Type? configType = null;
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        configType = asm.GetType("ValheimPlus.Configurations.Configuration");
                        if (configType != null) break;
                    }

                    if (configType == null)
                    {
                        Log.LogDebug("[HearthFix] V+ not loaded, skipping FireSource check");
                        return false;
                    }

                    _vplusCurrentProp = configType.GetProperty("Current");
                    var current = _vplusCurrentProp?.GetValue(null);
                    if (current == null) return false;

                    _vplusFireSourceProp = current.GetType().GetProperty("FireSource");
                    var fireSource = _vplusFireSourceProp?.GetValue(current);
                    if (fireSource == null) return false;

                    // "enabled" may be a property or field, and may be on a base type.
                    var fsType = fireSource.GetType();
                    Type? t = fsType;
                    while (t != null && _vplusFireSourceEnabledMember == null)
                    {
                        _vplusFireSourceEnabledMember =
                            (MemberInfo?)t.GetProperty("enabled", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly) ??
                            (MemberInfo?)t.GetField("enabled", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                        t = t.BaseType;
                    }

                    _vplusFireSourceFiresProp = fsType.GetProperty("fires");
                }
                catch (Exception ex)
                {
                    Log.LogWarning($"[HearthFix] Failed to reflect V+ config: {ex.Message}");
                }
            }

            try
            {
                if (_vplusCurrentProp == null || _vplusFireSourceProp == null ||
                    _vplusFireSourceFiresProp == null)
                    return false;

                var current = _vplusCurrentProp.GetValue(null);
                var fireSource = _vplusFireSourceProp.GetValue(current);
                var fires = _vplusFireSourceFiresProp.GetValue(fireSource) as bool?;
                if (fires != true) return false;

                // If we couldn't find the enabled flag, trust that fires=true implies enabled.
                if (_vplusFireSourceEnabledMember is PropertyInfo pi)
                    return pi.GetValue(fireSource) is true;
                if (_vplusFireSourceEnabledMember is FieldInfo fi)
                    return fi.GetValue(fireSource) is true;
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}

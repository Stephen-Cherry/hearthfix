# Bug Report: Newly Placed Fireplaces Do Not Light

**Reported by:** Narolith
**Affects:** NoSmokeSimplified 1.1.0 (TastyChickenLegs-NoSmokeSimplified-1.1.0); NoSmokeStayLit 2.3.8 (partially — see Notes)
**Severity:** Medium — newly placed hearths, campfires, and wisp torches appear unlit until relog

---

## Summary

When NoSmokeSimplified is installed, any freshly built `Fireplace`-based piece does not light on placement. The piece sits dark and non-functional until the player relogs or the world reloads. Affected pieces include hearths, campfires, standing torches, and wisp torches (`piece_demister`).

---

## Root Cause

NoSmokeSimplified patches `Fireplace.IsBurning` with a Harmony Postfix that unconditionally overwrites the return value using a direct ZDO fuel check:

```csharp
__result = position.y >= liquidLevel && m_nview.GetZDO().GetFloat("fuel", 0f) > 0f;
```

The problem is the timing of ZDO key initialization. When a fireplace is first built, Valheim creates its ZDO and sets the piece into its initial burning state — but the `"fuel"` key is **not written to the ZDO yet**. It is only persisted after the `Fireplace` component runs its first fuel update tick.

This creates the following sequence on placement:

| Step | What happens |
|------|-------------|
| 1 | Player builds a hearth; vanilla `IsBurning` correctly returns `true` |
| 2 | NoSmokeSimplified's Postfix runs and calls `GetFloat("fuel", 0f)` |
| 3 | `"fuel"` key does not exist in the ZDO yet; `GetFloat` returns the fallback `0f` |
| 4 | `0f > 0f` evaluates to `false`; `__result` is overwritten to `false` |
| 5 | Piece never lights; flame, light, and wisp fog-clearing effects are all absent |

On relog or world reload the ZDO is fully synced, the `"fuel"` key is present, and the piece works normally.

---

## Affected Pieces

Any piece backed by the `Fireplace` class, including:

- `hearth`
- `fire_pit`
- `piece_walltorch`
- `piece_groundtorch` / `piece_groundtorch_wood`
- `piece_demister` (Wisp Torch) — critically, this also prevents Mistlands fog from clearing on placement

---

## Suggested Fix

Add a second Postfix on `Fireplace.IsBurning` at `HarmonyPriority.Low` so it runs after NoSmokeSimplified's Postfix. If the result is still `false`, use `-1f` as a sentinel default when reading the `"fuel"` key. Because real fuel values are always `>= 0`, a return of `-1f` means the key is absent — i.e., a newly placed piece. If the piece also has `m_startFuel > 0` (it is supposed to start burning), correct the result to `true`.

```csharp
[HarmonyPatch(typeof(Fireplace), "IsBurning")]
[HarmonyPriority(Priority.Low)]
static void Postfix(Fireplace __instance, ref bool __result)
{
    if (__result) return;
    if (__instance.m_nview?.GetZDO() == null) return;

    // Use -1f as sentinel: real fuel values are always >= 0.
    // A return of -1f means the "fuel" key is absent (newly placed piece).
    float fuel = __instance.m_nview.GetZDO().GetFloat("fuel", -1f);
    if (fuel < 0f && __instance.m_startFuel > 0f)
        __result = true;
}
```

Alternatively, the root issue can be fixed directly inside NoSmokeSimplified's existing Postfix by replacing the `0f` fallback with `-1f` and guarding against it:

```csharp
// Before
__result = position.y >= liquidLevel && m_nview.GetZDO().GetFloat("fuel", 0f) > 0f;

// After
float fuel = m_nview.GetZDO().GetFloat("fuel", -1f);
bool hasFuel = fuel < 0f ? __instance.m_startFuel > 0f : fuel > 0f;
__result = position.y >= liquidLevel && hasFuel;
```

The second option is the cleaner fix since it resolves the issue at the source rather than requiring a downstream workaround.

---

## Reproduction Steps

1. Install NoSmokeSimplified
2. Start a world and build any `Fireplace`-based piece (hearth recommended)
3. Observe: piece does not light on placement
4. Relog to the world
5. Observe: piece is now lit normally

---

## Notes on NoSmokeStayLit 2.3.8

NoSmokeStayLit contains the same `GetFloat("fuel", 0f)` flaw in its `FireplaceIsBurning_Patch`, but its "Keep Lit" feature incidentally masks the bug for most standard pieces:

```csharp
__result = true;  // set true unconditionally...
if ((int)Math.Ceiling(___m_nview.GetZDO().GetFloat("fuel", 0f)) == 0
    && !Configs.ConfigCheck(((Object)__instance).name))
{
    __result = false;  // ...overridden only if fuel == 0 AND piece is not in the keep-lit list
    return;
}
```

For pieces where `ConfigCheck` returns `true` (the default for hearths, campfires, torches, etc.), the condition never fires and `__result` stays `true`. This is coincidental protection, not a fix.

**`piece_demister` is not in `ConfigCheck` at all**, so it still returns `false` for a newly placed wisp torch — the bug is fully present for that piece in NoSmokeStayLit. Additionally, any piece where the user has disabled "Keep Lit" in the config will exhibit the same broken placement behavior.

---

## Workaround

[HearthFix](https://github.com/stephen-cherry/hearthfix) is a client-side compatibility patch that implements the downstream fix described above while NoSmokeSimplified/NoSmokeStayLit is updated.

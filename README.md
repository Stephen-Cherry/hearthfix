# HearthFix

Fixes newly placed fireplaces and wisp torches not lighting when [NoSmokeSimplified](https://www.nexusmods.com/valheim/mods/1862) or [NoSmokeStayLit](https://www.nexusmods.com/valheim/mods/1862) is installed.

## Problem

When NoSmokeSimplified or NoSmokeStayLit is installed, newly built hearths, campfires, and wisp torches (`piece_demister`) do not light. The piece appears placed but never starts burning, requiring a relog or world reload to fix.

NoSmokeStayLit's "Keep Lit" feature incidentally masks this for standard kept-lit pieces, but **wisp torches (`piece_demister`) are still broken** in both mods, as are any pieces where the user has disabled "Keep Lit" in NoSmokeStayLit's config.

## Root Cause

Both mods patch `Fireplace.IsBurning` to check the ZDO `"fuel"` key directly. For a freshly placed fireplace, that key hasn't been written to the ZDO yet — `GetFloat("fuel", 0f)` returns `0`, so `IsBurning` incorrectly returns `false`.

## Fix

A second `Postfix` on `Fireplace.IsBurning` runs at `Priority.Low` (after the other mod's patch). If `IsBurning` is still false and the ZDO has no `"fuel"` key yet (detected via a `-1` sentinel), and the piece has `m_startFuel > 0`, the piece was just placed and should be burning — so `IsBurning` is corrected to `true`.

## Compatibility

- Soft dependency on NoSmokeSimplified and NoSmokeStayLit — mod loads and works correctly regardless of which is installed
- Client-side only
- No Jotunn required

## Installation

Install via [Thunderstore](https://thunderstore.io) or drop `Narolith.HearthFix.dll` into your BepInEx `plugins` folder.

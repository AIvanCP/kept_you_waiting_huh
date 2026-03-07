using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace JoinSoundMod
{
    // =====================================================================
    //  Kept You Waiting, Huh? — Harmony Patches
    //
    //  Three patches:
    //    1. Pawn.SetFaction              — colonist joins from outside map
    //    2. PassingShipManager.AddShip   — ANY orbital/passing trade ship
    //                                       (vanilla + all mods, including
    //                                        gravship mods) — opt-in
    //    3. IncidentWorker_TraderCaravanArrival.TryExecuteWorker
    //                                   — walk-in caravan trader — opt-in
    //
    //  WHY PassingShipManager.AddShip instead of OrbitalTraderArrival?
    //    IncidentWorker_OrbitalTraderArrival is only called for VANILLA
    //    orbital traders.  Mods like HybridPoweredGravships and similar
    //    use custom incident workers that bypass it entirely.  Every ship
    //    that lands as a trader — vanilla or modded — must call
    //    PassingShipManager.AddShip, making it the universal hook.
    //
    //  WHY context=MapOnly in the SoundDef XML?
    //    context=Any triggers a RimWorld 1.6 config validation error
    //    ("non-on-camera subsounds should use MapOnly") which causes the
    //    sound to be silently skipped at runtime.  MapOnly is correct
    //    because a map is always loaded when these events are relevant.
    // =====================================================================

    [StaticConstructorOnStartup]
    internal static class HarmonySetup
    {
        static HarmonySetup()
        {
            var harmony = new Harmony("aivancp.keptyouwaitinghuh");

            // ── Patch 1: Pawn.SetFaction (colonist joins from outside map) ──
            ApplyPatch(harmony,
                original:  AccessTools.Method(typeof(Pawn), nameof(Pawn.SetFaction)),
                prefix:    new HarmonyMethod(typeof(Patch_Pawn_SetFaction),
                               nameof(Patch_Pawn_SetFaction.Prefix)),
                postfix:   new HarmonyMethod(typeof(Patch_Pawn_SetFaction),
                               nameof(Patch_Pawn_SetFaction.Postfix)),
                label:     "Pawn.SetFaction");

            // ── Patch 2: PassingShipManager.AddShip (ALL trade ships: vanilla + modded) ──
            // This fires whenever any ship is added to the passing ships list.
            // It is universal: HybridPoweredGravships, OrbitalTradeColumn, and
            // vanilla comms-console traders all go through this method.
            ApplyPatch(harmony,
                original:  AccessTools.Method(typeof(PassingShipManager), "AddShip"),
                prefix:    null,
                postfix:   new HarmonyMethod(typeof(Patch_PassingShip_AddShip),
                               nameof(Patch_PassingShip_AddShip.Postfix)),
                label:     "PassingShipManager.AddShip");

            // ── Patch 3: Walk-in trader caravan ─────────────────────────
            ApplyPatch(harmony,
                original:  AccessTools.Method(typeof(IncidentWorker_TraderCaravanArrival),
                               "TryExecuteWorker"),
                prefix:    null,
                postfix:   new HarmonyMethod(typeof(Patch_WalkInTraderArrival),
                               nameof(Patch_WalkInTraderArrival.Postfix)),
                label:     "IncidentWorker_TraderCaravanArrival.TryExecuteWorker");

            Log.Message("[KeptYouWaitingHuh] Harmony setup complete.");
        }

        /// <summary>
        /// Applies a single Harmony patch and logs success or failure clearly.
        /// </summary>
        private static void ApplyPatch(
            Harmony harmony,
            MethodBase original,
            HarmonyMethod prefix,
            HarmonyMethod postfix,
            string label)
        {
            try
            {
                if (original == null)
                {
                    Log.Error($"[KeptYouWaitingHuh] Method not found, cannot patch: {label}");
                    return;
                }
                harmony.Patch(original, prefix: prefix, postfix: postfix);
                Log.Message($"[KeptYouWaitingHuh] Patched: {label}");
            }
            catch (Exception ex)
            {
                Log.Error($"[KeptYouWaitingHuh] Failed to patch '{label}': {ex.Message}");
            }
        }
    }


    // =====================================================================
    //  PATCH 1 — Pawn.SetFaction
    //
    //  RimWorld calls SetFaction in two cases we care about:
    //
    //    A) Colonist arrives from outside the map (wanderer, refugee, event):
    //       → The pawn was NOT a prisoner or slave beforehand.
    //       → We PLAY the sound.
    //
    //    B) Prisoner recruited / slave freed in-colony:
    //       → IsPrisoner or IsSlave was TRUE before the call.
    //       → We SKIP the sound.
    //
    //  A Prefix captures pre-call state; a Postfix evaluates it.
    // =====================================================================

    // NOTE: No [HarmonyPatch] attribute — this class is patched manually above.
    internal static class Patch_Pawn_SetFaction
    {
        internal static void Prefix(
            Pawn __instance,
            out (bool wasPrisoner, bool wasSlave, bool hadPlayerFaction) __state)
        {
            __state = (
                wasPrisoner:      __instance.IsPrisoner,
                wasSlave:         __instance.IsSlave,
                hadPlayerFaction: __instance.Faction?.IsPlayer ?? false
            );
        }

        internal static void Postfix(
            Pawn __instance,
            Faction newFaction,
            (bool wasPrisoner, bool wasSlave, bool hadPlayerFaction) __state)
        {
            try
            {
                if (!JoinSoundMod.Settings.enableJoinSound)               return;
                if (newFaction == null || !newFaction.IsPlayer)            return;
                if (__instance.RaceProps == null
                    || !__instance.RaceProps.Humanlike)                    return;
                if (Current.ProgramState != ProgramState.Playing)          return;
                if (__state.hadPlayerFaction)                              return;
                if (__state.wasPrisoner || __state.wasSlave)               return;
                if (__instance.Dead || __instance.Destroyed)               return;

                SoundHelper.PlayOnCamera(
                    "JoinSound_PawnJoined", JoinSoundMod.Settings.joinSoundVolume);
            }
            catch (Exception ex)
            {
                Log.Error($"[KeptYouWaitingHuh] Patch_Pawn_SetFaction.Postfix: {ex}");
            }
        }
    }


    // =====================================================================
    //  PATCH 2 — PassingShipManager.AddShip
    //
    //  Called whenever ANY trade ship (passing ship) is added to the map’s
    //  passing ship manager.  This covers:
    //    • Vanilla orbital traders (comms console + random events)
    //    • Modded trader ships: HybridPoweredGravships, OrbitalTradeColumn,
    //      BiggerGravship, and any other mod that adds a TradeShip via
    //      the standard passing-ship system.
    //
    //  The method is void (no __result) — if it was called, the ship was
    //  successfully added.  Only plays if enableCommsTraderSound is on.
    // =====================================================================

    internal static class Patch_PassingShip_AddShip
    {
        internal static void Postfix()
        {
            try
            {
                if (!JoinSoundMod.Settings.enableCommsTraderSound)        return;
                if (Current.ProgramState != ProgramState.Playing)         return;

                SoundHelper.PlayOnCamera(
                    SoundHelper.TraderSoundDefName, JoinSoundMod.Settings.traderSoundVolume);
            }
            catch (Exception ex)
            {
                Log.Error($"[KeptYouWaitingHuh] Patch_PassingShip_AddShip.Postfix: {ex}");
            }
        }
    }


    // =====================================================================
    //  PATCH 3 — IncidentWorker_TraderCaravanArrival.TryExecuteWorker
    //
    //  Fires when a walk-in trader caravan arrives on the map.
    //  Only plays if Settings.enableWalkInTraderSound is on (default: OFF).
    //
    //  NOTE: Walk-in caravans are REGULAR trader caravans only.
    //  Empire tribute-collecting caravans use a different game system and
    //  will NOT trigger this patch.
    // =====================================================================

    internal static class Patch_WalkInTraderArrival
    {
        internal static void Postfix(bool __result)
        {
            try
            {
                if (!__result)                                             return;
                if (!JoinSoundMod.Settings.enableWalkInTraderSound)       return;
                if (Current.ProgramState != ProgramState.Playing)         return;

                SoundHelper.PlayOnCamera(
                    SoundHelper.TraderSoundDefName, JoinSoundMod.Settings.traderSoundVolume);
            }
            catch (Exception ex)
            {
                Log.Error($"[KeptYouWaitingHuh] Patch_WalkInTraderArrival.Postfix: {ex}");
            }
        }
    }


    // =====================================================================
    //  Shared sound helper
    // =====================================================================

    internal static class SoundHelper
    {
        /// <summary>
        /// Returns the SoundDef name to use for trader arrival events.
        /// By default (useSeparateTraderSound = false) this is the same def
        /// as the colonist join sound, so the user hears one consistent clip.
        /// When the setting is on, it uses JoinSound_TraderArrived instead,
        /// letting the user supply a different trader_arrived.ogg.
        /// </summary>
        internal static string TraderSoundDefName =>
            JoinSoundMod.Settings.useSeparateTraderSound
                ? "JoinSound_TraderArrived"
                : "JoinSound_PawnJoined";
        /// <summary>
        /// Resolves a SoundDef by name and plays it on the camera.
        /// Returns silently if no colony map is loaded (world-map view) because
        /// the SoundDef uses context=MapOnly.
        /// </summary>
        internal static void PlayOnCamera(string defName, float volumeMultiplier = 1f)
        {
            // MapOnly sounds need an active map; skip gracefully on world-map view.
            if (Find.CurrentMap == null) return;

            SoundDef def = SoundDef.Named(defName);
            if (def == null || def.isUndefined)
            {
                Log.Warning(
                    $"[KeptYouWaitingHuh] SoundDef '{defName}' not found. " +
                    "Is pawn_joined.ogg present in Sounds/JoinSound/?");
                return;
            }

            SoundInfo info = SoundInfo.OnCamera(MaintenanceType.None);
            info.volumeFactor = Mathf.Clamp(volumeMultiplier, 0f, 4f);
            def.PlayOneShot(info);
        }
    }
}

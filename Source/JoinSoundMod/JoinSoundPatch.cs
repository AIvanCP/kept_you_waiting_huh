using System;
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
    //  Three patches in total:
    //    1. Patch_Pawn_SetFaction        — colonist joins from outside map
    //    2. Patch_OrbitalTraderArrival   — comms/orbital trader (opt-in)
    //    3. Patch_WalkInTraderArrival    — walk-in caravan trader (opt-in)
    //
    //  All patches are Postfix (non-destructive) and wrapped in try-catch.
    // =====================================================================

    [StaticConstructorOnStartup]
    internal static class HarmonySetup
    {
        static HarmonySetup()
        {
            // The Harmony instance ID should be unique — using packageId convention.
            var harmony = new Harmony("authoryou.keptyouwaitinghuh");
            harmony.PatchAll();
            Log.Message("[KeptYouWaitingHuh] Harmony patches applied.");
        }
    }


    // =====================================================================
    //  PATCH 1 — Pawn.SetFaction
    //
    //  RimWorld calls Pawn.SetFaction(newFaction, recruiter) in two cases
    //  we care about:
    //
    //    A) Colonist joins from outside the map (wanderer, refugee, event):
    //       • The pawn was NOT previously a prisoner or slave.
    //       • Their old faction was something other than the player (or null).
    //
    //    B) Prisoner is recruited in-colony:
    //       • The pawn's IsPrisoner / IsSlave was TRUE right before the call.
    //
    //  Strategy:
    //    Prefix  → capture old state (wasPrisoner, wasSlave, oldFaction).
    //    Postfix → if new faction is player, AND pawn was not prisoner/slave,
    //              AND game is in playing state → play join sound.
    // =====================================================================

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.SetFaction))]
    internal static class Patch_Pawn_SetFaction
    {
        /// <summary>
        /// Runs BEFORE SetFaction changes anything.
        /// We capture the pre-change state via __state (passed to Postfix automatically).
        /// </summary>
        internal static void Prefix(Pawn __instance, out (bool wasPrisoner, bool wasSlave, bool hadPlayerFaction) __state)
        {
            __state = (
                wasPrisoner:      __instance.IsPrisoner,
                wasSlave:         __instance.IsSlave,
                hadPlayerFaction: __instance.Faction?.IsPlayer ?? false
            );
        }

        /// <summary>
        /// Runs AFTER SetFaction. Plays the join sound when appropriate.
        /// </summary>
        internal static void Postfix(
            Pawn __instance,
            Faction newFaction,
            (bool wasPrisoner, bool wasSlave, bool hadPlayerFaction) __state)
        {
            try
            {
                // ── Guard: settings ──────────────────────────────────────
                if (!JoinSoundMod.Settings.enableJoinSound) return;

                // ── Guard: must be joining the player faction ────────────
                if (newFaction == null || !newFaction.IsPlayer) return;

                // ── Guard: must be humanlike ─────────────────────────────
                if (__instance.RaceProps == null || !__instance.RaceProps.Humanlike) return;

                // ── Guard: game must be running (not during map init) ────
                if (Current.ProgramState != ProgramState.Playing) return;

                // ── Guard: don't fire if they were already on the player faction ──
                // (e.g., internal reassignments that stay within the colony)
                if (__state.hadPlayerFaction) return;

                // ── Guard: exclude prisoner recruitment ──────────────────
                // If the pawn was a prisoner or slave before this call,
                // they were recruited/freed in-colony — NOT an outside arrival.
                if (__state.wasPrisoner || __state.wasSlave) return;

                // ── Guard: pawn must be alive and valid ──────────────────
                if (__instance.Dead || __instance.Destroyed) return;

                // ── All checks passed — play the join sound ──────────────
                SoundHelper.PlayOnCamera("JoinSound_PawnJoined", JoinSoundMod.Settings.joinSoundVolume);
            }
            catch (Exception ex)
            {
                Log.Error($"[KeptYouWaitingHuh] Error in Patch_Pawn_SetFaction.Postfix: {ex}");
            }
        }
    }


    // =====================================================================
    //  PATCH 2 — IncidentWorker_OrbitalTraderArrival.TryExecuteWorker
    //
    //  Fires when a new orbital trader contacts the colony via comms console.
    //  Controlled by Settings.enableCommsTraderSound (default: OFF).
    // =====================================================================

    [HarmonyPatch(typeof(IncidentWorker_OrbitalTraderArrival), "TryExecuteWorker")]
    internal static class Patch_OrbitalTraderArrival
    {
        /// <summary>
        /// Runs after the orbital trader incident resolves successfully.
        /// __result is true when the incident actually fired.
        /// </summary>
        internal static void Postfix(bool __result)
        {
            try
            {
                if (!__result) return;
                if (!JoinSoundMod.Settings.enableCommsTraderSound) return;
                if (Current.ProgramState != ProgramState.Playing) return;

                SoundHelper.PlayOnCamera("JoinSound_TraderArrived", JoinSoundMod.Settings.traderSoundVolume);
            }
            catch (Exception ex)
            {
                Log.Error($"[KeptYouWaitingHuh] Error in Patch_OrbitalTraderArrival.Postfix: {ex}");
            }
        }
    }


    // =====================================================================
    //  PATCH 3 — IncidentWorker_TraderCaravanArrival.TryExecuteWorker
    //
    //  Fires when a walk-in trader caravan arrives on the map.
    //  Controlled by Settings.enableWalkInTraderSound (default: OFF).
    // =====================================================================

    [HarmonyPatch(typeof(IncidentWorker_TraderCaravanArrival), "TryExecuteWorker")]
    internal static class Patch_WalkInTraderArrival
    {
        /// <summary>
        /// Runs after the walk-in trader caravan incident resolves successfully.
        /// </summary>
        internal static void Postfix(bool __result)
        {
            try
            {
                if (!__result) return;
                if (!JoinSoundMod.Settings.enableWalkInTraderSound) return;
                if (Current.ProgramState != ProgramState.Playing) return;

                SoundHelper.PlayOnCamera("JoinSound_TraderArrived", JoinSoundMod.Settings.traderSoundVolume);
            }
            catch (Exception ex)
            {
                Log.Error($"[KeptYouWaitingHuh] Error in Patch_WalkInTraderArrival.Postfix: {ex}");
            }
        }
    }


    // =====================================================================
    //  Shared sound helper
    //
    //  Resolves a SoundDef by name and plays it on the camera with an
    //  optional volume multiplier.  Null-safe.
    // =====================================================================

    internal static class SoundHelper
    {
        /// <summary>
        /// Plays the named SoundDef on the camera with an optional volume scale.
        /// Logs a warning (not an error) if the def is missing.
        /// </summary>
        internal static void PlayOnCamera(string defName, float volumeMultiplier = 1f)
        {
            SoundDef def = SoundDef.Named(defName);
            if (def == null || def.isUndefined)
            {
                Log.Warning(
                    $"[KeptYouWaitingHuh] SoundDef '{defName}' could not be found. " +
                    "Make sure you have placed a valid .ogg file in Sounds/JoinSound/.");
                return;
            }

            // SoundInfo.OnCamera plays from the camera's perspective, so it is
            // heard at full volume regardless of map pan position.
            SoundInfo info = SoundInfo.OnCamera(MaintenanceType.None);
            info.volumeFactor = Mathf.Clamp(volumeMultiplier, 0f, 4f);

            def.PlayOneShot(info);
        }
    }
}

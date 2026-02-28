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
    //  Three patches in total:
    //    1. Patch_Pawn_SetFaction        — colonist joins from outside map
    //    2. Patch_OrbitalTraderArrival   — comms/orbital trader (opt-in)
    //    3. Patch_WalkInTraderArrival    — walk-in caravan trader (opt-in)
    //
    //  Patches are applied explicitly (not via PatchAll) so any failure is
    //  logged individually with a clear message.
    //
    //  WHY context=Any IN THE SOUNDDEF:
    //    Trader events can fire while the player is on the world map.
    //    A SoundDef with context=MapOnly is silently skipped when no colony
    //    map is open.  context=Any lets it play in all views.
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

            // ── Patch 2: OrbitalTrader (comms console) ───────────────────
            ApplyPatch(harmony,
                original:  AccessTools.Method(typeof(IncidentWorker_OrbitalTraderArrival),
                               "TryExecuteWorker"),
                prefix:    null,
                postfix:   new HarmonyMethod(typeof(Patch_OrbitalTraderArrival),
                               nameof(Patch_OrbitalTraderArrival.Postfix)),
                label:     "IncidentWorker_OrbitalTraderArrival.TryExecuteWorker");

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
    //  PATCH 2 — IncidentWorker_OrbitalTraderArrival.TryExecuteWorker
    //
    //  Fires when a comms-console / orbital trader contacts the colony.
    //  Only plays if Settings.enableCommsTraderSound is on (default: OFF).
    //
    //  NOTE: The orbital trader window can open while you are on the world
    //  map.  The SoundDef context must be "Any" (not "MapOnly") for the
    //  sound to be heard.
    // =====================================================================

    internal static class Patch_OrbitalTraderArrival
    {
        internal static void Postfix(bool __result)
        {
            try
            {
                if (!__result)                                             return;
                if (!JoinSoundMod.Settings.enableCommsTraderSound)        return;
                if (Current.ProgramState != ProgramState.Playing)         return;

                SoundHelper.PlayOnCamera(
                    "JoinSound_TraderArrived", JoinSoundMod.Settings.traderSoundVolume);
            }
            catch (Exception ex)
            {
                Log.Error($"[KeptYouWaitingHuh] Patch_OrbitalTraderArrival.Postfix: {ex}");
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
                    "JoinSound_TraderArrived", JoinSoundMod.Settings.traderSoundVolume);
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
        /// Resolves a SoundDef by name and plays it on the camera.
        /// Safe to call from any game state; null-checked throughout.
        ///
        /// IMPORTANT: The SoundDef's XML must use context=Any (not MapOnly)
        /// so it plays even when the player is on the world map.
        /// </summary>
        internal static void PlayOnCamera(string defName, float volumeMultiplier = 1f)
        {
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

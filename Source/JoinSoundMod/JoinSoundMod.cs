using UnityEngine;
using Verse;

namespace JoinSoundMod
{
    /// <summary>
    /// Entry point for Kept You Waiting, Huh?
    /// Registers settings and draws the Mod Options UI.
    /// </summary>
    public class JoinSoundMod : Mod
    {
        // Static accessor so patches can reach settings without having a Mod reference.
        public static JoinSoundSettings Settings { get; private set; }

        // ── Constructor ──────────────────────────────────────────────────────

        public JoinSoundMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<JoinSoundSettings>();
            Log.Message("[KeptYouWaitingHuh] Mod loaded successfully.");
        }

        // ── ModSettings title ────────────────────────────────────────────────

        public override string SettingsCategory() => "Kept You Waiting, Huh?";

        // ── Settings UI ──────────────────────────────────────────────────────

        /// <summary>
        /// Draws the settings window shown in Options → Mod Settings.
        /// </summary>
        public override void DoSettingsWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);

            // ── Section: Colonist Join ────────────────────────────────────
            listing.Label("<b>Colonist Join Sound</b>");
            listing.Gap(4f);

            listing.CheckboxLabeled(
                label:   "Enable join sound  (plays when a pawn joins the colony from outside)",
                checkOn: ref Settings.enableJoinSound,
                tooltip: "Plays whenever a pawn arrives from outside the map (wanderer, refugee, event). " +
                         "Does NOT play when a prisoner is recruited in-colony.");

            if (Settings.enableJoinSound)
            {
                listing.Gap(4f);
                listing.Label($"Join sound volume: {Settings.joinSoundVolume:P0}",
                    tooltip: "Scales the volume of the join sound. 100 % = full volume.");
                Settings.joinSoundVolume = listing.Slider(Settings.joinSoundVolume, 0f, 2f);
            }

            listing.GapLine(12f);

            // ── Section: Trader Arrival ───────────────────────────────────
            listing.Label("<b>Trader Arrival Sounds  (disabled by default)</b>");
            listing.Gap(4f);

            listing.CheckboxLabeled(
                label:   "Enable sound for orbital / comms-console traders",
                checkOn: ref Settings.enableCommsTraderSound,
                tooltip: "Plays the trader-arrival sound when a new orbital trader contacts your colony via comms console.");

            listing.CheckboxLabeled(
                label:   "Enable sound for walk-in trader caravans",
                checkOn: ref Settings.enableWalkInTraderSound,
                tooltip: "Plays the trader-arrival sound when a trader caravan walks onto the map.");

            if (Settings.enableCommsTraderSound || Settings.enableWalkInTraderSound)
            {
                listing.Gap(4f);
                listing.Label($"Trader sound volume: {Settings.traderSoundVolume:P0}",
                    tooltip: "Scales the volume of trader-arrival sounds. 100 % = full volume.");
                Settings.traderSoundVolume = listing.Slider(Settings.traderSoundVolume, 0f, 2f);
            }

            listing.GapLine(12f);

            // ── Audio file hint ───────────────────────────────────────────
            listing.Label("<color=#aaaaaa>Place your .ogg files in:  Mods/kept_you_waiting_huh/Sounds/JoinSound/</color>");
            listing.Label("<color=#aaaaaa>  • pawn_joined.ogg   — played on colonist join (and traders if no separate clip)</color>");
            listing.Label("<color=#aaaaaa>  • trader_arrived.ogg — optional separate clip for trader events (see SoundDefs XML)</color>");

            listing.End();

            // Persist any changes immediately
            Settings.Write();
        }
    }
}

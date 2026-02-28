using Verse;

namespace JoinSoundMod
{
    /// <summary>
    /// Persistent settings for Kept You Waiting, Huh?
    /// Values are saved to the player's ModSettings file automatically by RimWorld.
    /// </summary>
    public class JoinSoundSettings : ModSettings
    {
        // ── Colonist join sound ──────────────────────────────────────────────

        /// <summary>
        /// Master switch: play the join sound at all.
        /// </summary>
        public bool enableJoinSound = true;

        /// <summary>
        /// Volume multiplier for the join sound (0.0 – 2.0).
        /// </summary>
        public float joinSoundVolume = 1.0f;


        // ── Trader arrival sounds ────────────────────────────────────────────

        /// <summary>
        /// Play a sound when an orbital / comms-console trader contacts the colony.
        /// Default: OFF (users who want it must opt in).
        /// </summary>
        public bool enableCommsTraderSound = false;

        /// <summary>
        /// Play a sound when a walk-in trader caravan arrives on the map.
        /// Default: OFF.
        /// </summary>
        public bool enableWalkInTraderSound = false;

        /// <summary>
        /// Volume multiplier for both trader arrival sounds (0.0 – 2.0).
        /// </summary>
        public float traderSoundVolume = 1.0f;

        /// <summary>
        /// When false (default), trader arrivals play the same sound as a colonist
        /// joining (JoinSound_PawnJoined). When true, they play the separate
        /// JoinSound_TraderArrived def — useful if you place a trader_arrived.ogg
        /// in Sounds/JoinSound/ and update the SoundDef clipPath.
        /// </summary>
        public bool useSeparateTraderSound = false;


        // ── Serialization ────────────────────────────────────────────────────

        /// <summary>
        /// Called by RimWorld when reading/writing this mod's settings file.
        /// Every field that should persist must be listed here.
        /// </summary>
        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref enableJoinSound,         "enableJoinSound",         true);
            Scribe_Values.Look(ref joinSoundVolume,         "joinSoundVolume",         1.0f);

            Scribe_Values.Look(ref enableCommsTraderSound,   "enableCommsTraderSound",   false);
            Scribe_Values.Look(ref enableWalkInTraderSound,  "enableWalkInTraderSound",  false);
            Scribe_Values.Look(ref traderSoundVolume,        "traderSoundVolume",        1.0f);
            Scribe_Values.Look(ref useSeparateTraderSound,   "useSeparateTraderSound",   false);
        }
    }
}

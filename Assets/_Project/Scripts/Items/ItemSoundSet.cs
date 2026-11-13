using UnityEngine;
using UnityEngine.Audio;

namespace CloseTheDeal.Items
{
    /// <summary>
    /// Designer-owned: every sound an item makes in the world, and the mixer bus they play
    /// through. Runtime only reads it (AGENTS.md §4). One set for every item, linked from the
    /// carry profile; an item definition may name its own set. An empty slot plays nothing.
    /// The clips are made by the UI sound lane's generator (ArtSource/Audio/ui/make_ui_sounds.py).
    /// Event list and timings: docs/systems/ITEMS.md, "Item sounds".
    /// </summary>
    [CreateAssetMenu(menuName = "Close the Deal/Items/Item Sound Set", fileName = "ItemSounds")]
    public sealed class ItemSoundSet : ScriptableObject
    {
        [Tooltip("The mixer bus item sounds play through, so the settings' sound-effects volume applies: the game mixer's SFX group.")]
        public AudioMixerGroup Output;

        [Header("Hands")]
        [Tooltip("Something is taken into a player's hands. Plays at the item, on every machine.")]
        public AudioClip PickUp;
        [Tooltip("Something is let go without throwing: dropped, set down, or knocked from the hands.")]
        public AudioClip Drop;
        [Tooltip("Something is thrown: a short whoosh at the hands.")]
        public AudioClip Throw;

        [Header("Thrown things landing")]
        [Tooltip("A thrown thing lighter than the weight below hits something hard.")]
        public AudioClip ImpactLight;
        [Tooltip("A thrown thing at or above the weight below hits something hard.")]
        public AudioClip ImpactHeavy;
        [Tooltip("The weight, in kilograms, from which a landing uses the heavy impact.")]
        [Min(0f)] public float HeavyFromKg = 6f;
        [Tooltip("Impact speed, in metres per second, that plays the impact at full volume. Slower landings are quieter, down to the carry profile's slowest counted hit.")]
        [Min(1f)] public float FullVolumeImpactSpeed = 14f;

        [Header("Grenade Case")]
        [Tooltip("The case is armed: the click before the first beep.")]
        public AudioClip Arm;
        [Tooltip("One beep of the fuse. Keep it under 0.06 s: near the end beeps come every 0.08 s.")]
        public AudioClip Beep;
        [Tooltip("The explosion.")]
        public AudioClip Explosion;

        [Header("Fire extinguisher")]
        [Tooltip("The gas venting. Loops for as long as it vents (6 s), so it must loop without a click.")]
        public AudioClip VentLoop;
        [Tooltip("The last gasp when it runs empty.")]
        public AudioClip VentEnd;

        [Header("Wet floor sign")]
        [Tooltip("The sign is set down and the floor is wet.")]
        public AudioClip WetFloorDown;
        [Tooltip("A runner slips on the wet floor.")]
        public AudioClip Slip;

        [Header("Volume")]
        [Tooltip("How loud item sounds are before the mixer, 0 to 1.")]
        [Range(0f, 1f)] public float Volume = 0.9f;
    }
}

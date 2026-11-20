using UnityEngine;
using UnityEngine.Audio;

namespace CloseTheDeal.Combat
{
    /// <summary>
    /// Designer-owned: the sounds guns, hits and glass make, and the mixer bus they play through.
    /// Each gun's own shot and reload sounds are on its WeaponDefinition; this holds the rest.
    /// Runtime only reads it (AGENTS.md §4). Timings and events: docs/systems/WEAPONS.md.
    /// </summary>
    [CreateAssetMenu(menuName = "Seal the Deal/Combat Sound Set", fileName = "CombatSounds")]
    public sealed class CombatSoundSet : ScriptableObject
    {
        [Tooltip("The mixer group every combat sound plays through: the game mixer's Master/SFX, so the SFX volume setting covers it.")]
        public AudioMixerGroup Output;

        [Tooltip("Loudness of every combat sound, as a fraction. 1 = as the clips are.")]
        [Range(0f, 1f)] public float Volume = 1f;

        [Tooltip("How much each shot and impact's pitch varies, as a fraction either way. 0.03 keeps a burst from sounding like one clip repeated; more sounds wobbly.")]
        [Range(0f, 0.2f)] public float PitchVariance = 0.03f;

        [Header("Shots landing")]
        [Tooltip("A shot stopping on the building, furniture or a door. Played where it hit.")]
        public AudioClip ImpactWorld;

        [Tooltip("A shot stopping on a player or a dummy: the thud everyone hears, separate from the shooter's hit marker.")]
        public AudioClip ImpactBody;

        [Tooltip("A pane of glass breaking. Played at the pane.")]
        public AudioClip GlassShatter;

        [Header("Your own body")]
        [Tooltip("You were hurt. Played flat, not in the world.")]
        public AudioClip Hurt;

        [Tooltip("You were knocked out.")]
        public AudioClip Death;

        [Tooltip("You are back.")]
        public AudioClip Respawn;

        [Header("Distance")]
        [Tooltip("Up to this distance a sound in the world plays at full loudness, in metres.")]
        [Min(0.1f)] public float NearDistance = 2f;

        [Tooltip("Beyond this distance a sound in the world can't be heard, in metres. The towers are 12 m apart and a floor is 18 m deep, so 60 carries a shot across the street.")]
        [Min(1f)] public float FarDistance = 60f;
    }
}

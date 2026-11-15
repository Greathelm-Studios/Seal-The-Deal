using UnityEngine;
using UnityEngine.Audio;

namespace CloseTheDeal.Player
{
    /// <summary>
    /// Designer-owned: every sound a moving body makes, and the bus they play through. Runtime only
    /// reads it (AGENTS.md §4). Linked from MovementProfile. An empty slot plays nothing. Clips are
    /// made by the sound lane's generator as Assets/_Project/Audio/Movement/Move_*.wav; Close the
    /// Deal > Lanes > Set Up Movement Sounds fills empty slots from there. Names: PLAYER_MOVEMENT.md.
    /// </summary>
    [CreateAssetMenu(menuName = "Close the Deal/Movement/Movement Sound Set", fileName = "MovementSounds")]
    public sealed class MovementSoundSet : ScriptableObject
    {
        [Tooltip("The mixer bus movement sounds play through, so the settings' sound-effects volume applies: the game mixer's SFX group.")]
        public AudioMixerGroup Output;

        [Header("Feet")]
        [Tooltip("One footstep at walking pace (Move_Footstep_Walk). Several variations are picked from at random, never the same one twice running.")]
        public AudioClip[] FootstepWalk = new AudioClip[0];
        [Tooltip("One footstep at a run or sprint (Move_Footstep_Run).")]
        public AudioClip[] FootstepRun = new AudioClip[0];
        [Tooltip("Distance between steps at walking speed, in metres. Higher = slower steps.")]
        [Min(0.3f)] public float WalkStride = 1.7f;
        [Tooltip("Distance between steps at sprint speed, in metres.")]
        [Min(0.3f)] public float RunStride = 2.4f;

        [Header("Jumping and landing")]
        [Tooltip("Take-off (Move_Jump_01, _02...). Variations are picked at random.")]
        public AudioClip[] Jump = new AudioClip[0];
        [Tooltip("A landing slower than the heavy speed below (Move_Land_Light).")]
        public AudioClip[] LandLight = new AudioClip[0];
        [Tooltip("A landing at or above the heavy speed (Move_Land_Heavy).")]
        public AudioClip[] LandHeavy = new AudioClip[0];
        [Tooltip("Falling speed, in metres per second, from which a landing uses the heavy sound. A full jump lands at about 8.")]
        [Min(0f)] public float HeavyLandSpeed = 11f;
        [Tooltip("Falling speed, in metres per second, that plays a landing at full volume; slower ones are quieter.")]
        [Min(1f)] public float FullVolumeLandSpeed = 12f;

        [Header("Slide")]
        [Tooltip("The drop into a slide (Move_Slide_Start).")]
        public AudioClip SlideStart;
        [Tooltip("The scrape while sliding (Move_Slide_Loop). Loops, so it must loop without a click.")]
        public AudioClip SlideLoop;
        [Tooltip("The slide ending (Move_Slide_Stop).")]
        public AudioClip SlideStop;

        [Header("Climbing")]
        [Tooltip("Pulling up onto something (Move_Mantle).")]
        public AudioClip[] Mantle = new AudioClip[0];
        [Tooltip("Vaulting over something thin, like the window sill (Move_Vault).")]
        public AudioClip[] Vault = new AudioClip[0];

        [Header("Wind")]
        [Tooltip("Air rushing past at speed, heard only by the player moving (Move_Wind_Loop). Loops.")]
        public AudioClip WindLoop;
        [Tooltip("Speed, in metres per second, at which the wind starts to be heard. Sprint is 10.1.")]
        [Min(0f)] public float WindFromSpeed = 10.5f;
        [Tooltip("Speed, in metres per second, at which the wind is at full volume.")]
        [Min(0f)] public float WindFullSpeed = 18f;

        [Header("Volume")]
        [Tooltip("How loud movement sounds are before the mixer, 0 to 1.")]
        [Range(0f, 1f)] public float Volume = 0.8f;
        [Tooltip("Footsteps relative to the other movement sounds, 0 to 1.")]
        [Range(0f, 1f)] public float FootstepVolume = 0.55f;
        [Tooltip("Farthest another player's movement can be heard, in metres.")]
        [Min(1f)] public float HearingDistance = 30f;
    }
}

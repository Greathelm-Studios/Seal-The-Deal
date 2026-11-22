using UnityEngine;

namespace CloseTheDeal.Audio
{
    /// <summary>Designer-owned tuning for 3D spatial audio, wall/floor occlusion, and voice pooling.</summary>
    [CreateAssetMenu(menuName = "Seal the Deal/Audio/Spatial Audio Profile", fileName = "SpatialAudioProfile")]
    public sealed class SpatialAudioProfile : ScriptableObject
    {
        [Header("Voice pool")]
        [Tooltip("How many simultaneous 3D one-shot sounds (shots, impacts, glass, pickups) can play at once. Higher uses more CPU; lower steals distant sounds sooner.")]
        [Range(8, 64)] public int PooledVoices = 32;

        [Header("Spatialisation")]
        [Tooltip("Enable Steam Audio HRTF binaural spatialisation on 3D sources when Steam Audio Spatializer is active in Project Settings > Audio.")]
        public bool EnableSpatializerPlugin = true;

        [Tooltip("Use bilinear HRTF interpolation for proximity voice chat. Smoother as speakers move around you; slightly higher CPU than nearest-neighbour.")]
        public bool VoiceBilinearHrtf = true;

        [Tooltip("Default stereo spread angle in degrees for world SFX. 0 = pinpoint point-source; 20-35 feels more natural up close.")]
        [Range(0f, 180f)] public float DefaultSpread = 20f;

        [Tooltip("Stereo spread angle in degrees for proximity voice chat. Slightly wider than SFX so close speakers feel natural on headphones.")]
        [Range(0f, 180f)] public float VoiceSpread = 25f;

        [Header("Wall and floor occlusion")]
        [Tooltip("Muffle 3D voices and world SFX when walls, floors, or unbroken glass sit between the sound and the listener.")]
        public bool EnableOcclusion = true;

        [Tooltip("Low-pass cutoff when nothing blocks the sound, in Hz. 22000 = full clarity (filter bypassed).")]
        [Range(8000f, 22000f)] public float ClearCutoffHz = 22000f;

        [Tooltip("Low-pass cutoff behind one wall, floor, or unbroken window, in Hz. Lower sounds thicker and more muffled.")]
        [Range(400f, 6000f)] public float SingleWallCutoffHz = 1600f;

        [Tooltip("Low-pass cutoff behind two or more walls or floors, in Hz. Lower makes distant rooms duller.")]
        [Range(250f, 3000f)] public float HeavyOcclusionCutoffHz = 650f;

        [Tooltip("Volume multiplier behind one wall, floor, or unbroken window (0 to 1). 1 = no volume drop, only muffling.")]
        [Range(0.1f, 1f)] public float SingleWallGain = 0.6f;

        [Tooltip("Volume multiplier behind two or more walls or floors (0 to 1).")]
        [Range(0.05f, 1f)] public float HeavyOcclusionGain = 0.28f;

        [Tooltip("How fast persistent sources (voices, footsteps, loops) transition between clear and muffled, in seconds. Lower snaps faster when glass breaks.")]
        [Range(0.02f, 0.5f)] public float OcclusionSmoothSeconds = 0.12f;

        [Tooltip("How many times per second each active persistent 3D source checks for walls between itself and the listener. Higher is more responsive; lower uses less physics CPU.")]
        [Range(2f, 30f)] public float OcclusionCheckHz = 12f;

        [Tooltip("Minimum distance in metres between two collider hits to count them as separate walls rather than two sides of the same wall.")]
        [Range(0.05f, 1f)] public float MinBarrierSeparation = 0.25f;
    }
}

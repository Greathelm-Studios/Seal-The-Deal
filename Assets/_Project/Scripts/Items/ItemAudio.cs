using CloseTheDeal.Audio;
using UnityEngine;

namespace CloseTheDeal.Items
{
    /// <summary>Routes item 3D one-shots and persistent gadget sources through SoundSystem with spatial occlusion.</summary>
    public static class ItemAudio
    {
        const float MinDistance = 1.5f;
        const float MaxDistance = 40f;
        const int OneShotPriority = 112;
        const int PersistentPriority = 96;

        /// <summary>Plays a clip once at a point in the world through the set's bus. Volume is 0 to 1 on top of the set's own.</summary>
        public static void PlayAt(ItemSoundSet set, AudioClip clip, Vector3 point, float volume = 1f)
        {
            if (clip == null || volume <= 0f)
                return;

            SoundSystem.PlayAt(
                clip,
                point,
                set != null ? set.Output : null,
                (set != null ? set.Volume : 1f) * volume,
                MinDistance,
                MaxDistance,
                pitch: 1f,
                priority: OneShotPriority,
                useOcclusion: true);
        }

        /// <summary>Gives an item's own source (a loop, the fuse beeps) the set's bus, 3D curve, and wall/floor occlusion.</summary>
        public static void Configure(AudioSource source, ItemSoundSet set)
        {
            if (source == null)
                return;

            SoundSystem.ConfigurePersistent3D(
                source,
                set != null ? set.Output : null,
                MinDistance,
                MaxDistance,
                priority: PersistentPriority,
                useOcclusion: true);
        }
    }
}

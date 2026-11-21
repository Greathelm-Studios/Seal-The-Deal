using UnityEngine;

namespace CloseTheDeal.Combat
{
    /// <summary>Designer-owned tuning for one weapon.</summary>
    [CreateAssetMenu(menuName = "Seal the Deal/Weapon Definition", fileName = "Weapon")]
    public sealed class WeaponDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("The name the HUD and kill feed show for this gun.")]
        public string DisplayName = "Gun";

        [Header("Firing")]
        [Tooltip("Tick for a gun that keeps firing while the button is held. Untick for one shot per press.")]
        public bool Automatic;

        [Tooltip("How fast it fires, in shots per minute. 300 is a quick pistol; 600 is a rifle on full auto. For a one-shot-per-press gun this is the fastest a player can tap.")]
        [Min(1f)] public float RoundsPerMinute = 300f;

        [Tooltip("Health a shot takes at close range. Players have 100 by default.")]
        [Min(0f)] public float Damage = 25f;

        [Tooltip("How far a shot travels, in metres. The towers stand 12 m apart and a floor is 14 m deep, so 60 covers any cross-street fight.")]
        [Min(1f)] public float Range = 80f;

        [Header("Damage over distance")]
        [Tooltip("Up to this distance a shot does full damage, in metres.")]
        [Min(0f)] public float FalloffStart = 15f;

        [Tooltip("From this distance on a shot does only the far-range fraction below, in metres. Between the two distances the damage slides down evenly.")]
        [Min(0f)] public float FalloffEnd = 40f;

        [Tooltip("Share of the damage left at long range, as a fraction. 1 = no falloff; 0.5 = half damage across the street.")]
        [Range(0f, 1f)] public float FarDamageFraction = 0.6f;

        [Header("Accuracy")]
        [Tooltip("How far a calm first shot can stray from the crosshair, in degrees. 0 = the first shot lands exactly on the crosshair. At 20 m, 1 degree is about 35 cm.")]
        [Range(0f, 15f)] public float BaseSpread;

        [Tooltip("Extra random stray added by every shot of a burst, in degrees. Keep it small: the recoil pattern below is what the player learns; this is only the fuzz on top of it.")]
        [Range(0f, 5f)] public float BloomPerShot = 0.05f;

        [Tooltip("The most extra stray a burst can build up to, in degrees.")]
        [Range(0f, 20f)] public float MaxBloom = 0.6f;

        [Tooltip("How fast the stray settles once the shooting stops, in degrees per second. Higher = short pauses fix the aim sooner.")]
        [Min(0.1f)] public float BloomRecovery = 8f;

        [Tooltip("How much wider the stray is while the shooter is moving on the ground, as a multiple. 1 = running costs nothing, which suits fast movement; 2 = twice the stray.")]
        [Range(1f, 5f)] public float MovingSpreadMultiplier = 1f;

        [Tooltip("How much wider the stray is while the shooter is in the air, as a multiple. 1 = jumping costs nothing. It multiplies the stray, so a gun with no stray at all stays dead on in the air.")]
        [Range(1f, 5f)] public float AirborneSpreadMultiplier = 1f;

        [Header("Recoil pattern (the shooter's view)")]
        [Tooltip("How each shot of a burst kicks the view, in degrees: X sideways (plus = right), Y upward. The first entry is the first shot. The crosshair moves with the kick and the next shot goes where it points, so this is the pattern a player learns to pull against. It is the same every burst. Empty = no kick.")]
        public Vector2[] RecoilPattern = new Vector2[0];

        [Tooltip("Where a burst longer than the pattern carries on from, counting from 0. 0 = start the whole pattern again; set it to the first entry of the sideways sweep so a long burst keeps sweeping instead of climbing forever.")]
        [Min(0)] public int PatternLoopFrom;

        [Tooltip("How long the trigger must rest before the pattern starts again from its first shot, in seconds. Shorter rewards tap-firing; longer makes the player wait out a spray.")]
        [Range(0.05f, 2f)] public float PatternResetSeconds = 0.35f;

        [Tooltip("How fast the view settles back once the burst stops, in degrees per second. It does not settle between the shots of a burst, so the pattern is the same at any frame rate.")]
        [Min(1f)] public float KickRecovery = 12f;

        [Header("Feel: the view's snap on each shot")]
        [Tooltip("How far each shot snaps the view up, in degrees, on top of the recoil pattern. Unlike the pattern this comes all the way back on its own, so it is the punch of the shot rather than the climb. 0 = no snap.")]
        [Range(0f, 10f)] public float SnapUp = 1.5f;

        [Tooltip("The most each shot snaps the view sideways, in degrees, a random amount either way. Keep it about a quarter of the snap up.")]
        [Range(0f, 5f)] public float SnapSide = 0.35f;

        [Tooltip("How long the snap takes to come back, in seconds. Set it a little shorter than the time between shots and every tap lands on a still view.")]
        [Range(0.03f, 1f)] public float SnapRecoverySeconds = 0.14f;

        [Tooltip("The shape of the snap coming back, left (the shot) to right (settled). Start at 1 and end at 0; dipping just below 0 near the end swings the view a touch past rest, which reads as weight.")]
        public AnimationCurve SnapCurve = DecayingImpulse.DefaultCurve(0.04f);

        [Tooltip("How hard each shot shakes the view, in degrees. A quick rattle that never moves the aim; keep it about a tenth of the snap. Shots don't stack it: the stronger shake wins.")]
        [Range(0f, 3f)] public float ShakeDegrees = 0.2f;

        [Tooltip("How long the shake lasts, in seconds.")]
        [Range(0.02f, 1f)] public float ShakeSeconds = 0.15f;

        [Tooltip("How far each shot widens the view for an instant, in degrees of field of view. 2 to 3 reads as a punch; 0 = none.")]
        [Range(0f, 8f)] public float FovPunch = 1.2f;

        [Header("Feel: the gun in the hand on each shot")]
        [Tooltip("How far the gun jumps back toward the shooter on each shot, in metres.")]
        [Range(0f, 0.2f)] public float ViewPushBack = 0.05f;

        [Tooltip("How far the muzzle flips up on each shot, in degrees. The gun should move far more than the view: four or five times the snap.")]
        [Range(0f, 45f)] public float ViewFlip = 7f;

        [Tooltip("How far the gun rolls on each shot, in degrees, always the same way.")]
        [Range(0f, 20f)] public float ViewRoll = 2.5f;

        [Tooltip("How far the gun twists sideways on each shot, in degrees, following the side the view snapped to.")]
        [Range(0f, 20f)] public float ViewTwist = 2f;

        [Tooltip("How long the gun takes to settle after a shot, in seconds. A little slower than the view's snap, so the gun trails it.")]
        [Range(0.03f, 1f)] public float ViewRecoverySeconds = 0.2f;

        [Tooltip("The shape of the gun settling, as for the snap. A bigger dip below 0 gives a springier gun.")]
        public AnimationCurve ViewCurve = DecayingImpulse.DefaultCurve(0.12f);

        [Header("Feel: muzzle and impact")]
        [Tooltip("How long the muzzle flash and its light stay lit, in seconds. Two or three frames (0.03 to 0.05) reads as a flash; longer reads as a flame.")]
        [Range(0.01f, 0.2f)] public float FlashSeconds = 0.04f;

        [Tooltip("The burst played where a shot hits something. Each player's game plays its own; nothing is sent over the network. Leave empty for none.")]
        public ParticleSystem ImpactEffect;

        [Header("Damage by where it hits")]
        [Tooltip("Damage on a head hit, as a multiple of the body damage. 1 = the head counts the same as the body. The head is the top of the body from the height below up.")]
        [Range(1f, 4f)] public float HeadMultiplier = 1f;

        [Tooltip("Height on the body above which a hit counts as a head hit, in metres from the feet. A 1.8 m player's head starts at about 1.5.")]
        [Range(0.5f, 2.5f)] public float HeadFromHeight = 1.5f;

        [Header("Magazine")]
        [Tooltip("Shots in a full magazine.")]
        [Range(1, 250)] public int MagazineSize = 12;

        [Tooltip("How long a reload takes, in seconds. The gun can't fire during it; switching guns cancels it.")]
        [Min(0.1f)] public float ReloadSeconds = 1.4f;

        [Tooltip("How long after switching to this gun it can first fire, in seconds.")]
        [Min(0f)] public float EquipSeconds = 0.35f;

        [Header("What a hit does to a player")]
        [Tooltip("How fast a hit shoves the player along the shot, in metres per second. 0 = hits never touch the target's movement, which keeps fast movement fair; 2 is a nudge; 6 staggers them back a step. Any shove also costs them the control loss below.")]
        [Min(0f)] public float HitShove;

        [Tooltip("How long a shoved player can't steer, in seconds. Only used when the hit shove above is more than 0. Keep it tiny on fast guns or a burst pins them in place.")]
        [Min(0f)] public float HitControlLoss;

        [Tooltip("How fast the killing shot throws the body along the shot, in metres per second. Window glass breaks when a body hits it at about 10, so 12 or more can send a kill out through the window.")]
        [Min(0f)] public float KillShove = 12f;

        [Tooltip("How much of a shove goes upward, as a fraction of the push along the shot. 0 = straight back; 0.5 lifts them off their feet.")]
        [Range(0f, 1.5f)] public float ShoveLift = 0.35f;

        [Header("Sounds")]
        [Tooltip("The shot, heard by everyone from the muzzle. Keep a fast gun's short, or a burst smears.")]
        public AudioClip ShotSound;

        [Tooltip("The click of pulling the trigger on an empty magazine.")]
        public AudioClip DryFireSound;

        [Tooltip("Bringing this gun out.")]
        public AudioClip DrawSound;

        [Tooltip("The reload's first stage, about 15 % of the way through.")]
        public AudioClip MagazineOutSound;

        [Tooltip("The reload's second stage, about 60 % of the way through.")]
        public AudioClip MagazineInSound;

        [Tooltip("The reload's last stage, about 85 % of the way through: a pistol's slide, a rifle's charging handle.")]
        public AudioClip ReloadEndSound;

        [Header("Looks")]

        [Tooltip("The gun model shown in the hand. It needs a child called Muzzle where the shot leaves, and may have a child called Flash that is lit for an instant on each shot.")]
        public GameObject ViewPrefab;

        [Tooltip("Where the gun sits in the shooter's own view, in metres from the eye: right, up, forward.")]
        public Vector3 ViewOffset = new(0.22f, -0.2f, 0.45f);

        [Tooltip("The line drawn along each shot. Leave empty for no tracer.")]
        public LineRenderer TracerPrefab;

        [Tooltip("How long a tracer stays on screen, in seconds.")]
        [Range(0.02f, 0.5f)] public float TracerSeconds = 0.08f;

        /// <summary>Seconds between shots.</summary>
        public float ShotInterval => 60f / Mathf.Max(1f, RoundsPerMinute);

        /// <summary>The kick for a shot of a burst (0 = first shot): X sideways, Y up, degrees. Past the end the pattern carries on from PatternLoopFrom.</summary>
        public Vector2 KickFor(int shotInBurst)
        {
            int length = RecoilPattern.Length;
            if (length == 0 || shotInBurst < 0)
                return Vector2.zero;
            if (shotInBurst < length)
                return RecoilPattern[shotInBurst];

            int loopFrom = Mathf.Clamp(PatternLoopFrom, 0, length - 1);
            return RecoilPattern[loopFrom + (shotInBurst - length) % (length - loopFrom)];
        }

        /// <summary>Damage after range falloff.</summary>
        public float DamageAt(float distance)
        {
            if (FalloffEnd <= FalloffStart)
                return distance <= FalloffStart ? Damage : Damage * FarDamageFraction;

            float far = Mathf.InverseLerp(FalloffStart, FalloffEnd, distance);
            return Damage * Mathf.Lerp(1f, FarDamageFraction, far);
        }
    }
}

using CloseTheDeal.Player;
using CloseTheDeal.Records;
using CloseTheDeal.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CloseTheDeal.Combat
{
    /// <summary>
    /// The combat HUD on the shooter's own screen: hit marker and kill confirm (only when the host
    /// says the hit landed), ammo and the two weapon slots, reload and draw progress, low and empty
    /// magazine, where damage came from, health while hurt, and the knocked-out card with its
    /// countdown. Built from the UI kit (plates, tokens, motion, sounds) by Set Up Shooting; reads
    /// the local player's WeaponHolder and Health and decides nothing. Contract: docs/systems/WEAPONS.md.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatHud : MonoBehaviour
    {
        [Tooltip("The UI kit's tokens: every colour, type size and motion timing here comes from them.")]
        [SerializeField] UiTokens _tokens;

        [Header("Hit marker")]
        [Tooltip("The four ticks around the crosshair: outline images, then the cores, in the same order.")]
        [SerializeField] RectTransform[] _tickOutlines = new RectTransform[4];
        [SerializeField] RectTransform[] _ticks = new RectTransform[4];

        [Tooltip("How long a hit marker stays, in seconds. A kill keeps it two and a half times as long.")]
        [Range(0.05f, 0.6f)] [SerializeField] float _hitSeconds = 0.18f;

        [Tooltip("How far the ticks drift outward as they fade, in pixels at 1080p.")]
        [Range(0f, 20f)] [SerializeField] float _hitSpread = 5f;

        [Tooltip("How far from the centre the ticks start, in pixels at 1080p.")]
        [Range(2f, 40f)] [SerializeField] float _hitGap = 9f;

        [Header("Weapon")]
        [Tooltip("The plate holding the ammo and the gun's name; it shakes on a dry fire.")]
        [SerializeField] RectTransform _weaponPlate;
        [SerializeField] TMP_Text _ammoText;
        [SerializeField] TMP_Text _magazineText;
        [SerializeField] TMP_Text _weaponName;
        [SerializeField] TMP_Text _weaponState;
        [Tooltip("The action strip behind the weapon status. It changes colour for reloads and ammo warnings.")]
        [SerializeField] Image _statePlate;
        [Tooltip("The thin bar under the ammo that fills during a reload or a draw.")]
        [SerializeField] RectTransform _progressFill;
        [Tooltip("The two slot rows, slot 1 first: their text and the bar that marks the gun in hand.")]
        [SerializeField] TMP_Text[] _slotTexts = new TMP_Text[2];
        [SerializeField] Image[] _slotBars = new Image[2];

        [Tooltip("At or below this share of a full magazine the count turns to a warning, as a fraction.")]
        [Range(0f, 1f)] [SerializeField] float _lowAmmo = 0.25f;

        [Header("Health")]
        [Tooltip("Health shows only while hurt and for this long after the last hit, in seconds.")]
        [Range(0f, 10f)] [SerializeField] float _healthLinger = 2.5f;
        [SerializeField] CanvasGroup _healthGroup;
        [SerializeField] RectTransform _healthFill;
        [SerializeField] Image _healthFillImage;
        [SerializeField] TMP_Text _healthText;

        [Tooltip("At or below this share of full health the bar turns to a warning, as a fraction.")]
        [Range(0f, 1f)] [SerializeField] float _lowHealth = 0.35f;

        [Header("Damage direction")]
        [Tooltip("The markers that point at whoever hit you. Reused; the oldest makes way for a new hit.")]
        [SerializeField] Image[] _damageMarks = new Image[4];
        [Tooltip("How far from the centre the damage markers sit, in pixels at 1080p.")]
        [Range(40f, 400f)] [SerializeField] float _damageRadius = 150f;
        [Tooltip("How long a damage marker stays, in seconds.")]
        [Range(0.2f, 3f)] [SerializeField] float _damageSeconds = 1.2f;

        [Header("Knocked out")]
        [SerializeField] CanvasGroup _downGroup;
        [SerializeField] TMP_Text _downCount;
        [Tooltip("The card's heading and its line: who and how, then where you come back.")]
        [SerializeField] TMP_Text _downTitle;
        [SerializeField] TMP_Text _downLine;

        const string DefaultDownTitle = "YOU HAVE BEEN LET GO";
        const string DefaultDownLine = "BACK ON THIS FLOOR IN";

        // ---- Runtime -------------------------------------------------------------------------

        enum HitKind : byte { Hit, Head, Kill }

        static readonly Vector2[] TickDirections =
        {
            new(-1f, 1f), new(1f, 1f), new(-1f, -1f), new(1f, -1f)
        };

        WeaponHolder _holder;
        Health _health;
        PlayerMotor _bound;
        Image[] _tickImages;
        Image[] _tickOutlineImages;

        float _hitAge = float.MaxValue;
        float _hitLife;
        HitKind _hitKind;

        UiSpring _shake;
        UiSpring _punch = new(1f);
        bool _animating;

        float _lastHurtTime = float.MinValue;
        UiTween _healthFade;
        float _shownHealth = -1f;

        readonly Vector3[] _damageSources = new Vector3[4];
        readonly float[] _damageAges = { float.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue };
        int _nextDamage;

        UiTween _downFade;
        int _shownCount = -1;
        int _shownAmmo = -1;
        int _shownMagazine = -1;
        bool _empty;

        void Awake()
        {
            _tickImages = ImagesOf(_ticks);
            _tickOutlineImages = ImagesOf(_tickOutlines);
            ShowHitMarker(false);
            foreach (Image mark in _damageMarks)
            {
                if (mark != null)
                    mark.enabled = false;
            }

            if (_healthGroup != null)
                _healthGroup.alpha = 0f;
            if (_downGroup != null)
                _downGroup.alpha = 0f;
            _healthFade.Snap(0f);
            _downFade.Snap(0f);
            // Here, not in OnEnable: the HUD is hidden while the menu is open, and a death then still needs its line.
            LocalDeath.Down += OnLocalDeath;
        }

        void OnDestroy()
        {
            LocalDeath.Down -= OnLocalDeath;
        }

        /// <summary>The match records' word on how this player went down: who, how, and where they come back.</summary>
        void OnLocalDeath(DeathReport report)
        {
            if (_downTitle == null || _downLine == null)
                return;

            string who = MatchRecords.NameOf(report.KillerClientId).ToUpperInvariant();
            switch (report.Kind)
            {
                case KillKind.Killed: _downTitle.text = "LET GO BY " + who; break;
                case KillKind.KnockedOut: _downTitle.text = "SHOWN THE WINDOW BY " + who; break;
                case KillKind.Fell: _downTitle.text = "LEFT THE BUILDING"; break;
                default: _downTitle.text = DefaultDownTitle; break;
            }

            _downLine.text = string.IsNullOrEmpty(report.ComesBackOn) ? DefaultDownLine : "BACK ON " + report.ComesBackOn.ToUpperInvariant() + " IN";
        }

        void OnEnable() => UiGlyphs.Changed += ShowState;

        void OnDisable()
        {
            UiGlyphs.Changed -= ShowState;
            Bind(null);
        }

        // ---- Following the local player ------------------------------------------------------

        void Update()
        {
            if (PlayerMotor.Local != _bound)
                Bind(PlayerMotor.Local);
            if (_holder == null)
                return;

            float dt = Time.unscaledDeltaTime;
            AgeHitMarker(dt);
            AgeDamageMarks(dt);
            ShowProgress();
            ShowHealthFade(dt);
            ShowDown(dt);
            Animate(dt);
        }

        void Bind(PlayerMotor player)
        {
            if (_holder != null)
            {
                _holder.HitConfirmed -= OnHitConfirmed;
                _holder.KillConfirmed -= OnKillConfirmed;
                _holder.AmmoChanged -= OnAmmoChanged;
                _holder.WeaponChanged -= OnWeaponChanged;
                _holder.DryFired -= OnDryFired;
                _holder.ReloadStarted -= OnReloadChanged;
                _holder.ReloadFinished -= OnReloadEnded;
                _holder.ReloadCancelled -= OnReloadEnded;
            }

            if (_health != null)
            {
                _health.Damaged -= OnDamaged;
                _health.HealthChanged -= OnHealthChanged;
                _health.Respawned -= OnRespawned;
            }

            _bound = player;
            _holder = player != null ? player.GetComponent<WeaponHolder>() : null;
            _health = player != null ? player.GetComponent<Health>() : null;
            if (_holder == null || _health == null)
            {
                _holder = null;
                _health = null;
                return;
            }

            _holder.HitConfirmed += OnHitConfirmed;
            _holder.KillConfirmed += OnKillConfirmed;
            _holder.AmmoChanged += OnAmmoChanged;
            _holder.WeaponChanged += OnWeaponChanged;
            _holder.DryFired += OnDryFired;
            _holder.ReloadStarted += OnReloadChanged;
            _holder.ReloadFinished += OnReloadEnded;
            _holder.ReloadCancelled += OnReloadEnded;
            _health.Damaged += OnDamaged;
            _health.HealthChanged += OnHealthChanged;
            _health.Respawned += OnRespawned;

            // A fresh player: everything as it is now, no leftovers from the last one.
            _shownAmmo = _shownMagazine = -1;
            _shownHealth = -1f;
            OnWeaponChanged(_holder.ActiveWeapon, _holder.ActiveSlot);
            OnHealthChanged(_health.Current, _health.Max);
        }

        // ---- Hit marker and kill confirm (host-confirmed only) -------------------------------

        void OnHitConfirmed(Vector3 point, float damage, bool killed, bool precision)
        {
            HitKind kind = killed ? HitKind.Kill : precision ? HitKind.Head : HitKind.Hit;
            if (WouldDowngradeKill(kind))
                return;

            _hitKind = kind;
            _hitAge = 0f;
            _hitLife = kind == HitKind.Kill ? _hitSeconds * 2.5f : _hitSeconds;
            if (kind != HitKind.Kill)
                UiSound.Play(precision ? UiSoundEvent.HitMarkerHead : UiSoundEvent.HitMarker);

            Color core = ColourOf(kind);
            for (int i = 0; i < _tickImages.Length; i++)
            {
                if (_tickImages[i] != null)
                    _tickImages[i].color = core;
            }

            ShowHitMarker(true);
            PlaceTicks(0f);
        }

        /// <summary>A later hit in the same moment never downgrades a kill marker that is still showing.</summary>
        bool WouldDowngradeKill(HitKind kind) => _hitAge < _hitLife && _hitKind == HitKind.Kill && kind != HitKind.Kill;

        Color ColourOf(HitKind kind)
        {
            switch (kind)
            {
                case HitKind.Kill: return _tokens.Electric;
                case HitKind.Head: return _tokens.Cyan;
                default: return _tokens.Paper;
            }
        }

        void OnKillConfirmed(Vector3 point)
        {
            UiSound.Play(UiSoundEvent.KillConfirm);
        }

        void AgeHitMarker(float dt)
        {
            if (_hitAge >= _hitLife)
                return;

            _hitAge += dt;
            if (_hitAge >= _hitLife)
            {
                ShowHitMarker(false);
                return;
            }

            float t = _hitAge / _hitLife;
            PlaceTicks(t);
            float alpha = 1f - t * t;
            SetAlpha(_tickImages, alpha);
            SetAlpha(_tickOutlineImages, alpha);
        }

        /// <summary>Ticks sit diagonally around the centre and drift outward as they fade; a kill's are longer.</summary>
        void PlaceTicks(float t)
        {
            float length = _hitKind == HitKind.Kill ? 14f : _hitKind == HitKind.Head ? 11f : 8f;
            float drift = UiMotion.Reduced ? 0f : _hitSpread * UiMotion.Bezier(_tokens.EaseOut, t);
            for (int i = 0; i < TickDirections.Length && i < _ticks.Length; i++)
            {
                Vector2 direction = TickDirections[i].normalized;
                Vector2 centre = direction * (_hitGap + drift + length * 0.5f);
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                Place(_ticks[i], centre, new Vector2(length, 2f), angle);
                Place(_tickOutlines[i], centre, new Vector2(length + 2f, 4f), angle);
            }
        }

        void ShowHitMarker(bool show)
        {
            SetEnabled(_tickImages, show);
            SetEnabled(_tickOutlineImages, show);
            if (show)
            {
                SetAlpha(_tickImages, 1f);
                SetAlpha(_tickOutlineImages, 1f);
            }
        }

        // ---- Weapon: ammo, slots, reload, low and empty --------------------------------------

        void OnWeaponChanged(WeaponDefinition weapon, int slot)
        {
            if (_holder == null)
                return;

            _weaponName.text = weapon != null ? weapon.DisplayName.ToUpperInvariant() : "NO WEAPON";
            for (int i = 0; i < WeaponHolder.SlotCount && i < _slotTexts.Length; i++)
            {
                WeaponDefinition inSlot = _holder.WeaponInSlot(i);
                bool active = i == _holder.ActiveSlot && inSlot != null;
                _slotTexts[i].text = inSlot != null ? $"{i + 1}  {inSlot.DisplayName.ToUpperInvariant()}" : $"{i + 1}  —";
                Color selectedInk = _statePlate != null ? _tokens.Paper : _tokens.Navy;
                _slotTexts[i].color = active ? selectedInk : inSlot != null ? _tokens.Ink2 : _tokens.Ink3;
                if (_statePlate != null)
                {
                    // The equipped weapon already has its name on the ammo strip.
                    _slotTexts[i].transform.parent.gameObject.SetActive(inSlot != null && !active);
                    _slotBars[i].color = _tokens.Paper;
                    _slotBars[i].enabled = true;
                    _slotTexts[i].color = _tokens.Navy;
                }
                else
                    _slotBars[i].enabled = active;
            }

            _shownAmmo = -1;
            OnAmmoChanged(_holder.AmmoInMagazine, weapon != null ? weapon.MagazineSize : 0);
        }

        void OnAmmoChanged(int inMagazine, int magazineSize)
        {
            if (inMagazine == _shownAmmo && magazineSize == _shownMagazine)
                return;

            // A round leaving the gun punches the count; a refill doesn't.
            bool roundLeft = inMagazine < _shownAmmo && magazineSize == _shownMagazine;
            if (roundLeft && !UiMotion.Reduced)
                Kick(ref _punch, 1.12f);

            _shownAmmo = inMagazine;
            _shownMagazine = magazineSize;
            _ammoText.SetText("{0}", inMagazine);
            _magazineText.SetText("/ {0}", magazineSize);
            _empty = magazineSize > 0 && inMagazine == 0;
            if (magazineSize == 0 && _statePlate != null)
            {
                _ammoText.text = "—";
                _magazineText.text = string.Empty;
            }
            _ammoText.color = _statePlate != null ? _tokens.Navy : _empty ? _tokens.Amber : LowAmmo ? _tokens.Electric : _tokens.Navy;
            ShowState();
        }

        bool LowAmmo => _shownMagazine > 0 && _shownAmmo <= _shownMagazine * _lowAmmo;

        void OnDryFired()
        {
            if (UiMotion.Reduced)
                return;

            _shake.Velocity += _tokens.ErrorShakeDistance * 40f;
            Wake();
        }

        void OnReloadChanged(float seconds) => ShowState();

        void OnReloadEnded() => ShowState();

        /// <summary>One line under the ammo: what the gun is doing, or what to do about it.</summary>
        void ShowState()
        {
            if (_holder == null)
                return;

            switch (_holder.State)
            {
                case WeaponState.Reloading: _weaponState.text = "RELOADING"; break;
                case WeaponState.Equipping: _weaponState.text = "DRAWING"; break;
                default: _weaponState.text = _empty ? EmptyText() : _holder.ActiveWeapon == null ? string.Empty : LowAmmo ? "LOW AMMO" : "READY"; break;
            }

            if (_statePlate == null)
            {
                _weaponState.color = _empty && _holder.State == WeaponState.Ready ? _tokens.Amber : _tokens.Ink3;
                return;
            }

            bool busy = _holder.State != WeaponState.Ready;
            bool show = _holder.ActiveWeapon != null && (busy || LowAmmo || _empty);
            _statePlate.enabled = show;
            _weaponState.enabled = show;
            _statePlate.color = busy ? _tokens.Cobalt : LowAmmo ? _tokens.Amber : _tokens.HoverTint;
            _weaponState.color = busy ? _tokens.Paper : _tokens.Navy;
        }

        WeaponState _lastState;
        float _shownProgress = -1f;

        static string EmptyText()
        {
            var device = UiGlyphs.Current;
            string binding = device == UiDeviceKind.KeyboardMouse ? WeaponHolder.ReloadKeyboardBinding : WeaponHolder.ReloadPadBinding;
            return "RELOAD · " + UiGlyphs.Name(binding, device).ToUpperInvariant();
        }

        void ShowProgress()
        {
            WeaponState state = _holder.State;
            if (state != _lastState)
            {
                _lastState = state;
                ShowState();
            }

            float progress = state == WeaponState.Ready ? 0f : _holder.StateProgress;
            if (!Mathf.Approximately(progress, _shownProgress))
            {
                _shownProgress = progress;
                _progressFill.localScale = new Vector3(progress, 1f, 1f);
            }
        }

        // ---- Health, shown while hurt --------------------------------------------------------

        void OnHealthChanged(float current, float max)
        {
            if (Mathf.Approximately(current, _shownHealth))
                return;

            _shownHealth = current;
            float share = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            _healthFill.localScale = new Vector3(share, 1f, 1f);
            _healthFillImage.color = share <= _lowHealth ? _tokens.Amber : _tokens.Cobalt;
            _healthText.SetText("{0}", Mathf.CeilToInt(current));
            if (current < max)
                _lastHurtTime = Time.unscaledTime;
        }

        void ShowHealthFade(float dt)
        {
            // Never while knocked out: the card says it all then.
            bool hurt = _health.Current < _health.Max;
            bool show = !_health.IsDead && (hurt || Time.unscaledTime - _lastHurtTime < _healthLinger);
            float target = show ? 1f : 0f;
            if (!Mathf.Approximately(_healthFade.To, target))
                _healthFade.Start(_healthGroup.alpha, target, show ? _tokens.PanelIn : _tokens.PanelOut, show ? _tokens.EaseOut : _tokens.EaseIn);

            float alpha = _healthFade.Step(dt);
            if (!Mathf.Approximately(alpha, _healthGroup.alpha))
                _healthGroup.alpha = alpha;
        }

        // ---- Where damage came from ----------------------------------------------------------

        void OnDamaged(Health health, DamageInfo info)
        {
            _lastHurtTime = Time.unscaledTime;
            if (info.AttackerClientId < 0 || info.Direction.sqrMagnitude < 0.0001f)
                return;

            // The attacker's side: back along the shot from where it landed.
            int slot = _nextDamage;
            _nextDamage = (_nextDamage + 1) % _damageMarks.Length;
            _damageSources[slot] = info.Point - info.Direction.normalized * 20f;
            _damageAges[slot] = 0f;
            _damageMarks[slot].enabled = true;
        }

        /// <summary>Each marker keeps pointing at where the hit came from as the player turns, and fades.</summary>
        void AgeDamageMarks(float dt)
        {
            Transform view = PlayerCamera.Instance != null ? PlayerCamera.Instance.transform : null;
            for (int i = 0; i < _damageMarks.Length; i++)
            {
                if (_damageAges[i] >= _damageSeconds)
                    continue;

                _damageAges[i] += dt;
                Image mark = _damageMarks[i];
                if (_damageAges[i] >= _damageSeconds || view == null)
                {
                    mark.enabled = false;
                    continue;
                }

                Vector3 toSource = Vector3.ProjectOnPlane(_damageSources[i] - view.position, Vector3.up);
                Vector3 forward = Vector3.ProjectOnPlane(view.forward, Vector3.up);
                float angle = Vector3.SignedAngle(forward, toSource, Vector3.up);
                float radians = (90f - angle) * Mathf.Deg2Rad;
                var rect = mark.rectTransform;
                rect.anchoredPosition = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)) * _damageRadius;
                rect.localRotation = Quaternion.Euler(0f, 0f, -angle);

                Color colour = _tokens.Amber;
                colour.a = 1f - _damageAges[i] / _damageSeconds;
                mark.color = colour;
            }
        }

        // ---- Knocked out ---------------------------------------------------------------------

        void ShowDown(float dt)
        {
            bool down = _health.IsDead;
            float target = down ? 1f : 0f;
            if (!Mathf.Approximately(_downFade.To, target))
                _downFade.Start(_downGroup.alpha, target, down ? _tokens.PanelIn : _tokens.PanelOut, down ? _tokens.EaseOut : _tokens.EaseIn);

            float downAlpha = _downFade.Step(dt);
            if (!Mathf.Approximately(downAlpha, _downGroup.alpha))
                _downGroup.alpha = downAlpha;
            if (!down)
                return;

            int seconds = Mathf.CeilToInt(_health.SecondsUntilRespawn);
            if (seconds == _shownCount)
                return;

            _shownCount = seconds;
            _downCount.SetText("{0}", seconds);
            if (seconds > 0)
                UiSound.Play(UiSoundEvent.CountdownTick);
        }

        void OnRespawned(Health health)
        {
            _shownCount = -1;
            if (_downTitle != null)
                _downTitle.text = DefaultDownTitle;
            if (_downLine != null)
                _downLine.text = DefaultDownLine;
            UiSound.Play(UiSoundEvent.CountdownGo);
            for (int i = 0; i < _damageAges.Length; i++)
                _damageAges[i] = float.MaxValue;
            foreach (Image mark in _damageMarks)
                mark.enabled = false;
        }

        // ---- Motion --------------------------------------------------------------------------

        void Kick(ref UiSpring spring, float value)
        {
            spring.Value = value;
            Wake();
        }

        void Wake() => _animating = true;

        void Animate(float dt)
        {
            if (!_animating)
                return;

            bool shaking = _shake.Step(dt, _tokens.ErrorShake);
            bool punching = _punch.Step(dt, _tokens.PressRelease);
            _weaponPlate.anchoredPosition = new Vector2(_weaponPlateRest.x + _shake.Value, _weaponPlateRest.y);
            _ammoText.rectTransform.localScale = Vector3.one * _punch.Value;
            _animating = shaking || punching;
        }

        Vector2 _weaponPlateRest;

        void Start()
        {
            _weaponPlateRest = _weaponPlate.anchoredPosition;
        }

        // ---- Helpers -------------------------------------------------------------------------

        static Image[] ImagesOf(RectTransform[] rects)
        {
            var images = new Image[rects.Length];
            for (int i = 0; i < rects.Length; i++)
                images[i] = rects[i] != null ? rects[i].GetComponent<Image>() : null;
            return images;
        }

        static void Place(RectTransform rect, Vector2 centre, Vector2 size, float angle)
        {
            if (rect == null)
                return;

            rect.anchoredPosition = centre;
            rect.sizeDelta = size;
            rect.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        static void SetEnabled(Image[] images, bool on)
        {
            foreach (Image image in images)
            {
                if (image != null)
                    image.enabled = on;
            }
        }

        static void SetAlpha(Image[] images, float alpha)
        {
            foreach (Image image in images)
            {
                if (image == null)
                    continue;
                Color c = image.color;
                c.a = alpha;
                image.color = c;
            }
        }
    }
}

// -----------------------------------------------------------------------------
//  AI-GENERATED SOURCE FILE
//  Generator : Claude Fable 5.2 (Claude Code)
//  Generated : 2026-11-15
//  Prompt    : Harvey: "Add VOIP/Proximity chat to the game."
//  Modified  : Claude Fable 5.2, 2026-11-15 — no longer uses Steam's voice. The microphone is
//              read through Unity, compressed by our own VoiceCodec and sent in our own
//              packets, so voice works with or without Steam (Harvey: "Make our own custom
//              VOIP for this").
//  Reviewed  : NOT YET REVIEWED BY A HUMAN
//  Summary   : Proximity voice chat. Records this player's microphone, compresses it, sends it
//              to the host, and the host passes it on only to players within earshot of the
//              speaker. Each machine plays what it receives from the speaker's head.
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using CloseTheDeal.Player;
using CloseTheDeal.UI;
using FishNet;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Serializing;
using FishNet.Transporting;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;

namespace CloseTheDeal.Voice
{
    /// <summary>One stretch of a player's compressed voice, on its way to the host.</summary>
    internal struct VoiceFrame : IBroadcast
    {
        public ArraySegment<byte> Payload;
    }

    /// <summary>One stretch of a player's compressed voice, passed on by the host to a player in earshot.</summary>
    internal struct VoiceRelay : IBroadcast
    {
        /// <summary>The speaker's FishNet client id.</summary>
        public int Speaker;
        public ArraySegment<byte> Payload;
    }

    /// <summary>Copies voice bytes into FishNet's writer and reads them for the receiving callback.</summary>
    internal static class VoiceSerializers
    {
        public static void WriteVoiceFrame(this Writer writer, VoiceFrame value)
        {
            writer.WriteArraySegmentAndSize(value.Payload);
        }

        public static VoiceFrame ReadVoiceFrame(this Reader reader)
        {
            return new VoiceFrame { Payload = reader.ReadArraySegmentAndSize() };
        }

        public static void WriteVoiceRelay(this Writer writer, VoiceRelay value)
        {
            writer.WriteInt32(value.Speaker);
            writer.WriteArraySegmentAndSize(value.Payload);
        }

        public static VoiceRelay ReadVoiceRelay(this Reader reader)
        {
            return new VoiceRelay { Speaker = reader.ReadInt32(), Payload = reader.ReadArraySegmentAndSize() };
        }
    }

    /// <summary>
    /// Proximity voice chat for every player in the game. All of it is ours: no voice
    /// service, no Steam voice, no added package.
    /// </summary>
    /// <remarks>
    /// <para>The path of a word: this machine reads the microphone through Unity, cuts it
    /// into 40 ms pieces, drops the silent ones, compresses the rest with
    /// <see cref="VoiceCodec"/> and sends each to the host. The host passes a piece to each
    /// other player whose body is within <see cref="RelayRange"/> of the speaker's. Each of
    /// those machines decompresses it and plays it from the speaker's head with a fade over
    /// distance.</para>
    /// <para>A packet is a 2-byte running number, then one <see cref="VoiceCodec"/> packet.
    /// The number lets a listener throw away a piece that arrives late or twice.</para>
    /// <para>WHY the host decides who hears: the game is host-authoritative, and a client
    /// that was sent every voice could listen to the whole map whatever its own volume
    /// curve said. The host also caps how much each player may send.</para>
    /// <para>WHY it makes itself at start-up and is not placed in a scene: it has no authored
    /// values and must exist in every scene with a network manager, so there is nothing for
    /// a scene or prefab to hold, and no scene has to be edited to get voice.</para>
    /// <para>Nothing here is predicted or reconciled, and no gameplay reads it.</para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class VoiceChat : MonoBehaviour
    {
        /// <summary>Samples a second voice travels and plays at. 16 kHz carries speech clearly (wideband telephone quality) at a third of the size of 48 kHz.</summary>
        const int SampleRate = 16000;

        /// <summary>Samples in one piece of voice: 40 ms. Shorter pieces mean more packets for the same sound; longer ones add delay and lose more when one goes missing.</summary>
        const int FrameSamples = SampleRate / 25;

        /// <summary>Bytes before the compressed sound in a packet: the running number.</summary>
        const int NumberBytes = 2;

        /// <summary>Seconds of microphone sound Unity keeps in its looping recording.</summary>
        const int MicSeconds = 1;

        /// <summary>
        /// Loudness (0 to 1, root mean square) above which a piece counts as speech. With an
        /// open microphone, quieter pieces are not sent, so room noise costs no bandwidth and
        /// is not played to everyone nearby.
        /// </summary>
        const float GateLevel = 0.012f;

        /// <summary>Seconds the gate stays open after the last loud piece, so the quiet ends of words are not clipped.</summary>
        const float GateHold = 0.35f;

        /// <summary>Metres within which a voice is at full volume: across a desk.</summary>
        const float FullRange = 3f;

        /// <summary>Metres at which a voice has faded to nothing: about the length of a floor.</summary>
        const float HearRange = 24f;

        /// <summary>
        /// Metres within which the host passes a voice on. A little past <see cref="HearRange"/>,
        /// so a listener walking into earshot already has sound arriving when the fade begins.
        /// </summary>
        const float RelayRange = HearRange + 4f;

        /// <summary>Largest voice packet the host accepts, in bytes. A real one is 325; anything much bigger is not ours.</summary>
        const int MaxPacketBytes = 600;

        /// <summary>
        /// Voice bytes a second the host accepts from one player. Talking non-stop is about
        /// 8,100; this leaves room for a burst and stops a client flooding the host.
        /// </summary>
        const float BytesPerSecondCap = 14000f;

        /// <summary>Seconds of silence from a speaker after which their running number is forgotten, so a rejoining player is not ignored.</summary>
        const float NumberForget = 1f;

        /// <summary>The id the player's own voice is filed under when they listen to themselves.</summary>
        const int SelfId = -1;

        /// <summary>Seconds between looks for a speaker's body when it is not known.</summary>
        const float BodySearchInterval = 0.5f;

        /// <summary>Name of the voice key's action in the Player map.</summary>
        const string VoiceAction = "Voice";

        /// <summary>Name of the mixer group voices play through, so the settings screen's Voice slider sets their volume.</summary>
        const string MixerGroupName = "Voice";

        /// <summary>The one voice chat, or null before start-up.</summary>
        public static VoiceChat Instance { get; private set; }

        sealed class Heard
        {
            public VoiceSpeaker Speaker;
            public PlayerMotor Body;
            public float NextSearch;
            public ushort LastNumber;
            public bool HasNumber;
        }

        readonly Dictionary<int, Heard> _heard = new();
        readonly Dictionary<int, float> _allowance = new();
        readonly byte[] _packet = new byte[NumberBytes + VoiceCodec.PacketBytes(FrameSamples)];
        readonly float[] _frame = new float[FrameSamples];
        readonly float[] _decoded = new float[FrameSamples * 2];
        readonly List<int> _scratchIds = new();

        NetworkManager _net;
        AudioClip _mic;
        float[] _micFrame;
        int _micRead;
        bool _warnedMic;
        VoiceCodec.State _encoder;
        ushort _number;
        float _gateOpenUntil = float.NegativeInfinity;
        PlayerMotor _keyOwner;
        InputAction _key;
        AudioMixerGroup _output;
        bool _outputLooked;
        float _lastSent = float.NegativeInfinity;

        /// <summary>True when this machine has a microphone to talk with. Hearing others needs none.</summary>
        public bool Available => Microphone.devices.Length > 0;

        /// <summary>True while this player's microphone is open (not muted, or the key is held).</summary>
        public bool MicOpen { get; private set; }

        /// <summary>True while this player's own voice is actually going out (they are making sound with the mic open).</summary>
        public bool Transmitting => Time.unscaledTime - _lastSent < 0.3f;

        /// <summary>True while the player with this FishNet client id is heard talking on this machine.</summary>
        public bool IsSpeaking(int clientId)
        {
            return _heard.TryGetValue(clientId, out Heard heard) && heard.Speaker != null && heard.Speaker.Speaking;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Create()
        {
            if (Instance != null)
                return;
            var host = new GameObject("VoiceChat");
            DontDestroyOnLoad(host);
            Instance = host.AddComponent<VoiceChat>();
        }

        void OnDestroy()
        {
            Unhook();
            StopMic();
            if (Instance == this)
                Instance = null;
        }

        void Update()
        {
            // A destroyed network manager (a scene change) compares equal to null.
            if (_net == null)
            {
                Unhook();
                Hook();
            }

            bool connected = _net != null && _net.ClientManager.Started;
            if (!connected && _heard.Count > 0)
                ClearSpeakers();

            ReadKey();
            RunMic(connected);
            if (_net != null && _net.ServerManager.Started)
                RefillAllowances();
        }

        void LateUpdate()
        {
            PlaceSpeakers();
        }

        // ---- Network wiring ------------------------------------------------------------------

        void Hook()
        {
            _net = InstanceFinder.NetworkManager;
            if (_net == null)
                return;
            _net.ClientManager.RegisterBroadcast<VoiceRelay>(OnRelay);
            _net.ServerManager.RegisterBroadcast<VoiceFrame>(OnFrame);
            _net.ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
        }

        void Unhook()
        {
            // Unity's null check, on purpose: a manager destroyed with its scene needs no unhooking.
            if (_net != null)
            {
                _net.ClientManager.UnregisterBroadcast<VoiceRelay>(OnRelay);
                _net.ServerManager.UnregisterBroadcast<VoiceFrame>(OnFrame);
                _net.ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
            }

            _net = null;
            _allowance.Clear();
            ClearSpeakers();
        }

        void OnRemoteConnectionState(NetworkConnection connection, RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState == RemoteConnectionState.Stopped)
                _allowance.Remove(connection.ClientId);
        }

        // ---- This player's microphone --------------------------------------------------------

        bool WantsMic()
        {
            switch (VoiceSettings.Mode)
            {
                case MicMode.Open:
                    return !VoiceSettings.Muted;
                case MicMode.PushToTalk:
                    return _key != null && _key.IsPressed();
                default:
                    return false;
            }
        }

        /// <summary>
        /// Finds the voice key on the local player's own copy of the input actions (so a
        /// rebinding is followed) and, with an open microphone, lets a press mute and unmute.
        /// </summary>
        void ReadKey()
        {
            PlayerMotor local = PlayerMotor.Local;
            if (local != _keyOwner)
            {
                _keyOwner = local;
                _key = null;
                if (local != null && local.TryGetComponent(out PlayerInputReader reader) && reader.Actions != null)
                    _key = reader.Actions.FindAction("Player/" + VoiceAction);
            }

            if (_key != null && VoiceSettings.Mode == MicMode.Open && _key.WasPressedThisFrame())
                VoiceSettings.SetMuted(!VoiceSettings.Muted);
        }

        /// <summary>
        /// Keeps the microphone recording while connected (unless voice is off), and sends
        /// each finished piece that should go out.
        /// </summary>
        /// <remarks>
        /// WHY it records the whole time and not only while the mic is open: starting a
        /// recording takes a noticeable moment, which would swallow the first word after a
        /// push-to-talk press. What is recorded and not sent is thrown away here.
        /// </remarks>
        void RunMic(bool connected)
        {
            bool record = connected && VoiceSettings.Mode != MicMode.Off && Available;
            if (!record)
            {
                StopMic();
                MicOpen = false;
                return;
            }

            if (_mic == null && !StartMic())
            {
                MicOpen = false;
                return;
            }

            MicOpen = WantsMic();
            int length = _mic.samples;
            int waiting = (Microphone.GetPosition(null) - _micRead + length) % length;
            // After a stall (a long frame, a paused editor) only the newest sound is worth
            // sending: skip to it so the voice does not run late.
            if (waiting > length / 2)
            {
                _micRead = (_micRead + waiting - _micFrame.Length + length) % length;
                waiting = _micFrame.Length;
            }

            while (waiting >= _micFrame.Length)
            {
                _mic.GetData(_micFrame, _micRead);
                _micRead = (_micRead + _micFrame.Length) % length;
                waiting -= _micFrame.Length;
                if (MicOpen)
                    SendPiece();
            }
        }

        bool StartMic()
        {
            _mic = Microphone.Start(null, true, MicSeconds, SampleRate);
            if (_mic == null)
            {
                if (!_warnedMic)
                {
                    _warnedMic = true;
                    Debug.LogWarning("[Voice] The microphone could not be started. This player will hear others but not be heard.");
                }

                return false;
            }

            // A device that cannot record at our rate gives its own; the piece is then
            // stretched or squeezed to ours as it is read.
            _micFrame = new float[Mathf.Max(1, Mathf.RoundToInt(FrameSamples * (_mic.frequency / (float)SampleRate)))];
            _micRead = Microphone.GetPosition(null);
            _encoder = default;
            return true;
        }

        void StopMic()
        {
            if (_mic == null)
                return;
            Microphone.End(null);
            Destroy(_mic);
            _mic = null;
        }

        /// <summary>Compresses the piece just read from the microphone and sends it, unless it is silence.</summary>
        void SendPiece()
        {
            // Bring the piece to our rate. With a microphone already at our rate this is a copy.
            int source = _micFrame.Length;
            double squares = 0.0;
            for (int i = 0; i < FrameSamples; i++)
            {
                float at = i * (source - 1) / (float)(FrameSamples - 1);
                int low = (int)at;
                int high = Mathf.Min(low + 1, source - 1);
                float value = Mathf.Lerp(_micFrame[low], _micFrame[high], at - low);
                _frame[i] = value;
                squares += value * value;
            }

            // Push-to-talk sends everything while the key is held; an open mic sends speech only.
            if (VoiceSettings.Mode == MicMode.Open)
            {
                if (Math.Sqrt(squares / FrameSamples) >= GateLevel)
                    _gateOpenUntil = Time.unscaledTime + GateHold;
                if (Time.unscaledTime > _gateOpenUntil)
                    return;
            }

            _number++;
            _packet[0] = (byte)_number;
            _packet[1] = (byte)(_number >> 8);
            int bytes = NumberBytes + VoiceCodec.Encode(_frame, FrameSamples, _packet, NumberBytes, ref _encoder);
            var payload = new ArraySegment<byte>(_packet, 0, bytes);

            _lastSent = Time.unscaledTime;
            // Unreliable: a lost piece is 40 ms of sound, and resending it would only make it late.
            _net.ClientManager.Broadcast(new VoiceFrame { Payload = payload }, Channel.Unreliable);
            if (VoiceSettings.HearSelf)
                Play(SelfId, payload);
        }

        // ---- The host passing voices on ------------------------------------------------------

        void RefillAllowances()
        {
            float add = BytesPerSecondCap * Time.unscaledDeltaTime;
            _scratchIds.Clear();
            foreach (int id in _allowance.Keys)
                _scratchIds.Add(id);
            for (int i = 0; i < _scratchIds.Count; i++)
                _allowance[_scratchIds[i]] = Mathf.Min(BytesPerSecondCap, _allowance[_scratchIds[i]] + add);
        }

        void OnFrame(NetworkConnection sender, VoiceFrame frame, Channel channel)
        {
            ArraySegment<byte> payload = frame.Payload;
            if (!LooksLikeVoice(payload))
                return;

            if (!_allowance.TryGetValue(sender.ClientId, out float allowance))
                allowance = BytesPerSecondCap;
            if (allowance < payload.Count)
                return;
            _allowance[sender.ClientId] = allowance - payload.Count;

            // A player with no body has nowhere to be heard from.
            NetworkObject speakerBody = sender.FirstObject;
            if (speakerBody == null)
                return;
            Vector3 from = speakerBody.transform.position;

            var relay = new VoiceRelay { Speaker = sender.ClientId, Payload = payload };
            foreach (NetworkConnection listener in _net.ServerManager.Clients.Values)
            {
                if (listener == sender || !listener.IsAuthenticated)
                    continue;
                NetworkObject listenerBody = listener.FirstObject;
                if (listenerBody == null)
                    continue;
                if ((listenerBody.transform.position - from).sqrMagnitude > RelayRange * RelayRange)
                    continue;
                _net.ServerManager.Broadcast(listener, relay, true, Channel.Unreliable);
            }
        }

        static bool LooksLikeVoice(ArraySegment<byte> payload)
        {
            return payload.Array != null
                && payload.Count > NumberBytes + VoiceCodec.HeaderBytes
                && payload.Count <= MaxPacketBytes;
        }

        // ---- Hearing other players -----------------------------------------------------------

        void OnRelay(VoiceRelay relay, Channel channel)
        {
            if (LooksLikeVoice(relay.Payload))
                Play(relay.Speaker, relay.Payload);
        }

        void Play(int speakerId, ArraySegment<byte> payload)
        {
            Heard heard = SpeakerFor(speakerId);
            byte[] bytes = payload.Array;
            int offset = payload.Offset;

            // Drop a piece that arrives after a newer one, or twice. The subtraction wraps
            // with the 16-bit number, so "newer" still works when it rolls over.
            ushort number = (ushort)(bytes[offset] | bytes[offset + 1] << 8);
            bool stale = Time.unscaledTime - heard.Speaker.LastHeard > NumberForget;
            if (heard.HasNumber && !stale && (short)(number - heard.LastNumber) <= 0)
                return;
            heard.LastNumber = number;
            heard.HasNumber = true;

            int samples = VoiceCodec.Decode(bytes, offset + NumberBytes, payload.Count - NumberBytes, _decoded);
            if (samples > 0)
                heard.Speaker.Push(_decoded, samples);
        }

        Heard SpeakerFor(int speakerId)
        {
            if (_heard.TryGetValue(speakerId, out Heard heard) && heard.Speaker != null)
                return heard;

            var go = new GameObject(speakerId == SelfId ? "Voice (own, mic test)" : "Voice (client " + speakerId + ")");
            go.transform.SetParent(transform, false);
            heard = new Heard { Speaker = go.AddComponent<VoiceSpeaker>() };
            heard.Speaker.Init(SampleRate, Output(), FullRange, HearRange);
            heard.Speaker.SetSpatial(speakerId != SelfId);
            _heard[speakerId] = heard;
            return heard;
        }

        /// <summary>The mixer's Voice group, found once through the UI sound set that already holds the game's mixer.</summary>
        AudioMixerGroup Output()
        {
            if (_outputLooked)
                return _output;
            UiSoundSet set = UiSoundPlayer.Set;
            if (set == null || set.output == null || set.output.audioMixer == null)
                return null; // Not loaded yet: look again for the next speaker.

            _outputLooked = true;
            AudioMixerGroup[] groups = set.output.audioMixer.FindMatchingGroups(MixerGroupName);
            _output = groups.Length > 0 ? groups[0] : null;
            if (_output == null)
                Debug.LogWarning($"[Voice] The game mixer has no '{MixerGroupName}' group, so the Voice volume slider will not change voice chat.");
            return _output;
        }

        /// <summary>Moves each voice to its speaker's head.</summary>
        void PlaceSpeakers()
        {
            foreach (KeyValuePair<int, Heard> pair in _heard)
            {
                Heard heard = pair.Value;
                if (pair.Key == SelfId || heard.Speaker == null)
                    continue;

                if (heard.Body == null && Time.unscaledTime >= heard.NextSearch)
                {
                    heard.NextSearch = Time.unscaledTime + BodySearchInterval;
                    heard.Body = FindBody(pair.Key);
                }

                // With no body found the voice stays where it last was, which is where the
                // host measured it from a moment ago.
                if (heard.Body != null)
                    heard.Speaker.transform.position = heard.Body.transform.position + Vector3.up * heard.Body.EyeHeight;
            }
        }

        static PlayerMotor FindBody(int clientId)
        {
            PlayerMotor[] bodies = FindObjectsByType<PlayerMotor>();
            for (int i = 0; i < bodies.Length; i++)
            {
                if (bodies[i].OwnerId == clientId)
                    return bodies[i];
            }

            return null;
        }

        void ClearSpeakers()
        {
            foreach (Heard heard in _heard.Values)
            {
                if (heard.Speaker != null)
                    Destroy(heard.Speaker.gameObject);
            }

            _heard.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
        }
    }
}

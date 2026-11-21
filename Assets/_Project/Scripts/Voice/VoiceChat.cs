// -----------------------------------------------------------------------------
//  AI-GENERATED SOURCE FILE
//  Generator : Claude Fable 5.2 (Claude Code)
//  Generated : 2026-11-15
//  Prompt    : Harvey: "Add VOIP/Proximity chat to the game."
//  Modified  : Claude Fable 5.2, 2026-11-15 — no longer uses Steam's voice. The microphone is
//              read through Unity, compressed by our own VoiceCodec and sent in our own
//              packets, so voice works with or without Steam (Harvey: "Make our own custom
//              VOIP for this").
//  Modified  : Claude Fable 5.2, 2026-11-15 — Opus compression (Concentus), RNNoise noise
//              suppression, a chosen microphone, 20 ms pieces at 48 kHz, lost pieces rebuilt,
//              and a mic test that works with no game running (Harvey: "The quality isnt
//              great / We probably need noise suppression / There's a delay", then "do the
//              delay and mic picker", "go grab that", "Yes on the RNNoise").
//  Reviewed  : NOT YET REVIEWED BY A HUMAN
//  Summary   : Proximity voice chat. Records this player's microphone, cleans and compresses it, sends it
//              to the host, and the host passes it on only to players within earshot of the
//              speaker. Each machine plays what it receives from the speaker's head.
// -----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using CloseTheDeal.Combat;
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
    /// Proximity voice chat for every player in the game. The path is ours (capture, packets,
    /// who hears whom, playback); the compression is Opus and the noise removal is RNNoise.
    /// No voice service and no Steam voice.
    /// </summary>
    /// <remarks>
    /// <para>The path of a word: this machine reads the chosen microphone through Unity, cuts
    /// it into 20 ms pieces, cleans each with <see cref="VoiceCleaner"/>, drops the ones with
    /// no speech, compresses the rest with <see cref="VoiceEncoder"/> and sends each to the
    /// host. The host passes a piece to each other player whose body is within
    /// <see cref="RelayRange"/> of the speaker's. Each of those machines decompresses it and
    /// plays it from the speaker's head with a fade over distance.</para>
    /// <para>A packet is a 2-byte running number, then one Opus packet. The number lets a
    /// listener throw away a piece that arrives late or twice, and see that one was lost so
    /// it can be rebuilt.</para>
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
        /// <summary>
        /// Samples a second voice is recorded, sent and played at. 48 kHz is what RNNoise
        /// needs and what sound cards run at, so nothing is resampled on the way.
        /// </summary>
        const int SampleRate = 48000;

        /// <summary>
        /// Samples in one piece of voice: 20 ms, the piece Opus is built around, and two
        /// RNNoise pieces. Shorter adds packets; longer adds delay.
        /// </summary>
        const int FrameSamples = SampleRate / 50;

        /// <summary>
        /// Bits a second of compressed voice. 32,000 is clear full-band speech; with the
        /// packet's 2-byte number that is about 4.1 kB a second while someone talks.
        /// </summary>
        const int Bitrate = 32000;

        /// <summary>Bytes before the compressed sound in a packet: the running number.</summary>
        const int NumberBytes = 2;

        /// <summary>Seconds of microphone sound Unity keeps in its looping recording.</summary>
        const int MicSeconds = 1;

        /// <summary>
        /// How sure RNNoise must be that a piece is speech (0 to 1) for an open microphone to
        /// send it. Used when noise suppression is on.
        /// </summary>
        const float SpeechChance = 0.5f;

        /// <summary>
        /// Loudness (0 to 1, root mean square) above which a piece counts as speech when noise
        /// suppression is off or missing. With an open microphone, quieter pieces are not
        /// sent, so room noise costs no bandwidth and is not played to everyone nearby.
        /// </summary>
        const float GateLevel = 0.012f;

        /// <summary>Loudness below which a piece is silence whatever RNNoise says about it.</summary>
        const float SilenceLevel = 0.001f;

        /// <summary>Seconds the gate stays open after the last speech, so the quiet ends of words are not clipped.</summary>
        const float GateHold = 0.35f;

        /// <summary>Seconds of not sending after which the encoder starts fresh, so the last sentence does not colour the next.</summary>
        const float EncoderForget = 0.5f;

        /// <summary>Most lost pieces in a row a listener fills in. A longer gap is left as a gap: guessing that much speech sounds worse than silence.</summary>
        const int MaxFill = 3;

        /// <summary>Seconds before retrying Microphone.Start after a failure.</summary>
        const float MicRetryDelay = 2f;

        /// <summary>Metres within which a voice is at full volume: across a desk.</summary>
        const float FullRange = 3f;

        /// <summary>Metres at which a voice has faded to nothing: about the length of a floor.</summary>
        const float HearRange = 24f;

        /// <summary>
        /// Metres within which the host passes a voice on. A little past <see cref="HearRange"/>,
        /// so a listener walking into earshot already has sound arriving when the fade begins.
        /// </summary>
        const float RelayRange = HearRange + 4f;

        /// <summary>Largest voice packet the host accepts, in bytes. A real one is about 82 and Opus may briefly go over; anything much bigger is not ours.</summary>
        const int MaxPacketBytes = 400;

        /// <summary>
        /// Voice bytes a second the host accepts from one player. Talking non-stop is about
        /// 4,100; this leaves room for a burst and stops a client flooding the host.
        /// </summary>
        const float BytesPerSecondCap = 8000f;

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
            public VoiceDecoder Decoder;
        }

        struct Allowance
        {
            public float Bytes;
            public float LastTime;
        }

        readonly Dictionary<int, Heard> _heard = new();
        readonly Dictionary<int, Allowance> _allowance = new();
        readonly HashSet<NetworkConnection> _relayTargets = new();
        readonly byte[] _packet = new byte[MaxPacketBytes];
        readonly float[] _frame = new float[FrameSamples];
        readonly float[] _decoded = new float[FrameSamples];

        NetworkManager _net;
        AudioClip _mic;
        /// <summary>The microphone being recorded, as Unity names it; null is the system default.</summary>
        string _micDevice;
        float[] _micFrame;
        int _micFrameSamples;
        int _micChannels = 1;
        float _prevMicSample;
        int _micRead;
        bool _warnedMic;
        float _nextMicRetry = float.NegativeInfinity;
        string[] _devices = Array.Empty<string>();
        bool _devicesKnown;
        VoiceEncoder _encoder;
        VoiceCleaner _cleaner;
        bool _cleanerTried;
        bool _wasConnected;
        ushort _number;
        float _gateOpenUntil = float.NegativeInfinity;
        PlayerMotor _keyOwner;
        InputAction _key;
        AudioMixerGroup _output;
        bool _outputLooked;
        float _lastSent = float.NegativeInfinity;
        float _lastEncoded = float.NegativeInfinity;

        /// <summary>True when this machine has a microphone to talk with. Hearing others needs none.</summary>
        public bool Available => Devices.Length > 0;

        /// <summary>The microphones plugged in, as Unity names them. Refreshed on startup, settings change, or mic failure.</summary>
        public string[] Devices
        {
            get
            {
                if (!_devicesKnown)
                    RefreshDevices();

                return _devices;
            }
        }

        /// <summary>Refreshes the cached list of plugged-in microphones.</summary>
        public string[] RefreshDevices()
        {
            _devices = Microphone.devices;
            _devicesKnown = true;
            return _devices;
        }

        /// <summary>True when the RNNoise library loaded, so noise suppression can run on this machine.</summary>
        public bool CanSuppressNoise
        {
            get
            {
                if (!_cleanerTried)
                {
                    _cleanerTried = true;
                    _cleaner = VoiceCleaner.Create();
                }

                return _cleaner != null;
            }
        }

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

        void Awake()
        {
            RefreshDevices();
            VoiceSettings.Changed += OnVoiceSettingsChanged;
        }

        void OnDestroy()
        {
            VoiceSettings.Changed -= OnVoiceSettingsChanged;
            Unhook();
            StopMic();
            _cleaner?.Dispose();
            _cleaner = null;
            if (Instance == this)
                Instance = null;
        }

        void OnVoiceSettingsChanged()
        {
            RefreshDevices();
            _nextMicRetry = float.NegativeInfinity;
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
            // Leaving a game ends every voice from it. Not every frame while disconnected:
            // the mic test plays through a speaker of its own with no game running.
            if (_wasConnected && !connected)
                ClearSpeakers();
            _wasConnected = connected;
            if (!VoiceSettings.HearSelf && _heard.TryGetValue(SelfId, out Heard own))
            {
                if (own.Speaker != null)
                    Destroy(own.Speaker.gameObject);
                _heard.Remove(SelfId);
            }

            ReadKey();
            RunMic(connected);
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
            _net.ClientManager.OnRemoteConnectionState += OnClientRemoteConnectionState;
            _net.ServerManager.RegisterBroadcast<VoiceFrame>(OnFrame);
            _net.ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
        }

        void Unhook()
        {
            // Unity's null check, on purpose: a manager destroyed with its scene needs no unhooking.
            if (_net != null)
            {
                _net.ClientManager.UnregisterBroadcast<VoiceRelay>(OnRelay);
                _net.ClientManager.OnRemoteConnectionState -= OnClientRemoteConnectionState;
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
            {
                _allowance.Remove(connection.ClientId);
                RemoveSpeaker(connection.ClientId);
            }
        }

        void OnClientRemoteConnectionState(RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState == RemoteConnectionState.Stopped)
                RemoveSpeaker(args.ConnectionId);
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
        /// Keeps the microphone recording while connected (unless voice is off) or while the
        /// mic test is on, and deals with each finished piece.
        /// </summary>
        /// <remarks>
        /// WHY it records the whole time and not only while the mic is open: starting a
        /// recording takes a noticeable moment, which would swallow the first word after a
        /// push-to-talk press. What is recorded and not sent is thrown away here.
        /// </remarks>
        void RunMic(bool connected)
        {
            bool test = VoiceSettings.HearSelf;
            bool record = ((connected && VoiceSettings.Mode != MicMode.Off) || test) && Available;
            if (!record)
            {
                StopMic();
                MicOpen = false;
                return;
            }

            // The player picked another microphone, or the one in use was unplugged.
            if (_mic != null && WantedDevice() != _micDevice)
                StopMic();
            if (_mic == null)
            {
                if (Time.unscaledTime < _nextMicRetry || !StartMic())
                {
                    MicOpen = false;
                    return;
                }
            }

            MicOpen = connected && WantsMic();
            int pos = Microphone.GetPosition(_micDevice);
            if (pos < 0)
            {
                StopMic();
                RefreshDevices();
                _nextMicRetry = Time.unscaledTime + MicRetryDelay;
                MicOpen = false;
                return;
            }

            bool skipProcessing = !test && (
                (VoiceSettings.Mode == MicMode.Open && VoiceSettings.Muted) ||
                (VoiceSettings.Mode == MicMode.PushToTalk && !MicOpen && Time.unscaledTime > _gateOpenUntil));
            if (skipProcessing)
            {
                _micRead = pos;
                _prevMicSample = 0f;
                return;
            }

            int length = _mic.samples;
            int waiting = (pos - _micRead + length) % length;
            // After a stall (a long frame, a paused editor) only the newest sound is worth
            // sending: skip to it so the voice does not run late.
            if (waiting > length / 2)
            {
                _micRead = (_micRead + waiting - _micFrameSamples + length) % length;
                waiting = _micFrameSamples;
                _prevMicSample = 0f;
            }

            while (waiting >= _micFrameSamples)
            {
                _mic.GetData(_micFrame, _micRead);
                _micRead = (_micRead + _micFrameSamples) % length;
                waiting -= _micFrameSamples;
                HandlePiece(MicOpen, test);
            }
        }

        /// <summary>The saved microphone if it is plugged in, else null for the system default.</summary>
        string WantedDevice()
        {
            string saved = VoiceSettings.Device;
            if (saved.Length == 0)
                return null;
            return Array.IndexOf(Devices, saved) >= 0 ? saved : null;
        }

        bool StartMic()
        {
            _micDevice = WantedDevice();
            _mic = Microphone.Start(_micDevice, true, MicSeconds, SampleRate);
            if (_mic == null)
            {
                if (!_warnedMic)
                {
                    _warnedMic = true;
                    Debug.LogWarning("[Voice] The microphone could not be started. This player will hear others but not be heard.");
                }

                RefreshDevices();
                _nextMicRetry = Time.unscaledTime + MicRetryDelay;
                return false;
            }

            // A device that cannot record at our rate gives its own; the piece is then
            // stretched or squeezed to ours as it is read. Multi-channel mics are downmixed to mono.
            _micChannels = Mathf.Max(1, _mic.channels);
            _micFrameSamples = Mathf.Max(1, Mathf.RoundToInt(FrameSamples * (_mic.frequency / (float)SampleRate)));
            _micFrame = new float[_micFrameSamples * _micChannels];
            int pos = Microphone.GetPosition(_micDevice);
            _micRead = pos >= 0 ? pos : 0;
            _prevMicSample = 0f;
            _encoder ??= new VoiceEncoder(SampleRate, Bitrate);
            _encoder.Reset();
            _ = CanSuppressNoise; // loads RNNoise the first time
            return true;
        }

        void StopMic()
        {
            if (_mic == null)
                return;
            Microphone.End(_micDevice);
            Destroy(_mic);
            _mic = null;
        }

        /// <summary>
        /// Cleans the piece just read from the microphone, decides whether it is speech, and
        /// if so compresses it and sends it, plays it back for the mic test, or both.
        /// </summary>
        /// <param name="send">True when the microphone is open and a game is running.</param>
        /// <param name="test">True when the player is listening to themselves.</param>
        void HandlePiece(bool send, bool test)
        {
            int source = _micFrameSamples;
            if (_micChannels > 1)
            {
                float invChannels = 1f / _micChannels;
                for (int i = 0; i < source; i++)
                {
                    float sum = 0f;
                    int baseIndex = i * _micChannels;
                    for (int c = 0; c < _micChannels; c++)
                        sum += _micFrame[baseIndex + c];
                    _micFrame[i] = sum * invChannels;
                }
            }

            // Bring the piece to our rate, carrying the previous frame's last sample across boundaries.
            if (source == FrameSamples)
            {
                Array.Copy(_micFrame, _frame, FrameSamples);
            }
            else
            {
                float step = source / (float)FrameSamples;
                for (int i = 0; i < FrameSamples; i++)
                {
                    float at = (i + 1) * step - 1f;
                    int low = Mathf.FloorToInt(at);
                    float frac = at - low;
                    float a = low < 0 ? _prevMicSample : _micFrame[low];
                    int high = low + 1;
                    float b = high < source ? _micFrame[high] : _micFrame[source - 1];
                    _frame[i] = a + (b - a) * frac;
                }
            }
            _prevMicSample = _micFrame[source - 1];

            // Cleaned even when nothing will be sent: RNNoise learns the room's noise as it
            // goes, and a cleaner that only woke on a key press would start each time deaf.
            float speechChance = -1f;
            if (_cleaner != null && VoiceSettings.NoiseSuppression)
            {
                speechChance = 0f;
                for (int at = 0; at + VoiceCleaner.FrameSamples <= FrameSamples; at += VoiceCleaner.FrameSamples)
                    speechChance = Mathf.Max(speechChance, _cleaner.Process(_frame, at));
            }

            if (!send && !test)
                return;

            // A held push-to-talk key sends everything. Otherwise only speech goes out.
            bool held = VoiceSettings.Mode == MicMode.PushToTalk && _key != null && _key.IsPressed();
            if (!held)
            {
                double squares = 0.0;
                for (int i = 0; i < FrameSamples; i++)
                    squares += _frame[i] * _frame[i];
                double level = Math.Sqrt(squares / FrameSamples);
                bool speech = speechChance >= 0f
                    ? speechChance >= SpeechChance && level >= SilenceLevel
                    : level >= GateLevel;
                if (speech)
                    _gateOpenUntil = Time.unscaledTime + GateHold;
                if (Time.unscaledTime > _gateOpenUntil)
                    return;
            }

            if (Time.unscaledTime - _lastEncoded > EncoderForget)
                _encoder.Reset();
            _lastEncoded = Time.unscaledTime;

            _number++;
            _packet[0] = (byte)_number;
            _packet[1] = (byte)(_number >> 8);
            int bytes = NumberBytes + _encoder.Encode(_frame, FrameSamples, _packet, NumberBytes);
            var payload = new ArraySegment<byte>(_packet, 0, bytes);

            if (send)
            {
                _lastSent = Time.unscaledTime;
                // Unreliable: a lost piece is 20 ms of sound the next packet can rebuild, and
                // resending it would only make it late.
                _net.ClientManager.Broadcast(new VoiceFrame { Payload = payload }, Channel.Unreliable);
            }

            if (test)
                Play(SelfId, payload);
        }

        // ---- The host passing voices on ------------------------------------------------------

        void OnFrame(NetworkConnection sender, VoiceFrame frame, Channel channel)
        {
            ArraySegment<byte> payload = frame.Payload;
            if (!LooksLikeVoice(payload))
                return;

            float now = Time.unscaledTime;
            if (!_allowance.TryGetValue(sender.ClientId, out Allowance state))
            {
                state = new Allowance { Bytes = BytesPerSecondCap, LastTime = now };
            }
            else
            {
                float elapsed = Mathf.Max(0f, now - state.LastTime);
                state.Bytes = Mathf.Min(BytesPerSecondCap, state.Bytes + BytesPerSecondCap * elapsed);
                state.LastTime = now;
            }

            if (state.Bytes < payload.Count)
            {
                _allowance[sender.ClientId] = state;
                return;
            }

            state.Bytes -= payload.Count;
            _allowance[sender.ClientId] = state;

            // A player with no body has nowhere to be heard from.
            NetworkObject speakerBody = sender.FirstObject;
            if (speakerBody == null)
                return;
            Vector3 from = speakerBody.transform.position;

            _relayTargets.Clear();
            foreach (NetworkConnection listener in _net.ServerManager.Clients.Values)
            {
                if (listener == sender || !listener.IsAuthenticated)
                    continue;
                NetworkObject listenerBody = listener.FirstObject;
                if (listenerBody == null)
                    continue;
                if ((listenerBody.transform.position - from).sqrMagnitude > RelayRange * RelayRange)
                    continue;
                _relayTargets.Add(listener);
            }

            if (_relayTargets.Count > 0)
            {
                var relay = new VoiceRelay { Speaker = sender.ClientId, Payload = payload };
                _net.ServerManager.Broadcast(_relayTargets, relay, true, Channel.Unreliable);
                _relayTargets.Clear();
            }
        }

        static bool LooksLikeVoice(ArraySegment<byte> payload)
        {
            return payload.Array != null
                && payload.Count > NumberBytes
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

            // How many pieces on from the last one this is: 1 is the next in line, 2 means
            // one was lost, 0 or less is late or a repeat. The subtraction wraps with the
            // 16-bit number, so it still works when the number rolls over.
            ushort number = (ushort)(bytes[offset] | bytes[offset + 1] << 8);
            bool fresh = !heard.HasNumber || Time.unscaledTime - heard.Speaker.LastHeard > NumberForget;
            int ahead = fresh ? 1 : (short)(number - heard.LastNumber);
            if (ahead <= 0)
                return;
            if (fresh)
                heard.Decoder.Reset();

            int sound = offset + NumberBytes;
            int soundBytes = payload.Count - NumberBytes;

            // Fill a short gap: carry the voice on through all but the last lost piece, then
            // rebuild that one from the spare copy this packet carries.
            int lost = ahead - 1;
            if (lost > 0 && lost <= MaxFill)
            {
                for (int i = 0; i < lost - 1; i++)
                    heard.Speaker.Push(_decoded, heard.Decoder.Conceal(_decoded, FrameSamples));
                int fecSamples = heard.Decoder.Decode(bytes, sound, soundBytes, _decoded, FrameSamples, previous: true);
                if (fecSamples <= 0)
                    return;
                heard.Speaker.Push(_decoded, fecSamples);
            }

            int samples = heard.Decoder.Decode(bytes, sound, soundBytes, _decoded, FrameSamples);
            if (samples <= 0)
                return;

            heard.LastNumber = number;
            heard.HasNumber = true;
            heard.Speaker.Push(_decoded, samples);
        }

        Heard SpeakerFor(int speakerId)
        {
            if (_heard.TryGetValue(speakerId, out Heard heard) && heard.Speaker != null)
                return heard;

            var go = new GameObject(speakerId == SelfId ? "Voice (own, mic test)" : "Voice (client " + speakerId + ")");
            go.transform.SetParent(transform, false);
            heard = new Heard { Speaker = go.AddComponent<VoiceSpeaker>(), Decoder = new VoiceDecoder(SampleRate) };
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
                if (pair.Key == SelfId || heard.Speaker == null || !heard.Speaker.Speaking)
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

        PlayerMotor FindBody(int clientId)
        {
            if (_net != null && _net.ServerManager.Started
                && _net.ServerManager.Clients.TryGetValue(clientId, out NetworkConnection conn)
                && conn.FirstObject != null
                && conn.FirstObject.TryGetComponent(out PlayerMotor hostBody))
            {
                return hostBody;
            }

            List<Health> all = Health.All;
            for (int i = 0; i < all.Count; i++)
            {
                Health health = all[i];
                if (health != null && health.OwnerId == clientId && health.TryGetComponent(out PlayerMotor body))
                    return body;
            }

            List<PlayerMotor> bodies = PlayerMotor.All;
            for (int i = 0; i < bodies.Count; i++)
            {
                PlayerMotor body = bodies[i];
                if (body != null && body.OwnerId == clientId)
                    return body;
            }

            return null;
        }

        void RemoveSpeaker(int speakerId)
        {
            if (!_heard.TryGetValue(speakerId, out Heard heard))
                return;
            if (heard.Speaker != null)
                Destroy(heard.Speaker.gameObject);
            _heard.Remove(speakerId);
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

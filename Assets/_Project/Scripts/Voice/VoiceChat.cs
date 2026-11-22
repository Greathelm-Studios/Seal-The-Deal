using System;
using System.Collections.Generic;
using CloseTheDeal.Audio;
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

    /// <summary>Proximity voice chat: captures, denoises, Opus-encodes, relays on the host, and plays in 3D.</summary>
    [DisallowMultipleComponent]
    public sealed class VoiceChat : MonoBehaviour
    {
        const int SampleRate = 48000;
        const int FrameSamples = SampleRate / 50;
        const int Bitrate = 32000;
        const int NumberBytes = 2;
        const int MicSeconds = 1;
        const float SpeechChance = 0.5f;
        const float GateLevel = 0.012f;
        const float SilenceLevel = 0.001f;
        const float GateHold = 0.35f;
        const float EncoderForget = 0.5f;
        const int MaxFill = 3;
        const float MicRetryDelay = 2f;
        const float FullRange = 3f;
        const float HearRange = 24f;
        const float RelayRange = HearRange + 4f;
        const int MaxPacketBytes = 400;
        const float BytesPerSecondCap = 8000f;
        const float NumberForget = 1f;
        const int SelfId = -1;
        const float BodySearchInterval = 0.5f;
        const string VoiceAction = "Voice";
        const string MixerGroupName = "Voice";

        /// <summary>Active VoiceChat singleton, or null before startup.</summary>
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
            // Clear remote speakers only on disconnect so the offline mic test keeps its own speaker.
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

        // Keeps mic active while connected/testing so push-to-talk never clips the first word.
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

            // Run RNNoise even while idle so its noise-floor estimate stays adapted to the room.
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
            if (speakerId != SelfId)
            {
                heard.Body = FindBody(speakerId);
                if (heard.Body != null)
                    heard.Speaker.transform.position = heard.Body.transform.position + Vector3.up * heard.Body.EyeHeight;
            }
            heard.Speaker.SetSpatial(speakerId != SelfId);
            _heard[speakerId] = heard;
            return heard;
        }

        /// <summary>The mixer's Voice group, resolved through SoundSystem.</summary>
        AudioMixerGroup Output()
        {
            if (_outputLooked)
                return _output;

            _output = SoundSystem.GetMixerGroup(MixerGroupName);
            if (_output != null)
                _outputLooked = true;
            return _output;
        }

        /// <summary>Moves each voice to its speaker's head.</summary>
        void PlaceSpeakers()
        {
            foreach (KeyValuePair<int, Heard> pair in _heard)
            {
                Heard heard = pair.Value;
                if (pair.Key == SelfId || heard.Speaker == null || Time.unscaledTime - heard.Speaker.LastHeard > 1f)
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

            if (_net != null && _net.ClientManager.Started)
            {
                foreach (NetworkObject nob in _net.ClientManager.Objects.Spawned.Values)
                {
                    if (nob != null && nob.OwnerId == clientId && nob.TryGetComponent(out PlayerMotor clientBody))
                        return clientBody;
                }
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

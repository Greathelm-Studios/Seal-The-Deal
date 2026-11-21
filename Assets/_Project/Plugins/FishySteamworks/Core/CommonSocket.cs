#if !FISHYSTEAMWORKS
using FishNet.Managing;
using FishNet.Managing.Logging;
using FishNet.Transporting;
using FishNet.Utility.Performance;
using Steamworks;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using UnityEngine;

namespace FishySteamworks
{

    public abstract class CommonSocket
    {

        #region Public.
        /// <summary>
        /// Current ConnectionState.
        /// </summary>
        private LocalConnectionState _connectionState = LocalConnectionState.Stopped;
        /// <summary>
        /// Returns the current ConnectionState.
        /// </summary>
        /// <returns></returns>
        internal LocalConnectionState GetLocalConnectionState()
        {
            return _connectionState;
        }
        /// <summary>
        /// Sets a new connection state.
        /// </summary>
        /// <param name="connectionState"></param>
        protected virtual void SetLocalConnectionState(LocalConnectionState connectionState, bool server)
        {
            //If state hasn't changed.
            if (connectionState == _connectionState)
                return;

            _connectionState = connectionState;

            if (server)
                Transport.HandleServerConnectionState(new ServerConnectionStateArgs(connectionState, Transport.Index));
            else
                Transport.HandleClientConnectionState(new ClientConnectionStateArgs(connectionState, Transport.Index));
        }
        #endregion

        #region Protected.
        /// <summary>
        /// True if using PeerToPeer.
        /// </summary>
        protected bool PeerToPeer = false;
        /// <summary>
        /// Transport controlling this socket.
        /// </summary>
        protected Transport Transport = null;
        /// <summary>
        /// Pointers for received messages per connection.
        /// </summary>
        protected IntPtr[] MessagePointers = null;
        /// <summary>
        /// Buffer used to receive data.
        /// </summary>
        protected byte[] InboundBuffer = null;
        // Fork patch: reusable persistently-pinned outbound buffer (see FORK.md).
        private byte[] _outboundBuffer = null;
        private GCHandle _outboundPinned;
        private IntPtr _outboundPtr = IntPtr.Zero;
        #endregion

        ~CommonSocket()
        {
            if (!Environment.HasShutdownStarted)
                FreeOutboundBuffer();
        }

        #region Const.
        /// <summary>
        /// Maximum number of messages which can be received per connection.
        /// </summary>
        protected const int MAX_MESSAGES = 256;
        private static readonly long MalformedLogIntervalTicks = Stopwatch.Frequency * 5L;
        private long _nextMalformedLogTimestamp = long.MinValue;
        private int _suppressedMalformedCount;
        #endregion

        /// <summary>
        /// Initializes this for use.
        /// </summary>
        /// <param name="t"></param>
        internal virtual void Initialize(Transport t)
        {
            Transport = t;
        }

        // Fork patch: lazily allocate receive buffers to skip ClientHostSocket (see FORK.md).
        protected void EnsureReceiveBuffers()
        {
            if (MessagePointers == null)
                MessagePointers = new IntPtr[MAX_MESSAGES];

            if (InboundBuffer == null)
            {
                int maxMTU = Transport.GetMTU(0);
                maxMTU = Math.Max(maxMTU, Transport.GetMTU(1));
                InboundBuffer = new byte[maxMTU];
            }
        }

        protected void FreeOutboundBuffer()
        {
            if (_outboundPinned.IsAllocated)
                _outboundPinned.Free();
            _outboundPtr = IntPtr.Zero;
            _outboundBuffer = null;
        }

        /// <summary>
        /// Gets bytes for address.
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        protected byte[] GetIPBytes(string address)
        {
            //If address is required then make sure it can be parsed.
            if (!string.IsNullOrEmpty(address))
            {
                if (!IPAddress.TryParse(address, out IPAddress result))
                {
                    Transport.NetworkManager.LogError($"Could not parse address {address} to IPAddress.");
                    return null;
                }
                else
                {
                    return result.GetAddressBytes();
                }
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Sends data over the steamConnection.
        /// </summary>
        /// <param name="steamConnection"></param>
        /// <param name="segment"></param>
        /// <param name="channelId"></param>
        /// <returns></returns>
        protected EResult Send(HSteamNetConnection steamConnection, ArraySegment<byte> segment, byte channelId)
        {
            // Fork patch: pack segment + trailing channel byte into reusable pinned _outboundBuffer (see FORK.md).
            int needed = segment.Count + 1;
            if (_outboundBuffer == null || _outboundBuffer.Length < needed)
            {
                int size = _outboundBuffer == null ? 2048 : _outboundBuffer.Length;
                while (size < needed)
                    size = size <= int.MaxValue / 2 ? size * 2 : needed;

                if (_outboundPinned.IsAllocated)
                    _outboundPinned.Free();

                _outboundBuffer = new byte[size];
                _outboundPinned = GCHandle.Alloc(_outboundBuffer, GCHandleType.Pinned);
                _outboundPtr = _outboundPinned.AddrOfPinnedObject();
            }
            else if (!_outboundPinned.IsAllocated)
            {
                _outboundPinned = GCHandle.Alloc(_outboundBuffer, GCHandleType.Pinned);
                _outboundPtr = _outboundPinned.AddrOfPinnedObject();
            }

            if (segment.Count > 0)
                Buffer.BlockCopy(segment.Array, segment.Offset, _outboundBuffer, 0, segment.Count);
            _outboundBuffer[segment.Count] = channelId;

            int sendFlag = (channelId == (byte)Channel.Unreliable) ? Constants.k_nSteamNetworkingSend_Unreliable : Constants.k_nSteamNetworkingSend_Reliable;
#if UNITY_SERVER
            EResult result = SteamGameServerNetworkingSockets.SendMessageToConnection(steamConnection, _outboundPtr, (uint)needed, sendFlag, out long _);
#else
            EResult result = SteamNetworkingSockets.SendMessageToConnection(steamConnection, _outboundPtr, (uint)needed, sendFlag, out long _);
#endif
            if (result != EResult.k_EResultOK)
                Transport.NetworkManager.LogWarning($"Send issue: {result}");

            return result;
        }

        /// <summary>
        /// Clears a queue.
        /// </summary>
        /// <param name="queue"></param>
        internal void ClearQueue(ConcurrentQueue<LocalPacket> queue)
        {
            while (queue.TryDequeue(out LocalPacket p))
                ByteArrayPool.Store(p.Data);
        }
        /// <summary>
        /// Clears a queue.
        /// </summary>
        /// <param name="queue"></param>
        internal void ClearQueue(Queue<LocalPacket> queue)
        {
            while (queue.Count > 0)
            {
                LocalPacket p = queue.Dequeue();
                ByteArrayPool.Store(p.Data);
            }
        }

        /// <summary>
        /// Returns a message from the steam network.
        /// </summary>
        /// <param name="ptr"></param>
        /// <param name="buffer"></param>
        /// <returns>False when the message was dropped as malformed; segment and channel are then unset.</returns>
        protected bool GetMessage(IntPtr ptr, byte[] buffer, out ArraySegment<byte> segment, out byte channel)
        {
            // Fork patch: read m_pData (offset 0) and m_cbSize (offset IntPtr.Size) directly (see FORK.md).
            IntPtr pData = Marshal.ReadIntPtr(ptr);
            int packetLength = Marshal.ReadInt32(ptr, IntPtr.Size);
            // Fork patch: drop and release empty or oversized messages before reading channel byte (see FORK.md).
            if (packetLength < 1 || packetLength > buffer.Length)
            {
                SteamNetworkingMessage_t.Release(ptr);
                segment = default;
                channel = 0;
                long now = Stopwatch.GetTimestamp();
                if (now >= _nextMalformedLogTimestamp)
                {
                    _nextMalformedLogTimestamp = now + MalformedLogIntervalTicks;
                    if (_suppressedMalformedCount > 0)
                    {
                        Transport.NetworkManager.LogWarning($"Dropped a malformed Steam message of {packetLength} bytes ({_suppressedMalformedCount} suppressed).");
                        _suppressedMalformedCount = 0;
                    }
                    else
                    {
                        Transport.NetworkManager.LogWarning($"Dropped a malformed Steam message of {packetLength} bytes.");
                    }
                }
                else
                {
                    _suppressedMalformedCount++;
                }
                return false;
            }

            Marshal.Copy(pData, buffer, 0, packetLength);

            //data.Release();
            SteamNetworkingMessage_t.Release(ptr);
            //Channel will be at the end of the packet.
            channel = buffer[packetLength - 1];
            //Set segment to length - 1 to exclude channel.
            segment = new ArraySegment<byte>(buffer, 0, packetLength - 1);
            return true;
        }
    }

}
#endif
using UnityEngine;

namespace CloseTheDeal.Net
{
    /// <summary>
    /// Packs a pose into 10 bytes and back. Position: three unsigned 16-bit numbers,
    /// little-endian, X then Y then Z, each the fraction of the way across the box
    /// (clamped to the box). Rotation: one unsigned 32-bit number, little-endian, smallest
    /// three: the top 2 bits are the index of the largest component (0 = x, 1 = y, 2 = z,
    /// 3 = w), then the other three components in x, y, z, w order at 10 bits each, each
    /// mapped from [-1/sqrt(2), +1/sqrt(2)]. The quaternion is negated first if its largest
    /// component is negative, so the dropped one is always positive.
    /// Pure and allocation-free. Design: docs/systems/SWITCHBOARD.md.
    /// </summary>
    public static class PoseCodec
    {
        public const int PoseBytes = 10;
        const float ComponentLimit = 0.7071067811865476f;

        /// <summary>Writes PoseBytes bytes at offset and advances it. The caller guarantees the room.</summary>
        public static void Write(byte[] buffer, ref int offset, in Bounds box, Vector3 localPosition, Quaternion rotation)
        {
            Vector3 min = box.min;
            Vector3 size = box.size;
            WriteU16(buffer, ref offset, PackPosition(localPosition.x, min.x, size.x));
            WriteU16(buffer, ref offset, PackPosition(localPosition.y, min.y, size.y));
            WriteU16(buffer, ref offset, PackPosition(localPosition.z, min.z, size.z));

            float magnitude = Mathf.Sqrt(rotation.x * rotation.x + rotation.y * rotation.y +
                rotation.z * rotation.z + rotation.w * rotation.w);
            if (magnitude <= 0f)
                rotation = Quaternion.identity;
            else
            {
                float inverse = 1f / magnitude;
                rotation = new Quaternion(rotation.x * inverse, rotation.y * inverse,
                    rotation.z * inverse, rotation.w * inverse);
            }

            float largest = Mathf.Abs(rotation.x);
            int largestIndex = 0;
            if (Mathf.Abs(rotation.y) > largest) { largest = Mathf.Abs(rotation.y); largestIndex = 1; }
            if (Mathf.Abs(rotation.z) > largest) { largest = Mathf.Abs(rotation.z); largestIndex = 2; }
            if (Mathf.Abs(rotation.w) > largest) largestIndex = 3;

            float dropped = largestIndex == 0 ? rotation.x : largestIndex == 1 ? rotation.y :
                largestIndex == 2 ? rotation.z : rotation.w;
            float sign = dropped < 0f ? -1f : 1f;
            uint packed = (uint)largestIndex << 30;
            int shift = 20;
            if (largestIndex != 0) { packed |= (uint)PackComponent(rotation.x * sign) << shift; shift -= 10; }
            if (largestIndex != 1) { packed |= (uint)PackComponent(rotation.y * sign) << shift; shift -= 10; }
            if (largestIndex != 2) { packed |= (uint)PackComponent(rotation.z * sign) << shift; shift -= 10; }
            if (largestIndex != 3) packed |= (uint)PackComponent(rotation.w * sign) << shift;

            buffer[offset++] = (byte)packed;
            buffer[offset++] = (byte)(packed >> 8);
            buffer[offset++] = (byte)(packed >> 16);
            buffer[offset++] = (byte)(packed >> 24);
        }

        /// <summary>Reads PoseBytes bytes at offset and advances it. The rotation comes back normalised.</summary>
        public static void Read(byte[] buffer, ref int offset, in Bounds box, out Vector3 localPosition, out Quaternion rotation)
        {
            Vector3 min = box.min;
            Vector3 size = box.size;
            localPosition = new Vector3(
                UnpackPosition(ReadU16(buffer, ref offset), min.x, size.x),
                UnpackPosition(ReadU16(buffer, ref offset), min.y, size.y),
                UnpackPosition(ReadU16(buffer, ref offset), min.z, size.z));

            uint packed = (uint)buffer[offset++] | (uint)buffer[offset++] << 8 |
                (uint)buffer[offset++] << 16 | (uint)buffer[offset++] << 24;
            int largestIndex = (int)(packed >> 30);
            int shift = 20;
            float x = largestIndex == 0 ? 0f : UnpackComponent((int)((packed >> shift) & 1023u));
            if (largestIndex != 0) shift -= 10;
            float y = largestIndex == 1 ? 0f : UnpackComponent((int)((packed >> shift) & 1023u));
            if (largestIndex != 1) shift -= 10;
            float z = largestIndex == 2 ? 0f : UnpackComponent((int)((packed >> shift) & 1023u));
            if (largestIndex != 2) shift -= 10;
            float w = largestIndex == 3 ? 0f : UnpackComponent((int)((packed >> shift) & 1023u));
            float missing = Mathf.Sqrt(Mathf.Max(0f, 1f - x * x - y * y - z * z - w * w));
            if (largestIndex == 0) x = missing;
            else if (largestIndex == 1) y = missing;
            else if (largestIndex == 2) z = missing;
            else w = missing;

            float inverse = 1f / Mathf.Sqrt(x * x + y * y + z * z + w * w);
            rotation = new Quaternion(x * inverse, y * inverse, z * inverse, w * inverse);
        }

        static ushort PackPosition(float value, float min, float size)
        {
            if (size <= 0f)
                return 0;
            return (ushort)Mathf.RoundToInt(Mathf.Clamp01((value - min) / size) * 65535f);
        }

        static float UnpackPosition(ushort value, float min, float size)
        {
            return min + value * (size / 65535f);
        }

        static int PackComponent(float value)
        {
            return Mathf.RoundToInt((Mathf.Clamp(value, -ComponentLimit, ComponentLimit) + ComponentLimit) *
                (1023f / (2f * ComponentLimit)));
        }

        static float UnpackComponent(int value)
        {
            return value * (2f * ComponentLimit / 1023f) - ComponentLimit;
        }

        static void WriteU16(byte[] buffer, ref int offset, ushort value)
        {
            buffer[offset++] = (byte)value;
            buffer[offset++] = (byte)(value >> 8);
        }

        static ushort ReadU16(byte[] buffer, ref int offset)
        {
            return (ushort)(buffer[offset++] | buffer[offset++] << 8);
        }
    }
}

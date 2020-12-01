using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WaveshareUARTFingerprintSensor
{
    public static class Utils
    {
        public static ushort Merge(byte high, byte low) => (ushort)(high << 8 | low);
        public static uint Merge(byte first, byte second, byte third) => (uint)(first << 16 | second << 8 | third);

        public static (byte high, byte low) Split(ushort value) => ((byte)(value >> 8), (byte)(value & 0xFF));
        public static (byte first, byte second, byte third) Split(uint value) => ((byte)(value >> 16 & 0xFF), (byte)(value >> 8 & 0xFF), (byte)(value & 0xFF));

        public static string ArrayDisplay<T>(T[] arr) => $"[ {string.Join(", ", arr)} ]";
    }
}

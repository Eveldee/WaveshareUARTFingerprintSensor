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

        public static (byte high, byte low) Split(ushort value) => ((byte)(value >> 8), (byte)(value & 0xFF));
    }
}

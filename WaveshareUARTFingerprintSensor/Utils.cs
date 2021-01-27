using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WaveshareUARTFingerprintSensor
{
    public static class Utils
    {
        /// <summary>
        /// Parse two <see cref="byte"/> to an <see cref="ushort"/> according to the sensor documentation
        /// </summary>
        /// <param name="high">Usually the first data <see cref="byte"/> from a response</param>
        /// <param name="low">Usually the second data <see cref="byte"/> from a response</param>
        /// <returns></returns>
        public static ushort Merge(byte high, byte low) => (ushort)(high << 8 | low);
        /// <summary>
        /// Parse threee <see cref="byte"/> to an <see cref="uint"/> according to the sensor documentation
        /// </summary>
        /// <param name="first">Usually the first data <see cref="byte"/> from a response</param>
        /// <param name="second">Usually the second data <see cref="byte"/> from a response</param>
        /// <param name="third">Usually the third data <see cref="byte"/> from a response</param>
        /// <returns></returns>
        public static uint Merge(byte first, byte second, byte third) => (uint)(first << 16 | second << 8 | third);

        /// <summary>
        /// Split an <see cref="ushort"/> to two bytes according to the sensor documentation
        /// </summary>
        /// <param name="value"></param>
        /// <returns>The first <see cref="byte"/> (high) and the second <see cref="byte"/> (low)</returns>
        public static (byte high, byte low) Split(ushort value) => ((byte)(value >> 8), (byte)(value & 0xFF));
        /// <summary>
        /// Split an <see cref="uint"/> to three bytes according to the sensor documentation
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static (byte first, byte second, byte third) Split(uint value) => ((byte)(value >> 16 & 0xFF), (byte)(value >> 8 & 0xFF), (byte)(value & 0xFF));

        /// <summary>
        /// Display an array in a standard format
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="arr"></param>
        /// <returns>A <see cref="string"/> in the form: [ element1, element2, element3, ... ]</returns>
        public static string ArrayDisplay<T>(T[] arr) => $"[ {string.Join(", ", arr)} ]";
    }
}

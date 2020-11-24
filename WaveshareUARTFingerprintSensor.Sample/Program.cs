using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unosquare.RaspberryIO;
using Unosquare.RaspberryIO.Abstractions;
using Unosquare.WiringPi;

namespace WaveshareUARTFingerprintSensor.Sample
{
    class Program
    {
        static void Main(string[] args)
        {
            var sensor = new FingerprintSensor(FingerprintSensor.PrimarySerialPort);

            sensor.Start();

            if (sensor.TryGetUserCount(out ushort count))
            {
                Console.WriteLine(count);
            }

            Console.WriteLine("End");

            Thread.Sleep(-1);
        }
    }
}

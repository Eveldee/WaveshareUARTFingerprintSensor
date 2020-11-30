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

            Console.WriteLine("Adding fingerprint");

            var response = sensor.AddFingerprint(40, UserPermission.Level3);

            Console.WriteLine("End");

            Thread.Sleep(-1);
        }
    }
}

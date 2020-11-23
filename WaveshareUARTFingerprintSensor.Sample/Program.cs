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

            var count = sensor.GetUserCount();

            Thread.Sleep(-1);
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Terminal.Gui;
using Unosquare.RaspberryIO;
using Unosquare.RaspberryIO.Abstractions;
using Unosquare.WiringPi;

namespace WaveshareUARTFingerprintSensor.Sample
{
    class Program
    {
        public static FingerprintSensor FingerprintSensor { get; private set; }

        static void Main(string[] args)
        {
            FingerprintSensor = new FingerprintSensor(FingerprintSensor.SecondarySerialPort);

            FingerprintSensor.Start();

            Application.Run<TUIManager>();
        }
    }
}

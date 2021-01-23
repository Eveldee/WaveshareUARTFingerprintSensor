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
using WaveshareUARTFingerprintSensor.Sample.Views;

namespace WaveshareUARTFingerprintSensor.Sample
{
    class Program
    {
        static void Main(string[] args)
        {
            Application.Run<TUIManager>();
        }
    }
}

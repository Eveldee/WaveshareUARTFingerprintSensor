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
            var sensor = new FingerprintSensor(FingerprintSensor.SecondarySerialPort);

            sensor.Start();

            Console.WriteLine("Here");

            //if (sensor.TryAcquireUserEigenvalues(2, out var image, out var permission))
            //{
            //    Console.WriteLine(Utils.ArrayDisplay(image.ToArray()));
            //}
            if (sensor.TryGetUserCount(out var count))
            {
                Console.WriteLine(count);
            }
            //while (true)
            //{
            //    var resp = sensor.AddFingerprintAndAcquireEigenvalues(67, UserPermission.Level3);

            //    Console.WriteLine(resp.responseType);
            //    if (resp.responseType == ResponseType.Success)
            //    {
            //        Console.WriteLine(Utils.ArrayDisplay(resp.eigenvalues));
            //    }
            //}

            Console.WriteLine("End");

            Thread.Sleep(-1);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unosquare.RaspberryIO;
using Unosquare.RaspberryIO.Abstractions;
using Unosquare.WiringPi;

namespace WaveshareUARTFingerprintSensor
{
    public class FingerprintSensor : IDisposable
    {
        public const string PrimarySerialPort = "/dev/ttyAMA0";
        public const string SecondarySerialPort = "/dev/ttyS0";
        public const int DefaultTimeout = 10_000;

        public event WakedEventHandler Waked;
        public delegate void WakedEventHandler(FingerprintSensor sender);

        public string PortName { get; }

        private const byte PacketSeparator = 0xF5;

        private SerialPort _serialPort;
        private int _wakePinNumber;
        private int _rstPinNumber;
        private IGpioPin _wakePin;
        private IGpioPin _rstPin;
        private object _lock = new object();

        public FingerprintSensor(string portName, int wakePin = 23, int rstPin = 24)
        {
            PortName = portName;
            _wakePinNumber = wakePin;
            _rstPinNumber = rstPin;
        }

        public void Start()
        {
            // Initialize Gpio
            Pi.Init<BootstrapWiringPi>();

            _wakePin = Pi.Gpio[_wakePinNumber];
            _rstPin = Pi.Gpio[_rstPinNumber];

            _wakePin.PinMode = GpioPinDriveMode.Input;
            _rstPin.PinMode = GpioPinDriveMode.Output;

            _rstPin.Write(GpioPinValue.High);
            _wakePin.RegisterInterruptCallback(EdgeDetection.FallingAndRisingEdge, OnWake);

            // Initialize SerialPort
            _serialPort = new SerialPort(PortName, 19200);

            _serialPort.Open();
        }

        private byte ComputeChecksum(byte[] data)
        {
            byte checksum = 0;

            for (int i = 1; i < 6; i++)
            {
                checksum += data[i];
            }

            return checksum;
        }

        private (byte first, byte second, ResponseType responseType) SendAndReceive(CommandType commandType, byte first, byte second, byte third, int timeout = DefaultTimeout)
        {
            // Command packet
            byte[] buffer = { PacketSeparator, (byte)commandType, first, second, third, 0, 0, PacketSeparator };

            lock (_lock)
            {
                // Set timeout
                _serialPort.WriteTimeout = timeout;
                _serialPort.ReadTimeout = timeout;

                // Checksum
                buffer[6] = ComputeChecksum(buffer);

                _serialPort.Write(buffer, 0, buffer.Length);

                // Response
                _serialPort.Read(buffer, 0, buffer.Length);

                if (buffer[0] != PacketSeparator || buffer[7] != PacketSeparator || buffer[1] != (byte)commandType)
                {
                    throw new InvalidDataException("Invalid response from the sensor");
                }

                if (buffer[6] != ComputeChecksum(buffer))
                {
                    throw new InvalidDataException("Invalid checksum");
                }
            }

            return (buffer[2], buffer[3], (ResponseType)buffer[4]);
        }

        private (byte first, byte second, byte third) SendAndReceiveRaw(CommandType commandType, byte first, byte second, byte third, int timeout = DefaultTimeout)
        {
            (byte f, byte s, ResponseType response) = SendAndReceive(commandType, first, second, third, timeout);

            return (f, s, (byte)response);
        }

        private bool TrySendAndReceive(CommandType commandType, byte first, byte second, byte third, out (byte first, byte second, ResponseType responseType) response, int timeout = DefaultTimeout)
        {
            try
            {
                response = SendAndReceive(commandType, first, second, third, timeout);
            }
            catch (Exception)
            {
                response = default;

                return false;
            }

            return true;
        }

        private bool TrySendAndReceiveRaw(CommandType commandType, byte first, byte second, byte third, out (byte first, byte second, byte third) response, int timeout = DefaultTimeout)
        {
            try
            {
                response = SendAndReceiveRaw(commandType, first, second, third, timeout);
            }
            catch (Exception)
            {
                response = default;

                return false;
            }

            return true;
        }

        public uint QuerySerialNumber()
        {
            (byte first, byte second, byte third) = SendAndReceiveRaw(CommandType.QuerySerialNumber, 0, 0, 0);

            return Utils.Merge(first, second, third);
        }

        public bool TryGetUserCount(out ushort count)
        {
            if (TrySendAndReceive(CommandType.QueryUserCount, 0, 0, 0, out var response, 1000))
            {
                (byte countHigh, byte countLow, ResponseType responseType) = response;

                count = Utils.Merge(countHigh, countLow);

                return responseType == ResponseType.Success;
            }

            count = default;

            return false;
        }

        public void Sleep()
        {
            _rstPin.Write(GpioPinValue.Low);
        }

        public void Wake()
        {
            _rstPin.Write(GpioPinValue.High);
        }

        private void OnWake()
        {
            if (_wakePin.Read())
            {
                Waked?.Invoke(this);
            }
        }

        public void Dispose()
        {
            _serialPort.Close();
        }
    }
}

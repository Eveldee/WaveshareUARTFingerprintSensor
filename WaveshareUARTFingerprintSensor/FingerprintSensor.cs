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
using WaveshareUARTFingerprintSensor.Exceptions;

namespace WaveshareUARTFingerprintSensor
{
    public class FingerprintSensor : IDisposable
    {
        public const string PrimarySerialPort = "/dev/ttyAMA0";
        public const string SecondarySerialPort = "/dev/ttyS0";
        public const int DefaultTimeout = 10_000;
        public const int MaxUserID = 0xFFF;

        public event WakedEventHandler Waked;
        public delegate void WakedEventHandler(FingerprintSensor sender);

        public string PortName { get; }

        private const byte PacketSeparator = 0xF5;

        private SerialPort _serialPort;
        private readonly int _wakePinNumber;
        private readonly int _rstPinNumber;
        private IGpioPin _wakePin;
        private IGpioPin _rstPin;
        private bool _sleeping = false;
        private readonly object _lock = new object();

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
                checksum ^= data[i];
            }

            return checksum;
        }

        private byte ComputeChecksumData(byte[] data, int length)
        {
            byte checksum = 0;

            for (int i = 1; i < length + 1; i++)
            {
                checksum ^= data[i];
            }

            return checksum;
        }

        private (byte first, byte second, ResponseType responseType) SendAndReceive(CommandType commandType, byte first, byte second, byte third, int timeout = DefaultTimeout)
        {
            if (_sleeping)
            {
                throw new SensorStateException(SensorStateException.SensorSleepingMessage);
            }

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

        private byte[] ReadData(int length)
        {
            byte first = (byte)_serialPort.ReadByte();

            if (first != PacketSeparator)
            {
                throw new InvalidDataException("Invalid response from the sensor");
            }

            byte[] data = new byte[length];

            _serialPort.Read(data, 0, length);

            byte checksum = (byte)_serialPort.ReadByte();
            byte separator = (byte)_serialPort.ReadByte();

            if (separator != PacketSeparator || checksum != ComputeChecksumData(data, length))
            {
                throw new InvalidDataException("Invalid checksum");
            }

            return data;
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

        public ResponseType AddFingerprint(ushort userID, UserPermission userPermission)
        {
            if (userID > MaxUserID)
            {
                return ResponseType.Full;
            }

            CommandType[] commands = { CommandType.AddFingerprint1, CommandType.AddFingerprint2, CommandType.AddFingerprint3 };
            (byte idHigh, byte idLow) = Utils.Split(userID);

            foreach (var command in commands)
            {
                if (TrySendAndReceive(command, idHigh, idLow, (byte)userPermission, out var response))
                {
                    if (response.responseType != ResponseType.Success)
                    {
                        return response.responseType;
                    }
                }
                else
                {
                    return ResponseType.Timeout;
                }

                Thread.Sleep(50);
            }

            return ResponseType.Success;
        }

        public (ResponseType responseType, byte[] eigenvalues) AddFingerprintAndAcquireEigenvalues(ushort userID, UserPermission userPermission)
        {
            if (userID > MaxUserID)
            {
                return (ResponseType.Full, null);
            }

            CommandType[] commands = { CommandType.AddFingerprint1, CommandType.AddFingerprint2 };
            (byte idHigh, byte idLow) = Utils.Split(userID);

            foreach (var command in commands)
            {
                if (TrySendAndReceive(command, idHigh, idLow, (byte)userPermission, out var loopResponse))
                {
                    if (loopResponse.responseType != ResponseType.Success)
                    {
                        return (loopResponse.responseType, null);
                    }
                }
                else
                {
                    return (ResponseType.Timeout, null);
                }

                Thread.Sleep(50);
            }

            if (TrySendAndReceive(CommandType.AddAndAcquireFingerprint, 0, 0, 0, out var response))
            {
                if (response.responseType != ResponseType.Success)
                {
                    return (response.responseType, null);
                }

                ushort length = Utils.Merge(response.first, response.second);

                var data = ReadData(length);
                var eigenvalues = data.Skip(3).ToArray();

                return (ResponseType.Success, eigenvalues);
            }
            else
            {
                return (ResponseType.Timeout, null);
            }
        }

        public bool DeleteUser(ushort userID)
        {
            (byte high, byte low) = Utils.Split(userID);

            if (TrySendAndReceive(CommandType.DeleteUser, high, low, 0, out var response, 1000))
            {
                return response.responseType == ResponseType.Success;
            }

            return false;
        }

        public bool DeleteAllUsers()
        {
            if (TrySendAndReceive(CommandType.DeleteAllUsers, 0, 0, 0, out var response, 1000))
            {
                return response.responseType == ResponseType.Success;
            }

            return false;
        }

        public bool DeleteAllUsersWithPermission(UserPermission userPermission)
        {
            if (TrySendAndReceive(CommandType.DeleteAllUsers, 0, 0, (byte)userPermission, out var response, 1000))
            {
                return response.responseType == ResponseType.Success;
            }

            return false;
        }

        /// <summary>
        /// Read a fingerprint and check if it matches with the specified user
        /// </summary>
        /// <param name="userID">A registered user ID</param>
        /// <returns></returns>
        public bool Comparison11(ushort userID)
        {
            (byte high, byte low) = Utils.Split(userID);

            if (TrySendAndReceive(CommandType.Comparison11, high, low, 0, out var response, 1000))
            {
                return response.responseType == ResponseType.Success;
            }

            return false;
        }

        /// <summary>
        /// Read a fingerprint and check if it match with any registered user
        /// </summary>
        /// <param name="userInfo">The matched user info</param>
        /// <returns></returns>
        public bool TryComparison1N(out (ushort userID, UserPermission permission) userInfo)
        {
            if (TrySendAndReceive(CommandType.Comparison11, 0, 0, 0, out var response, 1000))
            {
                if (response.responseType != ResponseType.NoUser && response.responseType != ResponseType.Timeout)
                {
                    userInfo = (Utils.Merge(response.first, response.second), (UserPermission)response.responseType);

                    return true;
                }
            }

            userInfo = default;

            return false;
        }

        public bool TryQueryPermission(ushort userID, out UserPermission userPermission)
        {
            (byte high, byte low) = Utils.Split(userID);

            if (TrySendAndReceive(CommandType.QueryPermission, high, low, 0, out var reponse, 1000))
            {
                if (reponse.responseType != ResponseType.NoUser)
                {
                    userPermission = (UserPermission)reponse.responseType;

                    return true;
                }
            }

            userPermission = default;

            return true;
        }

        public bool TryGetComparisonLevel(out byte comparisonLevel)
        {
            if (TrySendAndReceive(CommandType.ManageComparisonLevel, 0, 0, 1, out var response, 1000))
            {
                if (response.responseType == ResponseType.Success)
                {
                    comparisonLevel = response.second;

                    return true;
                }
            }

            comparisonLevel = default;

            return false;
        }

        public bool TrySetComparisonLevel(byte comparisonLevel)
        {
            if (comparisonLevel < 0 || comparisonLevel > 9)
            {
                return false;
            }

            if (TrySendAndReceive(CommandType.ManageComparisonLevel, 0, comparisonLevel, 0, out var response, 1000))
            {
                return response.responseType == ResponseType.Success;
            }

            return false;
        }

        public void Sleep()
        {
            _sleeping = true;
            _rstPin.Write(GpioPinValue.Low);
        }

        public void Wake()
        {
            _sleeping = false;
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

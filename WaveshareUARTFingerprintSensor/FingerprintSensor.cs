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
        /// <summary>
        /// Primary serial port on the RPI. <see href="https://www.raspberrypi.org/documentation/configuration/uart.md">Source</see>
        /// </summary>
        public const string PrimarySerialPort = "/dev/ttyAMA0";
        /// <summary>
        /// Secondary serial port on the RPI. <see href="https://www.raspberrypi.org/documentation/configuration/uart.md">Source</see>
        /// </summary>
        public const string SecondarySerialPort = "/dev/ttyS0";
        /// <summary>
        /// Timeout used for the <see cref="SerialPort"/> I/O operations
        /// </summary>
        public const int DefaultTimeout = 10_000;
        /// <summary>
        /// Maximum number of user that the sensor can store before throwing <see cref="ResponseType.Full"/>. It is also the ID of the last user
        /// </summary>
        public const int MaxUserID = 0xFFF;
        /// <summary>
        /// Internal buffer size
        /// </summary>
        public const int DataBufferSize = 4095;

        /// <summary>
        /// Thrown when "WAKE" pin is high, works like a press button. Can be used while the sensor is asleep
        /// </summary>
        public event WakedEventHandler Waked;
        /// <summary>
        /// Delegate for the <see cref="Waked"/> event
        /// </summary>
        /// <param name="sender"></param>
        public delegate void WakedEventHandler(FingerprintSensor sender);

        /// <summary>
        /// <see cref="SerialPort"/> name/path used
        /// </summary>
        public string PortName { get; }

        /// <summary>
        /// Mark the end of a command packet, usually the 8th byte
        /// </summary>
        private const byte PacketSeparator = 0xF5;

        private SerialPort _serialPort;
        private readonly int _wakePinNumber;
        private readonly int _rstPinNumber;
        private IGpioPin _wakePin;
        private IGpioPin _rstPin;
        private bool _sleeping = false;
        private readonly object _lock = new object();

        /// <summary>
        /// Create a new instance of <see cref="FingerprintSensor"/>, don't forget to call <see cref="Start"/> before using any command
        /// </summary>
        /// <param name="portName">A valid name/path to a serial port, see <see cref="PrimarySerialPort"/> and <see cref="SecondarySerialPort"/> for the RPI</param>
        /// <param name="wakePin">WAKE <see cref="GpioPin"/> number, default is according to the sensor documentation wiring</param>
        /// <param name="rstPin">RST <see cref="GpioPin"/> number, default is according to the sensor documentation wiring</param>
        public FingerprintSensor(string portName, int wakePin = 23, int rstPin = 24)
        {
            PortName = portName;
            _wakePinNumber = wakePin;
            _rstPinNumber = rstPin;
        }

        /// <summary>
        /// Start the sensor, initializing <see cref="BootstrapWiringPi"/>, the Gpio pins and the <see cref="SerialPort"/>
        /// </summary>
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

        /// <summary>
        /// Compute the checksum for a command packet: XOR of <see cref="byte"/> 1 to 5 (counting from 0)
        /// </summary>
        /// <param name="data">A command packet, usually 8 bytes</param>
        /// <returns>The computed checksum, usually stored in the 6th <see cref="byte"/></returns>
        private byte ComputeChecksum(byte[] data)
        {
            byte checksum = 0;

            for (int i = 1; i < 6; i++)
            {
                checksum ^= data[i];
            }

            return checksum;
        }

        /// <summary>
        /// Compute the checksum for a data packet: XOR of <see cref="byte"/> 1 to (last - 2) (counting from 0)
        /// </summary>
        /// <param name="data">A data, not to be confused with a command packet</param>
        /// <param name="length">Length of the data packet, should be 2 bytes less than data.Length</param>
        /// <returns>The computed checksum, usually stored in the (last - 2)th byte</returns>
        private byte ComputeChecksumData(byte[] data, int length)
        {
            byte checksum = 0;

            for (int i = 1; i < length; i++)
            {
                checksum ^= data[i];
            }

            return checksum;
        }

        /// <summary>
        /// Send an 8 <see cref="byte"/> command and read the response, can throw an <see cref="Exception"/>
        /// </summary>
        /// <param name="commandType">Command flag, see <see cref="CommandType"/></param>
        /// <param name="first">First data <see cref="byte"/>, the 2th <see cref="byte"/> (counting from 0)</param>
        /// <param name="second">Second data <see cref="byte"/>, the 3th <see cref="byte"/> (counting from 0)</param>
        /// <param name="third">Third data <see cref="byte"/>, the 4th <see cref="byte"/> (counting from 0)</param>
        /// <param name="timeout">Timeout used for the <see cref="SerialPort"/>, default to <see cref="DefaultTimeout"/></param>
        /// <returns>The 3 data <see cref="byte"/> from the response (2, 3 and 4), the third one is parsed as a <see cref="ResponseType"/></returns>
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
                int length = buffer.Length;
                int offset = 0;
                do
                {
                    int toRead = length - offset;
                    offset += _serialPort.Read(buffer, offset, toRead);
                } while (offset < length);

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

        /// <summary>
        /// Send an 8 <see cref="byte"/> command and read the response, can throw an <see cref="Exception"/>
        /// </summary>
        /// <param name="commandType">Command flag, see <see cref="CommandType"/></param>
        /// <param name="first">First data <see cref="byte"/>, the 2th <see cref="byte"/> (counting from 0)</param>
        /// <param name="second">Second data <see cref="byte"/>, the 3th <see cref="byte"/> (counting from 0)</param>
        /// <param name="third">Third data <see cref="byte"/>, the 4th <see cref="byte"/> (counting from 0)</param>
        /// <param name="timeout">Timeout used for the <see cref="SerialPort"/>, default to <see cref="DefaultTimeout"/></param>
        /// <returns>The 3 data <see cref="byte"/> from the response (2, 3 and 4)</returns>
        private (byte first, byte second, byte third) SendAndReceiveRaw(CommandType commandType, byte first, byte second, byte third, int timeout = DefaultTimeout)
        {
            (byte f, byte s, ResponseType response) = SendAndReceive(commandType, first, second, third, timeout);

            return (f, s, (byte)response);
        }

        /// <summary>
        /// Send an 8 <see cref="byte"/> command and read the response without throwing an <see cref="Exception"/>
        /// </summary>
        /// <param name="commandType">Command flag, see <see cref="CommandType"/></param>
        /// <param name="first">First data <see cref="byte"/>, the 2th <see cref="byte"/> (counting from 0)</param>
        /// <param name="second">Second data <see cref="byte"/>, the 3th <see cref="byte"/> (counting from 0)</param>
        /// <param name="third">Third data <see cref="byte"/>, the 4th <see cref="byte"/> (counting from 0)</param>
        /// <param name="response">The response as returned by <see cref="SendAndReceive(CommandType, byte, byte, byte, int)"/></param>
        /// <param name="timeout">Timeout used for the <see cref="SerialPort"/>, default to <see cref="DefaultTimeout"/></param>
        /// <returns>true if successful, false otherwise</returns>
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

        /// <summary>
        /// Send an 8 <see cref="byte"/> command and read the response without throwing an <see cref="Exception"/>
        /// </summary>
        /// <param name="commandType">Command flag, see <see cref="CommandType"/></param>
        /// <param name="first">First data <see cref="byte"/>, the 2th <see cref="byte"/> (counting from 0)</param>
        /// <param name="second">Second data <see cref="byte"/>, the 3th <see cref="byte"/> (counting from 0)</param>
        /// <param name="third">Third data <see cref="byte"/>, the 4th <see cref="byte"/> (counting from 0)</param>
        /// <param name="response">The response as returned by <see cref="SendAndReceive(CommandType, byte, byte, byte, int)"/></param>
        /// <param name="timeout">Timeout used for the <see cref="SerialPort"/>, default to <see cref="DefaultTimeout"/></param>
        /// <returns>true if successful, false otherwise</returns>
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

        /// <summary>
        /// Read a data packet
        /// </summary>
        /// <param name="length">The length of the data packet</param>
        /// <param name="skipChecksum">An invalid checksum will throw an <see cref="Exception"/>, use this to ignore the checksum</param>
        /// <returns></returns>
        private byte[] ReadData(int length, bool skipChecksum = false)
        {
            byte first = (byte)_serialPort.ReadByte();

            if (first != PacketSeparator)
            {
                throw new InvalidDataException("Invalid response from the sensor");
            }

            byte[] data = new byte[length];

            int offset = 0;
            do
            {
                int toRead = length - offset > DataBufferSize ? DataBufferSize : length - offset;
                offset += _serialPort.Read(data, offset, toRead);
            } while (offset < length);

            byte checksum = (byte)_serialPort.ReadByte();
            byte separator = (byte)_serialPort.ReadByte();

            if (separator != PacketSeparator || (!skipChecksum && checksum != ComputeChecksumData(data, length)))
            {
                throw new InvalidDataException("Invalid checksum");
            }

            return data;
        }

        /// <summary>
        /// Query the sensor serial number
        /// </summary>
        /// <returns></returns>
        public uint QuerySerialNumber()
        {
            (byte first, byte second, byte third) = SendAndReceiveRaw(CommandType.QuerySerialNumber, 0, 0, 0);

            return Utils.Merge(first, second, third);
        }

        /// <summary>
        /// Get the number of registered users (fingerprints)
        /// </summary>
        /// <param name="count">User count if the command is successful</param>
        /// <returns></returns>
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

        /// <summary>
        /// Add a fingerprint by sending three commands, with a delay between each to ensure the fingerprint is perfectly read
        /// </summary>
        /// <param name="userID">The id where to store the user (fingerprint)</param>
        /// <param name="userPermission">The <see cref="UserPermission"/> to store</param>
        /// <returns></returns>
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

        /// <summary>
        /// Although there is the word 'Add' in the command (in the official doc), it does not currently add the fingerprint
        /// in the captor storage for whatever reason
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="userPermission"></param>
        /// <returns></returns>
        public ResponseType AddFingerprintAndAcquireEigenvalues(ushort userID, UserPermission userPermission, out Span<byte> eigenvalues)
        {
            if (userID > MaxUserID)
            {
                eigenvalues = Span<byte>.Empty;
                return ResponseType.Full;
            }

            CommandType[] commands = { CommandType.AddFingerprint1, CommandType.AddFingerprint2 };
            (byte idHigh, byte idLow) = Utils.Split(userID);

            foreach (var command in commands)
            {
                if (TrySendAndReceive(command, idHigh, idLow, (byte)userPermission, out var loopResponse))
                {
                    if (loopResponse.responseType != ResponseType.Success)
                    {
                        eigenvalues = Span<byte>.Empty;
                        return loopResponse.responseType;
                    }
                }
                else
                {
                    eigenvalues = Span<byte>.Empty;
                    return ResponseType.Timeout;
                }

                Thread.Sleep(50);
            }

            if (TrySendAndReceive(CommandType.AddAndAcquireFingerprint, 0, 0, 0, out var response))
            {
                if (response.responseType != ResponseType.Success)
                {
                    eigenvalues = Span<byte>.Empty;
                    return response.responseType;
                }

                ushort length = Utils.Merge(response.first, response.second);

                var data = ReadData(length);
                eigenvalues = data.AsSpan(3);

                return ResponseType.Success;
            }
            else
            {
                eigenvalues = Span<byte>.Empty;
                return ResponseType.Timeout;
            }
        }

        /// <summary>
        /// Delete a user (fingerprint) from the sensor
        /// </summary>
        /// <param name="userID">A valid user (fingerprint) id</param>
        /// <returns>true if successful, false otherwise</returns>
        public bool DeleteUser(ushort userID)
        {
            (byte high, byte low) = Utils.Split(userID);

            if (TrySendAndReceive(CommandType.DeleteUser, high, low, 0, out var response, 1000))
            {
                return response.responseType == ResponseType.Success;
            }

            return false;
        }

        /// <summary>
        /// Delete all users (fingerprints) from the sensor
        /// </summary>
        /// <returns>true if successful, false otherwise</returns>
        public bool DeleteAllUsers()
        {
            if (TrySendAndReceive(CommandType.DeleteAllUsers, 0, 0, 0, out var response, 1000))
            {
                return response.responseType == ResponseType.Success;
            }

            return false;
        }

        /// <summary>
        /// Delete all users that match a specified <see cref="UserPermission"/>
        /// </summary>
        /// <param name="userPermission"></param>
        /// <returns>true if successful, false otherwise</returns>
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
        /// <returns>true if the fingerprint match</returns>
        public bool Comparison11(ushort userID)
        {
            (byte high, byte low) = Utils.Split(userID);

            if (TrySendAndReceive(CommandType.Comparison11, high, low, 0, out var response))
            {
                return response.responseType == ResponseType.Success;
            }

            return false;
        }

        /// <summary>
        /// Read a fingerprint and check if it match with any registered user
        /// </summary>
        /// <param name="userInfo">The matched user info</param>
        /// <returns>true if the fingerprint match</returns>
        public bool TryComparison1N(out (ushort userID, UserPermission permission) userInfo)
        {
            if (TrySendAndReceive(CommandType.Comparison1N, 0, 0, 0, out var response))
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

        /// <summary>
        /// Get the stored <see cref="UserPermission"/> for a user
        /// </summary>
        /// <param name="userID">A registered user</param>
        /// <param name="userPermission"></param>
        /// <returns>true if the user exist, false otherwise</returns>
        public bool TryQueryPermission(ushort userID, out UserPermission userPermission)
        {
            (byte high, byte low) = Utils.Split(userID);

            if (TrySendAndReceive(CommandType.QueryPermission, high, low, 0, out var reponse, 1000))
            {
                if (reponse.responseType != ResponseType.NoUser)
                {
                    userPermission = (UserPermission)reponse.responseType;

                    return userPermission != 0;
                }
            }

            userPermission = default;

            return false;
        }

        /// <summary>
        /// Retrieve the comparison level used internally by the sensor to compare fingerprints
        /// </summary>
        /// <param name="comparisonLevel"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Set the comparison level used internally by the sensor to compare fingerprints
        /// </summary>
        /// <param name="comparisonLevel">A value in 0..9 range, 9 is the strictest, default is 5</param>
        /// <returns></returns>
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

        /// <summary>
        /// Read a fingerprint and retrieve it's raw image
        /// </summary>
        /// <param name="image"></param>
        /// <returns>true if a valid fingerprint has been read</returns>
        public bool TryAcquireImage(out byte[] image)
        {
             if (TrySendAndReceive(CommandType.AcquireImage, 0, 0, 0, out var response))
             {
                if (response.responseType == ResponseType.Success)
                {
                    var length = Utils.Merge(response.first, response.second);

                    image = ReadData(length, skipChecksum: true);

                    return true;
                }
             }

            image = default;

            return false;
        }

        /// <summary>
        /// Read a fingerprint and retrieve the sensor computed eigenvalues
        /// </summary>
        /// <param name="eigenvalues"></param>
        /// <returns>true if a valid fingerprint has been read</returns>
        public bool TryAcquireEigenvalues(out Span<byte> eigenvalues)
        {
            if (TrySendAndReceive(CommandType.AcquireEigenvalues, 0, 0, 0, out var response))
            {
                if (response.responseType == ResponseType.Success)
                {
                    var length = Utils.Merge(response.first, response.second);

                    eigenvalues = ReadData(length).AsSpan(3);

                    return true;
                }
            }

            eigenvalues = Span<byte>.Empty;

            return false;
        }

        /// <summary>
        /// Retrieve the sensor computed eigenvalues from a registered user
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="eigenvalues"></param>
        /// <param name="userPermission"></param>
        /// <returns></returns>
        public bool TryAcquireUserEigenvalues(ushort userID, out Span<byte> eigenvalues, out UserPermission userPermission)
        {
            (byte high, byte low) = Utils.Split(userID);

            if (TrySendAndReceive(CommandType.AcquireEigenvaluesDSP, high, low, 0, out var response))
            {
                if (response.responseType == ResponseType.Success)
                {
                    var length = Utils.Merge(response.first, response.second);

                    var data = ReadData(length);
                    eigenvalues = data.AsSpan(3);
                    userPermission = (UserPermission)data[2];

                    return true;
                }
            }

            userPermission = default;
            eigenvalues = Span<byte>.Empty;

            return false;
        }

        /// <summary>
        /// Make the sensor sleep, in this mode the sensor use less power (&lt;16 µA) but won't answer commands until it is waked using <see cref="Wake"/>.
        /// <para/>
        /// You can know when to wake the sensor using the <see cref="Waked"/> event that is still triggered while asleep
        /// </summary>
        public void Sleep()
        {
            _sleeping = true;
            _rstPin.Write(GpioPinValue.Low);
        }

        /// <summary>
        /// Wake the sensor, do nothing if it was not sleeping
        /// </summary>
        public void Wake()
        {
            _sleeping = false;
            _rstPin.Write(GpioPinValue.High);

            // Needed after wake to really wake the sensor
            // Because the first command always fail for whatever reason
            TryGetUserCount(out var _);
        }

        private void OnWake()
        {
            if (_wakePin.Read())
            {
                Waked?.Invoke(this);
            }
        }

        /// <summary>
        /// Dispose by disposing the <see cref="SerialPort"/> used
        /// </summary>
        public void Dispose()
        {
            _serialPort.Close();
        }
    }
}

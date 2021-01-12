# Waveshare UART fingerprint sensor (C)

A C# library for the [**Waveshare UART fingerprint sensor (C)**][Sensor], running on
.Net Framework 4.7 (**Mono**) on a **Raspberry Pi**

This library is tested using a Raspberry Pi Zero (hence the use of Mono)
but should work on any Raspberry.  
It should also be easily portable to any device that supports Mono
or an equivalent (*.Net Core*).

## Usage

- First install it from [**nuget**](https://www.nuget.org/packages/WaveshareUARTFingerprintSensor/)

Then you only need to start the sensor

```csharp
// PrimarySerialPort refers to /dev/ttyAMA0
// SecondarySerialPort refers to /dev/ttyS0
// You need to choose it according to your Raspberry
// Check the table below
var sensor = new FingerprintSensor(FingerprintSensor.PrimarySerialPort);

sensor.Start();

// Do any command

// Example: get the user count
if (sensor.TryGetUserCount(out ushort count))
{
    Console.WriteLine($"User count: {count}");
}
```

Here is a table of which serial port to use on which Raspberry Pi,
it may be different for you
| Model     | Port                   |
| --------- | ---------------------- |
| Pi Zero   | Primary (/dev/ttyAMA0) |
| Pi Zero W | Secondary (/dev/ttyS0) |
| Pi 1      | Primary (/dev/ttyAMA0) |
| Pi 2      | Primary (/dev/ttyAMA0)   |
| Pi 3      | Secondary (/dev/ttyS0) |
| Pi Zero 4 | Secondary (/dev/ttyS0) |

> The Secondary UART is **disabled by default**, you an activate it in `raspi-config`  
> [**Source**](https://www.raspberrypi.org/documentation/configuration/uart.md)

## Sample App

You can find a [**sample app**](WaveshareUARTFingerprintSensor.Sample) which shows basic usages of
this library and may help you to understand how to use it

## Contributing

If you have a feature idea or want to report a bug, don't hesitate to create a new
[**Issue**](https://github.com/Eveldee/WaveshareUARTFingerprintSensor/issues) or do a
[**Pull Request**](https://github.com/Eveldee/WaveshareUARTFingerprintSensor/pulls)

## Copyright and license

*[**WaveshareUARTFingerprintSensor**](README.md)* library is licensed under the [MIT License](LICENSE).

*[**Unosquare.Raspberry.IO**](https://github.com/migueldeicaza/gui.cs/)* library is under the [MIT License](https://github.com/unosquare/raspberryio/blob/master/LICENSE).


<!-- Links -->
[Sensor]: https://www.waveshare.com/wiki/UART_Fingerprint_Sensor_(C)
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WaveshareUARTFingerprintSensor
{
    public enum ResponseType : byte
    {
        Success = 0x00,
        Fail = 0x01,
        Full = 0x04,
        NoUser = 0x05,
        UserOccupied = 0x06,
        FingerOccupied = 0x07,
        Timeout = 0x08
    }
}

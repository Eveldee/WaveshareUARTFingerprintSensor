using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WaveshareUARTFingerprintSensor
{
    /// <summary>
    /// Command flags recognized by the sensor
    /// </summary>
    public enum CommandType : byte
    {
        ModifySerialNumber = 0x08,
        QuerySerialNumber = 0x2A,
        SleepMode = 0x2C,
        ManageFingerprintAddingMode = 0x2D,
        AddFingerprint1 = 0x01,
        AddFingerprint2 = 0x02,
        AddFingerprint3 = 0x03,
        AddAndAcquireFingerprint = 0x06,
        DeleteUser = 0x04,
        DeleteAllUsers = 0x05,
        QueryUserCount = 0x09,
        Comparison11 = 0x0B,
        Comparison1N = 0x0C,
        QueryPermission = 0x0A,
        ManageComparisonLevel = 0x28,
        AcquireImage = 0x24,
        AcquireEigenvalues = 0x23,
        UploadEigenvaluesAndCompare = 0x44,
        UploadEigenvaluesAndCompare11 = 0x42,
        UploadEigenvaluesAndCompare1N = 0x43,
        AcquireEigenvaluesDSP = 0x31,
        CreateUserFromEigenvalues = 0x41,
        QueryUsersInfo = 0x2B,
        ManageCaptureTimeout = 0x2E
    }
}

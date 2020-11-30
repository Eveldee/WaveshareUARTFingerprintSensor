using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WaveshareUARTFingerprintSensor.Exceptions
{
    [Serializable]
    public class SensorStateException : Exception
    {
        public const string SensorSleepingMessage = "Sensor is sleeping, can't send commands";

        public SensorStateException() { }
        public SensorStateException(string message) : base(message) { }
        public SensorStateException(string message, Exception inner) : base(message, inner) { }
        protected SensorStateException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}

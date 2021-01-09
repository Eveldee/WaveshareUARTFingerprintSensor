using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Terminal.Gui;

namespace WaveshareUARTFingerprintSensor.Sample
{
    public class SleepDisplay : Toplevel
    {
        private FingerprintSensor _fingerprintSensor;
        private int _count;
        private int _lastID;
        private Label _sleepModeLabel;
        private Label _readCountLabel;
        private Label _lastReadLabel;

        public SleepDisplay(FingerprintSensor fingerprintSensor)
        {
            _fingerprintSensor = fingerprintSensor;
            _count = 0;
            _lastID = -1;

            Init();
        }

        private void Init()
        {
            ColorScheme = Colors.Error;

            // Creates the top-level window to show
            var win = new Window("TUIManager")
            {
                X = 0,
                Y = 0,

                // By using Dim.Fill(), it will automatically resize without manual intervention
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };

            win.ColorScheme = Colors.ColorSchemes["Dialog"];

            Add(win);

            // Window Content
            _sleepModeLabel = new Label("Sleep mode is on, waiting for fingerprints...")
            {
                X = 2,
                Y = 1,
                Width = Dim.Fill()
            };
            _readCountLabel = new Label("Read: 0")
            {
                X = Pos.Left(_sleepModeLabel),
                Y = Pos.Bottom(_sleepModeLabel) + 1,
                Width = Dim.Fill()
            };
            _lastReadLabel = new Label("Last User: - 1")
            {
                X = Pos.Left(_readCountLabel),
                Y = Pos.Bottom(_readCountLabel),
                Width = Dim.Fill()
            };

            var stopButton = new Button("_Stop")
            {
                X = Pos.Right(this) - 11,
                Y = Pos.Bottom(this) - 2
            };
            stopButton.Clicked += () => { _fingerprintSensor.Waked -= FingerprintSensor_Waked; Application.RequestStop(); };

            win.Add(_sleepModeLabel, _readCountLabel, _lastReadLabel);

            Add(stopButton);

            _fingerprintSensor.Waked += FingerprintSensor_Waked;

            _fingerprintSensor.Sleep();
        }

        private void UpdateInfo()
        {
            _readCountLabel.Text = $"Read: {_count}";
            _lastReadLabel.Text = $"Last User: {_lastID}";
        }

        private void FingerprintSensor_Waked(FingerprintSensor sender)
        {
            _fingerprintSensor.Wake();

            if (_fingerprintSensor.TryComparison1N(out var userInfo))
            {
                _count += 1;
                _lastID = userInfo.userID;

                Application.MainLoop.Invoke(UpdateInfo);
            }

            _fingerprintSensor.Sleep();
        }
    }
}

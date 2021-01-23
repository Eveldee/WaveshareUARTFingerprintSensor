using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Terminal.Gui;

namespace WaveshareUARTFingerprintSensor.Sample.Views
{
    public class SettingsDisplay : Toplevel
    {
        private string _port;
        private RadioGroup _radioPort;

        public SettingsDisplay()
        {
            Init();
        }

        private void Init()
        {
            Modal = true;

            ColorScheme = Colors.Error;

            // Creates the top-level window to show
            var win = new Window("Settings")
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
            var portLabel = new Label("Serial Port:")
            {
                X = 4,
                Y = 3
            };
            _radioPort = new RadioGroup(new NStack.ustring[] { FingerprintSensor.PrimarySerialPort, FingerprintSensor.SecondarySerialPort })
            {
                X = Pos.Right(portLabel) + 2,
                Y = Pos.Top(portLabel),
                Width = Dim.Fill()
            };

            var saveButton = new Button("_Save")
            {
                X = Pos.Right(this) - 14,
                Y = Pos.Bottom(this) - 4
            };
            saveButton.Clicked += () => { Save();  Application.RequestStop(); };

            win.Add(portLabel, _radioPort, saveButton);
        }

        private void Save()
        {
            _port = _radioPort.RadioLabels[_radioPort.SelectedItem].ToString();

            File.WriteAllText(TUIManager.SettingsFilePath, _port);
        }
    }
}

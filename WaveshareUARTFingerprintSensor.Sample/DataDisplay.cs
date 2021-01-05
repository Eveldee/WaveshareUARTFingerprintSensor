using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terminal.Gui;

namespace WaveshareUARTFingerprintSensor.Sample
{
    public class DataDisplay : Toplevel
    {
        private string _title;
        private byte[] _data;

        public DataDisplay(string title, byte[] data)
        {
            _title = title;
            _data = data;

            Init();
        }

        private void Init()
        {
            ColorScheme = Colors.TopLevel;

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

            var quitButton = new Button("_Ok")
            {
                X = Pos.Right(this) - 9,
                Y = Pos.Bottom(this) - 2
            };
            quitButton.Clicked += () => Application.RequestStop();

            var stream = new MemoryStream(_data);
            var text = new HexView(stream)
            {
                X = Pos.Center(),
                Y = Pos.Center(),
                Height = Dim.Fill() - 5,
                Width = Dim.Fill() - 2,
                AllowEdits = false
            };

            Add(text, quitButton);
        }
    }
}

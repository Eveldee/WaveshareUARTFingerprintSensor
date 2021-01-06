using NStack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terminal.Gui;

namespace WaveshareUARTFingerprintSensor.Sample
{
    public class FingerprintDialog : Dialog
    {
        public string ErrorTitle { get; set; }
        public string ErrorMessage { get; set; }

        public FingerprintDialog(string errorTitle, string errorMessage) : base(errorTitle, 60, 7)
        {
            ErrorTitle = errorTitle;
            ErrorMessage = errorMessage;

            ColorScheme = Colors.ColorSchemes["Menu"];
        }

        public void Show()
        {
            var label = new Label("Please place your finger flat on the sensor")
            {
                X = Pos.Center(),
                Y = Pos.Center(),
                Height = 1
            };

            Add(label);

            Application.Run(this);
        }

        public void Cancel()
        {
            Application.MainLoop.Invoke(() => Application.RequestStop());
        }

        public void CancelAndShowError()
        {
            Application.MainLoop.Invoke(() =>
            {
                MessageBox.ErrorQuery(ErrorTitle, ErrorMessage, "Ok");

                Application.RequestStop();
            });
        }
    }
}

using NStack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terminal.Gui;

namespace WaveshareUARTFingerprintSensor.Sample
{
    public class EntryDialog : Dialog
    {
        private Func<ustring, bool> _validator;
        private bool _success;
        private string _title;
        private string _errorMessage;

        public EntryDialog(string title, Func<ustring, bool> validator = null, string errorMessage = "") : base(title, 60, 7)
        {
            _title = title;
            _errorMessage = errorMessage;

            _validator = validator;

            ColorScheme = Colors.ColorSchemes["Menu"];
        }

        public bool TryShow(out ustring input)
        {
            _success = false;

            var levelEntry = new TextField("")
            {
                X = 1,
                Y = 2,
                Width = Dim.Fill(),
                Height = 1
            };

            var cancelButton = new Button("_Cancel")
            {
                X = Pos.Percent(82),
                Y = Pos.Percent(95)
            };
            cancelButton.Clicked += () => Application.RequestStop();


            var okButton = new Button("_Ok")
            {
                X = Pos.Percent(70),
                Y = Pos.Percent(95)
            };
            okButton.Clicked += () => CheckInput(levelEntry.Text);

            Add(levelEntry, okButton, cancelButton);

            levelEntry.SetFocus();

            Application.Run(this);

            if (_success)
            {
                input = levelEntry.Text;

                return true;
            }

            input = default;

            return false;
        }

        private void CheckInput(ustring input)
        {
            if (_validator?.Invoke(input) ?? true)
            {
                _success = true;

                Application.RequestStop();
            }
            else
            {
                MessageBox.ErrorQuery(_title, _errorMessage, "Ok");
            }
        }
    }
}

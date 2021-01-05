using NStack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terminal.Gui;

namespace WaveshareUARTFingerprintSensor.Sample
{
    public class TUIManager : Toplevel
    {
        private readonly FingerprintSensor _fingerprintSensor;
        private Label _comparisonLevelLabel;
        private Label _userCountLabel;

        public TUIManager()
        {
            _fingerprintSensor = Program.FingerprintSensor;

            Init();
        }

        private void Init()
        {
            ColorScheme = Colors.Error;

            // Creates the top-level window to show
            var win = new Window("TUIManager")
            {
                X = 0,
                Y = 1, // Leave one row for the toplevel menu

                // By using Dim.Fill(), it will automatically resize without manual intervention
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };

            win.ColorScheme = Colors.ColorSchemes["Dialog"];

            Add(win);

            // Creates a menubar, the item "New" has a help menu.
            var menu = new MenuBar(new MenuBarItem[] {
                new MenuBarItem ("_Options", new MenuItem [] {
                    new MenuItem ("Reset Config", "", () => { ResetConfig(); }),
                    new MenuItem ("_Quit", "", () => { Application.RequestStop(); })
                })
            });

            Add(menu);

            // Window Content
            _comparisonLevelLabel = new Label("Comparison Level: 0")
            {
                X = 2,
                Y = 1,
                Width = Dim.Fill()
            };
            _userCountLabel = new Label("Users: 0")
            {
                X = Pos.Left(_comparisonLevelLabel),
                Y = Pos.Bottom(_comparisonLevelLabel),
                Width = Dim.Fill()
            };

            var userCountButton = new Button("Query _User Count")
            {
                X = Pos.Center(),
                Y = 7
            };
            userCountButton.Clicked += UserCountButton_Clicked;

            var readFingerprintButton = new Button("_Read Fingerprint")
            {
                X = Pos.Center(),
                Y = Pos.Bottom(userCountButton) + 1
            };
            readFingerprintButton.Clicked += ReadFingerprintButton_Clicked;

            var readEigenvaluesButton = new Button("Read _Eigenvalues")
            {
                X = Pos.Center(),
                Y = Pos.Bottom(readFingerprintButton)
            };
            readEigenvaluesButton.Clicked += ReadEigenvaluesButton_Clicked;

            var readImageButton = new Button("Read _Image")
            {
                X = Pos.Center(),
                Y = Pos.Bottom(readEigenvaluesButton)
            };
            readImageButton.Clicked += ReadImageButton_Clicked;

            var addFingerprintButton = new Button("_Add Fingerprint")
            {
                X = Pos.Center(),
                Y = Pos.Bottom(readImageButton) + 1
            };
            addFingerprintButton.Clicked += AddFingerprintButton_Clicked;

            var deleteAFingerprint = new Button("_Delete a Fingerprint")
            {
                X = Pos.Center(),
                Y = Pos.Bottom(addFingerprintButton)
            };
            deleteAFingerprint.Clicked += DeleteAFingerprint_Clicked;

            var clearFingerprintsButton = new Button("_Clear Fingerprints")
            {
                X = Pos.Center(),
                Y = Pos.Bottom(deleteAFingerprint)
            };
            clearFingerprintsButton.Clicked += ClearFingerprintsButton_Clicked;

            var setComparisonLevelButton = new Button("Set Comparison _Level")
            {
                X = Pos.Center(),
                Y = Pos.Bottom(clearFingerprintsButton) + 1
            };
            setComparisonLevelButton.Clicked += SetComparisonLevelButton_Clicked;

            var sleepButton = new Button("_Sleep Mode")
            {
                X = Pos.Center(),
                Y = Pos.Bottom(setComparisonLevelButton) + 1
            };
            sleepButton.Clicked += SleepButton_Clicked;

            var quitButton = new Button("_Quit")
            {
                X = Pos.Center(),
                Y = Pos.Percent(95)
            };
            quitButton.Clicked += Quit_Clicked;

            win.Add(
                _comparisonLevelLabel,
                _userCountLabel,
                userCountButton,
                readFingerprintButton,
                readEigenvaluesButton,
                readImageButton,
                addFingerprintButton,
                deleteAFingerprint,
                clearFingerprintsButton,
                setComparisonLevelButton,
                sleepButton,
                quitButton
            );

            UpdateUserCount();
            UpdateComparisonLevel();

            // TODO Config at first start
        }

        private void UpdateUserCount()
        {
            if (_fingerprintSensor.TryGetUserCount(out ushort count))
            {
                _userCountLabel.Text = $"Users: {count}";
            }
        }

        private void UpdateComparisonLevel()
        {
            if (_fingerprintSensor.TryGetComparisonLevel(out byte comparisonLevel))
            {
                _comparisonLevelLabel.Text = $"Comparison Level: {comparisonLevel}";
            }
        }

        private void ReadImageButton_Clicked()
        {
            if (_fingerprintSensor.TryAcquireImage(out var image))
            {
                var window = new DataDisplay("Image", image.ToArray());

                Application.Run(window);
            }
            else
            {
                MessageBox.ErrorQuery("Acquire Image", "Can't acquire image, try to place your finger flat on the sensor", "Ok");
            }
        }

        private void ReadEigenvaluesButton_Clicked()
        {
            // TODO Fingerprint popup to know it is waiting for a fingerprint

            if (_fingerprintSensor.TryAcquireEigenvalues(out var eigenvalues))
            {
                var window = new DataDisplay("Eigenvalues", eigenvalues.ToArray());

                Application.Run(window);
            }
            else
            {
                MessageBox.ErrorQuery("Acquire Eigenvalues", "Can't acquire eigenvalues, try to place your finger flat on the sensor", "Ok");
            }
        }

        private void SetComparisonLevelButton_Clicked()
        {
            if (new EntryDialog("Comparison Level", i => int.TryParse(i.ToString(), out var n) && n > 0 && n < 10, "Need to be a valid number in 0-9 range").TryShow(out var input))
            {
                if (!_fingerprintSensor.TrySetComparisonLevel(byte.Parse(input.ToString())))
                {
                    MessageBox.ErrorQuery("Comparison Level", "Can't set comparison level", "Ok");
                }
            }

            UpdateComparisonLevel();
        }

        private void DeleteAFingerprint_Clicked()
        {
            if (new EntryDialog("User id", i => int.TryParse(i.ToString(), out var n), "Need to be a valid user id").TryShow(out var input))
            {
                if (!_fingerprintSensor.DeleteUser(ushort.Parse(input.ToString())))
                {
                    MessageBox.ErrorQuery("Delete User", "Can't delete user, check user id", "Ok");
                }
            }

            UpdateUserCount();
        }

        private void SleepButton_Clicked()
        {
            //TODO
        }

        private void ClearFingerprintsButton_Clicked()
        {
            if (!_fingerprintSensor.DeleteAllUsers())
            {
                MessageBox.ErrorQuery("Delete All Users", "Can't delete all user", "Ok");
            }
        }

        private void AddFingerprintButton_Clicked()
        {
            if (new EntryDialog("User id", i => int.TryParse(i.ToString(), out var n), "Need to be a valid user id").TryShow(out var input))
            {
                Console.WriteLine(input);
                // TODO
            }
        }

        private void ReadFingerprintButton_Clicked()
        {
            //TODO
        }

        private void UserCountButton_Clicked()
        {
            UpdateUserCount();
        }

        private void ResetConfig()
        {
            MessageBox.Query("Reset Config", "Config reset", "OK");

            //TODO
        }

        private void Quit_Clicked()
        {
            Application.RequestStop();
        }
    }
}

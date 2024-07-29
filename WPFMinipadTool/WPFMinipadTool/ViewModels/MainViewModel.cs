using DevExpress.Mvvm;
using MinipadWPFTest.Models;
using MinipadWPFTest.Utils;
using System;
using Serilog;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;


namespace MinipadWPFTest.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private Dispatcher _dispatcher;

        public bool IsBusy
        {
            get => GetValue<bool>();
            set { SetValue(value); }
        }

        private static readonly ILogger log = new LoggerConfiguration().WriteTo.File("errorlog-.txt", 
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:}{NewLine}{Exception}", rollingInterval: RollingInterval.Day).CreateLogger();

        public MainViewModel() 
        {
            AddAnalogKeyCommand = new DelegateCommand(AddAnalogKey);
            RemoveAnalogKeyCommand = new DelegateCommand(RemoveAnalogKey);

            AddDigitalKeyCommand = new DelegateCommand(AddDigitalKey);
            RemoveDigitalKeyCommand = new DelegateCommand(RemoveDigitalKey);
            ApplyHEKeysCommand = new AsyncCommand(ApplyHEKeysCall, CanApply);
            GetKeyValuesCommand = new DelegateCommand(GetKeyValues);
            
            RefreshMinipadsCommand = new DelegateCommand(RefreshMinipads);

            StartVisualizationCommand = new DelegateCommand(StartVisualization, CanVisualize);
            VisualizeButtonText = "Start visualization";

            CalibrationCommand = new DelegateCommand(CalibrateState, CanCalibrate);
            CalibrationText = "Start calibration";
            EnableChangingComport = true;

            TestCommand = new DelegateCommand(Test);

            MinipadPorts = new();
            BindingOperations.EnableCollectionSynchronization(MinipadPorts, _minipadPortsSync);

            _dispatcher = Application.Current.Dispatcher;
        }

        public void HandleException(DispatcherUnhandledExceptionEventArgs e)
        {
            log.Error(PrepareExceptionLogMessage(e.Exception));
            MessageBox.Show("An unhandled exception has been thrown.\nPlease report it to the developer and include the errorlog-%date%.log file located beside this exe", "Unhandled exception");
        }

        private string PrepareExceptionLogMessage(Exception exception)
        {
            return "Unhandled exception caught.\n" + PrepareExceptionMessage(exception);
        }

        private string PrepareExceptionMessage(Exception e)
        {
            string message = $"Exception: {e.GetType()}\nStackTrace: {e.StackTrace}\nSSource: {e.Source}\nMessage: {e.Message}";
            if(e.InnerException != null)
            {
                message += "\n" + PrepareExceptionMessage(e.InnerException);
            }

            return message;
        }

        public ICommand TestCommand { get; private set; }
        private async void Test()
        {

        }

        #region Calibrating

        public ICommand CalibrationCommand { get; private set; }
        
        private CalibrationState _calibrationState = CalibrationState.None;

        public bool EnableChangingComport
        {
            get => GetValue<bool>();
            set => SetValue(value);
        }
        public string CalibrationText
        {
            get => GetValue<string>();
            set => SetValue(value);
        }

        private int[] _rawRestValues;
        private int[] _rawDownValues;
        
        private async void CalibrateState()
        {
            switch (_calibrationState)
            {
                case CalibrationState.None:
                    {
                        var minipadValues = await MinipadHelper.GetMinipad(_selectedComPort);
                        if (minipadValues["state"] == "connected")
                        {
                            int heKeyCount = int.Parse(minipadValues["hkeys"]);

                            _rawRestValues = new int[heKeyCount];
                            _rawDownValues = new int[heKeyCount];

                            await MinipadHelper.SendCommand(_selectedComPort, "hkey.hid false");

                            EnableUI = false;
                            EnableChangingComport = false;
                            _calibrationState = CalibrationState.AllRelease;
                            CalibrationText = "Click when all released";
                        }
                    }
                    break;
                case CalibrationState.AllRelease:
                    {
                        var minipadValues = await MinipadHelper.GetMinipad(_selectedComPort);

                        if (minipadValues["state"] == "connected")
                        {
                            int heKeyCount = int.Parse(minipadValues["hkeys"]);
                            var vals = MinipadHelper.GetSensorValues(heKeyCount, _selectedComPort);
                            for(int i = 0; i < heKeyCount; i++)
                            {
                                _rawRestValues[i] = vals.RawValues[i];
                            }
                            _calibrationState = CalibrationState.AllPressed;
                            CalibrationText = "Click when all pressed";
                        }
                    }
                    break;
                case CalibrationState.AllPressed:
                    {
                        var minipadValues = await MinipadHelper.GetMinipad(_selectedComPort);

                        if (minipadValues["state"] == "connected")
                        {
                            int heKeyCount = int.Parse(minipadValues["hkeys"]);
                            var vals = MinipadHelper.GetSensorValues(heKeyCount, _selectedComPort);
                            for (int i = 0; i < heKeyCount; i++)
                            {
                                _rawDownValues[i] = vals.RawValues[i];
                            }
                            ShowCalibrationValues(minipadValues, heKeyCount);
                            await MinipadHelper.SendCommand(_selectedComPort, "hkey.hid true");
                        }
                    }
                    break;
            }
        }

        private void ShowCalibrationValues(Dictionary<string, string> minipadValues, int heKeyCount)
        {
            var msg = "";
            for (int i = 0; i < heKeyCount; i++)
            {
                var rest = minipadValues[$"hkey{i + 1}.rest"];
                var down = minipadValues[$"hkey{i + 1}.down"];
                msg += $"Key {i}\nReleased: {_rawRestValues[i]}(prev.: {rest})\nPressed: {_rawDownValues[i]}(prev.: {down})\n";
                if (i + 1 == heKeyCount)
                {
                    msg.TrimEnd();
                }
            }

            var result = MessageBox.Show("These values have been measured:\n" + msg + "\nWould you like to save these results?", "Calibration result", MessageBoxButton.YesNo);
            if (result == MessageBoxResult.Yes)
            {
                // Save calibration results
                SaveCalibrationResults();
            }
            EnableUI = true;
            EnableChangingComport = true;
            _calibrationState = CalibrationState.None;
            CalibrationText = "Calibrate";
        }

        private async void SaveCalibrationResults()
        {
            var minipadValues = await MinipadHelper.GetMinipad(_selectedComPort);

            if (minipadValues["state"] == "connected")
            {
                int heKeyCount = int.Parse(minipadValues["hkeys"]);
                List<string> commands = new();
                for (int i = 0; i < heKeyCount; i++)
                {
                    commands.Add($"hkey{i + 1}.rest {_rawRestValues[i]}");
                    commands.Add($"hkey{i + 1}.down {_rawDownValues[i]}");
                }
                commands.Add("save");

                var ret = await MinipadHelper.SendCommands(_selectedComPort, commands);
            }
        }

        private bool CanCalibrate()
        {
            if (!MinipadPorts.Any())
                return false;

            if (MinipadPorts[SelectedComDeviceIndex].Contains("disconnected") || MinipadPorts[SelectedComDeviceIndex].Contains("busy"))
            {
                return false;
            }

            if(ShouldVisualize)
                return false;

            return true;
        }

        #endregion

        #region Visualize

        public bool ShouldVisualize
        {
            get => GetValue<bool>();
            set => SetValue(value);
        }

        public ICommand StartVisualizationCommand { get; private set; }
        public string VisualizeButtonText
        {
            get => GetValue<string>();
            set => SetValue(value);
        }

        private async void StartVisualization()
        {
            if (ShouldVisualize)
            {
                ShouldVisualize = false;
                VisualizeButtonText = "Start Visualize";
            }
            else
            {
                ShouldVisualize = true;
                VisualizeButtonText = "Stop Visualize";
                await VisualizeTask(_selectedComPort);
            }
        }

        private bool CanVisualize()
        {
            if (!MinipadPorts.Any())
                return false;

            if (MinipadPorts[SelectedComDeviceIndex].Contains("disconnected") || MinipadPorts[SelectedComDeviceIndex].Contains("busy"))
            {
                return false;
            }

            return true;
        }

        private async Task VisualizeTask(int comport)
        {
            var visualizingIndex = SelectedComDeviceIndex;
            var minipadValues = await MinipadHelper.GetMinipad(comport);
            if (minipadValues["state"] == "connected")
            {
                int heKeyCount = int.Parse(minipadValues["hkeys"]);
                while (ShouldVisualize)
                {
                    if (visualizingIndex != SelectedComDeviceIndex)
                    {
                        StartVisualization();
                        break;
                    }
                    var vals = MinipadHelper.GetSensorValues(heKeyCount, comport);
                    for(int i = 0; i < heKeyCount; i++)
                    {
                        var key = HKeys[i];
                        //key.Value = vals.Item2[i];
                        var value = (float)(400 - vals.MappedValues[i]);
                        key.ValueText = $"{Math.Round(value / 100, 2)} mm pressed";
                    }
                    await Task.Delay(10);
                }

                for (int i = 0; i < heKeyCount; i++)
                {
                    HKeys[i].ValueText = "-";
                }
            }
            else
            {
                // HUH?
                foreach(var key in HKeys)
                {
                    key.Value = -1;
                }
            }
        }

        #endregion

        #region HandlingComDevices

        public ICommand RefreshMinipadsCommand { get; private set; }
        private async void RefreshMinipads()
        {
            var selected = -1;
            if (MinipadPorts.Any())
            {
                selected = GetComportFromName(MinipadPorts[SelectedComDeviceIndex]);
                MinipadPorts.Clear();
            }
            var test = RegistryHelper.GetPortsByIDs(0x727, 0x727);
            foreach (var i in test)
            {
                var device = await MinipadHelper.GetMinipad(i);
                if (device["state"] == "connected")
                {
                    if (device.ContainsKey("name"))
                    {
                        MinipadPorts.Add($"{device["name"]} (COM{i}) {device["state"]}");
                    }
                    else
                    {
                        MinipadPorts.Add($"COM{i} {device["state"]}");
                    }
                }
                else
                {
                    MinipadPorts.Add($"COM{i} {device["state"]}");
                }
                if(selected != -1 && i == selected)
                {
                    selected = MinipadPorts.Count - 1;
                }
            }
            if(selected != -1)
            {
                SelectedComDeviceIndex = selected;
            }
            else
            {
                SelectedComDeviceIndex = 0;
            }
        }
        public bool EnableUI
        {
            get => GetValue<bool>();
            set => SetValue(value);
        }
        public int SelectedComDeviceIndex
        {
            get => GetValue<int>();
            set
            {
                SetValue(value);
                if (!MinipadPorts.Any())
                    return;
                if (MinipadPorts[value].Contains("disconnected") || MinipadPorts[value].Contains("busy"))
                {
                    EnableUI = false;
                    HKeys.Clear();
                }
                else
                {
                    EnableUI = true;
                    _selectedComPort = GetComportFromName(MinipadPorts[value]);
                    GetKeysAndValues();
                }
            }
        }

        private async void GetKeysAndValues()
        {
            if (_selectedComPort == -1)
                return;

            var minipadValues = await MinipadHelper.GetMinipad(_selectedComPort);
            var state = minipadValues["state"];
            if (state == "connected")
            {
                // yippi
                // get shit
                HKeys.Clear();
                minipadValues.TryGetValue("hkeys", out string hKeyCount);
                int heKeyCount = int.Parse(minipadValues["hkeys"]);
                for (int i = 0; i < heKeyCount; i++)
                {
                    var key = new HKeyViewModel("HKey " + (i + 1));
                    var keyVar = $"hkey{i + 1}";
                    key.RapidTrigger = minipadValues[$"{keyVar}.rt"] == "0" ? false : true;
                    key.ContinuousRapidTrigger = minipadValues[$"{keyVar}.crt"] == "0" ? false : true;
                    key.RapidTriggerUpSens = double.Parse(minipadValues[$"{keyVar}.rtus"]) / 100;
                    key.RapidTriggerDownSens = double.Parse(minipadValues[$"{keyVar}.rtds"]) / 100;
                    key.LowerHysteresis = double.Parse(minipadValues[$"{keyVar}.lh"]) / 100;
                    key.UpperHysteresis = double.Parse(minipadValues[$"{keyVar}.uh"]) / 100;

                    var kkey = GetCharacter(minipadValues, $"{keyVar}.char");
                    key.Key = new HotKey((Key)Enum.Parse(typeof(Key), kkey.ToString().ToUpper()));
                    HKeys.Add(key);
                }
            }
            else
            {
                // Busy/disconnected
                MessageBox.Show("State: " + minipadValues["state"]);
            }
        }

        private int GetComportFromName(string name)
        {
            var temp = name.Substring(name.IndexOf("COM") + 3);
            return int.Parse(temp.Split(new char[] { ' ', ')' })[0]);
        }

        private int _selectedComPort = -1;

        #endregion

        #region GetHEKeyRegion
        public ICommand GetKeyValuesCommand { get; private set; }
        private async void GetKeyValues()
        {
            if (_selectedComPort == -1)
                return;

            var minipadValues = await MinipadHelper.GetMinipad(_selectedComPort);
            var state = minipadValues["state"];
            if(state == "connected")
            {
                // yippi
                // get shit
                minipadValues.TryGetValue("hkeys", out string hKeyCount);
                int heKeyCount = int.Parse(minipadValues["hkeys"]);
                for(int i = 0; i < heKeyCount; i++)
                {
                    var key = HKeys[i];
                    var keyVar = $"hkey{i + 1}";
                    key.RapidTrigger            = minipadValues[$"{keyVar}.rt"] == "0" ? false : true;
                    key.ContinuousRapidTrigger  = minipadValues[$"{keyVar}.crt"] == "0" ? false : true;
                    key.RapidTriggerUpSens      = double.Parse(minipadValues[$"{keyVar}.rtus"]) / 100;
                    key.RapidTriggerDownSens    = double.Parse(minipadValues[$"{keyVar}.rtds"]) / 100;
                    key.LowerHysteresis         = double.Parse(minipadValues[$"{keyVar}.lh"]) / 100;
                    key.UpperHysteresis         = double.Parse(minipadValues[$"{keyVar}.uh"]) / 100;

                    var kkey = GetCharacter(minipadValues, $"{keyVar}.char");
                    key.Key = new HotKey((Key)Enum.Parse(typeof(Key), kkey.ToString().ToUpper()));
                }
            }
            else
            {
                // Busy/disconnected
                MessageBox.Show("State: " + minipadValues["state"]);
            }
        }
        private char GetCharacter(Dictionary<string, string> dict, string key)
        {
            bool successful = int.TryParse(dict[key], out int result) && result <= char.MaxValue;
            return successful ? (char)(object)(char)result : 'z';
        }

        #endregion

        #region ListOfMinipads

        private async void GetMinipads()
        {
            var ports = RegistryHelper.GetPortsByIDs(0x727, 0x727);
            MinipadPorts.Clear();
            foreach (var port in ports)
            {
                var data = await MinipadHelper.GetMinipad(port);
                MinipadPorts.Add("COM" + port + "(" + data["state"] + ")");
            }
        }

        public ObservableCollection<string> MinipadPorts { get; set; }
        private readonly object _minipadPortsSync = new object();

        #endregion

        #region ApplyHeKeys

        public ICommand ApplyHEKeysCommand { get; set; }

        private async Task ApplyHEKeysCall()
        {
            await Task.Run(() => ApplyHEKeys());
        }

        private bool CanApply()
        {
            return !IsBusy;
        }

        private async Task ApplyHEKeys()
        {
            if (_selectedComPort == -1)
                return;

            IsBusy = true;
            EnableUI = false;
            int keynum = 1;
            foreach (var key in this.HKeys)
            {
                await SaveKey(_selectedComPort, key, keynum);
                keynum++;
            }

            _dispatcher.Invoke(() =>
            {
                MessageBox.Show("Done!");
            });
            IsBusy = false;
            EnableUI = true;
        }

        #endregion

        #region SavingKey
        private async Task SaveKey(int comport, HKeyViewModel key, int keynum)
        {
            var commands = new List<string>()
            {
                $"hkey{keynum}.rt {(key.RapidTrigger ? "1" : "0" )}" ,
                $"hkey{keynum}.crt {(key.ContinuousRapidTrigger ? "1" : "0" )}",
                $"hkey{keynum}.rtus {key.RTUS}",
                $"hkey{keynum}.rtds {key.RTDS}",
                $"hkey{keynum}.uh {key.UH}",
                $"hkey{keynum}.lh {key.LH}",
                $"hkey{keynum}.uh {key.UH}",
                $"hkey{keynum}.lh {key.LH}",
                $"hkey{keynum}.char {key.Key.Key.ToString()}",
                $"hkey{keynum}.hid 1",
                "save"
            };
            int failed = 0;

            var ret = await MinipadHelper.SendCommands(comport, commands);
            if (ret != 0)
            {
                // HUH?
                failed++;
            }
        }

        #endregion

        #region AnalogKeys
        public ObservableCollection<HKeyViewModel> HKeys { get; set; } = new();

        public ICommand AddAnalogKeyCommand { get; set; }
        public ICommand RemoveAnalogKeyCommand { get; set; }

        private void AddAnalogKey()
        {
            if (HKeys.Count < 4)
            {
                HKeys.Add(new HKeyViewModel("Key " + (HKeys.Count + 1)));
            }
        }

        private void RemoveAnalogKey()
        {
            if (HKeys.Count > 0)
            {
                HKeys.RemoveAt(HKeys.Count - 1);
            }
        }

        #endregion

        #region DigitalKeys
        public ObservableCollection<DKeyViewModel> DKeys { get; set; } = new();
        public ICommand AddDigitalKeyCommand { get; set; }
        public ICommand RemoveDigitalKeyCommand { get; set; }

        private void AddDigitalKey()
        {
            if (DKeys.Count < 26)
            {
                DKeys.Add(new DKeyViewModel("Key " + (DKeys.Count + 1)));
            }
        }

        private void RemoveDigitalKey()
        {
            if (DKeys.Count > 0)
            {
                DKeys.RemoveAt(DKeys.Count - 1);
            }
        }

        #endregion

    }
}

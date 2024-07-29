using DevExpress.Mvvm;
using MinipadWPFTest.Models;

namespace MinipadWPFTest.ViewModels
{
    public class HKeyViewModel : ViewModelBase
    {
        public const double HYST_MIN = 0.1;
        public const double HYST_MAX = 3.9;
        public HKeyViewModel() 
        {
            Key = new HotKey(System.Windows.Input.Key.Z);
            RapidTriggerDownSens = 0.1;
            RapidTriggerUpSens = 0.1;
            LowerHysteresis = 0.1;
        }

        public HKeyViewModel(string name) : this()
        {
            KeyName = name;
        }

        public int Value
        {
            get => GetValue<int>();
            set => SetValue(value);
        }
        public string ValueText
        {
            get => GetValue<string>();
            set => SetValue(value);
        }
        public string ValueContent
        {
            get => GetValue<string>();
            set => SetValue(value);
        }
        public string KeyName 
        { 
            get { return GetValue<string>(); }
            set { SetValue(value); } 
        }
        public bool RapidTrigger
        { 
            get { return GetValue<bool>(); }
            set { SetValue(value); } 
        }
        public bool ContinuousRapidTrigger
        { 
            get { return GetValue<bool>(); }
            set { SetValue(value); } 
        }
        public double RapidTriggerUpSens
        { 
            get { return GetValue<double>(); }
            set { SetValue(value); } 
        }
        public double RapidTriggerDownSens
        { 
            get { return GetValue<double>(); }
            set { SetValue(value); } 
        }
        public double LowerHysteresis
        { 
            get { return GetValue<double>(); }
            set 
            { 
                SetValue(value); 
                if(value + 0.1 > UpperHysteresis)
                {
                    UpperHysteresis = value + 0.1;
                }
            } 
        }
        public double UpperHysteresis
        { 
            get { return GetValue<double>(); }
            set 
            { 
                SetValue(value);
                if (value - 0.1 < LowerHysteresis)
                {
                    LowerHysteresis = value - 0.1;
                }
            } 
        }
        public HotKey Key
        { 
            get { return GetValue<HotKey>(); }
            set { SetValue(value); } 
        }

        public string RTUS => ((int)(RapidTriggerUpSens * 100)).ToString();
        public string RTDS => ((int)(RapidTriggerDownSens * 100)).ToString();
        public string LH => ((int)(LowerHysteresis * 100)).ToString();
        public string UH => ((int)(UpperHysteresis * 100)).ToString();
    }
}

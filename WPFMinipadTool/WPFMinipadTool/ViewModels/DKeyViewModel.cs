using DevExpress.Mvvm;
using MinipadWPFTest.Models;

namespace MinipadWPFTest.ViewModels
{
    public class DKeyViewModel : ViewModelBase
    {
        public DKeyViewModel()
        {
            Key = new HotKey(System.Windows.Input.Key.Z);
        }

        public DKeyViewModel(string name) : this()
        {
            KeyName = name;
        }

        public string KeyName
        {
            get { return GetValue<string>(); }
            set { SetValue(value); }
        }
        public HotKey Key
        {
            get { return GetValue<HotKey>(); }
            set { SetValue(value); }
        }
    }
}

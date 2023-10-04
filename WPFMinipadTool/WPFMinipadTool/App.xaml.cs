using MinipadWPFTest.ViewModels;
using System.Windows;

namespace MinipadWPFTest
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            this.Dispatcher.UnhandledException += OnUnhandledException;
        }

        private void OnUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            var viewmodel = this.MainWindow.DataContext as MainViewModel;
            viewmodel.HandleException(e);

            e.Handled = true;
        }
    }
}

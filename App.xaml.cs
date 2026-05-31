using System;
using System.Threading;
using System.Windows;

namespace AITranslator
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private static Mutex? _mutex;
        private const string MutexName = "Global\\AITranslatorUniqueMutexName_123456";

        protected override void OnStartup(StartupEventArgs e)
        {
            _mutex = new Mutex(true, MutexName, out bool createdNew);
            if (!createdNew)
            {
                MessageBox.Show("AI Highlight Translator dang chay ngam trong khay he thong (System Tray)!", "AI Highlight Translator", MessageBoxButton.OK, MessageBoxImage.Information);
                Current.Shutdown();
                return;
            }

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_mutex != null)
            {
                try
                {
                    _mutex.ReleaseMutex();
                }
                catch (Exception) { }
                _mutex.Dispose();
            }
            base.OnExit(e);
        }
    }
}

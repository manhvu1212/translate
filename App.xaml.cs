using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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

        private static readonly string AppFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
            "AITranslator"
        );
        private static readonly string CrashLogPath = Path.Combine(AppFolder, "crash_log.txt");

        protected override void OnStartup(StartupEventArgs e)
        {
            _mutex = new Mutex(true, MutexName, out bool createdNew);
            if (!createdNew)
            {
                MessageBox.Show("AI Highlight Translator đang chạy ngầm trong khay hệ thống (System Tray)!", "AI Highlight Translator", MessageBoxButton.OK, MessageBoxImage.Information);
                Current.Shutdown();
                return;
            }

            // Setup global exception handling to catch background crashes
            SetupExceptionHandling();

            // The tray icon (NotifyIcon/ContextMenuStrip) lives in WinForms territory.
            // Exceptions thrown inside its callbacks are routed to WinForms'
            // Application.ThreadException — NOT to WPF's DispatcherUnhandledException.
            // Without this hook WinForms tries to show its default ThreadExceptionDialog,
            // which can itself fail to create a window handle (during shutdown or under
            // handle pressure) and kill the process with 0xc000041d.
            try
            {
                System.Windows.Forms.Application.SetUnhandledExceptionMode(
                    System.Windows.Forms.UnhandledExceptionMode.CatchException);
            }
            catch (InvalidOperationException) { /* too late to change mode; the handler below still applies */ }
            System.Windows.Forms.Application.ThreadException += (s, ev) =>
                LogException(ev.Exception, "WinFormsThreadException");

            base.OnStartup(e);

            // Manually create and register the main host window
            var mainWindow = new MainWindow();
            this.MainWindow = mainWindow;
        }

        private void SetupExceptionHandling()
        {
            // Handler for UI dispatcher thread unhandled exceptions.
            // Log and keep running: shutting down on every dispatcher exception both
            // kills the tray app for recoverable errors and opens a race where the
            // still-alive tray icon is clicked during shutdown (fatal 0xc000041d).
            this.DispatcherUnhandledException += (s, e) =>
            {
                LogException(e.Exception, "DispatcherUnhandledException");
                e.Handled = true; // Prevent default Windows crash dialog
            };

            // Handler for background thread exceptions. The process is already
            // terminating at this point; just log and attempt a graceful shutdown.
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                {
                    LogException(ex, "AppDomainUnhandledException");
                    try { ShowCrashMessageBox(ex); } catch { }
                }
                var app = Current;
                if (app != null)
                {
                    try { app.Dispatcher.Invoke(() => app.Shutdown()); } catch { }
                }
            };

            // Handler for task scheduler exceptions (unobserved tasks)
            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                LogException(e.Exception, "TaskSchedulerUnobservedException");
                e.SetObserved();
            };
        }

        internal static void LogException(Exception ex, string context)
        {
            try
            {
                if (!Directory.Exists(AppFolder))
                {
                    Directory.CreateDirectory(AppFolder);
                }

                string logText = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{context}]{Environment.NewLine}" +
                                 $"Exception: {ex.GetType().FullName}{Environment.NewLine}" +
                                 $"Message: {ex.Message}{Environment.NewLine}" +
                                 $"StackTrace:{Environment.NewLine}{ex.StackTrace}{Environment.NewLine}";

                if (ex.InnerException != null)
                {
                    logText += $"InnerException: {ex.InnerException.GetType().FullName}{Environment.NewLine}" +
                               $"InnerMessage: {ex.InnerException.Message}{Environment.NewLine}" +
                               $"InnerStackTrace:{Environment.NewLine}{ex.InnerException.StackTrace}{Environment.NewLine}";
                }

                logText += new string('-', 80) + Environment.NewLine;

                File.AppendAllText(CrashLogPath, logText);
            }
            catch
            {
                // Ignore failures to write log file to prevent secondary crashes
            }
        }

        private static void ShowCrashMessageBox(Exception ex)
        {
            string message = $"Đã xảy ra lỗi hệ thống nghiêm trọng khiến ứng dụng không thể tiếp tục hoạt động.{Environment.NewLine}{Environment.NewLine}" +
                             $"Chi tiết lỗi: {ex.Message}{Environment.NewLine}{Environment.NewLine}" +
                             $"Lịch sử lỗi đã được lưu tại:{Environment.NewLine}{CrashLogPath}";
            
            MessageBox.Show(message, "Lỗi Hệ Thống - AI Highlight Translator", MessageBoxButton.OK, MessageBoxImage.Error);
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

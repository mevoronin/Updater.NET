using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Threading;
using NETUpdater.Properties;
using NETUpdater.UI;

namespace NETUpdater
{
    static class Program
    {
        private static Mutex m_instance;
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            bool tryCreateNewApp;
            m_instance = new Mutex(true, Settings.Default.ProductName,
                    out tryCreateNewApp);
            if (!tryCreateNewApp) return;
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new frmMain());
        }
        //Events
        static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            MessageBox.Show(((Exception)e.ExceptionObject).Message, "Фатальная ошибка",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

    }
}

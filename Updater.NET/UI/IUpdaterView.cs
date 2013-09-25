using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NETUpdater.UI
{
    interface IUpdaterView
    {
            string ProcessStatus { set; }
            bool EnabledForm { set; }
            bool DisableFormClosing { get; set; }
            Object Invoke(Delegate method, params Object[] args);
            IAsyncResult BeginInvoke(Delegate method);
            void CloseForm();
            void ShowForm();
    }
}

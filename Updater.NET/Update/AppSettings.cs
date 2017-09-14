using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;

namespace NETUpdater.Update
{
    public class AppSettings
    {
        public static bool AllowProxy
        {
            get
            {
                bool allow = true;
                var setting = ConfigurationManager.AppSettings.Get("AllowProxy");
                if (!string.IsNullOrEmpty(setting))
                    try
                    {
                        allow = bool.Parse(setting.ToLower());
                    }
                    catch { }
                return allow;
            }
        }
    }
}

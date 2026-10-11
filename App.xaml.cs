using System;
using System.IO;
using System.Windows;

namespace WallChange;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            try
            {
                File.WriteAllText("crash.log", args.ExceptionObject.ToString());
            }
            catch { }
        };

        DispatcherUnhandledException += (s, args) =>
        {
            try
            {
                File.WriteAllText("crash.log", args.Exception.ToString());
            }
            catch { }
            args.Handled = true;
        };

        base.OnStartup(e);
    }
}

using System;
using System.Windows;
using System.Windows.Controls;
namespace Security_agent
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            var application = new Application();
            application.Run(new MainWindow());
        }
    }
}
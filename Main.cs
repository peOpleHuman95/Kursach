using System;
using System.Windows;
using System.Windows.Controls;
namespace Security_agent
{
    // Точка входа приложения охранного агентства.
    internal static class Program
    {
        // Создаёт приложение WPF и открывает главное окно.
        [STAThread]
        private static void Main()
        {
            var application = new Application();
            application.Run(new MainWindow());
        }
    }
}
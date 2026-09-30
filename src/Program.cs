using System;
using System.Linq;
using System.Windows.Forms;
using HikiNotifier.UI;

namespace HikiNotifier
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm(args.Any(a => a == "--startup")));
        }
    }
}

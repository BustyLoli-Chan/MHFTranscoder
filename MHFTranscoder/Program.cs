using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Threading;

namespace MHFTranscoder
{
    static class Program
    {
        static Mutex _m;


        static bool IsSingleInstance()
        {
            try
            {
                // Try to open existing mutex.
                Mutex.OpenExisting("MHFTranscoder");
            }
            catch
            {
                // If exception occurred, there is no such mutex.
                Program._m = new Mutex(true, "MHFTranscoder");

                // Only one instance.
                return true;
            }
            // More than one instance.
            return false;
        }

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            if (!Program.IsSingleInstance())
            {
                //Console.WriteLine("More than one instance"); // Exit program.
                MessageBox.Show("Please limit yourself to running one instance of the program at a time!","Error!");
            }
            else
            {
                //Console.WriteLine("One instance"); // Continue with program.
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new Form1());
            }
        }
    }
}

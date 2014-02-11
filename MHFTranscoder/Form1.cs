using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Globalization;
using System.Windows;
using System.Web;
using System.Web.Script.Serialization;
using System.Net;
using System.IO;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Security;
using System.Security.Principal;
using System.Management;
using System.Media;

namespace MHFTranscoder
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        const int GWL_EXSTYLE = (-20);

        const int WS_EX_NOACTIVATE = 0x08000000;
        public const int WS_EX_TRANSPARENT = 0x00000020;

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams param = base.CreateParams;
                param.ExStyle |= WS_EX_NOACTIVATE;
                param.ExStyle |= WS_EX_TRANSPARENT;
                return param;
            }
        }

        /// <summary>
        /// Changes an attribute of the specified window. The function also sets the 32-bit (long) value at the specified offset into the extra window memory.
        /// </summary>
        /// <param name="hWnd">A handle to the window and, indirectly, the class to which the window belongs..</param>
        /// <param name="nIndex">The zero-based offset to the value to be set. Valid values are in the range zero through the number of bytes of extra window memory, minus the size of an integer. To set any other value, specify one of the following values: GWL_EXSTYLE, GWL_HINSTANCE, GWL_ID, GWL_STYLE, GWL_USERDATA, GWL_WNDPROC </param>
        /// <param name="dwNewLong">The replacement value.</param>
        /// <returns>If the function succeeds, the return value is the previous value of the specified 32-bit integer.
        /// If the function fails, the return value is zero. To get extended error information, call GetLastError. </returns>
        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(System.Windows.Forms.Keys vKey);


        delegate bool EnumWindowsProc(IntPtr hWnd, int lParam);

        [DllImport("USER32.DLL")]
        static extern bool EnumWindows(EnumWindowsProc enumFunc, int lParam);

        [DllImport("USER32.DLL")]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("USER32.DLL")]
        static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("USER32.DLL")]
        static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("USER32.DLL")]
        static extern IntPtr GetShellWindow();

        [DllImport("user32.dll", SetLastError = true)]
        static extern int GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

        [DllImport("kernel32.dll")]
        static extern IntPtr OpenProcess(ProcessAccessFlags dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, int dwProcessId);

        [Flags]
        enum ProcessAccessFlags : uint
        {
            All = 0x001F0FFF,
            Terminate = 0x00000001,
            CreateThread = 0x00000002,
            VMOperation = 0x00000008,
            VMRead = 0x00000010,
            VMWrite = 0x00000020,
            DupHandle = 0x00000040,
            SetInformation = 0x00000200,
            QueryInformation = 0x00000400,
            Synchronize = 0x00100000
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CloseHandle(IntPtr hObject);

        public static class OpenWindowGetter
        {
            /// <summary>Returns a dictionary that contains the handle and title of all the open windows.</summary> /// <returns>A dictionary that contains the handle and title of all the open windows.</returns>

            public static IDictionary<IntPtr, string> GetOpenWindows()
            {
                IntPtr lShellWindow = GetShellWindow();
                Dictionary<IntPtr, string> lWindows = new Dictionary<IntPtr, string>();

                EnumWindows(delegate(IntPtr hWnd, int lParam)
                {
                    if (hWnd == lShellWindow) return true;
                    if (!IsWindowVisible(hWnd)) return true;

                    int lLength = GetWindowTextLength(hWnd);
                    if (lLength == 0) return true;

                    StringBuilder lBuilder = new StringBuilder(lLength);
                    GetWindowText(hWnd, lBuilder, lLength + 1);

                    lWindows[hWnd] = lBuilder.ToString();
                    return true;

                }, 0);

                return lWindows;
            }
        }
        List<String> oStrings = new List<String>();

        long CurrentPos = 0;
        String lastRead = "";
        string theFile;
        WebClient tC = new WebClient();
        bool activate = true;
        double opacity = .7;
        Process mhfProc;

        private void Form1_Load(object sender, EventArgs e)
        {
            if (!IsAdministrator())
            {
                MessageBox.Show("Please run the application as an Administrator");
                Application.Exit();
            }

            this.Height = (int)((1.0 / 16.0) * Screen.PrimaryScreen.WorkingArea.Height);
            this.Height = listBox1.Height;
            this.Top = Screen.PrimaryScreen.WorkingArea.Height - this.Height;


            this.Width = (int)((1400.0 / 1920.0) * Screen.PrimaryScreen.WorkingArea.Width);
            this.Left = (Screen.PrimaryScreen.WorkingArea.Width - this.Width) / 2;
            this.TopMost = true;
            Process.EnterDebugMode();

            DateTime compDT = DateTime.MinValue;
            String thePath = "";
            string[] allFiles = new String[0];
            if (Process.GetProcessesByName("mhf").Length > 0)
            {
                mhfProc = Process.GetProcessesByName("mhf")[0];
                thePath = mhfProc.MainModule.FileName.Remove(mhfProc.MainModule.FileName.LastIndexOf("\\"));
                mhfProc.EnableRaisingEvents = true;
                mhfProc.Exited += mhfProc_Exited;
                Process.LeaveDebugMode();
            }
            else if (getMHF() != IntPtr.Zero)
            {
                if (Environment.Is64BitOperatingSystem)
                {
                    //start new app
                    Process.Start(Application.ExecutablePath);
                    System.Environment.Exit(1);
                }
                else
                {
                    thePath = "";
                    String[] allDrives = Environment.GetLogicalDrives();
                    foreach (string aDrive in allDrives)
                    {
                        if (Directory.Exists(aDrive + "Program Files (x86)\\CAPCOM\\Monster Hunter Frontier Online"))
                        {
                            thePath = aDrive + "Program Files (x86)\\CAPCOM\\Monster Hunter Frontier Online";
                            break;
                        }
                        else if (Directory.Exists(aDrive + "Program Files\\CAPCOM\\Monster Hunter Frontier Online"))
                        {
                            thePath = aDrive + "Program Files\\CAPCOM\\Monster Hunter Frontier Online";
                            break;
                        }
                        else
                        {
                            thePath = "";
                        }
                    }

                    //WORST CASE
                    if (thePath == "")
                    {
                        foreach (string aDrive in allDrives)
                        {
                            thePath = DirSearch(aDrive);
                            if (!string.IsNullOrEmpty(thePath))
                            {
                                break;
                            }
                        }
                    }

                    //start manual timer
                    tmr_window.Start();
                }
            }
            else
            {
                MessageBox.Show("Please make sure that Monster Hunter is Running or that mhf.exe exists in current system process list", "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Exit();
            }

            if (Directory.Exists(thePath + "\\チャットログ"))
            {
                allFiles = allFiles.Concat(Directory.GetFiles(thePath + "\\チャットログ")).ToArray();
            }
            if (Directory.Exists(thePath + "\\ƒ`ƒƒƒbƒgƒƒO"))
            {
                allFiles = allFiles.Concat(Directory.GetFiles(thePath + "\\ƒ`ƒƒƒbƒgƒƒO")).ToArray();
            }

            foreach (string aFile in allFiles)
            {
                String workStr = aFile.Substring(aFile.LastIndexOf("log_") + 4);
                int Year = int.Parse(workStr.Substring(0, 4));
                int Month = int.Parse(workStr.Substring(4, 2));
                int Day = int.Parse(workStr.Substring(6, 2));

                int Hour = int.Parse(workStr.Substring(9, 2));
                int Minute = int.Parse(workStr.Substring(11, 2));
                int Second = int.Parse(workStr.Substring(13, 2));

                DateTime dt = new DateTime(Year,
                    Month,
                    Day,
                    Hour,
                    Minute,
                    Second);


                if (dt > compDT)
                {
                    compDT = dt;
                    theFile = aFile;
                }
            }

            using (FileStream fs = new FileStream(theFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using (StreamReader sr = new StreamReader(fs, Encoding.GetEncoding("shift-jis"), true))
                {
                    sr.ReadToEnd();
                    CurrentPos = sr.BaseStream.Position;
                }
            }

            tmr_update.Start();
            notifyIcon1.Visible = true;
        }

        void mhfProc_Exited(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void tmr_update_Tick(object sender, EventArgs e)
        {
            using (FileStream fs = new FileStream(theFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using (StreamReader sr = new StreamReader(fs, Encoding.GetEncoding("shift-jis"), true))
                {
                    sr.BaseStream.Position = CurrentPos;
                    String curline = "";
                    while ((curline = sr.ReadLine()) != null)
                    {
                        resolve(curline);
                        cShow();

                        CurrentPos = sr.BaseStream.Position;
                    }
                    if (!activate)
                    {
                        tmr_hide.Start();
                    }
                }
            }
        }

        public void resolve(string curline)
        {
            if (curline.Contains(">"))
            {
                string time = curline.Substring(curline.IndexOf(" ") + 1);
                time = time.Substring(0, time.IndexOf(" "));

                string location = curline.Substring(curline.IndexOf(">") + 1, curline.IndexOf("]") - (curline.IndexOf(">") + 1)).Replace(" ", "");
                string transtring = "";
                string name = "SYSTEM";
                if (location.Contains("ID:"))
                {
                    transtring = curline.Substring(curline.IndexOf("]") + 1);
                    if (transtring.Contains(">"))
                    {
                        //it's a whisper
                        name = transtring;
                        name = name.Substring(0, name.IndexOf(">")).Replace(" ", "");

                        transtring = transtring.Substring(transtring.IndexOf(">") + 1);

                        oStrings.Add(transtring);
                        string eng = TranslateGoogle(transtring, "ja", "en");
                        listBox1.Items.Add(time + " : " + name + " > " + eng);
                    }
                    else if (transtring.Contains("<"))
                    {
                        //it's a whisper
                        name = transtring;
                        name = name.Substring(0, name.IndexOf("<")).Replace(" ", "");

                        transtring = transtring.Substring(transtring.IndexOf("<") + 1);

                        oStrings.Add(transtring);
                        string eng = TranslateGoogle(transtring, "ja", "en");
                        listBox1.Items.Add(time + " : " + name + " < " + eng);
                    }
                    else
                    {
                        oStrings.Add(transtring);
                        string eng = TranslateGoogle(transtring, "ja", "en");
                        listBox1.Items.Add(time + " : " + name + " > " + eng);
                    }
                }
                else
                {
                    name = curline.Substring(curline.IndexOf("]") + 1);
                    transtring = name.Substring(name.IndexOf(">") + 1);
                    name = name.Substring(0, name.IndexOf(">")).Replace(" ", "");

                    oStrings.Add(transtring);
                    string eng = TranslateGoogle(transtring, "ja", "en");
                    listBox1.Items.Add(time + " " + location + " : " + name + " > " + eng);
                }
            }

        }

        private void cShow()
        {
            this.Show();
            this.Opacity = .8;
            this.TopMost = false;
            this.TopMost = true;
            tmr_hide.Stop();
            tmr_fade.Stop();
            tmr_fade.Interval = 100;
            tmr_hide.Interval = 7000;
        }

        public string TranslateGoogle(string text, string fromCulture, string toCulture)
        {
            fromCulture = fromCulture.ToLower();
            toCulture = toCulture.ToLower();

            // normalize the culture in case something like en-us was passed 
            // retrieve only en since Google doesn't support sub-locales
            string[] tokens = fromCulture.Split('-');
            if (tokens.Length > 1)
                fromCulture = tokens[0];

            // normalize ToCulture
            tokens = toCulture.Split('-');
            if (tokens.Length > 1)
                toCulture = tokens[0];

            string url = string.Format(@"http://translate.google.com/translate_a/t?client=j&text={0}&hl=en&sl={1}&tl={2}",
                                       HttpUtility.UrlEncode(text), fromCulture, toCulture);

            // Retrieve Translation with HTTP GET call
            string html = null;
            try
            {
                WebClient web = new WebClient();

                // MUST add a known browser user agent or else response encoding doen't return UTF-8 (WTF Google?)
                web.Headers.Add(HttpRequestHeader.UserAgent, "Mozilla/5.0");
                web.Headers.Add(HttpRequestHeader.AcceptCharset, "UTF-8");

                // Make sure we have response encoding to UTF-8
                web.Encoding = Encoding.UTF8;
                html = web.DownloadString(url);
            }
            catch (Exception ex)
            {
                //this.ErrorMessage = Westwind.Globalization.Resources.Resources.ConnectionFailed + ": " +
                //                    ex.GetBaseException().Message;
                return "";
            }

            // Extract out trans":"...[Extracted]...","from the JSON string
            string result = html;

            //working string 
            string output = "";
            while (result.Contains("\"trans\":\""))
            {
                result = result.Substring(result.IndexOf("\"trans\":\"") + 9);
                output += result.Substring(0, result.IndexOf("\","));
            }


            return output;
        }

        private void tmr_hide_Tick(object sender, EventArgs e)
        {
            tmr_fade.Start();
            tmr_hide.Stop();
        }

        private void tmr_fade_Tick(object sender, EventArgs e)
        {
            if (this.Opacity > 0)
            {
                if ((this.Opacity - .05) > 0)
                {
                    this.Opacity -= .05;
                }
                else
                {
                    this.Opacity = 0;
                    tmr_fade.Stop();
                }
            }
            else
            {
                this.Hide();
                this.Opacity = opacity;
                tmr_fade.Stop();
            }
        }

        string DirSearch(string sDir)
        {
            foreach (string aFile in Directory.EnumerateFiles(sDir, "mhf.exe"))
            {
                return (aFile.Remove(aFile.LastIndexOf("\\")));
            }
            try
            {
                foreach (string d in Directory.EnumerateDirectories(sDir))
                {
                    string dir = DirSearch(d);
                    if (dir != "")
                    {
                        return dir;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }

            return "";
        }

        private void toolStripMenuItem1_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void toolStripMenuItem2_Click(object sender, EventArgs e)
        {
            MessageBox.Show("Slush.Net™ MHF Chat Transcoder" + Environment.NewLine +
                Application.ProductVersion + Environment.NewLine +
                "Built by BustyLoli-Chan" + Environment.NewLine +
                "killspire@gmail.com" + Environment.NewLine +
            Environment.NewLine +
            "Takes japanese chat from the game Monster Hunter and actively converts it into English" + Environment.NewLine +
            "but you already knew that, because it's impossible to read this dialogue without the game running" + Environment.NewLine +
            Environment.NewLine +
            "Translation services are provided by the courtesy of Google®" + Environment.NewLine +
            "Except they don't know it yet... so shhhh don't tell" + Environment.NewLine +
            Environment.NewLine +
            "All Names things nouns etc. are all properties of their respective parties" + Environment.NewLine +
            "P.S. please don't sue me", "About", MessageBoxButtons.OK);
        }

        private IntPtr getMHF()
        {
            foreach (KeyValuePair<IntPtr, string> lWindow in OpenWindowGetter.GetOpenWindows())
            {
                IntPtr lHandle = lWindow.Key;
                string lTitle = lWindow.Value;

                if (lTitle.Contains("MONSTER HUNTER FRONTIER ONLINE"))
                {
                    return lHandle;
                }
            }

            return IntPtr.Zero;
        }

        private void tmr_window_Tick(object sender, EventArgs e)
        {
            if (getMHF() == IntPtr.Zero)
            {
                Application.Exit();
            }
        }

        public static bool IsAdministrator()
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            notifyIcon1.Visible = false;
            notifyIcon1.Dispose();
        }

        private void listBox1_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            //if the item state is selected them change the back color 
            if ((e.State & DrawItemState.Selected) == DrawItemState.Selected)
                e = new DrawItemEventArgs(e.Graphics,
                                          e.Font,
                                          e.Bounds,
                                          e.Index,
                                          e.State ^ DrawItemState.Selected,
                                          e.ForeColor,
                                          Color.Black);//Choose the color

            // Draw the background of the ListBox control for each item.
            e.DrawBackground();
            // Draw the current item text
            e.Graphics.DrawString(listBox1.Items[e.Index].ToString(), e.Font, Brushes.White, e.Bounds, StringFormat.GenericDefault);
            // If the ListBox has focus, draw a focus rectangle around the selected item.
            e.DrawFocusRectangle();
        }

        private void tmr_keys_Tick(object sender, EventArgs e)
        {
            if (GetAsyncKeyState(Keys.Menu) < 0)
            {
                if (GetAsyncKeyState(Keys.Escape) < 0)
                {
                    this.Close();
                }
            }

            if (GetAsyncKeyState(Keys.Oem3) < 0)
            {
                if (activate)
                {
                    this.Height = (int)((1.0 / 6.0) * Screen.PrimaryScreen.WorkingArea.Height);
                    this.Height = listBox1.Height;
                    this.Top = Screen.PrimaryScreen.WorkingArea.Height - this.Height;
                    System.Windows.Forms.Cursor.Position = PointToScreen(new Point(this.Width / 2, this.Height / 2));
                    listBox1.Focus();
                    activate = false;
                    int extendedStyle = GetWindowLong(this.Handle, GWL_EXSTYLE);
                    SetWindowLong(this.Handle, GWL_EXSTYLE, extendedStyle & ~(WS_EX_TRANSPARENT | WS_EX_NOACTIVATE));
                    cShow();
                    this.Focus();
                }

            }
            else
            {
                int extendedStyle = GetWindowLong(this.Handle, GWL_EXSTYLE);
                if ((extendedStyle & WS_EX_NOACTIVATE) != WS_EX_NOACTIVATE)
                {
                    SetWindowLong(this.Handle, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE);
                    this.Height = (int)((1.0 / 16.0) * Screen.PrimaryScreen.WorkingArea.Height);
                    this.Height = listBox1.Height;
                    this.Top = Screen.PrimaryScreen.WorkingArea.Height - this.Height;
                    listBox1.ClearSelected();
                }
                activate = true;
                
                if (listBox1.SelectedIndex != (listBox1.Items.Count - 1))
                {
                    listBox1.SelectedIndex = listBox1.Items.Count - 1;
                }

                if (!tmr_hide.Enabled)
                {
                    tmr_hide.Start();
                }
            }
        }

        private void listBox1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            int index = this.listBox1.IndexFromPoint(e.Location);
            if (index != System.Windows.Forms.ListBox.NoMatches)
            {
                //do your stuff here
                listBox1.SelectedIndex = index;
                SystemSounds.Asterisk.Play();
                Clipboard.SetText(oStrings[index]);
            }
        }
    }
}

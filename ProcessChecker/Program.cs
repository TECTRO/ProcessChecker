using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Text;
using System.Threading;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ProcessChecker
{
    internal delegate void SignalHandler(ConsoleSignal consoleSignal);

    internal enum ConsoleSignal
    {
        CtrlC = 0,
        CtrlBreak = 1,
        Close = 2,
        LogOff = 5,
        Shutdown = 6
    }

    internal static class ConsoleHelper
    {
        [DllImport("Kernel32", EntryPoint = "SetConsoleCtrlHandler")]
        public static extern bool SetSignalHandler(SignalHandler handler, bool add);
    }


    class Program
    {
        private static string[] MainPaths =
        {
            "C:\\ProgramData\\NVIDIA\\Log",
            "C:\\ProgramData\\TamoSoft\\Log",
            "C:\\Users\\Public\\Documents\\Steam\\Apps",
            $"C:\\Users\\{Environment.UserName}\\source",
            //GetCurrentDirectory()
        };
        static readonly string SavingDirectory = $"[SESSIONS]\\{DateTime.Now:D}";
        static readonly string CurrentSessionName = $"{SavingDirectory}\\[SESSION] " + DateTime.Now.ToString("f").Replace(":", ".") + ".txt";
        private static DateTime _startTime = DateTime.Now;
        private static SignalHandler _signalHandler;

        public static List<int> NotificationDelays = new List<int> { 100, 10, 5 };

        static string GetCurrentExecutableName()
        {
            var path1 = Application.ExecutablePath.Substring(Application.ExecutablePath.LastIndexOf('\\') + 1);
            return path1.Remove(path1.LastIndexOf('.'));
        }
        static string GetCurrentDirectory()
        {
            return Application.ExecutablePath.Remove(Application.ExecutablePath.LastIndexOf('\\')); ;
        }
        static void Main(string[] args)
        {
            var path = GetCurrentExecutableName();

            if (Process.GetProcesses().Select(t => t.ProcessName).Count(t => t.Equals(path)) > 1) return;
            
            ////////////////////////////////////////////////////////////////
            if (args.Any()) DateTime.TryParse(args[0], out _startTime);

            foreach (var mainPath in MainPaths)
            {
                var directoryInfo = new DirectoryInfo("C:\\");
                directoryInfo.CreateSubdirectory(Path.Combine(mainPath.Replace("C:\\", ""), SavingDirectory));
            }

            LoadPreviousTime();
            WriteToAutoExecAndHide();

            ////////////////////////////////////////////////////////////////
            _signalHandler += e =>
            {
                foreach (var mainPath in MainPaths)
                {
                    var fs = new StreamWriter(Path.Combine(mainPath, CurrentSessionName), true, Encoding.Unicode);
                    TimeSpan workingTime = DateTime.Now.Subtract(_startTime);
                    fs.WriteLine(
                        $"[ACTIVE_TIME] {workingTime.Hours} h. {workingTime.Minutes} m. {workingTime.Seconds} sec.");
                    fs.Close();
                }
            };

            ConsoleHelper.SetSignalHandler(_signalHandler, true);
            ///////////////////////////////////////////////////////////////

            PushWatcher();

            List<string> oldProcessList = Process.GetProcesses().Where(p => !string.IsNullOrEmpty(p.MainWindowTitle)).Select(t => t.ProcessName).ToList();

            while (true)
            {
                var curProcList = Process.GetProcesses().Where(p => !string.IsNullOrEmpty(p.MainWindowTitle)).Select(t => t.ProcessName).ToList();

                var diff = curProcList.Except(oldProcessList).Union(oldProcessList.Except(curProcList)).ToList();

                foreach (var mainPath in MainPaths)
                {
                    using (var fs = new StreamWriter(Path.Combine(mainPath, CurrentSessionName), true, Encoding.Unicode))
                    {
                        foreach (var pr in diff)
                        {
                            if (oldProcessList.Contains(pr))
                                fs.WriteLine($"{DateTime.Now:t} [CLOSED] {pr}");

                            if (curProcList.Contains(pr))
                                fs.WriteLine($"{DateTime.Now:t} [OPENED] {pr}");
                        }

                        PostTimeAttention(fs);
                    }
                }

                oldProcessList = curProcList;

                Thread.Sleep(500);
            }
        }

        private static DateTime _lastAttention = DateTime.Now;

        private static void PostTimeAttention(StreamWriter sr)
        {
            if (DateTime.Now.Subtract(_lastAttention).TotalMinutes > 10)
            {
                _lastAttention = DateTime.Now;
                TimeSpan workingTime = DateTime.Now.Subtract(_startTime);
                sr.WriteLine($"{DateTime.Now:t} [ATTENTION] {workingTime.Hours} h. {workingTime.Minutes} m. {workingTime.Seconds} sec.");
            }
        }

        static string WordEnding(string word, int value)
        {
            string result = word;

            int key = value % 100;

            // ReSharper disable once StringLiteralTypo
            if ("аеёийоуыэюя".Contains(word[word.Length - 1]))
            {
                result = word.Remove(word.Length - 1);

                if (new[] { 0, 5, 6, 7, 8, 9 }.Contains(key % 10) || key / 10 == 1)
                    return result;

                if (key % 10 == 1)
                    return result + 'у';

                return result + 'ы';
            }

            if (new[] { 0, 5, 6, 7, 8, 9 }.Contains(key % 10) || key / 10 == 1)
                return result + "ов";

            if (key % 10 == 1)
                return word;
            return result + 'а';
        }

        static void PushWatcher()
        {
            Thread th = new Thread(() =>
            {
                DateTime startPos = DateTime.Now;

                void PushNotifier(int minutesLimit)
                {
                    string wiredSound = Path.Combine(GetCurrentDirectory(), "SystemWiredSound.exe");
                    
                    var currentLimit = DateTime.Now.Subtract(_startTime);

                    if (currentLimit.TotalMinutes > minutesLimit)
                        Process.Start(wiredSound);

                    if (currentLimit.TotalMinutes < minutesLimit)
                        UploadWarning(new[] {
                            $"Внимание! Вы играете {currentLimit.Hours} {WordEnding("час",currentLimit.Hours)} {currentLimit.Minutes} {WordEnding("минута",currentLimit.Minutes)}",
                            $"Время начала игры {_startTime:f}",
                            $"Время необходимого завершения {_startTime.AddMinutes(minutesLimit):t}"
                        });
                    else
                        UploadWarning(new[] {
                            $"Внимание! Вы играете слишком долго! {currentLimit.Hours} {WordEnding("час",currentLimit.Hours)} {currentLimit.Minutes} {WordEnding("минута",currentLimit.Minutes)}",
                            $"Время начала игры {_startTime:f}",
                            $"Время необходимого завершения {_startTime.AddMinutes(minutesLimit):t}"
                        });
                }

                do
                {
                    UpdateTotalTime(NotificationDelays[0]);

                    if (DateTime.Now.Subtract(startPos).TotalMinutes >= NotificationDelays[0])
                    {
                        PushNotifier(120);

                        if (NotificationDelays.Count > 1)
                            NotificationDelays.RemoveAt(0);

                        startPos = DateTime.Now;
                    }
                    Thread.Sleep(30000);
                }
                while (true);

                // ReSharper disable once FunctionNeverReturns
            });
            th.Start();
        }


        static string totalTimeAnchor = "[TOTAL_TIME] ";
        static string lastSessionAnchor = "[LAST_SESSION] ";
        public static void UpdateTotalTime(int interval)
        {
            TimeSpan totalTime;
            DateTime lastSession;

            foreach (var mainPath in MainPaths)
            {
                using (var sr =
                    new StreamReader(new FileStream(Path.Combine(mainPath, SavingDirectory) + "\\[TOTAL_TIME].txt", FileMode.OpenOrCreate),
                        Encoding.Unicode))
                {
                    TimeSpan.TryParse(sr.ReadLine()?.Replace(totalTimeAnchor, ""), out totalTime);
                    DateTime.TryParse(sr.ReadLine()?.Replace(lastSessionAnchor, ""), out lastSession);
                }

                if (lastSession == DateTime.MinValue)
                {
                    lastSession = DateTime.Now;
                    totalTime = totalTime.Add(new TimeSpan(0,0, interval,0));
                }

                using (var sr = new StreamWriter(Path.Combine(mainPath, SavingDirectory) + "\\[TOTAL_TIME].txt", false, Encoding.Unicode))
                {
                    sr.WriteLine($"{totalTimeAnchor}{totalTime + DateTime.Now.Subtract(lastSession):g}");
                    sr.WriteLine($"{lastSessionAnchor}{DateTime.Now}");
                    sr.WriteLine(string.Join(":",NotificationDelays));
                }
            }
        }

        private class LoadingData
        {
            public TimeSpan TotalTime;
            public List<int> Delays;
        }
        public static void LoadPreviousTime()
        {
            
           TimeSpan totalTime;
           List<int> delays = null;
            List<LoadingData> date = new List<LoadingData>();

            foreach (var mainPath in MainPaths)
            {
                LoadingData current = new LoadingData();

                using (var sr =
                    new StreamReader(new FileStream(Path.Combine(mainPath, SavingDirectory) + "\\[TOTAL_TIME].txt", FileMode.OpenOrCreate),
                        Encoding.Unicode))
                {
                    TimeSpan.TryParse(sr.ReadLine()?.Replace(totalTimeAnchor, ""), out current.TotalTime);
                    sr.ReadLine();
                    current.Delays = sr.ReadLine()?.Split(':').Where(t=>t.Length>0).Select(int.Parse).ToList();
                }

                date.Add(current);
            }

            totalTime = date.Select(t => t.TotalTime).Max();

            foreach (var list in date.Select(t => t.Delays).Where(t=>t!=null).Where(t => t.Any()))
            {
                if (delays == null) delays = list;
                else if (delays.Distinct().Sum() > list.Distinct().Sum())
                    delays = list;
            }

            if (DateTime.Now - totalTime < _startTime)
                _startTime = DateTime.Now - totalTime;

            if(delays!=null)
                if (NotificationDelays.Distinct().Sum() > delays.Distinct().Sum())
                    NotificationDelays = delays;
        }



        static void UploadWarning(string[] lines)
        {
            XmlDocument toastXml = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastImageAndText04);
            //var nodes = toastXml.Attributes.ToList();
            // Fill in the text elements
            XmlNodeList stringElements = toastXml.GetElementsByTagName("text");
            for (int i = 0; i < lines.Length; i++)
            {
                stringElements[i].AppendChild(toastXml.CreateTextNode(lines[i]));
            }

            // Create the toast and attach event listeners
            ToastNotification toast = new ToastNotification(toastXml);
            // Show the toast. Be sure to specify the AppUserModelId on your application's shortcut!
            ToastNotificationManager.CreateToastNotifier("Уведомление!").Show(toast);
        }

        static void WriteToAutoExecAndHide()
        {
            var curDir = GetCurrentDirectory();
            var inputDir = "C:\\ProgramData\\VsTelemetry\\System";

            var files = new[]
            {
                "Microsoft.Toolkit.Uwp.Notifications.dll",
                "NAudio.dll",
                "ProcessChecker.exe",
                "SystemWiredSound.exe"
            };

            try
            {
                RegistryKey key =
                    Registry.LocalMachine.OpenSubKey(
                        "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run\\", true);

                if (key == null) return;
                key.SetValue(GetCurrentExecutableName(), Path.Combine(inputDir, files[2]));
                key.Close();
                File.Create("success.txt");
            }
            catch { File.Create("acces denied.txt"); }

            if (curDir != inputDir)
            {
                new DirectoryInfo("C:\\").CreateSubdirectory(inputDir.Replace("C:\\", ""));

                foreach (var file in files)
                {
                    if (!File.Exists(Path.Combine(inputDir, file)))
                        File.Copy(Path.Combine(curDir, file), Path.Combine(inputDir, file));
                }
            }
        }

    }
}

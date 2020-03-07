using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ProjectBuildCounter
{
    class Program
    {
        static List<string> _args;
        static int startVerNum = 3;
        static bool _noFileVer = false; // if true - file version won't be incremented
        static bool _useEventLog = false;

        /// <summary>
        /// This function will incriment versions. Builds are counted from 1 to 9999, Minor: from 1 to 99
        /// </summary>
        /// <param name="args">
        /// Command Line Arguments:
        /// 0 - Path to AssemblyInfo.cs.
        /// 1 - version to start incrementation from
        /// 2 - nofile - indicating to skip file version incrementation
        /// 3 - useEventLog - indicats that exception messages will be written in the event log also
        ///</param>
        static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                return 0;
            }

            InitInput(args);

            if (!_args[0].Contains("AssemblyInfo.cs"))
            {
                _args[0] = Path.Combine(_args[0], "AssemblyInfo.cs");
            }

            if (!File.Exists(_args[0]))
            {
                if (_useEventLog)
                {
                    using (EventLog evlog = new EventLog("Application", Environment.MachineName, AppDomain.CurrentDomain.FriendlyName))
                    {
                        evlog.WriteEntry(string.Format("File [{0}] not found", _args[0]), EventLogEntryType.Error);
                    }
                }

                Console.Write("File [{0}] not found", _args[0]);

                return -1;
            }

            try
            {
                string[] contents = File.ReadAllLines(_args[0], Encoding.UTF8);
                for (int i = 0; i < contents.Length; i++)
                {
                    if (!contents[i].Contains("AssemblyVersion")
                        && !contents[i].Contains("AssemblyFileVersion")
                        && !contents[i].Contains("AssemblyInformationalVersion"))
                    {
                        continue;
                    }

                    contents[i] = ChangeVersion(contents[i]);
                }

                // save file
                File.WriteAllLines(_args[0], contents, Encoding.UTF8);
            }
            catch (Exception e)
            {
                TextWriter errWriter = Console.Error;
                errWriter.WriteLine("(Reading File) Exception thrown: " + e);

                if (_useEventLog)
                {
                    using (EventLog evlog = new EventLog("Application", Environment.MachineName, AppDomain.CurrentDomain.FriendlyName))
                    {
                        evlog.WriteEntry("(Reading File) Exception thrown: " + e.Message, EventLogEntryType.Error);
                    }
                }
                return -2;
            }

            return 0;
        }

        /// <summary>
        /// check input and fill missing arguments
        /// </summary>
        /// <param name="args">command line input arguments</param>
        private static void InitInput(string[] args)
        {
            _args = args.ToList<String>();

            if (_args.Count < 1)
            {
                _args.Add("AssemblyInfo.cs");
            }

            if (_args.Count == 1)
            {
                // default version number to start incrementation from
                _args.Add("R");
            }

            if (_args.Count == 3)
            {
                // check if 3rd parameter is supplied as 'nofile'
                _noFileVer = _args[2].ToLower().Equals("nofile");

                // check if 3th parameter is supplied as 'useEventLog'
                _useEventLog = _args[3].ToLower().Equals("useeventlog");
            }

            if (_args.Count == 4)
            {
                // if 4th parameter supplied must be 'useEventLog'
                _useEventLog = _args[3].ToLower().Equals("useeventlog");
            }

            switch (_args[1])
            {
                case "M": startVerNum = 0; break;
                case "m": startVerNum = 1; break;
                case "b":
                case "B": startVerNum = 2; break;
                default: startVerNum = 3; break; // default number to incrementat is Release
            }
        }

        /// <summary>
        /// Transforms version string into <seealso cref="System.Int32"/> increment numbers and return back the new numbers as a string
        /// </summary>
        /// <param name="str">Current version number as a <see cref="System.String"/></param>
        /// <returns><see cref="System.String"/> with the new version number</returns>
        private static string ChangeVersion(string str)
        {
            // flag prohibiting file version incrementation is set
            if (_noFileVer && str.Contains("AssemblyFileVersion"))
            {
                return str;
            }

            // some files have * instead of build figure
            str = str.Replace('*', '0');

            // get version numbers
            string sNums = Regex.Replace(str, @"[^\d\.]", "");

            int[] ver = Array.ConvertAll<String, Int32>(
                sNums.Split(new[] { '.' },
                StringSplitOptions.RemoveEmptyEntries),
                v => Convert.ToInt32(v));

            Increment(ref ver, startVerNum);

            // replace old with new version
            return str.Replace(sNums, string.Join(".", Array.ConvertAll<Int32, String>(ver, v => v.ToString())));
        }

        /// <summary>
        /// Recursively increment version mubers starting from right to left.
        /// </summary>
        /// <param name="ver">array with versions. Zero indexed is Major. Max indexed (3) is Release</param>
        /// <param name="idx">Starting number - from bigger to smaller</param>
        private static void Increment(ref int[] ver, int idx)
        {
            int maxVal = Convert.ToInt32(new string('9', idx + 1));
            try
            {
                if (ver[idx] + 1 > maxVal)
                {
                    Increment(ref ver, idx - 1);
                    ver[idx] = 1;
                    return;
                }
                ver[idx]++;
            }
            catch (Exception e)
            {
                try
                {
                    if (_useEventLog)
                    {
                        using (EventLog evlog = new EventLog("Application", Environment.MachineName, AppDomain.CurrentDomain.FriendlyName))
                        {
                            evlog.WriteEntry("Exception thrown: " + e.Message, EventLogEntryType.Error);
                        }
                    }
                }
                catch (Exception ex) { }
                return;
            }
        }
    }
}
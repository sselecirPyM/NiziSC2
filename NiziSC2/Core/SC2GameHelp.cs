using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace NiziSC2.Core
{
    public static class SC2GameHelp
    {
        public static void LaunchSC2(int port, out string starcraftMaps)
        {
            string starcraftExe = "";
            string starcraftDir = "";
            starcraftMaps = "";

            var myDocuments = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var executeInfo = Path.Combine(myDocuments, "StarCraft II", "ExecuteInfo.txt");
            if (File.Exists(executeInfo))
            {
                foreach (var line in File.ReadAllLines(executeInfo))
                {
                    var argument = line.Substring(line.IndexOf('=') + 1).Trim();
                    if (line.Trim().StartsWith("executable"))
                    {
                        starcraftExe = argument;
                        starcraftDir = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(starcraftExe)));
                        if (starcraftDir != null)
                            starcraftMaps = Path.Combine(starcraftDir, "Maps");
                        break;
                    }
                }
            }
            else
            {
                throw new Exception("Unable to find:" + executeInfo + ". Make sure you started the game successfully at least once.");
            }

            var processStartInfo = new ProcessStartInfo(starcraftExe)
            {
                Arguments = string.Format("-listen {0} -port {1} -displayMode 0", "127.0.0.1", port),
                WorkingDirectory = Path.Combine(starcraftDir, "Support64")
            };
            Process.Start(processStartInfo);
        }
    }
}

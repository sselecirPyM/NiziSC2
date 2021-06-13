using System;
using System.Collections.Generic;
using System.Text;
using SC2APIProtocol;
using System.Reflection;

namespace NiziSC2.Core
{
    public class LaunchOptions
    {
        public string Map;
        public int Port;
        public string Address;
        public Race OurRace;
        public Race OpponentRace;
        public Difficulty OpponentDifficulty;
    }
}

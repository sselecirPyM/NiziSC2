using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace NiziSC2.Core
{
    public class AutoCast
    {
        [XmlElement("AutoResearches")]
        public List<AutoResearch> AutoResearches;
    }
    public class AutoResearch
    {
        public UnitType Unit;
        public List<Abilities> Abilities;
    }
}

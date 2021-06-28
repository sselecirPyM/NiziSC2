using System;
using System.Collections.Generic;
using System.Text;

namespace NiziSC2.Core
{
    public class TechUnit
    {
        public List<UnitType> Depends;
        public UnitType UnitType;
        public UnitType Spawner;
    }
    public class RaceData
    {
        public SC2APIProtocol.Race Race;
        public List<TechUnit> TechUnits;
        public List<TechUnit> Barracks;
        public TechUnit Worker;
        public UnitType ResourceBuilding;
        public UnitType VespeneBuilding;
        public UnitType SupplyBuilding;
        public UnitType DefenseGround;
        public UnitType DefenseAir;
    }
}

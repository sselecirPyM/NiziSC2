using System;
using System.Collections.Generic;
using System.Text;
using System.Numerics;
using SC2APIProtocol;

namespace NiziSC2.Core
{
    public struct ResourceStat
    {
        public int collected;
        public int consumed;
        public int current;
        public int expect;
        public int lost;
        public float incomeSpeed;
        public float consumeSpeed;
    }
    public class PlayerProperties
    {
        public int id;
        public SC2APIProtocol.Race race;
        public Vector3 StartPoint;
        public ResourceStat MineralStat;
        public ResourceStat VespeneStat;
        public float SupplyProvideVirtual;
        public float SupplyProvide;
        public float SupplyConsume;
        public int ArmyCount;
        public float FoodArmy;
        public float FoodWorkers;
        public int IdleWorkerCount;
        public int WarpGateCount;

        public void FromPlayerCommon(PlayerCommon playerCommon)
        {
            MineralStat.current = (int)playerCommon.Minerals;
            VespeneStat.current = (int)playerCommon.Vespene;
            SupplyProvide = playerCommon.FoodCap;
            SupplyConsume = playerCommon.FoodUsed;
            ArmyCount = (int)playerCommon.ArmyCount;
            FoodArmy = (int)playerCommon.FoodArmy;
            FoodWorkers = (int)playerCommon.FoodWorkers;
            IdleWorkerCount = (int)playerCommon.IdleWorkerCount;
            WarpGateCount = (int)playerCommon.WarpGateCount;
        }
    }
}

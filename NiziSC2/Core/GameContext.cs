﻿using SC2APIProtocol;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Linq;
using System.Xml.Serialization;

namespace NiziSC2.Core
{
    public class GameContext
    {
        public LaunchOptions launchOptions;
        public ResponseGameInfo gameInfo;
        public ResponseData gameData;
        public ResponseObservation observation;

        public Dictionary<int, PlayerProperties> players;
        public Dictionary<ulong, Unit> units = new Dictionary<ulong, Unit>();
        public Dictionary<ulong, Unit> unitLose = new Dictionary<ulong, Unit>();
        public Dictionary<uint, UnitType> buildId2Units = new Dictionary<uint, UnitType>();
        public Dictionary<Race, RaceData> raceDatas;
        public Dictionary<UnitType, TechUnit> type2Tech = new Dictionary<UnitType, TechUnit>();
        public Race playerRace;
        public ulong frame = 0;
        public int playerId;
        #region Game Connection
        public GameConnection gameConnection;
        public void ConnectToGame(int port)
        {
            gameConnection = new GameConnection();
            gameConnection.Connect("127.0.0.1", port);
        }

        public uint JoinGame(Race race)
        {
            var request = new Request
            {
                JoinGame = new RequestJoinGame
                {
                    Race = race,
                    Options = new InterfaceOptions
                    {
                        Raw = true,
                        Score = true,
                    }
                }
            };
            var response = gameConnection.Request(request);

            if (response.JoinGame.Error != ResponseJoinGame.Types.Error.Unset)
            {
                if (response.CreateGame.Error != ResponseCreateGame.Types.Error.Unset)
                {
                    throw new Exception(string.Format("Response error \ndetail:{0}", response.CreateGame.ErrorDetails));
                }
            }
            playerId = (int)response.JoinGame.PlayerId;
            playerRace = race;
            return response.JoinGame.PlayerId;
        }

        public uint JoinGameLadder(Race race, int startPort)
        {
            var joinGame = new RequestJoinGame();
            joinGame.Race = race;

            joinGame.SharedPort = startPort + 1;
            joinGame.ServerPorts = new PortSet();
            joinGame.ServerPorts.GamePort = startPort + 2;
            joinGame.ServerPorts.BasePort = startPort + 3;

            joinGame.ClientPorts.Add(new PortSet());
            joinGame.ClientPorts[0].GamePort = startPort + 4;
            joinGame.ClientPorts[0].BasePort = startPort + 5;

            joinGame.Options = new InterfaceOptions
            {
                Raw = true,
                Score = true
            };

            var request = new Request();
            request.JoinGame = joinGame;

            var response = gameConnection.Request(request);

            if (response.JoinGame.Error != ResponseJoinGame.Types.Error.Unset)
            {
                throw new Exception(string.Format("{0} {1}", response.JoinGame.Error.ToString(), response.JoinGame.ErrorDetails));
            }

            playerId = (int)response.JoinGame.PlayerId;
            playerRace = race;
            return response.JoinGame.PlayerId;
        }
        #endregion
        public void GameInitialize()
        {
            LoadRaceData();
            gameInfo = gameConnection.RequestGameInfo().GameInfo;
            gameData = gameConnection.RequestData().Data;
            players = new Dictionary<int, PlayerProperties>();
            players.Add(playerId, new PlayerProperties());
            players[playerId].race = playerRace;

            foreach (var unitType in gameData.Units)
            {
                buildId2Units[unitType.AbilityId] = (UnitType)unitType.UnitId;
            }
        }

        public void LoadRaceData()
        {
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(RaceData));

            raceDatas = new Dictionary<Race, RaceData>();
            var raceTerran = (RaceData)xmlSerializer.Deserialize(File.OpenRead("Data/RaceDataTerran.xml"));
            var raceProtoss = (RaceData)xmlSerializer.Deserialize(File.OpenRead("Data/RaceDataProtoss.xml"));
            var raceZerg = (RaceData)xmlSerializer.Deserialize(File.OpenRead("Data/RaceDataZerg.xml"));
            raceDatas.Add(Race.Terran, raceTerran);
            raceDatas.Add(Race.Protoss, raceProtoss);
            raceDatas.Add(Race.Zerg, raceZerg);

            foreach (var u in raceTerran.TechUnits)
                type2Tech[u.UnitType] = u;
            foreach (var u in raceProtoss.TechUnits)
                type2Tech[u.UnitType] = u;
            foreach (var u in raceZerg.TechUnits)
                type2Tech[u.UnitType] = u;
        }

        public void FrameBegin()
        {
            var player = players[playerId];
            player.SupplyProvideVirtual = 0;
            HashSet<Unit> unTracked = new HashSet<Unit>(units.Values);
            foreach (var rawUnit in observation.Observation.RawData.Units)
            {
                if (units.TryGetValue(rawUnit.Tag, out var unit))
                {
                }
                else if (unitLose.TryGetValue(rawUnit.Tag, out unit))
                {
                    units.Add(unit.Tag, unit);
                    unitLose.Remove(unit.Tag);
                }
                else
                {
                    unit = new Unit();
                    units.Add(rawUnit.Tag, unit);
                }
                unit.RawDataUpdate(rawUnit);
                unit.lastTrackFrame = frame;

                unTracked.Remove(unit);
                var unitData = gameData.Units[(int)rawUnit.UnitType];
                if (rawUnit.Alliance == Alliance.Self)
                    player.SupplyProvideVirtual += unitData.FoodProvided;
            }
            foreach (var unit in unTracked)
            {
                unitLose[unit.Tag] = unit;
                units.Remove(unit.Tag);
            }
            RemoveUnitDeactivated();

            player.FromPlayerCommon(observation.Observation.PlayerCommon);

            cache1.Clear();
            playerUnits.Clear();
            anotherCache.Clear();
            unitProjections.Clear();
        }
        public void FrameEnd()
        {
            gameConnection.RequestStep(1);
            frame++;
        }

        public void RemoveUnitDeactivated()
        {
            //HashSet<ulong> deathUnits = new HashSet<ulong>();
            //foreach (var unit in units)
            //    if (unit.Value.health == 0 && unit.Value.healthMax != 0)
            //        deathUnits.Add(unit.Key);
            //foreach (var unit in deathUnits)
            //{
            //    units.Remove(unit);
            //}
        }

        public bool CanAfford(UnitType unitType)
        {
            var unitData = gameData.Units[(int)unitType];
            var player = players[playerId];
            return (player.MineralStat.current >= unitData.MineralCost) && (player.VespeneStat.current >= unitData.VespeneCost &&
                (unitData.FoodRequired == 0 || player.SupplyProvide - player.SupplyConsume >= unitData.FoodRequired));
        }

        public bool Afford(UnitType unitType)
        {
            var unitData = gameData.Units[(int)unitType];
            var player = players[playerId];
            if (CanAfford(unitType))
            {
                player.MineralStat.current -= (int)unitData.MineralCost;
                player.VespeneStat.current -= (int)unitData.VespeneCost;
                player.SupplyConsume += unitData.FoodRequired;
                return true;
            }
            return false;
        }

        public string GetUnitName(UnitType unitType)
        {
            return gameData.Units[(int)unitType].Name;
        }

        public UnitType GetUnitEqualType(UnitType unitType)
        {
            return (UnitType)gameData.Units[(int)unitType].UnitAlias;
        }
        Dictionary<SC2APIProtocol.Alliance, List<Unit>> cache1 = new Dictionary<Alliance, List<Unit>>();
        Dictionary<UnitType, List<Unit>> playerUnits = new Dictionary<UnitType, List<Unit>>();
        Dictionary<string, List<Unit>> anotherCache = new Dictionary<string, List<Unit>>();
        Dictionary<UnitType, List<UnitProjection>> unitProjections = new Dictionary<UnitType, List<UnitProjection>>();
        public List<Unit> GetUnits(SC2APIProtocol.Alliance alliance)
        {
            if (!cache1.TryGetValue(alliance, out var u1))
            {
                u1 = units.Where(u => { return u.Value.alliance == alliance; }).Select(u => { return u.Value; }).ToList();
                cache1[alliance] = u1;
            }
            return u1;
        }
        public List<Unit> GetPlayerUnits(UnitType unitType)
        {
            if (!playerUnits.TryGetValue(unitType, out var u1))
            {
                u1 = GetUnits(Alliance.Self).Where(u => { return (u.type == unitType || GetUnitEqualType(u.type) == unitType) && u.buildProgress == 1; }).ToList();
                playerUnits[unitType] = u1;
            }
            return u1;
        }
        public List<Unit> GetWorkers()
        {
            string workers = "Workers";
            if (!anotherCache.TryGetValue(workers, out var u1))
            {
                u1 = GetUnits(Alliance.Self).Where(u => { return UnitTypeInfo.Workers.Contains(u.type); }).ToList();
                anotherCache[workers] = u1;
            }
            return u1;
        }
        public List<Unit> GetMinerals()
        {
            string minerals = "Minerals";
            if (!anotherCache.TryGetValue(minerals, out var u1))
            {
                u1 = GetUnits(Alliance.Neutral).Where(u => { return UnitTypeInfo.MineralFields.Contains(u.type); }).ToList();
                anotherCache[minerals] = u1;
            }
            return u1;
        }
        public List<Unit> GetVespeneGeysers()
        {
            string vespeneGeysers = "VespeneGeysers";
            if (!anotherCache.TryGetValue(vespeneGeysers, out var u1))
            {
                u1 = GetUnits(Alliance.Neutral).Where(u => { return UnitTypeInfo.VespeneGeysers.Contains(u.type); }).ToList();
                anotherCache[vespeneGeysers] = u1;
            }
            return u1;
        }
        public List<UnitProjection> GetUnitProjections(UnitType unitType)
        {
            List<UnitProjection> proj = new List<UnitProjection>();
            proj.AddRange(GetUnits(Alliance.Self).Where(u => { return (u.type == unitType || GetUnitEqualType(u.type) == unitType); })
                .Select(u => { return new UnitProjection { position = u.position, Tag = u.Tag, unitType = u.type, }; }));
            foreach (var u in GetWorkers())
            {
                foreach (var order in u.orders)
                {
                    if (buildId2Units.TryGetValue(order.AbilityId, out var unitType1) && order.TargetUnitTag == 0)
                    {
                        proj.Add(new UnitProjection { position = u.position, Tag = 0, unitType = u.type, });
                    }
                }
            }

            return null;
        }
        public List<UnitProjection> GetUnitProjectionsRefinery()
        {
            List<UnitProjection> proj = new List<UnitProjection>();
            proj.AddRange(GetUnits(Alliance.Self).Where(u => { return UnitTypeInfo.Refinery.Contains(u.type); })
                .Select(u => { return new UnitProjection { position = u.position, Tag = u.Tag, unitType = u.type, }; }));
            proj.AddRange(GetUnits(Alliance.Enemy).Where(u => { return UnitTypeInfo.Refinery.Contains(u.type); })
                .Select(u => { return new UnitProjection { position = u.position, Tag = u.Tag, unitType = u.type, }; }));
            foreach (var u in GetWorkers())
            {
                foreach (var order in u.orders)
                {
                    if (buildId2Units.TryGetValue(order.AbilityId, out var unitType1) && UnitTypeInfo.Refinery.Contains(unitType1))
                    {
                        if (units.TryGetValue(order.TargetUnitTag, out var unit1) && UnitTypeInfo.VespeneGeysers.Contains(unit1.type))
                            proj.Add(new UnitProjection { position = unit1.position, Tag = 0, unitType = unitType1, });
                        else if (unitLose.TryGetValue(order.TargetUnitTag, out unit1) && UnitTypeInfo.VespeneGeysers.Contains(unit1.type))
                            proj.Add(new UnitProjection { position = unit1.position, Tag = 0, unitType = unitType1, });
                    }
                }
            }
            return proj;
        }
        public bool UnitCanBuild(UnitType unitType)
        {
            var techUnit = type2Tech[unitType];
            if (GetPlayerUnits(techUnit.Spawner).Count > 0)
            {
                if (techUnit.Depends == null || techUnit.Depends.Count == 0)
                    return true;
                foreach(var depend in techUnit.Depends)
                {
                    if (GetPlayerUnits(depend).Count == 0)
                    {
                        return false;
                    }
                }
                return true;
            }
            return false;
        }
    }
}
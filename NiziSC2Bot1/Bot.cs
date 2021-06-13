using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Numerics;
using NiziSC2.Core;
using System.Threading.Tasks;

namespace NiziSC2Bot1
{
    public class Bot
    {
        public GameContext gameContext;
        public Random random;
        public List<Vector2> startLocations;
        public WriteableImageData placementGrid;
        public WriteableImageData nonResPlacementGrid;
        public NearGroup mineralGroup;
        public BotActionFlag flags;
        public ImageScaledMap scaledVisMap = new ImageScaledMap();

        public void Initialize()
        {
            random = new Random();
            placementGrid = new WriteableImageData(gameContext.gameInfo.StartRaw.PlacementGrid);
            nonResPlacementGrid = new WriteableImageData(gameContext.gameInfo.StartRaw.PlacementGrid);

            scaledVisMap.Init(new Int2(16, 16));

            var neutrals = gameContext.units.Where(u => { return u.Value.alliance == SC2APIProtocol.Alliance.Neutral; }).Select(u => { return u.Value; }).ToArray();
            var minerals = neutrals.Where(u => { return UnitTypeInfo.MineralFields.Contains(u.type); }).ToArray();
            foreach (var mineral in minerals)
            {
                Int2 p = new Int2((int)mineral.position.X, (int)mineral.position.Y);
                foreach (var p1 in TimeAndSpace.Box(p.X - 4, p.Y - 4, p.X + 4, p.Y + 4))
                {
                    placementGrid.Write(p1, false);
                }
                foreach (var p1 in TimeAndSpace.Box(p.X - 3, p.Y - 3, p.X + 3, p.Y + 3))
                {
                    nonResPlacementGrid.Write(p1, false);
                }
            }
            mineralGroup = new NearGroup();
            mineralGroup.BuildMineral(minerals, 15);

            for (int i = 0; i < mineralGroup.middlePoints.Count; i++)
            {
                Vector2 middlePoint = mineralGroup.middlePoints[i];
                if (!placementGrid.Query((int)middlePoint.X, (int)middlePoint.Y))
                    for (int l = 0; l < 100; l++)
                    {
                        Vector2 middlePoint1 = RandomPosition(middlePoint, 4);
                        if (placementGrid.Query((int)middlePoint1.X, (int)middlePoint1.Y))
                        {
                            mineralGroup.middlePoints[i] = middlePoint1;
                            break;
                        }
                    }
            }
            foreach (var middlePoint in mineralGroup.middlePoints)
            {
                var p = new Int2((int)middlePoint.X, (int)middlePoint.Y);
                foreach (var p1 in TimeAndSpace.Box(p.X - 3, p.Y - 3, p.X + 3, p.Y + 3))
                {
                    nonResPlacementGrid.Write(p1, false);
                }
            }
        }
        public ActionList Work()
        {
            var player = gameContext.players[gameContext.playerId];
            ActionList action = new ActionList();
            action.gameContext = gameContext;
            var visibility = gameContext.observation.Observation.RawData.MapState.Visibility;

            var playerUnits = gameContext.GetUnits(SC2APIProtocol.Alliance.Self);
            var enemies = gameContext.GetUnits(SC2APIProtocol.Alliance.Enemy);
            var neutrals = gameContext.GetUnits(SC2APIProtocol.Alliance.Neutral);

            var commandCenters = playerUnits.Where(u => { return UnitTypeInfo.ResourceCenters.Contains(u.type); }).ToArray();
            var raceData = gameContext.raceDatas[player.race];
            var resourceCommandCenter = commandCenters.Where(u =>
            {
                for (int i = 0; i < mineralGroup.middlePoints.Count; i++)
                {
                    Vector2 p = mineralGroup.middlePoints[i];
                    if (Vector2.Distance(p, u.position) < 5)
                    {
                        foreach (var mineral in mineralGroup.nearUnits[i])
                        {
                            if (mineral.health > 0)
                                return true;
                        }
                    }
                }
                return false;
            }).ToArray();
            var workers = gameContext.GetWorkers();
            List<Unit> barracks;
            if (raceData.Race == SC2APIProtocol.Race.Terran)
                barracks = playerUnits.Where(u => { return UnitTypeInfo.Barracks.Contains(u.type); }).ToList();
            else if (raceData.Race == SC2APIProtocol.Race.Protoss)
                barracks = playerUnits.Where(u => { return UnitTypeInfo.GateWays.Contains(u.type); }).ToList();
            else if (raceData.Race == SC2APIProtocol.Race.Zerg)
                barracks = null;
            else
                throw new NotImplementedException();
            var refineries = playerUnits.Where(u => { return UnitTypeInfo.Refinery.Contains(u.type); }).ToArray();
            var refineriesProjection = gameContext.GetUnitProjectionsRefinery();

            var army = playerUnits.Where(u => { return UnitTypeInfo.Army.Contains(u.type); }).ToList();
            var minerals = gameContext.GetMinerals();
            var minerals1 = new HashSet<Unit>(minerals);
            var geysers = gameContext.GetVespeneGeysers();
            var mineralsInSight = minerals.Where(u =>
            {
                if (u.healthMax != 0)
                {
                    return commandCenters.Any(commandCenter => Vector2.Distance(commandCenter.position, u.position) < 8);
                }
                return false;
            }).ToArray();
            var needAddons = playerUnits.Where(u => { return UnitTypeInfo.NeedAddon.Contains(u.type) && u.addOnTag == 0 && u.orders.Count == 0; }).ToArray();
            var unitBuildRequst = new Dictionary<UnitType, int>();
            var unitVirtualCount = new Dictionary<UnitType, int>();

            foreach (var unit in playerUnits)
            {
                foreach (var order in unit.orders)
                {
                    if (gameContext.buildId2Units.TryGetValue(order.AbilityId, out var buildUnitType))
                    {
                        if (unitVirtualCount.ContainsKey(buildUnitType))
                        {
                            unitVirtualCount[buildUnitType]++;
                        }
                        else
                        {
                            unitVirtualCount[buildUnitType] = 1;
                        }
                    }
                }
                if (unitVirtualCount.ContainsKey(unit.type))
                {
                    unitVirtualCount[unit.type]++;
                }
                else
                {
                    unitVirtualCount[unit.type] = 1;
                }
            }
            var geysersCanBuild = geysers.Where(u =>
            {
                if (u.healthMax == 0) return false;
                foreach (var commandCenter in commandCenters)
                    if (Vector2.Distance(u.position, commandCenter.position) < 15)
                    {
                        foreach (var refinery in refineriesProjection)
                            if (Vector2.Distance(refinery.position, u.position) < 2)
                                return false;
                        return true;
                    }
                return false;
            });
            if (startLocations == null)
            {
                var result = gameContext.gameInfo.StartRaw.StartLocations.Where(p =>
                {
                    var pos = commandCenters[0].position;
                    return Vector2.Distance(new Vector2(p.X, p.Y), new Vector2(pos.X, pos.Y)) > 20;
                }).Select(p => { return new Vector2(p.X, p.Y); });

                startLocations = new List<Vector2>(result);
                foreach (var startLoc in startLocations)
                {
                    scaledVisMap.Write(new Int2((int)(startLoc.X / visibility.Size.X * scaledVisMap.Size.X), (int)(startLoc.Y / visibility.Size.Y * scaledVisMap.Size.Y)), -1);
                }
                for (int i = 0; i < scaledVisMap.Size.X; i++)
                {
                    scaledVisMap.Write(new Int2(i, 0), int.MaxValue);
                    scaledVisMap.Write(new Int2(i, 1), int.MaxValue);
                    scaledVisMap.Write(new Int2(i, scaledVisMap.Size.Y - 1), int.MaxValue);
                    scaledVisMap.Write(new Int2(i, scaledVisMap.Size.Y - 2), int.MaxValue);
                }
                for (int i = 0; i < scaledVisMap.Size.Y; i++)
                {
                    scaledVisMap.Write(new Int2(0, i), int.MaxValue);
                    scaledVisMap.Write(new Int2(1, i), int.MaxValue);
                    scaledVisMap.Write(new Int2(scaledVisMap.Size.X - 1, i), int.MaxValue);
                    scaledVisMap.Write(new Int2(scaledVisMap.Size.X - 2, i), int.MaxValue);
                }
            }

            List<Unit> GetUnits(UnitType _unitType)
            {
                return gameContext.GetPlayerUnits(_unitType);
            }
            int GetUnitVirtualCount(UnitType _unitType)
            {
                unitVirtualCount.TryGetValue(_unitType, out int value);
                return value;
            }
            int workerTest1 = 0;
            foreach (var unit1 in workers)
            {
                if (unit1.orders.Count == 0 && mineralsInSight.Length > 0)
                {
                    action.EnqueueSmart(new[] { unit1 }, mineralsInSight[random.Next(0, mineralsInSight.Length)].Tag);
                }
                foreach (var order in unit1.orders)
                {
                    if (gameContext.buildId2Units.TryGetValue(order.AbilityId, out UnitType unitType))
                    {
                        player.SupplyProvideVirtual += gameContext.gameData.Units[(int)unitType].FoodProvided;
                    }
                    Abilities abil = (Abilities)order.AbilityId;
                    if (workerTest1 < 1 && (abil == Abilities.HARVEST_GATHER_SCV || abil == Abilities.HARVEST_GATHER_DRONE || abil == Abilities.HARVEST_GATHER_PROBE))
                    {
                        if (gameContext.units.TryGetValue(order.TargetUnitTag, out var target) && minerals1.Contains(target))
                        {
                            foreach (var commandCenter in commandCenters)
                            {
                                if (Vector2.Distance(commandCenter.position, target.position) < 15 && commandCenter.assignedHarvesters > commandCenter.idealHarvesters * 3 / 2)
                                {
                                    action.EnqueueAbility(new[] { unit1 }, Abilities.STOP);
                                    workerTest1++;
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            foreach (var needAddon in needAddons)
            {
                if (gameContext.Afford(UnitType.TERRAN_TECHLAB))
                {
                    action.EnqueueAbility(new[] { needAddon }, Abilities.LIFT);
                    var pos = RandomPosition(needAddon.position, 10);
                    if (nonResPlacementGrid.Query((int)pos.X, (int)pos.Y))
                    {
                        action.EnqueueAbility(new[] { needAddon }, Abilities.BUILD_TECHLAB, pos);
                    }
                }
            }

            foreach (var engineeringBay in GetUnits(UnitType.TERRAN_ENGINEERINGBAY))
            {
                action.EnqueueAbility(new[] { engineeringBay }, Abilities.RESEARCH_TERRANINFANTRYWEAPONS);
                action.EnqueueAbility(new[] { engineeringBay }, Abilities.RESEARCH_TERRANINFANTRYARMOR);
            }
            foreach (var p in scaledVisMap.EachPoint1())
            {
                Vector2 p1 = scaledVisMap.GetPos(p);
                if (visibility.SampleBytePoint(p1) == 2)
                {
                    scaledVisMap.Write(p, (int)gameContext.frame);
                }
                if (!gameContext.gameInfo.StartRaw.PlacementGrid.SamplePoint(p1))
                {
                    scaledVisMap.Write(p, (int)gameContext.frame);
                }
            }
            var pos1 = scaledVisMap.GetPos(scaledVisMap.GetLow(out _));
            Vector2 attackPos = pos1 * new Vector2(visibility.Size.X - 1, visibility.Size.Y - 1);
            if (enemies.Count > 0)
            {
                if (player.FoodArmy > 50)
                    action.EnqueueAttack(army, enemies[0].position);
                else if (player.FoodArmy > 30 && commandCenters.Length > 0 && Vector2.Distance(enemies[0].position, commandCenters[0].position) < 50)
                    action.EnqueueAttack(army, enemies[0].position);
                if (player.FoodArmy > 20)
                {
                    for (int i = 0; i < 1; i++)
                    {
                        var randomUnit = GetRandom(army);
                        var randomEnemy = GetRandom(enemies);
                        if (Vector2.Distance(randomUnit.position, randomEnemy.position) < 3)
                        {
                            action.EnqueueAbility(new[] { randomUnit }, Abilities.ATTACK, randomEnemy.Tag);
                        }
                    }
                }
            }
            else if (player.FoodArmy > 50)
            {
                //action.EnqueueAttack(army, new Vector2(startLocations[0].X, startLocations[0].Y));
                action.EnqueueAttack(army, attackPos);
            }
            UnitType barrack = UnitType.TERRAN_BARRACKS;

            if (raceData.Race == SC2APIProtocol.Race.Terran)
                barrack = UnitType.TERRAN_BARRACKS;
            else if (raceData.Race == SC2APIProtocol.Race.Protoss)
                barrack = UnitType.PROTOSS_GATEWAY;
            else if (raceData.Race == SC2APIProtocol.Race.Zerg)
                barrack = 0;
            else
                throw new NotImplementedException();

            if (workers.Count > 0)
            {
                if (barrack != 0)
                {
                    if (player.SupplyProvideVirtual - player.SupplyConsume < 4 + player.SupplyConsume * 0.3f)
                    {
                        if (gameContext.Afford(raceData.SupplyBuilding))
                        {
                            var unit = GetRandom(workers);
                            Vector2 pos = RandomPosition(unit.position, 10);
                            if (nonResPlacementGrid.Query((int)pos.X, (int)pos.Y))
                                action.EnqueueBuild(unit.Tag, raceData.SupplyBuilding, pos);
                        }
                    }
                    if (barracks.Count < (commandCenters.Length > 2 ? 10 : 3))
                    {
                        if (gameContext.Afford(barrack))
                        {
                            var unit = GetRandom(workers);
                            Vector2 pos = RandomPosition(unit.position, 10);
                            if (nonResPlacementGrid.Query((int)pos.X, (int)pos.Y))
                                action.EnqueueBuild(unit.Tag, barrack, pos);
                        }
                    }
                }
                else
                {
                    if (player.SupplyProvideVirtual - player.SupplyConsume < 4 + player.SupplyConsume * 0.3f)
                    {
                        var larvas = gameContext.GetPlayerUnits(UnitType.ZERG_LARVA);
                        if (larvas.Count > 0)
                        {
                            var tbase = GetRandom(larvas);
                            TryTrain(tbase, raceData.SupplyBuilding, action);
                        }
                    }
                }
                if (GetUnitVirtualCount(barrack) > 2 && (gameContext.frame & 8) > 0 || player.FoodWorkers >= 16 && player.VespeneStat.current < 500)
                    foreach (var geyser in geysersCanBuild)
                    {
                        if (gameContext.Afford(raceData.VespeneBuilding))
                        {
                            var unit = GetRandom(workers);
                            action.EnqueueBuild(unit.Tag, raceData.VespeneBuilding, geyser.Tag);
                        }
                    }
                foreach (var refinery in refineries)
                {
                    if (refinery.buildProgress == 1 && refinery.vespeneContents > 0)
                    {
                        if (refinery.assignedHarvesters < 3)
                        {
                            var unit = GetRandom(workers);
                            action.EnqueueSmart(new[] { unit }, refinery.Tag);
                        }
                        else if (refinery.assignedHarvesters > 3)
                        {
                            var worker = workers.FirstOrDefault(u => { return u.orders.Count > 0 && u.orders[0].TargetUnitTag == refinery.Tag; });
                            if (worker != null)
                            {
                                action.EnqueueAbility(new[] { worker }, Abilities.STOP);
                            }
                        }
                    }
                }
            }
            bool EnoughWorker = commandCenters.All(u => { return u.assignedHarvesters >= u.idealHarvesters; });
            if (player.SupplyConsume > GetUnitVirtualCount(raceData.ResourceBuilding) * 20 + 10 || (EnoughWorker && commandCenters.Length > 2 && resourceCommandCenter.Length < 3) || player.MineralStat.current > 1000)
            {
                flags |= BotActionFlag.Expand;
            }
            else
            {
                flags &= ~BotActionFlag.Expand;
            }

            if (!flags.HasFlag(BotActionFlag.Expand) || player.MineralStat.current > 800)
                foreach (var techUnit in raceData.TechUnits)
                {
                    //if (UnitTypeInfo.Workers.Contains(techUnit.UnitType)) continue;

                    var spawners = GetUnits(techUnit.Spawner);
                    //var techUnits = GetUnits(techUnit.UnitType);
                    if (!UnitTypeInfo.Workers.Contains(techUnit.Spawner))
                    {
                        foreach (var spawner in spawners)
                            if (spawner.orders.Count == 0 && GetUnitVirtualCount(techUnit.UnitType) < GetUnits(techUnit.UnitType).Count + 2)
                            {
                                if (random.NextDouble() < 0.1)
                                    TryTrain(spawner, techUnit.UnitType, action);
                            }
                    }
                    else
                    {
                        if (workers.Count > 0)
                        {
                            var spawner = GetRandom(workers);

                            if (GetUnitVirtualCount(techUnit.UnitType) < 1)
                            {
                                Vector2 pos = RandomPosition(spawner.position, 10);
                                if (nonResPlacementGrid.Query((int)pos.X, (int)pos.Y) && gameContext.Afford(techUnit.UnitType))
                                    action.EnqueueBuild(spawner.Tag, techUnit.UnitType, pos);
                            }
                        }

                    }
                }
            if (flags.HasFlag(BotActionFlag.Expand))
            {
                if (gameContext.Afford(raceData.SupplyBuilding))
                {
                    Vector2 expandPoint = GetRandom(mineralGroup.middlePoints);


                    bool _stop1 = false;
                    for (int l = 0; l < 10; l++)
                    {
                        var expandPoint1 = GetRandom(mineralGroup.middlePoints);
                        foreach (var commandCenter in commandCenters)
                        {
                            var distance = Vector2.Distance(commandCenter.position, expandPoint1);
                            if (distance > 10 && distance < 50)
                            {
                                expandPoint = expandPoint1;
                                _stop1 = true;
                                break;
                            }
                        }
                        if (_stop1) break;
                    }
                    if (_stop1 && workers.Count > 0)
                    {
                        var spawner = GetRandom(workers);
                        for (int l = 0; l < 5; l++)
                        {
                            Vector2 pos = RandomPosition(expandPoint, 3);
                            if (placementGrid.Query((int)pos.X, (int)pos.Y))
                            {
                                action.EnqueueBuild(spawner.Tag, raceData.ResourceBuilding, pos);
                                break;
                            }
                        }
                    }

                }
            }
            if (!EnoughWorker && player.FoodWorkers < 80)
            {
                var larvas = gameContext.GetPlayerUnits(raceData.Worker.Spawner);
                if (larvas.Count > 0)
                {
                    var tbase = GetRandom(larvas);
                    TryTrain(tbase, raceData.Worker.UnitType, action);
                }
            }

            return action;
        }

        public Vector2 RandomPosition(Vector2 position, float range)
        {
            return position + new Vector2((float)(random.NextDouble() * range * 2 - range), (float)(random.NextDouble() * range * 2 - range));
        }

        public bool TryTrain(Unit unit, UnitType unitType, ActionList action)
        {
            if (unit.orders.Count == 0 && gameContext.Afford(unitType))
            {
                action.EnqueueTrain(unit.Tag, unitType);
                return true;
            }
            return false;
        }

        public T GetRandom<T>(IList<T> list)
        {
            return list[random.Next(0, list.Count)];
        }

        [Flags]
        public enum BotActionFlag
        {
            None = 0,
            Expand = 1,
            Attack = 2,
        }
    }
}

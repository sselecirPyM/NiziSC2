using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Numerics;
using NiziSC2.Core;
using System.Threading.Tasks;

namespace PhotonCannonRush
{
    public class Bot
    {
        public GameContext gameContext;
        public Random random;
        public List<Vector2> startLocations;
        public WriteableImageData placementGrid;
        public WriteableImageData nonResPlacementGrid;
        public NearGroup mineralGroup;
        public ImageScaledMap scaledVisMap = new ImageScaledMap();
        public int cancelCount = 0;
        public ulong perviousEnemyTag;

        public void Initialize()
        {
            random = new Random();
            placementGrid = new WriteableImageData(gameContext.gameInfo.StartRaw.PlacementGrid);
            nonResPlacementGrid = new WriteableImageData(gameContext.gameInfo.StartRaw.PlacementGrid);

            scaledVisMap.Init(new Int2(24, 24));

            var minerals = gameContext.GetMinerals();
            var vespenes = gameContext.GetVespeneGeysers();
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
            mineralGroup.BuildMineral(minerals, vespenes, gameContext.gameInfo.StartRaw.PlacementGrid, 15);

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
            var enemiesArmy = enemies.Where(u => UnitTypeInfo.Army.Contains(u.type)).ToList();
            var enemiesArmyFood = enemiesArmy.Sum(u => gameContext.gameData.Units[(int)u.type].FoodRequired);

            var commandCenters = playerUnits.Where(u => { return UnitTypeInfo.ResourceCenters.Contains(u.type); }).ToArray();
            var raceData = gameContext.raceDatas[player.race];
            var resourceCommandCenter = commandCenters.Where(u =>
            {
                if (u.buildProgress < 1)
                    return true;
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

            var refineries = playerUnits.Where(u => { return UnitTypeInfo.Refinery.Contains(u.type); }).ToArray();
            var refineriesProjection = gameContext.GetUnitProjectionsRefinery();
            var refinerWorkerCount = refineries.Sum(u => u.assignedHarvesters);

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

                var unitData = gameContext.gameData.Units[(int)unit.type];
                foreach (var alias in unitData.TechAlias)
                {
                    var alias1 = (UnitType)alias;
                    if (unitVirtualCount.ContainsKey(alias1))
                    {
                        unitVirtualCount[alias1]++;
                    }
                    else
                    {
                        unitVirtualCount[alias1] = 1;
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

                if (unitVirtualCount.ContainsKey((UnitType)unitData.UnitAlias))
                {
                    unitVirtualCount[(UnitType)unitData.UnitAlias]++;
                }
                else
                {
                    unitVirtualCount[(UnitType)unitData.UnitAlias] = 1;
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
                    Unit u1 = GetRandom(mineralsInSight);
                    for (int i = 0; i < 2; i++)
                    {
                        var u2 = GetRandom(mineralsInSight);
                        if (Vector2.DistanceSquared(unit1.position, u1.position) > Vector2.DistanceSquared(unit1.position, u2.position))
                            u1 = u2;
                    }
                    action.EnqueueSmart(new[] { unit1 }, u1.Tag);
                }
                foreach (var order in unit1.orders)
                {
                    if (gameContext.buildId2Units.TryGetValue(order.AbilityId, out UnitType unitType))
                    {
                        if (!UnitTypeInfo.ResourceCenters.Contains(unitType))
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

            foreach (var commandCenter in commandCenters)
            {
                if (commandCenter.type == UnitType.TERRAN_COMMANDCENTER && player.VespeneStat.current >= 1000)
                {
                    action.EnqueueAbility(new[] { commandCenter }, Abilities.MORPH_PLANETARYFORTRESS);
                }
                if (player.race == SC2APIProtocol.Race.Terran && commandCenter.health < commandCenter.healthMax - 100 && commandCenter.buildProgress == 1 && player.VespeneStat.current >= 200)
                {
                    var nearUnits = GetNearbyUnit(workers, commandCenter.position, 5);
                    action.EnqueueAbility(nearUnits, Abilities.EFFECT_REPAIR, commandCenter.Tag);
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
                if (engineeringBay.orders.Count == 0)
                {
                    int r = random.Next(0, 4);
                    if (r == 0)
                        action.EnqueueAbility(new[] { engineeringBay }, Abilities.RESEARCH_TERRANINFANTRYWEAPONS);
                    if (r == 1)
                        action.EnqueueAbility(new[] { engineeringBay }, Abilities.RESEARCH_TERRANINFANTRYARMOR);
                    if (r == 2)
                        action.EnqueueAbility(new[] { engineeringBay }, Abilities.RESEARCH_HISECAUTOTRACKING);
                    if (r == 3)
                        action.EnqueueAbility(new[] { engineeringBay }, Abilities.RESEARCH_TERRANSTRUCTUREARMORUPGRADE);
                }
            }
            foreach (var armory in GetUnits(UnitType.TERRAN_ARMORY))
            {
                if (armory.orders.Count == 0)
                {
                    int r = random.Next(0, 3);
                    if (r == 0)
                        action.EnqueueAbility(new[] { armory }, Abilities.RESEARCH_TERRANSHIPWEAPONS);
                    if (r == 1)
                        action.EnqueueAbility(new[] { armory }, Abilities.RESEARCH_TERRANVEHICLEWEAPONS);
                    if (r == 2)
                        action.EnqueueAbility(new[] { armory }, Abilities.RESEARCH_TERRANVEHICLEANDSHIPPLATING);
                }
            }
            foreach (var archive in GetUnits(UnitType.PROTOSS_TEMPLARARCHIVE))
            {
                if (archive.orders.Count == 0)
                {
                    action.EnqueueAbility(new[] { archive }, Abilities.RESEARCH_PSISTORM);
                }
            }
            foreach (var lab in GetUnits(UnitType.TERRAN_BARRACKSTECHLAB))
            {
                if (lab.orders.Count == 0)
                {
                    int r = random.Next(0, 3);
                    if (r == 0)
                        action.EnqueueAbility(new[] { lab }, Abilities.RESEARCH_STIMPACK);
                    if (r == 1)
                        action.EnqueueAbility(new[] { lab }, Abilities.RESEARCH_COMBATSHIELD);
                    if (r == 2)
                        action.EnqueueAbility(new[] { lab }, Abilities.RESEARCH_CONCUSSIVESHELLS);
                }
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

            for (int i = 0; i < 10; i++)
            {
                if (army.Count < 1 || enemies.Count < 1) break;
                var randomUnit = GetRandom(army);
                var randomEnemy = GetRandom(enemies);
                if (randomUnit.type == UnitType.TERRAN_GHOST && Vector2.Distance(randomUnit.position, randomEnemy.position) < 10 && (randomEnemy.shield > 0 || randomEnemy.energy > 20))
                {
                    action.EnqueueAbility(new[] { randomUnit }, Abilities.EFFECT_EMP, randomEnemy.position);
                    army.Remove(randomUnit);
                }
                if (randomUnit.type == UnitType.PROTOSS_HIGHTEMPLAR && Vector2.Distance(randomUnit.position, randomEnemy.position) < 8)
                {
                    action.EnqueueAbility(new[] { randomUnit }, Abilities.EFFECT_PSISTORM, randomEnemy.position);
                    army.Remove(randomUnit);
                }
                if (randomUnit.type == UnitType.PROTOSS_HIGHTEMPLAR && Vector2.Distance(randomUnit.position, randomEnemy.position) < 8 && randomEnemy.energy > 20)
                {
                    action.EnqueueAbility(new[] { randomUnit }, Abilities.EFFECT_FEEDBACK, randomEnemy.Tag);
                    army.Remove(randomUnit);
                }
            }

            var pos1 = scaledVisMap.GetPos(scaledVisMap.GetLow(out _));
            Vector2 attackPos = pos1 * new Vector2(visibility.Size.X, visibility.Size.Y);
            float foodArmyReal = army.Sum(u => gameContext.gameData.Units[(int)u.type].FoodRequired);
            var army2 = army.Where(u => u.weaponCooldown < 0.1);
            var army3 = army.Where(u => u.weaponCooldown >= 0.1);
            foreach (var unit in army3)
            {
                if (unit.engagedTargetTag != 0 && gameContext.units.TryGetValue(unit.engagedTargetTag, out var enemy))
                {
                    if (enemiesArmy.Count > 2)
                        action.EnqueueSmart(new[] { unit }, unit.position + Vector2.Normalize(unit.position - enemy.position) * 3);
                    else
                        action.EnqueueSmart(new[] { unit }, unit.position - Vector2.Normalize(unit.position - enemy.position) * 3);
                }
            }
            if (enemies.Count > 0 && enemiesArmyFood <= 10 && foodArmyReal >= 50 && (gameContext.frame & 16) != 0)
            {
                var prevEnemy = enemies.Where(u => u.Tag == perviousEnemyTag).ToArray();
                Unit actualEnemy;
                if (prevEnemy.Length == 0 || random.NextDouble() < 0.01)
                {
                    actualEnemy = GetRandom(enemies);
                    perviousEnemyTag = actualEnemy.Tag;
                }
                else
                {
                    actualEnemy = prevEnemy[0];
                }
                Vector2 enemiesPosition = actualEnemy.position;

                if (foodArmyReal >= 50)
                    action.EnqueueAttack(army2, enemiesPosition);
                //else if (foodArmyReal >= 20 && commandCenters.Length > 0 &&commandCenters.Any(u=>Vector2.Distance(enemiesPosition, u.position) < 40) )
                //    action.EnqueueAttack(army2, enemiesPosition);
                if (foodArmyReal >= 15)
                {
                    for (int i = 0; i < 1; i++)
                    {
                        var randomUnit = GetRandom(army);
                        var randomEnemy1 = GetRandom(enemies);
                        if (Vector2.Distance(randomUnit.position, randomEnemy1.position) < 3)
                        {
                            action.EnqueueAbility(new[] { randomUnit }, Abilities.ATTACK, randomEnemy1.Tag);
                        }
                    }
                }
            }
            else if (foodArmyReal >= 40 && (gameContext.frame & 16) != 0)
            {
                action.EnqueueAttack(army2, attackPos);
            }

            if (workers.Count > 0)
            {
                if (raceData.Race == SC2APIProtocol.Race.Terran || raceData.Race == SC2APIProtocol.Race.Protoss)
                {
                    if (player.SupplyProvideVirtual - player.SupplyConsume < 4 + player.SupplyConsume * 0.3f && gameContext.Afford(raceData.SupplyBuilding))
                    {
                        var unit = GetRandom(workers);
                        Vector2 pos = RandomPosition(unit.position, 10);
                        if (nonResPlacementGrid.Query((int)pos.X, (int)pos.Y))
                            action.EnqueueBuild(unit.Tag, raceData.SupplyBuilding, pos);
                    }
                }
                else
                {
                    if (player.SupplyProvideVirtual - player.SupplyConsume < 4 + player.SupplyConsume * 0.3f && gameContext.Afford(raceData.SupplyBuilding))
                    {
                        var larvas = GetUnits(UnitType.ZERG_LARVA);
                        if (larvas.Count > 0)
                        {
                            var tbase = GetRandom(larvas);
                            TryTrain(tbase, raceData.SupplyBuilding, action);
                        }
                    }
                }
                if (cancelCount > 0 || player.MineralStat.current > 2000)
                    foreach (var barrack1 in raceData.Barracks)
                    {
                        if (barrack1.UnitType != raceData.ResourceBuilding && GetUnitVirtualCount(barrack1.UnitType) < player.SupplyProvide / 14)
                        {
                            if (gameContext.Afford(barrack1.UnitType))
                            {
                                var unit = GetRandom(workers);
                                Vector2 pos = RandomPosition(unit.position, 10);
                                if (nonResPlacementGrid.Query((int)pos.X, (int)pos.Y))
                                    action.EnqueueBuild(unit.Tag, barrack1.UnitType, pos);
                            }
                        }
                    }
                if (((gameContext.frame & 8) > 0 && player.FoodWorkers >= 16) && player.VespeneStat.current < 500 && player.MineralStat.current > 500)
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
                    if (refinery.buildProgress == 1 && refinery.vespeneContents > 0 && player.VespeneStat.current < 500 && refinerWorkerCount * 8 < workers.Count)
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
            bool expand = player.SupplyConsume > GetUnitVirtualCount(raceData.ResourceBuilding) * 20 || EnoughWorker || player.MineralStat.current > 1000 || (cancelCount == 0 && commandCenters.Length < 7);
            //expand = true;

            if ((cancelCount > 0) || player.MineralStat.current > 2000 || commandCenters.Length > mineralGroup.middlePoints.Count / 2)
                foreach (TechUnit techUnit in raceData.TechUnits)
                {
                    var spawners = GetUnits(techUnit.Spawner);
                    //var techUnits = GetUnits(techUnit.UnitType);
                    if (!UnitTypeInfo.Workers.Contains(techUnit.Spawner))
                    {
                        foreach (var spawner in spawners)
                            if (spawner.orders.Count == 0 && player.SupplyConsume < 197 && player.MineralStat.current > 400)
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

                            if (GetUnitVirtualCount(techUnit.UnitType) < 2)
                            {
                                Vector2 pos = RandomPosition(spawner.position, 10);
                                if (nonResPlacementGrid.Query((int)pos.X, (int)pos.Y) && gameContext.Afford(techUnit.UnitType))
                                    action.EnqueueBuild(spawner.Tag, techUnit.UnitType, pos);
                            }
                        }
                    }
                }
            var commandCenterProjection = gameContext.GetUnitProjections(UnitTypeInfo.ResourceCenters);
            if (expand)
            {
                if (gameContext.Afford(raceData.SupplyBuilding))
                {
                    Vector2 expandPoint = GetRandom(mineralGroup.middlePoints);

                    var expandPoint1 = GetRandom(mineralGroup.middlePoints);

                    //bool _stop1 = false;
                    bool _stop1 = true;
                    //foreach (var commandCenter in commandCenterProjection)
                    //{
                    //    var distance = Vector2.Distance(commandCenter.position, expandPoint1);
                    //    if (distance < 50)
                    //    {
                    //        expandPoint = expandPoint1;
                    //        _stop1 = true;
                    //        break;
                    //    }
                    //}

                    foreach (var commandCenter in commandCenterProjection)
                    {
                        var distance = Vector2.Distance(commandCenter.position, expandPoint1);
                        if (distance < 1)
                        {
                            _stop1 = false;
                            break;
                        }
                    }
                    if (_stop1 && workers.Count > 0)
                    {
                        Unit spawner = GetRandom(workers);
                        for (int i = 0; i < 2; i++)
                        {
                            var u2 = GetRandom(workers);
                            if (Vector2.DistanceSquared(expandPoint, spawner.position) > Vector2.DistanceSquared(expandPoint, u2.position))
                                spawner = u2;
                        }
                        action.EnqueueBuild(spawner.Tag, raceData.ResourceBuilding, expandPoint);
                    }
                }
            }
            if (!EnoughWorker && (player.FoodWorkers < 120 || (player.FoodWorkers < 150 && player.SupplyConsume < 150) || cancelCount == 0))
            {
                var larvas = GetUnits(raceData.Worker.Spawner);
                if (larvas.Count > 0)
                {
                    var tbase = GetRandom(larvas);
                    TryTrain(tbase, raceData.Worker.UnitType, action);
                }
            }

            var unitCaceled = playerUnits.Where(u => u.buildProgress < 1 && u.buildProgress * 0.25 > (float)u.health / u.healthMax).ToList();
            if (unitCaceled.Count > 0)
            {
                cancelCount += unitCaceled.Count;
                action.EnqueueAbility(unitCaceled, Abilities.CANCEL_BUILDINPROGRESS);

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
        public List<Unit> GetNearbyUnit(IEnumerable<Unit> units, Vector2 position, float range, HashSet<UnitType> unitTypes)
        {
            return units.Where(u => Vector2.Distance(u.position, position) < range && unitTypes.Contains(u.type)).ToList();
        }
        public List<Unit> GetNearbyUnit(IEnumerable<Unit> units, Vector2 position, float range)
        {
            return units.Where(u => Vector2.Distance(u.position, position) < range).ToList();
        }

        public T GetRandom<T>(IList<T> list)
        {
            return list[random.Next(0, list.Count)];
        }
    }
}

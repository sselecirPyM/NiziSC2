using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Numerics;
using NiziSC2.Core;
using System.Threading.Tasks;

namespace MiningMachine
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
        public ulong perviousEnemyTag;
        public Dictionary<UnitType, int> suggestBarracksFood = new Dictionary<UnitType, int>()
        { { UnitType.TERRAN_BARRACKS,20}, {UnitType.TERRAN_FACTORY,20},{UnitType.TERRAN_STARPORT,40}, };
        public Dictionary<UnitType, int> MinCount = new Dictionary<UnitType, int>()
        { { UnitType.TERRAN_BARRACKS,1}, {UnitType.TERRAN_FACTORY,2},{UnitType.TERRAN_STARPORT,1}, };
        public HashSet<Unit> otherCommand = new HashSet<Unit>();

        public void Initialize()
        {
            random = new Random();
            placementGrid = new WriteableImageData(gameContext.gameInfo.StartRaw.PlacementGrid);
            nonResPlacementGrid = new WriteableImageData(gameContext.gameInfo.StartRaw.PlacementGrid);

            scaledVisMap.Init(new Int2(32, 32));

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
            var watch1 = System.Diagnostics.Stopwatch.StartNew();

            var player = gameContext.players[gameContext.playerId];
            ActionList action = new ActionList();
            action.gameContext = gameContext;
            var visibility = gameContext.observation.Observation.RawData.MapState.Visibility;

            var playerUnits = gameContext.GetUnits(SC2APIProtocol.Alliance.Self);
            var enemies = gameContext.GetUnits(SC2APIProtocol.Alliance.Enemy);
            var neutrals = gameContext.GetUnits(SC2APIProtocol.Alliance.Neutral);
            var enemiesArmy = enemies.Where(u => UnitTypeInfo.Army.Contains(u.type)).ToList();
            var enemiesArmyFood = enemiesArmy.Sum(u => gameContext.gameData.Units[(int)u.type].FoodRequired);

            var commandCenters = gameContext.commandCenters;
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
                        if (mineralGroup.nearUnits[i].Any(u1 => u1.health > 0))
                            return true;
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
            otherCommand.RemoveWhere(u => u.orders.Count == 0);
            var geysersCanBuild = geysers.Where(u =>
            {
                if (u.healthMax == 0) return false;
                foreach (var commandCenter in commandCenters)
                    if (Vector2.Distance(u.position, commandCenter.position) < 15)
                    {
                        foreach (var refinery in refineriesProjection)
                            if (Vector2.Distance(refinery.position, u.position) < 1)
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
                gameContext.unitVirtualCount.TryGetValue(_unitType, out int value);
                return value;
            }
            watch1.Stop();
            long tick1 = watch1.ElapsedTicks;
            watch1.Restart();
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
                    if (workerTest1 < 1 && AbilitiesInfo.Harvest.Contains(abil))
                    {
                        if (gameContext.units.TryGetValue(order.TargetUnitTag, out var target) && minerals1.Contains(target))
                        {
                            bool fullWorkers = commandCenters.Any(u => Vector2.Distance(u.position, target.position) < 15 && u.assignedHarvesters > u.idealHarvesters);
                            if (fullWorkers)
                            {
                                action.EnqueueAbility(new[] { unit1 }, Abilities.STOP);
                                workerTest1++;
                            }
                        }
                    }
                }
            }

            foreach (var commandCenter in commandCenters)
            {
                if (commandCenter.type == UnitType.TERRAN_COMMANDCENTER && player.VespeneStat.current >= 200 && commandCenter.orders.Count == 0)
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

            foreach (var unitType in gameContext.AutoCast.AutoResearches)
            {
                foreach (var researcher in GetUnits(unitType.Unit))
                {
                    if (researcher.orders.Count == 0)
                    {
                        int r = random.Next(0, unitType.Abilities.Count);
                        action.EnqueueAbility(new[] { researcher }, unitType.Abilities[r]);
                    }
                }
            }
            List<uint> debugIds = new List<uint>(gameContext.observation.Observation.RawData.Player.UpgradeIds);

            watch1.Stop();
            long tick2 = watch1.ElapsedTicks;
            watch1.Restart();

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
            var widowMines = army.Where(u => UnitTypeInfo.WidowMine.Contains(u.type)).ToList();

            var army4 = army.Where(u => u.weaponCooldown < 0.1 && !otherCommand.Contains(u));
            var army5 = army.Where(u => u.weaponCooldown >= 0.1 && !otherCommand.Contains(u));

            foreach (var widowMine in widowMines)
            {
                if (widowMine.type == UnitType.TERRAN_WIDOWMINE && !otherCommand.Contains(widowMine) && GetNearbyUnit(widowMines, widowMine.position, 4).Count > 3)
                {
                    action.EnqueueSmart(new[] { widowMine }, RandomPosition(widowMine.position, 2.5f));
                    otherCommand.Add(widowMine);
                }
            }

            action.EnqueueAbility(widowMines.Where(u => u.type == UnitType.TERRAN_WIDOWMINE && !otherCommand.Contains(u) && (foodArmyReal < 50 || GetNearbyUnit(enemiesArmy, u.position, 8).Count > 0)), Abilities.BURROWDOWN_WIDOWMINE);
            action.EnqueueAbility(widowMines.Where(u => u.type == UnitType.TERRAN_WIDOWMINEBURROWED && foodArmyReal >= 50 && GetNearbyUnit(enemiesArmy, u.position, 10).Count == 0), Abilities.BURROWUP_WIDOWMINE);

            if (foodArmyReal < 70)
                action.EnqueueAbility(GetUnits(UnitType.TERRAN_SIEGETANK), Abilities.MORPH_SIEGEMODE);
            else
                action.EnqueueAbility(GetUnits(UnitType.TERRAN_SIEGETANKSIEGED), Abilities.MORPH_UNSIEGE);

            foreach (var unit in army5)
            {
                if (unit.engagedTargetTag != 0 && gameContext.units.TryGetValue(unit.engagedTargetTag, out var enemy) && !UnitTypeInfo.Melee.Contains(unit.type) && !UnitTypeInfo.Thor.Contains(unit.type))
                {
                    if (enemiesArmy.Count > 2)
                        action.EnqueueSmart(new[] { unit }, unit.position + Vector2.Normalize(unit.position - enemy.position) * unit.weaponCooldown * gameContext.UnitData[(int)unit.type].MovementSpeed * 0.5f);
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
                    action.EnqueueAttack(army4, enemiesPosition);
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
                action.EnqueueAttack(army4, attackPos);
            }

            var commandCenterProjection = gameContext.GetUnitProjections(UnitTypeInfo.ResourceCenters);
            gameContext.unitOnBuild.TryGetValue(raceData.ResourceBuilding, out int onbuildCommandCenterCount);
            bool EnoughWorker = commandCenters.All(u => { return u.assignedHarvesters >= u.idealHarvesters; }) && player.FoodWorkers > 20 && onbuildCommandCenterCount == 0;
            bool expand = player.SupplyConsume > GetUnitVirtualCount(raceData.ResourceBuilding) * 20 || EnoughWorker || player.MineralStat.current > 1000 || commandCenterProjection.Count < 5;

            watch1.Stop();
            long tick3 = watch1.ElapsedTicks;
            watch1.Restart();

            if (workers.Count > 0)
            {
                if (raceData.Race == SC2APIProtocol.Race.Terran || raceData.Race == SC2APIProtocol.Race.Protoss)
                {
                    if (player.SupplyProvideVirtual - player.SupplyConsume < 4 + player.SupplyConsume * 0.3f && gameContext.Afford(raceData.SupplyBuilding))
                    {
                        var spawner = GetRandom(workers);
                        Vector2 pos = RandomPosition(spawner.position, 10);
                        if (nonResPlacementGrid.Query((int)pos.X, (int)pos.Y))
                            action.EnqueueBuild(spawner.Tag, raceData.SupplyBuilding, pos);
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
                if (((gameContext.frame & 8) > 0 && player.FoodWorkers >= 16) && player.VespeneStat.current < 800 && (player.MineralStat.current > 500))
                    foreach (var geyser in geysersCanBuild)
                    {
                        if (gameContext.Afford(raceData.VespeneBuilding))
                        {
                            Unit unit = GetRandom(workers);
                            for (int i = 0; i < 3; i++)
                            {
                                var unit1 = GetRandom(workers);
                                if (Vector2.DistanceSquared(unit.position, geyser.position) > Vector2.DistanceSquared(unit1.position, geyser.position))
                                {
                                    unit = unit1;
                                }
                            }
                            action.EnqueueBuild(unit.Tag, raceData.VespeneBuilding, geyser.Tag);
                        }
                    }
                foreach (var refinery in refineries)
                {
                    if (refinery.buildProgress == 1 && refinery.vespeneContents > 0 && player.VespeneStat.current < 800)
                    {
                        if (refinery.assignedHarvesters < 3 && refinerWorkerCount * 4 < workers.Count)
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
                if (gameContext.UnitCanBuild(raceData.DefenseAir))
                    foreach (var commandCenter in commandCenterProjection)
                    {
                        if (player.MineralStat.current < 300) break;
                        var nearbyDefense = GetNearbyUnit(gameContext.GetUnitProjections(raceData.DefenseAir), commandCenter.position, 7);
                        var nearbyWorkers = GetNearbyUnit(workers, commandCenter.position, 6);
                        if (nearbyDefense.Count < 1 && nearbyWorkers.Count > 0)
                        {
                            var spawner = GetRandom(nearbyWorkers);

                            Vector2 pos = RandomPosition(commandCenter.position, 6);
                            if (nonResPlacementGrid.Query((int)pos.X, (int)pos.Y) && (spawner.orders.Count == 0 || !gameContext.buildId2Units.ContainsKey(spawner.orders[0].AbilityId)) && gameContext.Afford(raceData.DefenseAir))
                            {
                                action.EnqueueBuild(spawner.Tag, raceData.DefenseAir, pos);
                                break;
                            }
                        }
                    }
            }
            if (GetUnitVirtualCount(UnitType.TERRAN_WIDOWMINE) < 15)
            {
                TechUnit techUnit = raceData.TechUnits.Find(u => u.UnitType == UnitType.TERRAN_WIDOWMINE);
                var spawners = GetUnits(techUnit.Spawner);
                if (player.SupplyConsume > 60)
                {
                    foreach (var spawner in spawners)
                        if (spawner.orders.Count == 0 && player.SupplyConsume < 150 && player.MineralStat.current > 200)
                        {
                            TryTrain(spawner, techUnit.UnitType, action);
                        }
                }
            }

            if ((commandCenterProjection.Count >= mineralGroup.middlePoints.Count / 4 || player.SupplyConsume > 80 || player.MineralStat.current > 900))
                foreach (TechUnit techUnit in raceData.TechUnits)
                {
                    var spawners = GetUnits(techUnit.Spawner);
                    //var techUnits = GetUnits(techUnit.UnitType);
                    if (!gameContext.UnitCanBuild(techUnit.UnitType)) continue;
                    if (!UnitTypeInfo.Workers.Contains(techUnit.Spawner))
                    {
                        if (player.MineralStat.current > 700 || player.SupplyConsume > 100)
                        {
                            foreach (var spawner in spawners)
                                if (spawner.orders.Count == 0 && player.SupplyConsume < 194 && player.MineralStat.current > 600)
                                {
                                    if (random.NextDouble() < 0.1)
                                        TryTrain(spawner, techUnit.UnitType, action);
                                }
                        }
                    }
                    else
                    {
                        if (spawners.Count > 0 && player.MineralStat.current > 500)
                        {
                            var spawner = GetRandom(spawners);

                            if (GetUnitVirtualCount(techUnit.UnitType) < 2)
                            {
                                Vector2 pos = RandomPosition(spawner.position, 10);
                                if (nonResPlacementGrid.Query((int)pos.X, (int)pos.Y) && gameContext.Afford(techUnit.UnitType))
                                    action.EnqueueBuild(spawner.Tag, techUnit.UnitType, pos);
                            }
                        }
                    }
                }

            if (workers.Count > 0)
            {
                foreach (var barrack1 in raceData.Barracks)
                {
                    if ((player.MineralStat.current > 1000 || (GetUnitVirtualCount(barrack1.UnitType) < MinCount[barrack1.UnitType] && player.MineralStat.current > 500)) && barrack1.UnitType != raceData.ResourceBuilding && GetUnitVirtualCount(barrack1.UnitType) < player.SupplyProvide / suggestBarracksFood[barrack1.UnitType])
                    {
                        if (player.VespeneStat.current > 200 && gameContext.Afford(barrack1.UnitType))
                        {
                            var unit = GetRandom(workers);
                            Vector2 pos = RandomPosition(unit.position, 10);
                            if (nonResPlacementGrid.Query((int)pos.X, (int)pos.Y))
                                action.EnqueueBuild(unit.Tag, barrack1.UnitType, pos);
                        }
                    }
                }
            }
            if (expand)
            {
                if (gameContext.Afford(raceData.SupplyBuilding))
                {
                    Vector2 expandPoint = GetRandom(mineralGroup.middlePoints);

                    bool _stop1 = true;

                    foreach (var commandCenter in commandCenterProjection)
                    {
                        var distance = Vector2.Distance(commandCenter.position, expandPoint);
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
            if (!EnoughWorker && (player.FoodWorkers < 100 || (player.FoodWorkers < 140 && player.SupplyConsume < 140)))
            {
                var larvas = GetUnits(raceData.Worker.Spawner);
                if (larvas.Count > 0)
                {
                    var tbase = GetRandom(larvas);
                    TryTrain(tbase, raceData.Worker.UnitType, action);
                }
            }

            var unitCaceled = playerUnits.Where(u => u.buildProgress < 1 && u.buildProgress * 0.25 > (float)u.health / u.healthMax).ToList();
            action.EnqueueAbility(unitCaceled, Abilities.CANCEL_BUILDINPROGRESS);

            watch1.Stop();
            long tick4 = watch1.ElapsedTicks;
            if (playerUnits.Count == 100)
                Console.WriteLine("cost: {0} {1} {2} {3}", tick1, tick2, tick3, tick4);

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
        public List<UnitProjection> GetNearbyUnit(IEnumerable<UnitProjection> units, Vector2 position, float range, HashSet<UnitType> unitTypes)
        {
            return units.Where(u => Vector2.Distance(u.position, position) < range && unitTypes.Contains(u.unitType)).ToList();
        }
        public List<Unit> GetNearbyUnit(IEnumerable<Unit> units, Vector2 position, float range)
        {
            return units.Where(u => Vector2.Distance(u.position, position) < range).ToList();
        }
        public List<UnitProjection> GetNearbyUnit(IEnumerable<UnitProjection> units, Vector2 position, float range)
        {
            return units.Where(u => Vector2.Distance(u.position, position) < range).ToList();
        }

        public T GetRandom<T>(IList<T> list)
        {
            return list[random.Next(0, list.Count)];
        }
    }
}

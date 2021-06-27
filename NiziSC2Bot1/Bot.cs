using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Numerics;
using NiziSC2.Core;
using System.Threading.Tasks;
using System.Xml.Serialization;

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
        public ImageScaledMap scaledVisMap = new ImageScaledMap();
        public ulong perviousEnemyTag;
        public AutoCast autoCast;

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
            var enemiesArmyFood = enemiesArmy.Sum(u => gameContext.UnitData[(int)u.type].FoodRequired);

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
            player.SupplyProvideVirtual += playerUnits.Sum(u =>
            {
                float _virtual = 0;
                foreach (var order in u.orders)
                {
                    if (gameContext.buildId2Units.TryGetValue(order.AbilityId, out UnitType unitType))
                    {
                        if (!UnitTypeInfo.ResourceCenters.Contains(unitType))
                            _virtual += gameContext.UnitData[(int)unitType].FoodProvided;
                    }
                }
                return _virtual;
            });
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
                    Abilities abil = (Abilities)order.AbilityId;
                    if (workerTest1 < 1 && AbilitiesInfo.Harvest.Contains(abil) && gameContext.units.TryGetValue(order.TargetUnitTag, out var target) && minerals1.Contains(target))
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

            foreach (var commandCenter in commandCenters)
            {
                if (commandCenter.type == UnitType.TERRAN_COMMANDCENTER && player.VespeneStat.current >= 200)
                {
                    action.EnqueueAbility(new[] { commandCenter }, Abilities.MORPH_PLANETARYFORTRESS);
                }
                if (player.race == SC2APIProtocol.Race.Terran && commandCenter.health < commandCenter.healthMax - 100 && commandCenter.buildProgress == 1 && player.VespeneStat.current >= 200)
                {
                    var nearUnits = GetNearbyUnit(workers, commandCenter.position, 5);
                    action.EnqueueAbility(nearUnits, Abilities.EFFECT_REPAIR, commandCenter.Tag);
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
                if (randomUnit.type == UnitType.ZERG_INFESTOR && Vector2.Distance(randomUnit.position, randomEnemy.position) < 8)
                {
                    action.EnqueueAbility(new[] { randomUnit }, Abilities.EFFECT_FUNGALGROWTH, randomEnemy.position);
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
            var queens = GetUnits(UnitType.ZERG_QUEEN);
            if (queens.Count > 0 && commandCenters.Length > 0)
                foreach (var randomUnit in queens)
                {
                    var randomCommandCenter = GetRandom(commandCenters);
                    var randomFriendly = GetRandom(playerUnits);
                    if (randomUnit.type == UnitType.ZERG_QUEEN && Vector2.Distance(randomUnit.position, randomCommandCenter.position) < 5 && randomUnit.energy > 25)
                    {
                        action.EnqueueAbility(new[] { randomUnit }, Abilities.EFFECT_INJECTLARVA, randomCommandCenter.Tag);
                        army.Remove(randomUnit);
                    }
                    if (randomUnit.type == UnitType.ZERG_QUEEN && Vector2.Distance(randomUnit.position, randomFriendly.position) < 5 && randomUnit.energy > 50 && randomFriendly.health < randomFriendly.healthMax - 60)
                    {
                        action.EnqueueAbility(new[] { randomUnit }, Abilities.EFFECT_TRANSFUSION, randomFriendly.Tag);
                        army.Remove(randomUnit);
                    }
                }

            var pos1 = scaledVisMap.GetPos(scaledVisMap.GetLow(out _));
            Vector2 attackPos = pos1 * new Vector2(visibility.Size.X, visibility.Size.Y);
            float foodArmyReal = army.Sum(u => gameContext.UnitData[(int)u.type].FoodRequired);
            var widowMines = army.Where(u => UnitTypeInfo.WidowMine.Contains(u.type));

            var army4 = army.Where(u => u.weaponCooldown < 0.1);
            var army5 = army.Where(u => u.weaponCooldown >= 0.1);

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
                action.EnqueueAttack(army4, attackPos);
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
                if (((gameContext.frame & 8) > 0 && player.FoodWorkers >= 16) && player.VespeneStat.current < 800 && player.MineralStat.current > 500)
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
            }
            var commandCenterProjection = gameContext.GetUnitProjections(UnitTypeInfo.ResourceCenters);
            gameContext.unitOnBuild.TryGetValue(raceData.ResourceBuilding, out int onbuildCommandCenterCount);
            bool EnoughWorker = commandCenters.All(u => { return u.assignedHarvesters >= u.idealHarvesters; }) && player.FoodWorkers > 20 && onbuildCommandCenterCount == 0;
            bool expand = player.SupplyConsume > GetUnitVirtualCount(raceData.ResourceBuilding) * 30 || EnoughWorker || player.MineralStat.current > 1200 || commandCenterProjection.Count < 2 || resourceCommandCenter.Length < 1;
            var lairs = GetUnits(UnitType.ZERG_LAIR);
            var hives = GetUnits(UnitType.ZERG_HIVE);
            if (hives.Count == 0)
            {
                if (lairs.Count == 0)
                    foreach (var base1 in commandCenters)
                    {
                        if (base1.type == UnitType.ZERG_HATCHERY)
                        {
                            action.EnqueueAbility(new[] { base1 }, Abilities.MORPH_LAIR);
                            break;
                        }
                    }
                else
                    foreach (var base1 in commandCenters)
                    {
                        if (base1.type == UnitType.ZERG_LAIR)
                        {
                            action.EnqueueAbility(new[] { base1 }, Abilities.MORPH_HIVE);
                            break;
                        }
                    }
            }

            if (commandCenterProjection.Count > mineralGroup.middlePoints.Count / 4 || player.FoodWorkers > 50 || player.MineralStat.current > 600)
                foreach (TechUnit techUnit in raceData.TechUnits)
                {
                    var spawners = GetUnits(techUnit.Spawner);
                    //var techUnits = GetUnits(techUnit.UnitType);
                    if (!gameContext.UnitCanBuild(techUnit.UnitType)) continue;
                    if (!UnitTypeInfo.Workers.Contains(techUnit.Spawner) && player.FoodWorkers > 50)
                    {
                        foreach (var spawner in spawners)
                            if (spawner.orders.Count == 0 && player.SupplyConsume < 194 && player.MineralStat.current > 600)
                            {
                                if (random.NextDouble() < 0.1)
                                    TryTrain(spawner, techUnit.UnitType, action);
                            }
                    }
                    else
                    {
                        if (spawners.Count > 0 && player.MineralStat.current > 500 && random.NextDouble() < 0.1f)
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
                if (player.MineralStat.current > 1000)
                    foreach (var barrack1 in raceData.Barracks)
                    {
                        if (GetUnitVirtualCount(barrack1.UnitType) < player.SupplyProvide / 18)
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
            }
            if (expand)
            {
                if (gameContext.Afford(raceData.SupplyBuilding))
                {
                    Vector2 expandPoint = GetRandom(mineralGroup.middlePoints);

                    bool _stop1 = commandCenterProjection.Any(u =>
                    {
                        var distance = Vector2.Distance(u.position, expandPoint);
                        return distance < 50;
                    });
                    if (_stop1)
                        _stop1 = !commandCenterProjection.Any(u =>
                        {
                            var distance = Vector2.Distance(u.position, expandPoint);
                            return distance < 1;
                        });

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
            if (!EnoughWorker && (player.FoodWorkers < 70 || (player.FoodWorkers < 85 && player.SupplyConsume < 85)))
            {
                var larvas = GetUnits(raceData.Worker.Spawner);
                if (larvas.Count > 0)
                {
                    var tbase = GetRandom(larvas);
                    TryTrain(tbase, raceData.Worker.UnitType, action);
                }
            }

            action.EnqueueAbility(playerUnits.Where(u => u.buildProgress < 1 && u.buildProgress * 0.25 > (float)u.health / u.healthMax), Abilities.CANCEL_BUILDINPROGRESS);

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

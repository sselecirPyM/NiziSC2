using System;
using System.Collections.Generic;
using System.Text;

namespace NiziSC2.Core
{
    public static class UnitTypeInfo
    {
        public static Abilities GetBuildAbility(SC2APIProtocol.ResponseData gameData, UnitType unitType)
        {
            return (Abilities)gameData.Units[(int)unitType].AbilityId;
        }
        public static HashSet<UnitType> Workers = new HashSet<UnitType>
        {
            UnitType.TERRAN_SCV,
            UnitType.PROTOSS_PROBE,
            UnitType.ZERG_DRONE,
            UnitType.ZERG_DRONEBURROWED,
        };
        public static HashSet<UnitType> MineralFields = new HashSet<UnitType>
        {
            UnitType.NEUTRAL_BATTLESTATIONMINERALFIELD,
            UnitType.NEUTRAL_BATTLESTATIONMINERALFIELD750,
            UnitType.NEUTRAL_LABMINERALFIELD,
            UnitType.NEUTRAL_LABMINERALFIELD750,
            UnitType.NEUTRAL_MINERALFIELD,
            UnitType.NEUTRAL_MINERALFIELD750,
            UnitType.NEUTRAL_PURIFIERMINERALFIELD,
            UnitType.NEUTRAL_PURIFIERMINERALFIELD750,
            UnitType.NEUTRAL_PURIFIERRICHMINERALFIELD,
            UnitType.NEUTRAL_PURIFIERRICHMINERALFIELD750,
            UnitType.NEUTRAL_RICHMINERALFIELD,
            UnitType.NEUTRAL_RICHMINERALFIELD750,
        };
        public static HashSet<UnitType> VespeneGeysers = new HashSet<UnitType>
        {
            UnitType.NEUTRAL_PROTOSSVESPENEGEYSER,
            UnitType.NEUTRAL_PURIFIERVESPENEGEYSER,
            UnitType.NEUTRAL_RICHVESPENEGEYSER,
            UnitType.NEUTRAL_SHAKURASVESPENEGEYSER,
            UnitType.NEUTRAL_VESPENEGEYSER,
        };
        public static HashSet<UnitType> ResourceCenters = new HashSet<UnitType>
        {
            UnitType.PROTOSS_NEXUS,
            UnitType.TERRAN_COMMANDCENTER,
            UnitType.TERRAN_COMMANDCENTERFLYING,
            UnitType.TERRAN_ORBITALCOMMAND,
            UnitType.TERRAN_ORBITALCOMMANDFLYING,
            UnitType.TERRAN_PLANETARYFORTRESS,
            UnitType.ZERG_HATCHERY,
            UnitType.ZERG_HATCHERY,
            UnitType.ZERG_LAIR,
            UnitType.ZERG_HIVE,
        };
        public static HashSet<UnitType> Barracks = new HashSet<UnitType>()
        {
            UnitType.TERRAN_BARRACKS,
            UnitType.TERRAN_BARRACKSFLYING,
        };
        public static HashSet<UnitType> GateWays = new HashSet<UnitType>()
        {
            UnitType.PROTOSS_GATEWAY,
            UnitType.PROTOSS_WARPGATE,
        };
        public static HashSet<UnitType> Refinery = new HashSet<UnitType>()
        {
            UnitType.TERRAN_REFINERY,
            UnitType.TERRAN_REFINERY_RICH,
            UnitType.PROTOSS_ASSIMILATOR,
            UnitType.PROTOSS_ASSIMILATOR_RICH,
            UnitType.ZERG_EXTRACTOR,
        };
        public static HashSet<UnitType> Army = new HashSet<UnitType>()
        {
            UnitType.PROTOSS_ADEPT,
            UnitType.PROTOSS_ARCHON,
            UnitType.PROTOSS_CARRIER,
            UnitType.PROTOSS_COLOSSUS,
            UnitType.PROTOSS_DARKTEMPLAR,
            UnitType.PROTOSS_DISRUPTOR,
            UnitType.PROTOSS_DISRUPTOR,
            UnitType.PROTOSS_HIGHTEMPLAR,
            UnitType.PROTOSS_IMMORTAL,
            UnitType.PROTOSS_MOTHERSHIP,
            UnitType.PROTOSS_MOTHERSHIPCORE,
            UnitType.PROTOSS_ORACLE,
            UnitType.PROTOSS_PHOENIX,
            UnitType.PROTOSS_SENTRY,
            UnitType.PROTOSS_STALKER,
            UnitType.PROTOSS_TEMPEST,
            UnitType.PROTOSS_VOIDRAY,
            UnitType.PROTOSS_ZEALOT,
            UnitType.TERRAN_BANSHEE,
            UnitType.TERRAN_BATTLECRUISER,
            UnitType.TERRAN_CYCLONE,
            UnitType.TERRAN_GHOST,
            UnitType.TERRAN_HELLION,
            UnitType.TERRAN_HELLIONTANK,
            UnitType.TERRAN_LIBERATOR,
            UnitType.TERRAN_LIBERATORAG,
            UnitType.TERRAN_MARINE,
            UnitType.TERRAN_MARAUDER,
            UnitType.TERRAN_MEDIVAC,
            UnitType.TERRAN_RAVEN,
            UnitType.TERRAN_REAPER,
            UnitType.TERRAN_SIEGETANK,
            UnitType.TERRAN_SIEGETANKSIEGED,
            UnitType.TERRAN_THOR,
            UnitType.TERRAN_VIKINGFIGHTER,
            UnitType.TERRAN_VIKINGASSAULT,
            UnitType.TERRAN_WIDOWMINE,
            UnitType.TERRAN_WIDOWMINEBURROWED,
            UnitType.ZERG_BROODLORD,
            UnitType.ZERG_BANELING,
            UnitType.ZERG_CORRUPTOR,
            UnitType.ZERG_HYDRALISK,
            UnitType.ZERG_INFESTOR,
            UnitType.ZERG_MUTALISK,
            UnitType.ZERG_ROACH,
            UnitType.ZERG_SWARMHOSTMP,
            UnitType.ZERG_ULTRALISK,
            UnitType.ZERG_VIPER,
            UnitType.ZERG_ZERGLING,
        };
        public static HashSet<UnitType> NeedAddon = new HashSet<UnitType>()
        {
            UnitType.TERRAN_BARRACKS,
            UnitType.TERRAN_BARRACKSFLYING,
            UnitType.TERRAN_FACTORY,
            UnitType.TERRAN_FACTORYFLYING,
            UnitType.TERRAN_STARPORT,
            UnitType.TERRAN_STARPORTFLYING,
        };
    }
}

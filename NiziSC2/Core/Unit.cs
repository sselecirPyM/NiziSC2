using System;
using System.Collections.Generic;
using System.Text;
using System.Numerics;
using System.Diagnostics.CodeAnalysis;

namespace NiziSC2.Core
{
    public class Unit
    {
        public int owner;
        public ulong Tag;
        public UnitType type;
        public SC2APIProtocol.Alliance alliance;
        public int health;
        public int healthMax;
        public int shield;
        public int shieldMax;
        public int energy;
        public int energyMax;
        public Vector2 position;
        public float positionZ;
        public float weaponCooldown;
        public float radius;
        public float buildProgress;
        public bool isCloaked;
        public bool isPowered;
        public bool isFlying;
        public bool isBurrowed;
        public float lastTrackTime;
        public List<int> passangers;
        public int cargoSpaceTaken;
        public int cargoSpaceMax;
        public int passagerGuess;
        public bool tracking;
        public ulong lastTrackFrame;
        public int assignedHarvesters;
        public int idealHarvesters;
        public int mineralContents;
        public int vespeneContents;
        public ulong addOnTag;
        public ulong engagedTargetTag;
        public List<SC2APIProtocol.UnitOrder> orders = new List<SC2APIProtocol.UnitOrder>();

        public void RawDataUpdate(SC2APIProtocol.Unit unit)
        {
            owner = unit.Owner;
            Tag = unit.Tag;
            type = (UnitType)unit.UnitType;
            alliance = unit.Alliance;
            health = (int)unit.Health;
            healthMax = (int)unit.HealthMax;
            shield = (int)unit.Shield;
            shieldMax = (int)unit.ShieldMax;
            energy = (int)unit.Energy;
            energyMax = (int)unit.EnergyMax;
            position = new Vector2(unit.Pos.X, unit.Pos.Y);
            positionZ = unit.Pos.Z;
            weaponCooldown = unit.WeaponCooldown;
            radius = unit.Radius;
            buildProgress = unit.BuildProgress;
            isCloaked = unit.Cloak == SC2APIProtocol.CloakState.Cloaked;
            isPowered = unit.IsPowered;
            isFlying = unit.IsFlying;
            isBurrowed = unit.IsBurrowed;
            cargoSpaceTaken = unit.CargoSpaceTaken;
            cargoSpaceMax = unit.CargoSpaceMax;
            assignedHarvesters = unit.AssignedHarvesters;
            idealHarvesters = unit.IdealHarvesters;
            orders.Clear();
            orders.AddRange(unit.Orders);
            addOnTag = unit.AddOnTag;
            engagedTargetTag = unit.EngagedTargetTag;
            mineralContents = unit.MineralContents;
            vespeneContents = unit.VespeneContents;
            //tracking = true;
        }
    }
}

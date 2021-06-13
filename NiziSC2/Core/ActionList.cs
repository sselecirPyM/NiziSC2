using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using SC2APIProtocol;
using Action = SC2APIProtocol.Action;

namespace NiziSC2.Core
{
    public class ActionList
    {
        public List<Action> actions = new List<Action>();
        public GameContext gameContext;
        public Random random = new Random();
        public void Clear()
        {
            actions.Clear();
        }
        public void EnqueueChat(string message, bool team = false)
        {
            var actionChat = new ActionChat();
            actionChat.Channel = team ? ActionChat.Types.Channel.Team : ActionChat.Types.Channel.Broadcast;
            actionChat.Message = message;
            actions.Add(new Action { ActionChat = actionChat });
        }
        public void EnqueueSmart(IEnumerable<Unit> units, ulong target)
        {
            UnitsAction(Command(Abilities.SMART, target), units);
        }
        public void EnqueueSmart(IEnumerable<Unit> units, Vector2 target)
        {
            UnitsAction(Command(Abilities.SMART, target), units);
        }
        public void EnqueueAttack(IEnumerable<Unit> units, Vector2 target)
        {
            UnitsAction(Command(Abilities.ATTACK, target), units);
        }
        public void EnqueueAbility(IEnumerable<Unit> units, Abilities abilities)
        {
            UnitsAction(Command(abilities), units);
        }
        public void EnqueueAbility(IEnumerable<Unit> units, Abilities abilities, ulong target)
        {
            UnitsAction(Command(abilities,target), units);
        }
        public void EnqueueAbility(IEnumerable<Unit> units, Abilities abilities, Vector2 target)
        {
            UnitsAction(Command(abilities, target), units);
        }

        public void EnqueueTrain(ulong unit, UnitType unitType)
        {
            var cmd = Command(UnitTypeInfo.GetBuildAbility(gameContext.gameData, unitType));
            cmd.ActionRaw.UnitCommand.UnitTags.Add(unit);
            actions.Add(cmd);
            System.Diagnostics.Debug.WriteLine(string.Format("Start train {0}", gameContext.GetUnitName(unitType)), "Nizi");
        }

        public void EnqueueBuild(ulong unit, UnitType unitType, Vector2 position)
        {
            var cmd = Command(UnitTypeInfo.GetBuildAbility(gameContext.gameData, unitType));
            cmd.ActionRaw.UnitCommand.UnitTags.Add(unit);
            cmd.ActionRaw.UnitCommand.TargetWorldSpacePos = new Point2D();
            cmd.ActionRaw.UnitCommand.TargetWorldSpacePos.X = position.X;
            cmd.ActionRaw.UnitCommand.TargetWorldSpacePos.Y = position.Y;
            actions.Add(cmd);
            System.Diagnostics.Debug.WriteLine(string.Format("Start build {0}", gameContext.GetUnitName(unitType)), "Nizi");
        }

        public void EnqueueBuild(ulong unit, UnitType unitType, ulong target)
        {
            var cmd = Command(UnitTypeInfo.GetBuildAbility(gameContext.gameData, unitType), target);
            cmd.ActionRaw.UnitCommand.UnitTags.Add(unit);
            actions.Add(cmd);
            System.Diagnostics.Debug.WriteLine(string.Format("Start build {0}", gameContext.GetUnitName(unitType)), "Nizi");
        }

        public void UnitsAction(SC2APIProtocol.Action action, IEnumerable<Unit> units)
        {
            foreach (var unit in units)
                action.ActionRaw.UnitCommand.UnitTags.Add(unit.Tag);
            if (action.ActionRaw.UnitCommand.UnitTags.Count > 0)
                actions.Add(action);
        }

        public static Action Command(Abilities ability)
        {
            var action = new Action();
            action.ActionRaw = new ActionRaw();
            action.ActionRaw.UnitCommand = new ActionRawUnitCommand();
            action.ActionRaw.UnitCommand.AbilityId = (int)ability;
            return action;
        }

        public static Action Command(Abilities ability, ulong target)
        {
            var action = new Action();
            action.ActionRaw = new ActionRaw();
            action.ActionRaw.UnitCommand = new ActionRawUnitCommand();
            action.ActionRaw.UnitCommand.AbilityId = (int)ability;
            action.ActionRaw.UnitCommand.TargetUnitTag = target;
            return action;
        }

        public static Action Command(Abilities ability, Vector2 target)
        {
            var action = new Action();
            action.ActionRaw = new ActionRaw();
            action.ActionRaw.UnitCommand = new ActionRawUnitCommand();
            action.ActionRaw.UnitCommand.AbilityId = (int)ability;
            action.ActionRaw.UnitCommand.TargetWorldSpacePos = new Point2D
            {
                X = target.X,
                Y = target.Y
            };
            return action;
        }
    }
}

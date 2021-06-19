using System;
using System.Collections.Generic;
using System.Text;

namespace NiziSC2.Core
{
    public class AbilAutoCast
    {
        public List<Unit> AutoCastGroup;
        public GameContext gameContext;

        public bool InAutoCastGroup(Unit unit)
        {
            return false;
        }
    }
}

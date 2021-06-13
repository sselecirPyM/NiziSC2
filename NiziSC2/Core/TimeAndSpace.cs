using System;
using System.Collections.Generic;
using System.Text;

namespace NiziSC2.Core
{
    public static class TimeAndSpace
    {
        public static IEnumerable<Int2> Box(int startX, int startY, int endX, int endY)
        {
            for (int i = startX; i < endX; i++)
            {
                for (int j = startY; j < endY; j++)
                {
                    yield return new Int2(i, j);
                }
            }
        }
    }
}

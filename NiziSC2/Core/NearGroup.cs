using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
//using UnitSet = System.Collections.Generic.List<NiziSC2.Core.Unit>;
//using UnitSet = System.Collections.Generic.SortedSet<NiziSC2.Core.Unit>;
using UnitSet = System.Collections.Generic.HashSet<NiziSC2.Core.Unit>;

namespace NiziSC2.Core
{
    public class NearGroup
    {
        public List<UnitSet> nearUnits;
        public List<Vector2> middlePoints;
        public List<bool> debugArray;

        public void BuildMineral(IEnumerable<Unit> minerals, IEnumerable<Unit> vespenes, SC2APIProtocol.ImageData placementData, float range)
        {
            System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
            nearUnits = new List<UnitSet>();
            middlePoints = new List<Vector2>();
            debugArray = new List<bool>();
            //List<Unit> sortedUnits = units.ToList();
            //sortedUnits.Sort((u1, u2) => u1.position.X.CompareTo(u2.position.X));
            foreach (var unit in minerals)
            {
                bool create = true;
                foreach (var nearGroup in nearUnits)
                {
                    if (nearGroup.Contains(unit))
                    {
                        create = false;
                        break;
                    }
                }
                if (create)
                {
                    var newGroup = new UnitSet();
                    nearUnits.Add(newGroup);
                    //newGroup.Add(unit);
                    //float maxa = unit.position.X + range;
                    foreach (var testUnit in minerals)
                    //foreach (var testUnit in ForRangeX(sortedUnits, unit.position.X - range, unit.position.X + range))
                    {
                        if (Vector2.Distance(unit.position, testUnit.position) < range)
                        {
                            newGroup.Add(testUnit);
                        }
                        //if (testUnit.position.X > maxa)
                        //    break;
                    }
                }
            }
            foreach (var unitGroup in nearUnits)
            {
                Vector2 ad = Vector2.Zero;
                foreach (var unit in unitGroup)
                {
                    ad += unit.position;
                }
                ad /= unitGroup.Count;
                middlePoints.Add(ad);
                debugArray.Add(false);
            }
            watch.Stop();
            WriteableImageData writeableImageData = new WriteableImageData(placementData);
            foreach (var mineral in minerals)
            {
                int baseX = (int)Math.Ceiling(mineral.position.X) - 4;
                int baseY = (int)Math.Ceiling(mineral.position.Y) - 4;
                for (int x = 0; x < 8; x++)
                    for (int y = 0; y < 7; y++)
                    {
                        bool a = (x == 0) || (x == 7);
                        bool b = (y == 0) || (y == 6);
                        if (!(a & b))
                        {
                            writeableImageData.Write(baseX + x, baseY + y, false);
                        }
                    }
            }
            foreach (var vespene in vespenes)
            {
                int baseX = (int)Math.Ceiling(vespene.position.X) - 5;
                int baseY = (int)Math.Ceiling(vespene.position.Y) - 5;
                for (int x = 0; x < 9; x++)
                    for (int y = 0; y < 9; y++)
                    {
                        bool a = (x == 0) || (x == 8);
                        bool b = (y == 0) || (y == 8);
                        if (!(a & b))
                        {
                            writeableImageData.Write(baseX + x, baseY + y, false);
                        }
                    }
            }
            for (int i = 0; i < middlePoints.Count; i++)
            {
                Vector2 point = middlePoints[i];
                Int2 p1 = new Int2((int)point.X, (int)point.Y);
                float dist1 = 9999999999;
                foreach (var baseBuildPoint in TimeAndSpace.Box(p1.X - 8, p1.Y - 8, p1.X + 7, p1.Y + 75))
                {
                    if (writeableImageData.RectCheck(baseBuildPoint, new Int2(5, 5)))
                    {
                        float dist2 = nearUnits[i].Sum(u => Vector2.DistanceSquared(u.position, new Vector2(baseBuildPoint.X + 2.5f, baseBuildPoint.Y + 2.5f)));
                        if (dist2 < dist1)
                        {
                            middlePoints[i] = new Vector2(baseBuildPoint.X + 2.5f, baseBuildPoint.Y + 2.5f);
                            dist1 = dist2;
                            debugArray[i] = true;
                        }
                    }
                }
            }

            Console.WriteLine(string.Format("build mineral near groups time cost {0}", watch.ElapsedTicks));
        }

        public IEnumerable<Unit> ForRangeX(List<Unit> units, float minX, float maxX)
        {
            if (units.Count == 0)
                yield break;
            int FindIndex(float val)
            {
                int min = 0;
                int max = units.Count - 1;
                int mid = (min + max) / 2;
                while (mid != min)
                {
                    mid = (min + max) / 2;

                    if (units[mid].position.X < val)
                    {
                        min = mid;
                    }
                    else if (units[mid].position.X > val)
                    {
                        max = mid;
                    }
                    else
                    {
                        if (units[min + 1].position.X < val)
                        {
                            min++;
                        }
                        else
                        {
                            max--;
                        }
                    }
                }
                return min;
            }
            int _min = FindIndex(minX);
            int _max = FindIndex(maxX) + 1;
            for (int i = _min; i < _max; i++)
            {
                yield return units[i];
            }
        }
    }
}

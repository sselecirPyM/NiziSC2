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

        public void BuildMineral(IEnumerable<Unit> units, float range)
        {
            System.Diagnostics.Stopwatch watch = System.Diagnostics.Stopwatch.StartNew();
            nearUnits = new List<UnitSet>();
            middlePoints = new List<Vector2>();
            List<Unit> sortedUnits = units.ToList();
            //sortedUnits.Sort((u1, u2) => u1.position.X.CompareTo(u2.position.X));
            foreach (var unit in sortedUnits)
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
                    foreach (var testUnit in sortedUnits)
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
            foreach(var unitGroup in nearUnits)
            {
                Vector2 ad = Vector2.Zero;
                foreach(var unit in unitGroup)
                {
                    ad += unit.position;
                }
                ad /= unitGroup.Count;
                middlePoints.Add(ad);
            }
            watch.Stop();
            //System.Diagnostics.Debug.WriteLine(string.Format("near grouptime cost {0}", watch.ElapsedTicks));
            Console.WriteLine(string.Format("build near group time cost {0}", watch.ElapsedTicks));
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

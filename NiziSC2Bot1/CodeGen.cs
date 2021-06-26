using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NiziSC2.Core;

namespace NiziSC2Bot1
{
    static class CodeGen
    {
        public static void Gen1()
        {
            //foreach (var enum1 in Enum.GetNames(typeof(Abilities)))
            //{
            //    if (enum1.Contains("RESEARCH_"))
            //        Console.WriteLine("            Abilities.{0},", enum1);
            //}
            //foreach (var enum1 in Enum.GetNames(typeof(Abilities)))
            //{
            //    if (enum1.Contains("MORPH_"))
            //        Console.WriteLine("            Abilities.{0},", enum1);
            //}
            foreach (var enum1 in Enum.GetNames(typeof(Abilities)))
            {
                if (enum1.Contains("BUILD_"))
                    Console.WriteLine("            Abilities.{0},", enum1);
            }
        }
    }
}

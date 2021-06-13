using System;
using System.Collections.Generic;
using System.Text;
using System.Numerics;

namespace NiziSC2.Core
{
    public class ImageScaledMap
    {
        public Int2 Size;
        public int[] data;

        public void Init(Int2 size)
        {
            Size = size;
            data = new int[size.Y * size.X];
        }

        public IEnumerable<Vector2> EachPoint()
        {
            for (int y = 0; y < Size.Y; y++)
            {
                for (int x = 0; x < Size.X; x++)
                {
                    yield return new Vector2(((float)x + 0.5f) / Size.X, ((float)y + 0.5f) / Size.Y);
                }
            }
        }
        public IEnumerable<Int2> EachPoint1()
        {
            for (int y = 0; y < Size.Y; y++)
            {
                for (int x = 0; x < Size.X; x++)
                {
                    yield return new Int2(x, y);
                }
            }
        }
        public Vector2 GetPos(Int2 p)
        {
            return new Vector2(((float)p.X + 0.5f) / Size.X, ((float)p.Y + 0.5f) / Size.Y);
        }
        public Int2 GetLow(out int value)
        {
            value = data[0];
            Int2 p = new Int2();
            for (int y = 0; y < Size.Y; y++)
            {
                for (int x = 0; x < Size.X; x++)
                {
                    int v1 = data[y * Size.X + x];
                    if (v1 < value)
                    {
                        value = v1;
                        p = new Int2(x, y);
                    }
                }
            }
            return p;
        }
        public void Write(Int2 p, int value)
        {
            data[p.Y * Size.X + p.X] = value;
        }
    }
}

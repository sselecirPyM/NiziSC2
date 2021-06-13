using System;
using System.Collections.Generic;
using System.Collections;
using System.Text;
using SC2APIProtocol;

namespace NiziSC2.Core
{
    public class WriteableImageData
    {
        public byte[] Data;
        public int SizeX;
        public int SizeY;
        public WriteableImageData(ImageData imageData)
        {
            Data = imageData.Data.ToByteArray();
            SizeX = imageData.Size.X;
            SizeY = imageData.Size.Y;
        }
        public bool Query(Int2 p) => Query(p.X, p.Y);

        public bool Query(int x, int y)
        {
            if (x < 0 || y < 0 || x >= SizeX || y >= SizeY)
            {
                return false;
            }
            int pixelID = x + y * SizeX;
            int byteLocation = pixelID / 8;
            int bitLocation = pixelID % 8;
            var result = ((Data[byteLocation] & 1 << (7 - bitLocation)) == 0) ? 0 : 1;
            return result != 0;
        }
        public bool Write(Int2 p, bool value) => Write(p.X, p.Y, value);
        public bool Write(int x, int y, bool value)
        {
            if (x < 0 || y < 0 || x >= SizeX || y >= SizeY)
            {
                return false;
            }
            int pixelID = x + y * SizeX;
            int byteLocation = pixelID / 8;
            int bitLocation = pixelID % 8;

            if (value)
            {
                Data[byteLocation] = (byte)(Data[byteLocation] | 1 << (7 - bitLocation));
            }
            else
            {
                Data[byteLocation] = (byte)(Data[byteLocation] & ~(1 << (7 - bitLocation)));
            }

            return true;
        }
    }
}

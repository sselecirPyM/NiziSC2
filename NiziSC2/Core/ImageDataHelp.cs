using System;
using System.Collections.Generic;
using System.Text;
using SC2APIProtocol;
using System.Numerics;

namespace NiziSC2.Core
{
    public static class ImageDataHelp
    {
        public static bool SamplePoint(this ImageData imageData, Vector2 position)
        {
            return Query(imageData, new Int2((int)(position.X * (imageData.Size.X - 1)), (int)(position.Y * (imageData.Size.Y - 1))));
        }
        public static int SampleBytePoint(this ImageData imageData, Vector2 position)
        {
            return GetDataValueByte(imageData,(int)(position.X * (imageData.Size.X - 1)), (int)(position.Y * (imageData.Size.Y - 1)));
        }


        public static bool Query(this ImageData imageData, Int2 p) => Query(imageData, p.X, p.Y);

        public static bool Query(this ImageData imageData, int x, int y)
        {
            if (x < 0 || y < 0 || x >= imageData.Size.X || y >= imageData.Size.Y)
            {
                return false;
            }
            int pixelID = x + y * imageData.Size.X;
            int byteLocation = pixelID / 8;
            int bitLocation = pixelID % 8;
            var result = ((imageData.Data.Span[byteLocation] & (1 << (7 - bitLocation))) == 0) ? 0 : 1;
            if(result!=0)
            {

            }
            return result != 0;
        }
        public static int GetDataValueByte(ImageData data, int x, int y)
        {
            int pixelID = x + y * data.Size.X;
            return data.Data[pixelID];
        }
    }
}

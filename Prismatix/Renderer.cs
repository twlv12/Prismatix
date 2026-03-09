using System;
using Prismatix.Math;

namespace Prismatix
{
    public class Renderer
    {
        public static byte[] Render()
        {
            int width = Config.imgWidth;
            int height = Config.imgHeight;

            //multiply by 3 to give 3 bytes for each RGB
            byte[] image = new byte[width*height*3];

            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                {
                    int i = (y * width + x) * 3;

                    //map red to x and green to y, blue is 0
                    float r = Utils.Remap(x, 0f, width, 0f, 255f);
                    float g = Utils.Remap(y, 0f, height, 0f, 255f);

                    //ramshackle round cause MathF.Round dont work
                    image[i] = (byte)(r+0.5f);
                    image[i+1] = (byte)(g+0.5f);
                    image[i+2] = 0;
                }
            
            return image;
        }
    }
}
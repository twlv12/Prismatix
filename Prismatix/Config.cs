using System;
using System.IO;
using System.Text.Json;
using SysMath = System.Math;

namespace Prismatix
{
    //this is a short intermediate utility to load the data from the json
    //so that i dont have to rebuild every time to change the config

    public static class Config
    {
        public static int imgWidth { get; set; }
        public static int imgHeight { get; set; }
        public static float aspectRatio { get; set; }
        public static float fov { get; set; }
        public static float maxSamples { get; set; }
        public static float maxBounces { get; set; }
        public static float maxRayDepth { get; set; }
        public static int[] bgColour { get; set; }

        public static void Load(string path)
        {
            string json = File.ReadAllText(path);
            var config = JsonSerializer.Deserialize<ConfigData>(json);
            //automatically assigns config data attributes to fields in the json
            //had problems before directly setting the static attributes

            imgWidth = config.imgWidth;
            imgHeight = config.imgHeight;
            aspectRatio = (float)config.imgWidth / (float)config.imgHeight;
            fov = config.fov * (float)SysMath.PI / 180f; // convert to radians if needed
            maxSamples = config.maxSamples;
            maxBounces = config.maxBounces;
            maxRayDepth = config.maxRayDepth;
            bgColour = config.bgColour;

            Console.WriteLine($"Resolution: {imgWidth}x{imgHeight}px FOV: {fov}rad");
        }

        private class ConfigData
        { //temporary struct to hold data from json
            public int imgWidth { get; set; }
            public int imgHeight { get; set; }
            public float aspectRatio { get; set; }
            public float fov { get; set; }
            public float maxSamples { get; set; }
            public float maxBounces { get; set; }
            public float maxRayDepth { get; set; }
            public int[] bgColour { get; set; }
        }
    }
}
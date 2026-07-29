using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using SysMath = System.Math;

namespace Prismatix
{
    //this is a short intermediate utility to load the data from the json
    //so that i dont have to rebuild every time to change the config
    //STILL NEED TO REBUILD WHEN ADDING NEW FIELD

    public static class Config
    {
        public static int imgWidth { get; set; }
        public static int imgHeight { get; set; }
        public static float fov { get; set; }

        public static int maxSamples { get; set; }
        public static int maxRayDepth { get; set; }
        public static int triThreshold { get; set; }

        public static int[] bgColour { get; set; }
        public static float ambientIntensity { get; set; }


        public static float aspectRatio => (float)imgWidth / imgHeight;
        public static float fovRad => fov * (float)SysMath.PI / 180f;


        public static void Load(string path)
        {
            string json = File.ReadAllText(path);
            var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
            //deserialize to dictionary so can directly apply new configs 

            foreach (var keyval in dict)
            {
                var prop = typeof(Config).GetProperty(keyval.Key, BindingFlags.Public | BindingFlags.Static);

                if (prop != null && prop.CanWrite)
                {
                    object value = JsonSerializer.Deserialize(keyval.Value.GetRawText(), prop.PropertyType);
                    prop.SetValue(null, value);
                }
            }

            Console.WriteLine($"Resolution: {imgWidth}x{imgHeight}px FOV: {fovRad}rad");
        }
    }
}

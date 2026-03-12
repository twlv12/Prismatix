namespace Prismatix
{
    public static class Config
    {
        public static int imgWidth = 1024;
        public static int imgHeight = 1024;
        public static float fov = 90f;
        public static int aspectRatio = imgWidth / imgHeight;

        public static int maxSamples = 32;
        public static int maxBounces = 6;
        public static float maxRayDistance = 1000f;
    }
}
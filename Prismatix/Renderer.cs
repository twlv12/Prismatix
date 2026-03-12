using System;
using SysMath = System.Math;
using Prismatix.Math;
using Prismatix.Geometry;

namespace Prismatix
{
    public class Renderer
    {
        public static byte[] Render(Scene scene)
        {
            int width = Config.imgWidth;
            int height = Config.imgHeight;

            //multiply by 3 to give 3 bytes for each RGB
            byte[] image = new byte[width * height * 3];

            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    Raycast ray = scene.mainCamera.ShootRay(x, y);
                    HitInfo? closestHit = null;

                    foreach (var obj in scene.objects){
                        for (int i = 0; i < obj.mesh.indices.Count; i += 3)
                        {
                            Vector3 a = obj.position + obj.mesh.vertices[obj.mesh.indices[i]];
                            Vector3 c = obj.position + obj.mesh.vertices[obj.mesh.indices[i + 1]];
                            Vector3 b = obj.position + obj.mesh.vertices[obj.mesh.indices[i + 2]];

                            var hit = Utils.GetRayIntersect(ray, a, b, c);
                            if (hit.HasValue) {
                                if (closestHit == null || hit.Value.distance < closestHit.Value.distance) {
                                    closestHit = hit;
                                }
                            }
                        }
                    }

                    int index = (y * width + x) * 3; //index of first byte (R) for x,y
                    byte shade = 0;

                    if (closestHit.HasValue)
                    {
                        float depth = Utils.Remap(closestHit.Value.distance, 0, 25f, 255, 0);
                        shade = (byte)Utils.Clamp(depth, 0f, 255f);
                    }
                    else { shade = 0; }

                    image[index] = shade;
                    image[index + 1] = shade;
                    image[index + 2] = shade;              
                }
            return image;
        }
    }

    public class Raycast
    {
        public Vector3 origin;
        public Vector3 direction;

        public Raycast(Vector3 start, Vector3 dir)
        {
            origin = start;
            direction = dir;
        }
    }

    public class Camera
    {
        public Vector3 position;
        public Vector3 forward; ///vectors used for rays
        public Vector3 up;
        public Vector3 right;

        public float vpHeight, vpWidth;
        public Vector3 origin;

        public Camera(Vector3 pos, Vector3 forwardDir, Vector3 upDir)
        {
            position = pos;
            forward = forwardDir.Normalized();
            up = upDir.Normalized();
            right = Utils.Cross(forward, up).Normalized();

            vpHeight = 2f * (float)SysMath.Tan(Config.fov / 2f);
            vpWidth = vpHeight * Config.aspectRatio;

            Vector3 center = position + forward; //origin is top left of viewplane
            Vector3 origin = center - right * (vpWidth / 2f) - up * (vpHeight / 2f);
        }

        public Raycast ShootRay(int x, int y)
        {
            float u = (float)x / (Config.imgWidth - 1);
            float v = (float)y / (Config.imgHeight - 1);

            Vector3 pixelVector = origin + right * (u * vpWidth) + up * (v * vpHeight);
            Vector3 rayDirection = (pixelVector - position).Normalized();

            Raycast ray = new Raycast(position, rayDirection);

            return ray;
        }
    }
}
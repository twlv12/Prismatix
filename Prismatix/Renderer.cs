using System;
using SysMath = System.Math; //fixing ambugiuity with own prismatix.math
using Prismatix.Math;
using Prismatix.Geometry;
using System.Threading.Tasks;

namespace Prismatix
{
    public class Renderer
    {
        public static Image RenderDepth(Scene scene)
        {
            #region Render Setup
            int width = Config.imgWidth;
            int height = Config.imgHeight;
            Image image = new Image(width, height);

            //using a depth buffer for a 2 stage render to find min and max depth first
            float[] depthBuffer = new float[width*height];
            float minDepth = float.MaxValue;
            float maxDepth = float.MinValue;
            #endregion

            #region Main Rendering Loop => depthBuffer
            Parallel.For(0, height, y => //multithread for each row of pixels
            {   for (int x = 0; x < width; x++) //for every pixel...
                {
                    Raycast ray = scene.mainCamera.ShootRay(x, y);
                    HitInfo? closestHit = null;

                    #region Geometry Intersections => closestHit
                    foreach (var obj in scene.objects){
                        for (int i = 0; i < obj.mesh.indices.Count/3; i++) //for every tri...
                        {
                            var (a, b, c) = obj.mesh.GetTri(i, obj.position); //get tri vertices
                            HitInfo? hit = Utils.GetRayIntersect(ray, a, b, c);

                            if (hit.HasValue) { //had an issue where the if statement was still evaluating when null and crashing
                                if (!closestHit.HasValue || hit.Value.distance < closestHit.Value.distance){
                                    closestHit = hit; //only take the closest hit
                                }
                            }
                        }
                    }
                    #endregion

                    #region Depth Logic => depthBuffer
                    float depth = -1;
                    if (closestHit.HasValue){
                        depth = closestHit.Value.distance; //use these .Value things for nullable (HitInfo?) structs
                        if (depth < minDepth) minDepth = depth; //calculating the min and max depth from shorted and longest rays
                        if (depth > maxDepth) maxDepth = depth;
                    }
                    depthBuffer[y*width +x] = depth;
                    #endregion
                }
            });
            #endregion Main Rendering Loop

            #region Shading Pass => image
            for (int i = 0; i < depthBuffer.Length; i++){ //now convert all to shade
                float depth = depthBuffer[i];
                byte shade = 0;

                Vector3 colour = new Vector3(0, 0, 0);
                if (depth >= 0){
                    float value = Utils.Remap(depth, minDepth, maxDepth, 255, 0);
                    shade = (byte)Utils.Clamp(value, 0, 255);
                    colour = new Vector3(shade, shade, shade);
                }
                else{
                    colour = new Vector3(Config.bgColour[0], Config.bgColour[1], Config.bgColour[2]);
                }
                
                image.SetPixel(i%width, i/width, colour);
            }
            #endregion Shading Pass

            return image;
        }

        public static Image RenderNormal(Scene scene)
        {
            #region Render Setup
            int width = Config.imgWidth;
            int height = Config.imgHeight;

            //multiply by 3 to give 3 bytes for each RGB
            Image image = new Image(width, height);
            #endregion

            #region Main Rendering Loop => image
            Parallel.For(0, height, y => //multithread for each row of pixels
            {   for (int x = 0; x < width; x++)
                {
                    Raycast ray = scene.mainCamera.ShootRay(x, y);
                    HitInfo? closestHit = null;

                    #region Geometry Intersections => closestHit
                    foreach (var obj in scene.objects)
                    {
                        for (int i = 0; i < obj.mesh.indices.Count / 3; i++)
                        {
                            var (a, b, c) = obj.mesh.GetTri(i, obj.position);
                            HitInfo? hit = Utils.GetRayIntersect(ray, a, b, c);

                            if (hit.HasValue){
                                if (!closestHit.HasValue || hit.Value.distance < closestHit.Value.distance){
                                    closestHit = hit;
                                }
                            }
                        }
                    }
                    #endregion

                    #region Normal Logic => image
                    if (!closestHit.HasValue)
                    { //if no hit so background
                        image.SetPixel(x, y, new Vector3(Config.bgColour[0], Config.bgColour[1], Config.bgColour[2]));
                        continue;
                    } //for some reason causes a crash when using an else statement below
                    //solved: the bg colour wasnt set in the config cs.

                    Vector3 colour = new Vector3(0, 0, 0);
                    //normal logic

                    if (closestHit.HasValue){
                        Vector3 normal = closestHit.Value.normal;
                        byte red = (byte)Utils.Remap(normal.x, -1, 1, 0, 255);
                        byte green = (byte)Utils.Remap(normal.y, -1, 1, 0, 255);
                        byte blue = (byte)Utils.Remap(normal.z, -1, 1, 0, 255);

                        image.SetPixel(x, y, new Vector3(red, green, blue));
                    }

                    
                    #endregion
                }
            });
            #endregion

            return image;
        }

        public static Image RenderDiffuse(Scene scene)
        {
            #region Render Setup
            int width = Config.imgWidth;
            int height = Config.imgHeight;

            //multiply by 3 to give 3 bytes for each RGB
            Image image = new Image(width, height);
            #endregion

            #region Main Rendering Loop => image
            Parallel.For(0, height, y => //multithread for each row of pixels
            {
                for (int x = 0; x < width; x++)
                {
                    Raycast ray = scene.mainCamera.ShootRay(x, y);
                    HitInfo? closestHit = null;

                    #region Geometry Intersections => closestHit
                    foreach (var obj in scene.objects)
                    {
                        for (int i = 0; i < obj.mesh.indices.Count / 3; i++)
                        {
                            var (a, b, c) = obj.mesh.GetTri(i, obj.position);
                            HitInfo? hit = Utils.GetRayIntersect(ray, a, b, c);

                            if (hit.HasValue)
                            {
                                if (!closestHit.HasValue || hit.Value.distance < closestHit.Value.distance)
                                {
                                    closestHit = hit;
                                }
                            }
                        }
                    }
                    #endregion

                    #region Shadow Rays & Dot => image
                    if (!closestHit.HasValue)
                    { //if no hit so background
                        image.SetPixel(x, y, new Vector3(Config.bgColour[0], Config.bgColour[1], Config.bgColour[2]));
                        continue;
                    }

                    float illumination = 0f;
                    if (closestHit.HasValue)
                    {
                        foreach (var lamp in scene.lamps)
                        {
                            Vector3 vecToLamp = lamp.position - closestHit.Value.point;
                            Vector3 shadowRayOrigin = closestHit.Value.point + closestHit.Value.normal * 0.001f;

                            Raycast shadowRay = new Raycast(shadowRayOrigin, vecToLamp.Normalized());
                            HitInfo? closestShadowHit = null;

                            #region Geometry Intersections => closestShadowHit
                            foreach (var obj in scene.objects)
                            {
                                for (int i = 0; i < obj.mesh.indices.Count / 3; i++)
                                {
                                    var (a, b, c) = obj.mesh.GetTri(i, obj.position);
                                    HitInfo? shadowHit = Utils.GetRayIntersect(shadowRay, a, b, c);

                                    if (shadowHit.HasValue)
                                    {
                                        if (!closestShadowHit.HasValue || shadowHit.Value.distance < closestShadowHit.Value.distance)
                                        {
                                            closestShadowHit = shadowHit;
                                        }
                                    }
                                }
                            }
                            #endregion

                            float distToLamp = vecToLamp.Magnitude();
                            if (!closestShadowHit.HasValue || closestShadowHit.Value.distance > distToLamp)
                            {
                                float dot = Utils.Dot(vecToLamp, closestHit.Value.normal);
                                if (dot < 0) { dot =  0; }
                                illumination += (lamp.brightness * dot) / (distToLamp*distToLamp);
                            }
                        }

                        float pixelLumen = Utils.Clamp(illumination, 0f, 255f);
                        image.SetPixel(x, y, new Vector3(pixelLumen, pixelLumen, pixelLumen));
                    }

                    #endregion
                }
            });
            #endregion

            return image;
        }

        public static Image RenderDiffuseFast(Scene scene)
        {
            #region Render Setup
            int width = Config.imgWidth;
            int height = Config.imgHeight;

            //multiply by 3 to give 3 bytes for each RGB
            Image image = new Image(width, height);
            #endregion

            #region Main Rendering Loop => image
            Parallel.For(0, height, y => //multithread for each row of pixels
            {
                for (int x = 0; x < width-3; x+=3)
                {
                    Raycast ray = scene.mainCamera.ShootRay(x, y);
                    HitInfo? closestHit = null;

                    #region Geometry Intersections => closestHit
                    foreach (var obj in scene.objects)
                    {
                        for (int i = 0; i < obj.mesh.indices.Count / 3; i++)
                        {
                            var (a, b, c) = obj.mesh.GetTri(i, obj.position);
                            HitInfo? hit = Utils.GetRayIntersect(ray, a, b, c);

                            if (hit.HasValue)
                            {
                                if (!closestHit.HasValue || hit.Value.distance < closestHit.Value.distance)
                                {
                                    closestHit = hit;
                                }
                            }
                        }
                    }
                    #endregion

                    #region Shadow Rays & Dot => image
                    if (!closestHit.HasValue)
                    { //if no hit so background
                        image.SetPixel(x, y, new Vector3(Config.bgColour[0], Config.bgColour[1], Config.bgColour[2]));
                        continue;
                    }

                    float illumination = 0f;
                    if (closestHit.HasValue)
                    {
                        foreach (var lamp in scene.lamps)
                        {
                            Vector3 vecToLamp = lamp.position - closestHit.Value.point;
                            Vector3 shadowRayOrigin = closestHit.Value.point + closestHit.Value.normal * 0.001f;

                            Raycast shadowRay = new Raycast(shadowRayOrigin, vecToLamp.Normalized());
                            HitInfo? closestShadowHit = null;

                            #region Geometry Intersections => closestShadowHit
                            foreach (var obj in scene.objects)
                            {
                                for (int i = 0; i < obj.mesh.indices.Count / 3; i++)
                                {
                                    var (a, b, c) = obj.mesh.GetTri(i, obj.position);
                                    HitInfo? shadowHit = Utils.GetRayIntersect(shadowRay, a, b, c);

                                    if (shadowHit.HasValue)
                                    {
                                        if (!closestShadowHit.HasValue || shadowHit.Value.distance < closestShadowHit.Value.distance)
                                        {
                                            closestShadowHit = shadowHit;
                                        }
                                    }
                                }
                            }
                            #endregion

                            float distToLamp = vecToLamp.Magnitude();
                            if (!closestShadowHit.HasValue || closestShadowHit.Value.distance > distToLamp)
                            {
                                float dot = Utils.Dot(vecToLamp, closestHit.Value.normal);
                                if (dot < 0) { dot = 0; }
                                illumination += (lamp.brightness * dot) / (distToLamp * distToLamp);
                            }
                        }

                        float pixelLumen = Utils.Clamp(illumination, 0f, 255f);
                        image.SetPixel(x, y, new Vector3(pixelLumen, pixelLumen, pixelLumen));
                    }

                    #endregion
                }
            });
            #endregion

            return image;
        }
    }

    public class Raycast 
    {
        #region Raycast
        public Vector3 origin;
        public Vector3 direction;
        public Raycast(Vector3 start, Vector3 dir){
            origin = start;
            direction = dir;
        }
        #endregion
    }

    public class Camera
    {
        #region Camera Vectors
        public Vector3 position;
        public Vector3 forward; //vectors used for rays
        public Vector3 up;
        public Vector3 right;
        public Vector3 origin;
        public Vector3 center;
        public float vpHeight, vpWidth;
        #endregion

        #region Boring Camera Stuff
        public Camera(Vector3 pos, Vector3 forwardDir, Vector3 upDir)
        {
            position = pos;
            forward = forwardDir.Normalized();
            up = upDir.Normalized();
            right = Utils.Cross(forward, up).Normalized();

            vpHeight = 2f * (float)SysMath.Tan(Config.fov / 2f);
            vpWidth = vpHeight * Config.aspectRatio;

            center = position + forward; //origin is top left of viewplane
            origin = center - right*(vpWidth / 2f) - up*(vpHeight / 2f);
            //previously had Vector3 origin here, which was only creating local var. meaning tris
            //were created relative to camera origin and not in world space, I think that was the issue
        }

        public Raycast ShootRay(int x, int y)
        {
            float u = (float)x / (Config.imgWidth -1);
            float v = (float)y / (Config.imgHeight -1);

            Vector3 pixelVector = origin + right*(u*vpWidth) + up*(v*vpHeight);
            Vector3 rayDirection = (pixelVector - position).Normalized();

            return new Raycast(position, rayDirection);
        }

        public void RotateTo(Vector3 target)
        {
            forward = (target - position).Normalized();

            right = Utils.Cross(forward, new Vector3(0, 0, 1)).Normalized();
            up = Utils.Cross(right, forward).Normalized();

            center = position + forward;
            origin = center - right*(vpWidth / 2f) - up*(vpHeight / 2f);
        }
        #endregion
    }
}
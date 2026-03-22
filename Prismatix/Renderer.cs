using System;
using SysMath = System.Math; //fixing ambugiuity with own prismatix.math
using Prismatix.Math;
using Prismatix.Geometry;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

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

            foreach (var obj in scene.objects)
            {
                if (obj.needsPrecomp)
                {
                    obj.BakeAllTris();
                    (obj.boundsMin, obj.boundsMax) = Utils.CalculateBounds(obj.bakedTriangles);
                }
            }
            #endregion

            #region Main Rendering Loop => depthBuffer
            Parallel.For(0, height, y => //multithread for each row of pixels
            {   for (int x = 0; x < width; x++) //for every pixel...
                {
                    Raycast ray = scene.mainCamera.ShootRay(x, y);
                    HitInfo? closestHit = null;

                    #region Geometry Intersections => closestHit
                    foreach (var obj in scene.objects)
                    {
                        if (!Utils.GetRayHitsBounds(ray, obj.boundsMin, obj.boundsMax)){
                            continue;
                        }

                        foreach (Triangle tri in obj.bakedTriangles) //for every tri...
                        {
                            HitInfo? hit = Utils.GetRayIntersect(ray, tri);

                            if (hit.HasValue){
                                var hV = hit.Value;
                                if (!closestHit.HasValue || hV.distance < closestHit.Value.distance){
                                    closestHit = hV;
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

            foreach (var obj in scene.objects){
                if (obj.needsPrecomp){
                    obj.BakeAllTris();
                    (obj.boundsMin, obj.boundsMax) = Utils.CalculateBounds(obj.bakedTriangles);
                }
            }
            #endregion

            #region Main Rendering Loop => image
            Parallel.For(0, height, y => //multithread for each row of pixels
            {   for (int x = 0; x < width; x++)
                {
                    Raycast ray = scene.mainCamera.ShootRay(x, y);
                    HitInfo? closestHit = Utils.TraverseBVH(ray, scene.rootBVH);

                    #region Geometry Intersections => closestHit
                    //foreach (var obj in scene.objects)
                    //{
                    //    //Console.WriteLine($"NextObj Bounds: {obj.boundsMin},{obj.boundsMax}");
                    //    if (!Utils.GetRayHitsBounds(ray, obj.boundsMin, obj.boundsMax)){
                    //        continue;
                    //    }
                    //    //Console.WriteLine("Ray entered bounds. Now checking triangles.");
                    //
                    //    foreach (Triangle tri in obj.bakedTriangles)
                    //    {
                    //        //Vector3 triCenter = (tri.a + tri.b + tri.c) / 3;
                    //        //Console.WriteLine($"CurrTri Center: {triCenter.x},{triCenter.y},{triCenter.z}");
                    //
                    //        HitInfo? hit = Utils.GetRayIntersect(ray, tri);
                    //
                    //        if (hit.HasValue){
                    //            var hV = hit.Value;
                    //            //Console.WriteLine("Hit");
                    //            if (!closestHit.HasValue || hV.distance < closestHit.Value.distance){
                    //                closestHit = hV;
                    //            }
                    //        }
                    //        //else { Console.WriteLine("No hit."); }
                    //    }
                    //}
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

            foreach (var obj in scene.objects){
                if (obj.needsPrecomp){
                    obj.BakeAllTris();
                    (obj.boundsMin, obj.boundsMax) = Utils.CalculateBounds(obj.bakedTriangles);
                }
            }

            Vector3 bgColour = new Vector3(Config.bgColour[0], Config.bgColour[1], Config.bgColour[2]);
            Vector3 bgLight = new Vector3(Config.bgLight[0], Config.bgLight[1], Config.bgLight[2]);
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
                        if (!Utils.GetRayHitsBounds(ray, obj.boundsMin, obj.boundsMax)){
                            continue;
                        }

                        foreach (Triangle tri in obj.bakedTriangles)
                        {
                            HitInfo? hit = Utils.GetRayIntersect(ray, tri);

                            if (hit.HasValue){
                                var hV = hit.Value;
                                hV.material = obj.material;

                                if (!closestHit.HasValue || hV.distance < closestHit.Value.distance){
                                    closestHit = hV;
                                }
                            }
                        }
                    }
                    #endregion

                    if (!closestHit.HasValue)
                    { //if no hit so background
                        image.SetPixel(x, y, bgColour);
                        continue;
                    }

                    #region Shadow Rays & Dot => image
                    float illumination = 0f;
                    Vector3 shadowRayOrigin = closestHit.Value.point + closestHit.Value.normal * 0.001f;

                    if (closestHit.HasValue)
                    {
                        foreach (var lamp in scene.lamps)
                        {
                            Boolean blocked = false;
                            Vector3 vecToLamp = lamp.position - closestHit.Value.point;

                            float distToLamp = vecToLamp.Magnitude();
                            Vector3 dirToLamp = vecToLamp/distToLamp;

                            Raycast shadowRay = new Raycast(shadowRayOrigin, dirToLamp); 

                            #region Geometry Intersections => closestShadowHit
                            foreach (var obj in scene.objects)
                            {
                                if (!Utils.GetRayHitsBounds(shadowRay, obj.boundsMin, obj.boundsMax)){
                                    continue;
                                }

                                foreach (Triangle tri in obj.bakedTriangles){
                                    HitInfo? shadowHit = Utils.GetRayIntersect(shadowRay, tri);

                                    if (shadowHit.HasValue && shadowHit.Value.distance < distToLamp){
                                        blocked = true;
                                        break;
                                    }
                                }
                                if (blocked) { break; }
                            }
                            #endregion

                            #region Shade => image
                            if (!blocked) {
                                float dot = Utils.Dot(dirToLamp, closestHit.Value.normal);
                                if (dot < 0) dot = 0;
                                illumination += (lamp.brightness * dot) / (distToLamp * distToLamp);
                            }
                        }

                        float pixelLumen = Utils.Clamp(illumination, 0f, 255f);
                        image.SetPixel(x, y, pixelLumen*closestHit.Value.material.colour +bgLight);
                        #endregion
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

            foreach (var obj in scene.objects){
                if (obj.needsPrecomp){
                    obj.BakeAllTris();
                    (obj.boundsMin, obj.boundsMax) = Utils.CalculateBounds(obj.bakedTriangles);
                }
            }
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

                        if (!Utils.GetRayHitsBounds(ray, obj.boundsMin, obj.boundsMax)){
                            continue;
                        }

                        foreach (Triangle tri in obj.bakedTriangles)
                        {
                            HitInfo? hit = Utils.GetRayIntersect(ray, tri);

                            if (hit.HasValue){
                                var hV = hit.Value;
                                if (!closestHit.HasValue || hV.distance < closestHit.Value.distance){
                                    closestHit = hV;
                                }
                            }
                        }
                    }
                    #endregion

                    if (!closestHit.HasValue)
                    { //if no hit so background
                        image.SetPixel(x, y, new Vector3(Config.bgColour[0], Config.bgColour[1], Config.bgColour[2]));
                        continue;
                    }

                    #region Shadow Rays & Dot => image
                    float illumination = 0f;
                    if (closestHit.HasValue)
                    {
                        foreach (var lamp in scene.lamps)
                        {
                            Boolean blocked = false;
                            Vector3 vecToLamp = lamp.position - closestHit.Value.point;
                            Vector3 shadowRayOrigin = closestHit.Value.point + closestHit.Value.normal * 0.001f;

                            float distToLamp = vecToLamp.Magnitude();
                            Vector3 dirToLamp = vecToLamp / distToLamp;

                            Raycast shadowRay = new Raycast(shadowRayOrigin, dirToLamp);

                            #region Geometry Intersections => closestShadowHit
                            foreach (var obj in scene.objects)
                            {
                                foreach (Triangle tri in obj.bakedTriangles)
                                {
                                    HitInfo? shadowHit = Utils.GetRayIntersect(shadowRay, tri);

                                    if (shadowHit.HasValue && shadowHit.Value.distance < distToLamp)
                                    {
                                        blocked = true;
                                        break;
                                    }
                                }

                                if (blocked) { break; }
                            }
                            #endregion

                            #region Shade => image
                            if (!blocked)
                            {
                                float dot = Utils.Dot(dirToLamp, closestHit.Value.normal);
                                if (dot < 0) dot = 0;
                                illumination += (lamp.brightness * dot) / (distToLamp * distToLamp);
                            }
                        }
                        float pixelLumen = Utils.Clamp(illumination, 0f, 255f);
                        image.SetPixel(x, y, new Vector3(pixelLumen, pixelLumen, pixelLumen));
                        #endregion
                    }

                    #endregion
                }
            });
            #endregion

            return image;
        }
    }

    public class BoundingVolume
    {
        #region BVH Vars
        public Vector3 boundsMin;
        public Vector3 boundsMax;
        public BoundingVolume left;
        public BoundingVolume right;
        public Boolean isLeaf;
        public string longestAxis;
        public List<Triangle> triangles;
        #endregion

        public BoundingVolume(List<Triangle> trisGiven)
        {
            #region Calulate BVH Bounds
            (boundsMin, boundsMax) = Utils.CalculateBounds(trisGiven);

            //calculate longest axis to split and spatially sort tris by
            float xSize = boundsMax.x - boundsMin.x;
            float ySize = boundsMax.y - boundsMin.y;
            float zSize = boundsMax.z - boundsMin.z;

            longestAxis = "x";
            if (xSize >= ySize && xSize >= zSize) { longestAxis = "x"; }
            else if (ySize >= zSize ) { longestAxis = "y"; }
            else { longestAxis = "z"; }
            #endregion

            #region Leaf or Parent?
            if (trisGiven.Count <= Config.triThreshold)
            {
                isLeaf = true;
                triangles = trisGiven;
            }
            else
            { 
                isLeaf = false;

                List<Triangle> trianglesToGive = new List<Triangle>();
                if (longestAxis == "x"){
                    trianglesToGive = trisGiven.OrderBy(tri => tri.center.x).ToList();}
                if (longestAxis == "y"){
                    trianglesToGive = trisGiven.OrderBy(tri => tri.center.y).ToList();}
                if (longestAxis == "z"){
                    trianglesToGive = trisGiven.OrderBy(tri => tri.center.z).ToList();}

                int numTri = trianglesToGive.Count;
                left = new BoundingVolume(trianglesToGive.GetRange (0       , numTri/2          ));
                right = new BoundingVolume(trianglesToGive.GetRange(numTri/2, numTri-(numTri/2) ));
            }
            #endregion
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
        public Vector3 horizontal;
        public Vector3 vertical;
        public float vpHeight, vpWidth;
        #endregion

        #region Camera Methods (Vector Hell)
        public Camera(Vector3 pos, Vector3 forwardDir, Vector3 upDir)
        {
            position = pos;
            forward = forwardDir.Normalized();
            right = Utils.Cross(upDir, forward).Normalized();

            //recompute up to ensure its perpendicualar, caused the weird parallelogram effect
            up = Utils.Cross(forward, right);

            vpHeight = 2f * (float)SysMath.Tan(Config.fov / 2f);
            vpWidth = vpHeight * Config.aspectRatio;
            horizontal = right * vpWidth;
            vertical = up * vpHeight;

            center = position + forward; //origin is top left of viewplane
            origin = center - right*(vpWidth / 2f) - up*(vpHeight / 2f);
            //previously had Vector3 origin here, which was only creating local var. meaning tris
            //were created relative to camera origin and not in world space, I think that was the issue
        }

        public Raycast ShootRay(int x, int y)
        {
            float u = (float)x / (Config.imgWidth -1);
            float v = (float)y / (Config.imgHeight -1);

            Vector3 pixelVector = origin + u*horizontal + v*vertical;
            Vector3 rayDirection = (pixelVector - position).Normalized();

            //Console.WriteLine($"Forward: {forward}, RayDir: {rayDirection}");

            return new Raycast(position, rayDirection);
        }

        public void RotateTo(Vector3 target)
        {
            forward = (target - position).Normalized();
            
            Vector3 worldUp = new Vector3(0,0,1);
            up = worldUp - forward*Utils.Dot(worldUp, forward);
            up = up.Normalized();
            
            //calc up
            
            right = Utils.Cross(up, forward).Normalized();
            
            //recompute up to ensure its perpendicualar, caused the weird parallelogram effect
            up = Utils.Cross(forward, right);
            
            horizontal = right * vpWidth;
            vertical = up * vpHeight;
            
            center = position + forward; //origin is top left of viewplane
            origin = center - right * (vpWidth / 2f) - up * (vpHeight / 2f);
        }
        #endregion
    }

}
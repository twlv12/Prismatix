using ComputeSharp;
using Prismatix.Geometry;
using Prismatix.Math;
using Prismatix.Shaders;
using System;
using System.Collections.Generic;
using System.Linq;
using SysMath = System.Math; //fixing ambugiuity with own prismatix.math

namespace Prismatix
{
    public class Renderer
    {
        //GPU buffers
        private static ReadOnlyBuffer<GPUNode> gpuNodes;
        private static ReadOnlyBuffer<GPUTriangle> gpuTris;
        private static ReadOnlyBuffer<GPULamp> gpuLamps;
        private static ReadWriteTexture2D<uint> gpuImage;
        private static ReadOnlyTexture2D<float4> gpuHdri;
        private static ReadOnlyBuffer<float4> gpuTexture;

        //CPU buffers (for pythno)
        private static byte[] rawPixelData;
        private static uint[] gpuDownloadBuffer;

        public static Image RenderGPU(Scene scene, int renderMode)
        {
            int width = Config.imgWidth;
            int height = Config.imgHeight;
            Image image = new Image(width, height);

            #region Precompute & Build Buffers
            bool needsGPUTransmit = false;

            //precompute and check if gpu buffers need to be rebuilt (if scene changed)
            foreach (var obj in scene.objects)
                if (obj.needsPrecomp)
                {
                    obj.BakeAllTris();
                    obj.needsPrecomp = false;
                    needsGPUTransmit = true;
                }

            //only need to rebuild genometry buffers if scene changed
            if (needsGPUTransmit || gpuNodes == null || scene.isOutdated)
            {
                scene.BuildBVH();
                var (nodesArr, trisArr) = scene.GPUifyBVH();

                //previously added an empty bounds from 0,0,0 to 0,0,0, as well as a lamp at 0,
                //causing light falloff calc of distance to div by 0, causing crash when zero objects.

                if (nodesArr.Length == 0)
                {
                    nodesArr = new GPUNode[]
                    { new GPUNode { //random bounds to prevent any division weirdness
                            boundsMin = new Math.Vector3(23132f, 32142f, 12512f),
                            boundsMax = new Math.Vector3(-12124f, -63721f, -12562f),
                            leftChild = -1, rightChild = -1, triCount = 0
                        } };
                }
                if (trisArr.Length == 0){
                    trisArr = new GPUTriangle[1];
                }

                //flatten data to value types for gpu
                Shaders.GPULamp[] flatLamps = new Shaders.GPULamp[scene.lamps.Count];
                for (int i = 0; i < scene.lamps.Count; i++)
                    flatLamps[i] = new Shaders.GPULamp
                    {
                        position = scene.lamps[i].position,
                        brightness = scene.lamps[i].isVisible ? scene.lamps[i].brightness : 0.0f
                    };

                if (flatLamps.Length == 0)
                {
                    flatLamps = new Shaders.GPULamp[] {
                        new Shaders.GPULamp {
                            //move lamp somewhere random to prevent NAN div
                            position = new Math.Vector3(0, 32132f, 0),
                            brightness = 0.0f
                        }
                    };
                }

                //remove old and create new gpu vram buffers
                gpuNodes?.Dispose();
                gpuTris?.Dispose();
                gpuLamps?.Dispose();
                gpuNodes = GraphicsDevice.GetDefault().AllocateReadOnlyBuffer(nodesArr);
                gpuTris = GraphicsDevice.GetDefault().AllocateReadOnlyBuffer(trisArr);
                gpuLamps = GraphicsDevice.GetDefault().AllocateReadOnlyBuffer(flatLamps);

                scene.isOutdated = false;
            }

            if (needsGPUTransmit || gpuTexture == null || scene.isOutdated)
            {
                gpuTexture?.Dispose();
                gpuTexture = GraphicsDevice.GetDefault().AllocateReadOnlyBuffer(scene.gpuTextureAtlas);
            }

            //only need to rebuild image and bytearr buffers if resolution changed
            if (gpuImage == null || gpuImage.Width != width || gpuImage.Height != height)
            {
                gpuImage?.Dispose();
                gpuImage = GraphicsDevice.GetDefault().AllocateReadWriteTexture2D<uint>(width, height);
                gpuDownloadBuffer = new uint[width * height];
                rawPixelData = new byte[width * height * 3]; 
            }

            if (gpuHdri == null || scene.hdriOutdated)
            {
                gpuHdri?.Dispose();
                gpuHdri = GraphicsDevice.GetDefault().AllocateReadOnlyTexture2D<float4>(scene.hdriWidth, scene.hdriHeight);
                gpuHdri.CopyFrom(scene.hdriArray);
                scene.hdriOutdated = false;
            }

            #endregion

            float3 bgColour = new float3(
                Config.bgColour[0] / 255f,
                Config.bgColour[1] / 255f,
                Config.bgColour[2] / 255f);

            var shader = new Shaders.Shader(
                gpuNodes, gpuTris, gpuLamps, gpuImage,
                renderMode, Config.maxSamples, Config.maxRayDepth, bgColour,
                scene.mainCamera.position, scene.mainCamera.origin,
                scene.mainCamera.horizontal, scene.mainCamera.vertical,
                width, height, gpuHdri, scene.useHdri, (uint)Environment.TickCount, scene.hdriIntensity, gpuTexture
            );

            //GO GPU! and retrieve once done
            GraphicsDevice.GetDefault().For(width, height, shader);
            gpuImage.CopyTo(gpuDownloadBuffer);

            //much faster raw bytearr rather than double for loop drawing pixel
            int byteIndex = 0;

            if (renderMode == 4)
            {
                float minDepth = float.MaxValue;
                float maxDepth = float.MinValue;

                //find min and max vals
                for (int i = 0; i < gpuDownloadBuffer.Length; i++)
                {
                    float dist = BitConverter.UInt32BitsToSingle(gpuDownloadBuffer[i]);
                    if (dist >= 0)
                    {
                        if (dist < minDepth) minDepth = dist;
                        if (dist > maxDepth) maxDepth = dist;
                    }
                }

                if (maxDepth == minDepth) maxDepth = minDepth + 0.1f;
                //check to prevent div by zero

                //remap data to 0-255
                for (int i = 0; i < gpuDownloadBuffer.Length; i++)
                {
                    float dist = BitConverter.UInt32BitsToSingle(gpuDownloadBuffer[i]);
                    byte col = 0; // Background is black

                    if (dist >= 0)
                    {
                        // Normalize 0.0 to 1.0 (Closest = 1.0/White, Furthest = 0.0/Black)
                        float normalized = 1.0f - ((dist - minDepth) / (maxDepth - minDepth));
                        col = (byte)(normalized * 255f);
                    }

                    rawPixelData[byteIndex++] = col;
                    rawPixelData[byteIndex++] = col;
                    rawPixelData[byteIndex++] = col;
                }
            }
            else
                //info why bitpacking in CShaders.cs
                for (int i = 0; i < gpuDownloadBuffer.Length; i++)
                {
                    uint packedColour = gpuDownloadBuffer[i];
                    rawPixelData[byteIndex++] = (byte)(packedColour & 0xFF);   //first 8
                    rawPixelData[byteIndex++] = (byte)((packedColour >> 8) & 0xFF);  //second 8
                    rawPixelData[byteIndex++] = (byte)((packedColour >> 16) & 0xFF); //third 8
                }
            
            Image output = new Image(width, height);
            output.data = rawPixelData;
            return output;
        }
    }

    public class BoundingVolume
    {
        #region BVH Vars
        public int depth;
        public Vector3 boundsMin;
        public Vector3 boundsMax;
        public BoundingVolume left;
        public BoundingVolume right;
        public Boolean isLeaf;
        public string longestAxis;
        public List<Triangle> triangles;
        #endregion

        public BoundingVolume(List<Triangle> trisGiven, int depth)
        {
            #region Calulate BVH Bounds&Axis
            this.depth = depth;

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

            #region Leaf || Parent?
            if (trisGiven.Count <= Config.triThreshold || depth > 64 || trisGiven.Count <= 1)
            {
                isLeaf = true;
                triangles = trisGiven;
            }
            else
            { 
                isLeaf = false;

                #region Split & Recurse
                List<Triangle> trianglesToGive = new List<Triangle>();
                if (longestAxis == "x"){
                    trianglesToGive = trisGiven.OrderBy(tri => tri.center.x).ToList();}
                if (longestAxis == "y"){
                    trianglesToGive = trisGiven.OrderBy(tri => tri.center.y).ToList();}
                if (longestAxis == "z"){
                    trianglesToGive = trisGiven.OrderBy(tri => tri.center.z).ToList();}

                int numTri = trianglesToGive.Count;
                int mid = numTri / 2;

                if (mid == 0 || mid == numTri)
                {
                    isLeaf = true;
                    triangles = trisGiven;
                    return;
                }

                left = new BoundingVolume(trianglesToGive.GetRange (0       , numTri/2         ), depth+1);
                right = new BoundingVolume(trianglesToGive.GetRange(numTri/2, numTri-(numTri/2)), depth+1);
                #endregion
            }
            #endregion
        }
    }

    public class Raycast 
    {
        #region Raycast
        public Vector3 origin;
        public Vector3 direction;
        public Vector3 invDirection;
        //precompute the direction for much faster bounds checking
        //division is very slow compared to *
        //before: suzanne took normal:0.05s, traced:0.5s
        //before: fighter took normal:0.5s, traced:5.2s

        public Raycast(Vector3 start, Vector3 dir){
            origin = start;
            direction = dir;
            invDirection = new Vector3(
                1f / (dir.x == 0 ? 0.00001f : dir.x),
                1f / (dir.y == 0 ? 0.00001f : dir.y),
                1f / (dir.z == 0 ? 0.00001f : dir.z)
            );
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

            //recompute up to ensure its perpendicualar
            right = Utils.Cross(upDir, forward).Normalized();
            up = Utils.Cross(forward, right);

            vpHeight = 2f * (float)SysMath.Tan(Config.fovRad / 2f);
            vpWidth = vpHeight * Config.aspectRatio;

            horizontal = right * vpWidth;
            vertical = up * vpHeight;

            center = position + forward; //origin is top left of viewplane
            origin = center - right * (vpWidth / 2f) - up * (vpHeight / 2f);
            //previously had Vector3 origin here, which was only creating local var. meaning tris
            //were created relative to camera origin and not in world space, I think that was the issue
        }

        public Raycast ShootRay(float x, float y) //now uses floats instead/ints for AA jitter
        {
            float screenX = x / (Config.imgWidth - 1);
            float screenY = y / (Config.imgHeight - 1);

            Vector3 pixelVector = origin + screenX * horizontal + screenY * vertical;
            Vector3 rayDirection = (pixelVector - position).Normalized();

            return new Raycast(position, rayDirection);
        }

        public void RotateTo(Vector3 target)
        {
            forward = (target - position).Normalized();
            
            Vector3 worldUp = new Vector3(0,1,0);
            float dot = Utils.Dot(worldUp, forward);

            //this prevents gimbal locking when cam aligned with world Z axis
            if (SysMath.Abs(dot) > 0.999f)
                //temp change reference axis
                worldUp = new Vector3(0, 0, 1);

            up = worldUp - forward * Utils.Dot(worldUp, forward);
            up = up.Normalized();

            right = Utils.Cross(up, forward).Normalized();
            up = Utils.Cross(forward, right);

            horizontal = right * vpWidth;
            vertical = up * vpHeight;
            
            center = position + forward; //origin is top left of viewplane
            origin = center - right * (vpWidth / 2f) - up * (vpHeight / 2f);
        }
        #endregion
    }
}
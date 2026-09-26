using ComputeSharp;
using SysMath = System.Math;

namespace Prismatix.Shaders
{
    //no classes in gpu-land!

    //but gpu-land is fussy, "custom" array struct
    public struct GPUArrayOf32Ints
    {
        public int e0, e1, e2, e3, e4, e5, e6, e7, e8, e9, e10, e31;
        public int e11, e12, e13, e14, e15, e16, e17, e18, e19, e20;
        public int e21, e22, e23, e24, e25, e26, e27, e28, e29, e30;

        //subscript operators that dx comp wanted
        public void Add(ref int index, int value)
        {
            if (index == 0) e0 = value;
            else if (index == 1) e1 = value;
            else if (index == 2) e2 = value;
            else if (index == 3) e3 = value;
            else if (index == 4) e4 = value;
            else if (index == 5) e5 = value;
            else if (index == 6) e6 = value;
            else if (index == 7) e7 = value;
            else if (index == 8) e8 = value;
            else if (index == 9) e9 = value;
            else if (index == 10) e10 = value;
            else if (index == 11) e11 = value;
            else if (index == 12) e12 = value;
            else if (index == 13) e13 = value;
            else if (index == 14) e14 = value;
            else if (index == 15) e15 = value;
            else if (index == 16) e16 = value;
            else if (index == 17) e17 = value;
            else if (index == 18) e18 = value;
            else if (index == 19) e19 = value;
            else if (index == 20) e20 = value;
            else if (index == 21) e21 = value;
            else if (index == 22) e22 = value;
            else if (index == 23) e23 = value;
            else if (index == 24) e24 = value;
            else if (index == 25) e25 = value;
            else if (index == 26) e26 = value;
            else if (index == 27) e27 = value;
            else if (index == 28) e28 = value; 
            else if (index == 29) e29 = value; 
            else if (index == 30) e30 = value;
            else e31 = value;
            index++;
        }
        public int Get(ref int index)
        {
            index--;
            if (index == 0) return e0; 
            if (index == 1) return e1;   
            if (index == 2) return e2;   
            if (index == 3) return e3;
            if (index == 4) return e4;   
            if (index == 5) return e5;   
            if (index == 6) return e6;   
            if (index == 7) return e7;
            if (index == 8) return e8;   
            if (index == 9) return e9;   
            if (index == 10) return e10; 
            if (index == 11) return e11;
            if (index == 12) return e12; 
            if (index == 13) return e13; 
            if (index == 14) return e14; 
            if (index == 15) return e15;
            if (index == 16) return e16; 
            if (index == 17) return e17; 
            if (index == 18) return e18; 
            if (index == 19) return e19;
            if (index == 20) return e20; 
            if (index == 21) return e21; 
            if (index == 22) return e22; 
            if (index == 23) return e23;
            if (index == 24) return e24; 
            if (index == 25) return e25; 
            if (index == 26) return e26; 
            if (index == 27) return e27;
            if (index == 28) return e28; 
            if (index == 29) return e29; 
            if (index == 30) return e30;
            return e31;
        }
    }

    public struct GPUTriangle
    {
        public float3 a, b, c;
        public float2 texA, texB, texC;
        public float3 normal;
        public float3 colour;
        public float roughness;
        public float metallic;
        public int albedoOffset,
            roughnessOffset,
            metallicOffset,
            textureWidth,
            textureHeight;
    }

    public struct GPUHitInfo
    {
        public float3 point;
        public float3 normal;
        public float distance;
        public float3 colour;
        public float2 tex;
        public float roughness;
        public float metallic;
        public int albedoOffset,
            roughnessOffset,
            metallicOffset,
            textureOffset,
            textureWidth,
            textureHeight;
    }

    public struct GPUNode
    {
        public float3 boundsMin;
        public float3 boundsMax;
        public int leftChild;  //-1 for leaves
        public int rightChild;
        public int triStart;   //where tris start in gpu tri array
        public int triCount;   //num tris following triStart
    }

    public struct GPULamp
    {
        public float3 position;
        public float brightness;
    }

    //powers of 2, and creating a 2D image, so no 3rd dim needed
    [ThreadGroupSize(8, 8, 1)]
    [GeneratedComputeShaderDescriptor]
    public readonly partial struct Shader : IComputeShader
    {
        //all these must be readonly, otherwise HLSL compiler would reject it
        //essentially promises gpu that these will be constant
        //partial allows the HLSL compiler to compile, as this struct can be split up
        public readonly ReadOnlyBuffer<GPUNode> bvhNodes;
        public readonly ReadOnlyBuffer<GPUTriangle> triangles;
        public readonly ReadOnlyBuffer<GPULamp> lamps;
        public readonly ReadWriteTexture2D<uint> outputImage;
        public readonly ReadOnlyTexture2D<float4> hdriTexture;
        public readonly ReadOnlyBuffer<float4> textureAtlas;
        public readonly bool useHdri;
        public readonly float hdriIntensity;
        public readonly float hdriRotation;

        //GPU will not be able to access any c# obj data, so must pass in here now
        public readonly int renderMode; //0 depth, 1 normal, 2 diffuse, 3 traced
        public readonly int maxSamples;
        public readonly int maxRayDepth;
        public readonly float3 bgColour;
        public readonly uint frameSeed;

        public readonly float3 camPos;
        public readonly float3 camOrigin;
        public readonly float3 camHorizontal;
        public readonly float3 camVertical;
        public readonly float width;
        public readonly float height;

        public Shader (ReadOnlyBuffer<GPUNode> bvhNodes, ReadOnlyBuffer<GPUTriangle> triangles, ReadOnlyBuffer<GPULamp> lamps, ReadWriteTexture2D<uint> outputImage,
            int renderMode, int maxSamples, int maxRayDepth, float3 bgColour,
            float3 camPos, float3 camOrigin, float3 camHorizontal, float3 camVertical, float width, float height, ReadOnlyTexture2D<float4> hdriTexture, bool useHdri, uint frameSeed, float hdriIntensity, ReadOnlyBuffer<float4> textureAtlas
            ,float hdriRotation) 
        {
            this.bvhNodes = bvhNodes; this.triangles = triangles; this.lamps = lamps; this.outputImage = outputImage;
            this.renderMode = renderMode; this.maxSamples = maxSamples; this.maxRayDepth = maxRayDepth; this.bgColour = bgColour;
            this.camPos = camPos; this.camOrigin = camOrigin; this.camHorizontal = camHorizontal; this.camVertical = camVertical;
            this.width = width; this.height = height;
            this.hdriTexture = hdriTexture;
            this.textureAtlas = textureAtlas;
            this.useHdri = useHdri;
            this.frameSeed = frameSeed * 100;
            this.hdriIntensity = hdriIntensity;
            this.hdriRotation = hdriRotation;
        }

        //the gpu cant use the default c# random lib,
        private uint NextRandom(ref uint x)
        {
            //so instead we use Permuted Congruential Generator hash for each pixel
            x = x * 747796405u + 2891336453u;
            uint y = ((x >> ((int)((x >> 28) + 4u))) ^ x) * 277803737u;
            return (y >> 22) ^ y;
        }
        private float RandomFloat(ref uint x)
        {
            //just uses nextrandom but normalize to 0-1 float
            return (NextRandom(ref x) & 0xFFFFFF) / 16777216.0f;
        }
        private float3 RandomVector(ref uint x)
        {
            //same idea as cpu version
            while (true)
            {
                float3 vector = new float3(
                    RandomFloat(ref x) * 2f - 1f,
                    RandomFloat(ref x) * 2f - 1f,
                    RandomFloat(ref x) * 2f - 1f);

                //cant use own dot function now, must use HLSL ver
                if (Hlsl.Dot(vector, vector) >= 1.0f) continue;
                return Hlsl.Normalize(vector);
            }
        }

        //this func is essentially parallel.for previously
        public void Execute() //PER PIXEL
        {
            int x = ThreadIds.X;
            int y = ThreadIds.Y;
            uint seed = (uint)((y * width) + x) + frameSeed;

            float3 totalColour = new float3(0,0,0);
            int samples = (renderMode == 2) ? maxSamples : 1;

            for (int sample = 0; sample < samples; sample++) //PER SAMPLE
            {
                //ensure that random doesnt ouput same for every sample
                NextRandom(ref seed);

                //AA jitter ONLY if using traced mode
                float jitterFactorX = (renderMode == 2) ? RandomFloat(ref seed) - 0.5f : 0f;
                float jitterFactorY = (renderMode == 2) ? RandomFloat(ref seed) - 0.5f : 0f;
                //absolute jitter ray origin coord
                float jitterX = (x + jitterFactorX) / (width - 1f);
                float jitterY = (y + jitterFactorY) / (height - 1f);

                float3 pixelVector = camOrigin + (jitterX * camHorizontal) + (jitterY * camVertical);
                float3 rayDir = Hlsl.Normalize(pixelVector - camPos);
                float3 rayOrigin = camPos;
                float3 invDir = 1.0f / rayDir;  
                //precomp invdir for faster bv bounds check

                bool hasHit;
                GPUHitInfo hit;
                bool shadowHit;
                GPUHitInfo shadowInfo;

                if (renderMode == 1)
                {
                    TraverseBVH(rayOrigin, rayDir, invDir, out hasHit, out  hit);
                    if (hasHit)
                    {
                        float3 normal = (hit.normal + 1.0f) * 0.5f;
                        totalColour += normal;
                    }
                    else
                        totalColour += bgColour;

                } //normal
                else if (renderMode == 2)
                {
                    //currentLight is final light hitting camera
                    float3 currentLight = new float3(0, 0, 0);
                    //lightColour is the colour of the ray
                    float3 lightColour = new float3(1, 1, 1);

                    for (int bounce = 0; bounce < maxRayDepth; bounce++)
                    {
                        TraverseBVH(rayOrigin, rayDir, invDir, out hasHit, out hit);

                        if (!hasHit)
                        {
                            if (useHdri)
                            {
                                float u = 0.5f 
                                    + (Hlsl.Atan2(rayDir.Z, rayDir.X) 
                                    / (2.0f * (float)SysMath.PI));

                                u = Hlsl.Frac(u + hdriRotation);
                                float v = 0.5f 
                                    - (Hlsl.Asin(rayDir.Y) 
                                    / (float)SysMath.PI);

                                int texX = (int)(u * hdriTexture.Width);
                                int texY = (int)(v * hdriTexture.Height);

                                float3 skyColor = hdriTexture[new int2(texX, texY)].XYZ;
                                skyColor *= hdriIntensity;
                                skyColor = Hlsl.Clamp(skyColor, 0.0f, 10.0f);
                                currentLight += lightColour * skyColor;
                            }
                            else{

                                currentLight += lightColour * bgColour * bgColour;
                            }
                            break;
                        }

                        float3 albedo = hit.colour;
                        float roughness = hit.roughness;
                        float metallic = hit.metallic;

                        if (hit.textureWidth > 0 && hit.textureHeight > 0)
                        {
                            //subtract floor to remove any integer value leave only decimal, wrap around
                            float u = hit.tex.X - Hlsl.Floor(hit.tex.X);
                            float v = hit.tex.Y - Hlsl.Floor(hit.tex.Y);
                            v = 1.0f - v;
                            int px = (int)(u * (hit.textureWidth - 1));
                            int py = (int)(v * (hit.textureHeight - 1));

                            int index = (py * hit.textureWidth + px);

                            if (hit.albedoOffset != -1)
                                albedo = textureAtlas[hit.albedoOffset + index].XYZ;

                            //only need red channel since these are greyscale, any would do
                            if (hit.roughnessOffset != -1)
                                roughness = textureAtlas[hit.roughnessOffset + index].X;
                            if (hit.metallicOffset != -1)
                                metallic = textureAtlas[hit.metallicOffset + index].X;
                        }

                        //tally up the direct lighting from all lamps
                        float3 directLight = new float3(0, 0, 0);
                        for (int i = 0; i < lamps.Length; i++)
                        {
                            float3 dirToLamp = lamps[i].position - hit.point;
                            float distToLamp = Hlsl.Length(dirToLamp);
                            dirToLamp = dirToLamp / distToLamp; //normalize

                            float3 shadowOrigin = hit.point + hit.normal * 0.001f;
                            float3 shadowInvDir = 1.0f / dirToLamp;

                            TraverseBVH(shadowOrigin, dirToLamp, shadowInvDir, out shadowHit, out shadowInfo);

                            //if hit nothing or object hit is further than lamp, then directly lit
                            if (!shadowHit || shadowInfo.distance > distToLamp)
                            {
                                float diffToLight = Hlsl.Max(0.0f, Hlsl.Dot(hit.normal, dirToLamp));
                                float intensity = lamps[i].brightness / (distToLamp * distToLamp);

                                directLight += albedo * intensity * diffToLight;
                            }
                        }
                        currentLight += lightColour * directLight;

                        float3 diffuse = Hlsl.Normalize(hit.normal + RandomVector(ref seed));

                        //blend between a completely random vector and total reflection baseed on roughness
                        float3 specularBounce = Hlsl.Lerp( //linear interp
                            Hlsl.Reflect(rayDir, hit.normal),
                            diffuse, 
                            roughness);

                        //determine whether ray is specular - metallic materials have higher chance of specular bounce
                        //splitting rays into diffuse and specular every bounce would be expensive,
                        //so we rely on monte-carlo mixing these together.
                        bool isSpecular = RandomFloat(ref seed) < metallic;

                        //set values for next ray
                        rayOrigin = hit.point + hit.normal * 0.001f;
                        rayDir = isSpecular ? specularBounce : diffuse;
                        invDir = 1.0f / rayDir;

                        lightColour *= albedo;
                    }

                    totalColour += currentLight;
                } //traced
                else if (renderMode == 3)
                {
                    //diffuse is same as traced but only one bounce & sample
                    TraverseBVH(rayOrigin, rayDir, invDir, out hasHit, out hit);

                    if (hasHit)
                    {
                        float3 directLight = new float3(0, 0, 0);

                        for (int i = 0; i < lamps.Length; i++)
                        {
                            float3 dirToLamp = lamps[i].position - hit.point;
                            float distToLamp = Hlsl.Length(dirToLamp);
                            dirToLamp = dirToLamp / distToLamp;

                            float3 shadowOrigin = hit.point + hit.normal * 0.001f;
                            float3 shadowInvDir = 1.0f / dirToLamp;

                            TraverseBVH(shadowOrigin, dirToLamp, shadowInvDir, out shadowHit, out shadowInfo);

                            if (!shadowHit || shadowInfo.distance > distToLamp)
                            {
                                float diffToLight = Hlsl.Max(0.0f, Hlsl.Dot(hit.normal, dirToLamp));
                                float intensity = lamps[i].brightness / (distToLamp * distToLamp);
                                directLight += hit.colour * intensity * diffToLight;
                            }
                        }
                        totalColour += directLight;
                    }
                    else
                    {
                        totalColour += bgColour;
                    }
                } //diffuse
                else if (renderMode == 4)
                {
                    TraverseBVH(rayOrigin, rayDir, invDir, out hasHit, out hit);
                    if (hasHit)
                        totalColour = new float3(hit.distance, 0, 0);
                    else
                        totalColour = new float3(-1.0f, 0, 0);
                } //depth
            }

            //color transformation (reinhard, sRGB)
            float3 finalColour = totalColour / (float)samples;
            if (renderMode == 2 || renderMode == 3)
            {
                //reinhard
                finalColour = finalColour / (1.0f + finalColour);
                //colour transform from raw to sRGB 0-255
                finalColour = Hlsl.Sqrt(finalColour);
            }
            if (renderMode == 4)
            {
                //force return raw depth data for depth mode
                outputImage[ThreadIds.XY] = (uint)(totalColour.X);
                return;
            }

            uint r = (uint)Hlsl.Clamp(finalColour.X * 255f, 0f, 255f);
            uint g = (uint)Hlsl.Clamp(finalColour.Y * 255f, 0f, 255f);
            uint b = (uint)Hlsl.Clamp(finalColour.Z * 255f, 0f, 255f);
            ///the gpu processor doesnt understand bytes - they only use 32 bits
            ///therefore, instead of using a byte for each we pack all 4 (including static alpha for now)
            ///into a single 32 bit uinteger
            uint packedColour = (uint)(r | (g << 8) | (b << 16) | (255u << 24));

            //can still write to readonly reference type as the location in mem doesnt change
            outputImage[ThreadIds.XY] = packedColour;
        }

        private void TraverseBVH(float3 rayOrigin, float3 rayDir, float3 invDir, out bool hasHit, out GPUHitInfo closestHit)
        {
            //iterative method
            //culls the futher bounding boxes, essentially early exit

            hasHit = false;
            closestHit = default;
            closestHit.distance = float.MaxValue;

            ///create fixed array of indices, this is enough for many many nodes
            ///i have been fighting with the DX compilor about this array syntax here
            ///Computesharp compiles my index into an HLSL method caller, but the DX compiler
            ///doesnt like me using brackets on what is a "method"

            GPUArrayOf32Ints array = default;
            int index = 0;
            array.Add(ref index, 0);

            while (index > 0)
            {
                //get node off front of array (and decrement index)
                int nodeIndex = array.Get(ref index);
                GPUNode node = bvhNodes[nodeIndex];

                //if is leaf
                if (node.leftChild == -1 && node.rightChild == -1)
                {
                    for (int i = node.triStart; i < node.triStart+node.triCount; i++)
                    {
                        GetRayIntersect(rayOrigin, rayDir, triangles[i], out bool hit, out GPUHitInfo hitInfo);
                        if (hit && hitInfo.distance < closestHit.distance)
                        {
                            hasHit = true;
                            closestHit = hitInfo;
                        }
                    }
                }
                else
                {
                    bool hitLeft = false, hitRight = false;
                    float distL = float.MaxValue, distR = float.MaxValue;

                    //parent -> test each child
                    if (node.leftChild != -1) 
                        GetRayHitsBounds(rayOrigin, invDir, bvhNodes[node.leftChild].boundsMin, bvhNodes[node.leftChild].boundsMax, out hitLeft, out distL);
                    if (node.rightChild != -1) 
                        GetRayHitsBounds(rayOrigin, invDir, bvhNodes[node.rightChild].boundsMin, bvhNodes[node.rightChild].boundsMax, out hitRight, out distR);

                    //early exit: test if box is further than already hit tri
                    if (hasHit)
                    {
                        if (hitLeft && distL >= closestHit.distance) hitLeft = false;
                        if (hitRight && distR >= closestHit.distance) hitRight = false;
                    }

                    //put furthest first, so closest is retrieved from front of array
                    if (hitLeft && hitRight)
                    {
                        if (distL < distR)
                        {
                            array.Add(ref index, node.rightChild);
                            array.Add(ref index, node.leftChild);
                        }
                        else
                        {
                            array.Add(ref index, node.leftChild);
                            array.Add(ref index, node.rightChild);
                        }
                    }
                    else if (hitLeft) array.Add(ref index, node.leftChild);
                    else if (hitRight) array.Add(ref index, node.rightChild);
                }
            }
        }

        private void GetRayHitsBounds(float3 rayOrigin, float3 invDir, float3 min, float3 max, out bool hit, out float hitDistance)
        {
            //two different times where the ray crosses the infinite box planes
            float3 tEnter = (min - rayOrigin) * invDir;
            float3 tExit = (max - rayOrigin) * invDir;

            //corners of box
            float3 tMin = Hlsl.Min(tEnter, tExit);
            float3 tMax = Hlsl.Max(tEnter, tExit);

            //tnear is the entry point, tfar is exit point
            float tNear = Hlsl.Max(Hlsl.Max(tMin.X, tMin.Y), tMin.Z);
            float tFar = Hlsl.Min(Hlsl.Min(tMax.X, tMax.Y), tMax.Z);

            //if exit point is further than entry point, ray passed through box
            hit = tFar >= tNear && tFar >= 0;
            hitDistance = tNear < 0 ? 0 : tNear;
        }

        private void GetRayIntersect(float3 rayOrigin, float3 rayDir, GPUTriangle tri, out bool hit, out GPUHitInfo hitInfo)
        {
            hit = false;
            hitInfo = default;

            //using Moller-Trumbore algorithm
            float3 edgeAB = tri.b - tri.a; //vectors of edges of A
            float3 edgeAC = tri.c - tri.a;
            float3 rayCrossAC = Hlsl.Cross(rayDir, edgeAC);
            float determinent = Hlsl.Dot(edgeAB, rayCrossAC);
            //the dot product is a scalar value that represnts how aligned
            //two vectors are. >0: same direction, <0: opposite direction, 0: perpendicular

            if (Hlsl.Abs(determinent) < 0.00001f) return;

            //ray is parallel to triangle if invdet near 0
            float invDeterminent = 1.0f / determinent;
            //vector from A to ray start
            float3 vertAtoRay = rayOrigin - tri.a;

            //barycentric coordinates are a way of expressing a point in a triangle
            // as a vector of the triangles vertices
            float baryC = Hlsl.Dot(vertAtoRay, rayCrossAC) * invDeterminent;
            if (baryC < 0 || baryC > 1) return;

            float3 rayCrossVertAtoRay = Hlsl.Cross(vertAtoRay, edgeAB);
            float baryB = Hlsl.Dot(rayDir, rayCrossVertAtoRay) * invDeterminent;

            if (baryB < 0 || baryB + baryC > 1) return;

            float distance = Hlsl.Dot(edgeAC, rayCrossVertAtoRay) * invDeterminent;
            if (distance < 0) return; //ray goes away from triangle

            hit = true;
            hitInfo.distance = distance;
            hitInfo.point = rayOrigin + rayDir * distance;
            hitInfo.tex = tri.texA * (1 - baryB - baryC) + tri.texB * baryC + tri.texC * baryB;
            hitInfo.albedoOffset = tri.albedoOffset;
            hitInfo.roughnessOffset = tri.roughnessOffset;
            hitInfo.metallicOffset = tri.metallicOffset;
            hitInfo.textureWidth = tri.textureWidth;
            hitInfo.textureHeight = tri.textureHeight;
            hitInfo.roughness = tri.roughness;
            hitInfo.metallic = tri.metallic;
            hitInfo.normal = Hlsl.Dot(rayDir, tri.normal) > 0 ? -tri.normal : tri.normal;
            hitInfo.colour = tri.colour;
        }
    }
}
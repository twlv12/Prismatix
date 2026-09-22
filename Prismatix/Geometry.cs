using Prismatix.Math;
using Prismatix.Shaders;
using StbImageSharp;
using System;
using System.Collections.Generic;
using System.IO;
using static System.Net.Mime.MediaTypeNames;

namespace Prismatix.Geometry
{
    public class Scene
    {
        public List<Object> objects = new List<Object>();
        public List<Lamp> lamps = new List<Lamp>();
        public Camera mainCamera;
        public BoundingVolume rootBVH;
        public bool isOutdated = true;

        public bool useHdri = false;
        public bool hdriOutdated = true;
        public int hdriWidth = 1;
        public int hdriHeight = 1;
        public float4[] hdriArray = { new float4(0, 0, 0, 1) };
        public float hdriIntensity = 1.0f;

        public ComputeSharp.Float4[] gpuTextureAtlas = 
            new ComputeSharp.Float4[] 
        { new ComputeSharp.Float4(0, 0, 0, 0) };

        public Scene(){
            BuildBVH();
        }

        public void AddObject(Object obj){
            obj.BakeAllTris();
            objects.Add(obj); 
        }
        public void AddLamp(Lamp lamp){
            lamps.Add(lamp); }
        public void SetHDRI(string path)
        {
            try
            {
                using (Stream stream = File.OpenRead(path))
                {
                    //force loading as 32 bits per channel
                    ImageResultFloat image = ImageResultFloat.FromStream(stream, ColorComponents.RedGreenBlueAlpha);

                    hdriWidth = image.Width;
                    hdriHeight = image.Height;
                    hdriArray = new float4[hdriWidth * hdriHeight];

                    //float array with each val being R G B A sequential
                    for (int i = 0; i < hdriWidth * hdriHeight; i++)
                    {
                        float r = image.Data[i * 4 + 0];
                        float g = image.Data[i * 4 + 1];
                        float b = image.Data[i * 4 + 2];
                        float a = image.Data[i * 4 + 3];

                        hdriArray[i] = new float4(r, g, b, a);
                    }
                }
                hdriOutdated = true;
                Console.WriteLine($"loaded di hdri: {Path.GetFileName(path)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"failed to load hdri: {ex.Message}");
            }
        }

        public void BuildBVH()
        {
            List<ComputeSharp.Float4> textureAtlasArr = new List<ComputeSharp.Float4>();
            textureAtlasArr.Add(new ComputeSharp.Float4(1, 1, 1, 1));

            List<Triangle> listOfAllTris = new List<Triangle>();
            foreach (Object obj in objects)
            {
                if (!obj.isVisible) continue; //can simply exclude hidden objects from bvh to disable rendering them.

                foreach (Triangle tri in obj.bakedTriangles){
                    listOfAllTris.Add(tri);
                }

                if (obj.material.albedoData != null)
                {
                    obj.material.albedoOffset = textureAtlasArr.Count;
                    textureAtlasArr.AddRange(obj.material.albedoData);
                }
                if (obj.material.roughnessData != null)
                {
                    obj.material.roughnessOffset = textureAtlasArr.Count;
                    textureAtlasArr.AddRange(obj.material.roughnessData);
                }
                if (obj.material.metallicData != null)
                {
                    obj.material.metallicOffset = textureAtlasArr.Count;
                    textureAtlasArr.AddRange(obj.material.metallicData);
                }
            }
            gpuTextureAtlas = textureAtlasArr.ToArray();

            if (listOfAllTris.Count > 0)
                rootBVH = new BoundingVolume(listOfAllTris, 0);
            else
                rootBVH = null;
        }

        public (GPUNode[] nodeArr, GPUTriangle[] trisArr) GPUifyBVH()
        {
            //first use lists as dont know final amount
            List<GPUNode> nodesList = new List<GPUNode>();
            List<GPUTriangle> trisList = new List<GPUTriangle>();

            GPUifyNode(rootBVH, nodesList, trisList);
            //pass reference, populated HERE

            //now this is the flattened arrays - array for GPU
            return (nodesList.ToArray(), trisList.ToArray());
        }

        public int GPUifyNode(BoundingVolume node, List<GPUNode> nodesList, List<GPUTriangle> trisList)
        {
            //if doesnt exist, return that previous was leaf
            if (node == null) return -1;

            int currentIndex = nodesList.Count;
            nodesList.Add(new GPUNode()); //placeholder, will be overwritten
            //must place children after parent to make the GPU happy
            //but dont know parent data until children are placed
            //so place children first, then parent

            GPUNode gpuNode = new GPUNode
            {
                boundsMin = node.boundsMin,
                boundsMax = node.boundsMax,
                leftChild = -1, 
                rightChild = -1, //default leaf
                triStart = -1,
                triCount = 0
            };

            if (node.isLeaf)
            {
                //all tris are just placed sequentially per leaf in separate array
                gpuNode.triStart = trisList.Count;
                gpuNode.triCount = node.triangles.Count;

                //add tris
                foreach (var tri in node.triangles)
                {
                    trisList.Add(new GPUTriangle
                    {
                        a = tri.a,
                        b = tri.b,
                        c = tri.c,
                        normal = tri.normal,
                        colour = tri.hostObj.material.colour,
                        roughness = tri.hostObj.material.roughness,
                        metallic = tri.hostObj.material.metallic,
                        texA = tri.texA,
                        texB = tri.texB,
                        texC = tri.texC,
                        albedoOffset = tri.hostObj.material.albedoOffset,
                        roughnessOffset = tri.hostObj.material.roughnessOffset,
                        metallicOffset = tri.hostObj.material.metallicOffset,
                        textureWidth = tri.hostObj.material.textureWidth,
                        textureHeight = tri.hostObj.material.textureHeight
                    });
                }
            }
            else //if parent
            {
                //recurse and return indice of child
                gpuNode.leftChild = GPUifyNode(node.left, nodesList, trisList);
                gpuNode.rightChild = GPUifyNode(node.right, nodesList, trisList);
            }

            //then overwrite the reserved index with complete parent datas
            nodesList[currentIndex] = gpuNode;
            //return index, so parent of this node (if is one) knows start pos
            return currentIndex;
        }
    }

    public class Lamp
    {
        #region Lamp Data
        public string name;
        public Vector3 position;
        public float brightness = 10.0f;
        public bool isVisible = true;
        
        public Lamp(Vector3 pos, float lumen){
            position = pos; 
            brightness = lumen;
            name = "Point Lamp";
        }
        #endregion
    }

    public class Mesh
    {
        #region Mesh Data
        public List<Vector3> vertices = new List<Vector3>(); //hold all sequential vertex positions
        public List<int> indices = new List<int>(); //list of index numbers referring to vertices
        public List<ComputeSharp.Float2> uvs = new List<ComputeSharp.Float2>();
        public List<int> uvIndices = new List<int>();

        //each 3 ints represents a tri

        //public Mesh(List<Vector3> verts, List<int> tris)
        //{
        //    Vertices = verts;
        //    indices = tris;
        //}

        //sad forgotten function :(..... NOT ANYMORE!!!

        #endregion

        public (Vector3, Vector3, Vector3) GetTri(int index, Vector3 offset)
        {
            int i = index * 3;
            return (
                offset + vertices[indices[i]],
                offset + vertices[indices[i+1]],
                offset + vertices[indices[i+2]]
            );
        }

        public (ComputeSharp.Float2, ComputeSharp.Float2, ComputeSharp.Float2) GetUVs(int index)
        {
            int i = index * 3;
            if (uvIndices.Count > i + 2 && uvIndices[i] != -1)
                return (uvs[uvIndices[i]], uvs[uvIndices[i + 1]], uvs[uvIndices[i + 2]]);
            return (new ComputeSharp.Float2(0, 0), new ComputeSharp.Float2(0, 0), new ComputeSharp.Float2(0, 0));
        }
    }

    public class Object
    {
        #region Constructor
        public Mesh mesh;
        public string name;
        public Vector3 position;
        public float scale;
        public Vector3 rotation = new Vector3(0, 0, 0);
        public Material material;
        public List<Triangle> bakedTriangles = new List<Triangle>();
        public Boolean needsPrecomp = true;
        public bool isVisible = true;


        public Object(string nam, Vector3 pos, float scl)
        {
            name = nam; position = pos; scale = scl;
        }
        #endregion

        public void BakeAllTris()
        {
            List<Triangle> newBakedTriangles = new List<Triangle>();

            //precompute convert degrees to radians for use in euler rotation
            float radX = rotation.x * (MathF.PI / 180f);
            float radY = rotation.y * (MathF.PI / 180f);
            float radZ = rotation.z * (MathF.PI / 180f);

            float cosx = MathF.Cos(radX), sinx = MathF.Sin(radX);
            float cosy = MathF.Cos(radY), siny = MathF.Sin(radY);
            float cosz = MathF.Cos(radZ), sinz = MathF.Sin(radZ);

            //rotate a single vert about 
            Vector3 RotateVert(Vector3 vert)
            {
                //y1 and z1 set after x rot,
                //x2 and z2 set after y rot,
                //x3 and y3 set after z rot,

                float y1 = vert.y * cosx - vert.z * sinx;
                float z1 = vert.y * sinx + vert.z * cosx;
                float x2 = vert.x * cosy + z1 * siny;
                float z2 = -vert.x * siny + z1 * cosy;
                float x3 = x2 * cosz - y1 * sinz;
                float y3 = x2 * sinz + y1 * cosz;
                return new Vector3(x3, y3, z2);
            }

            for (int i = 0; i < mesh.indices.Count / 3; i++)
            {
                //Console.WriteLine($"Baking Tri {i}");
                var (aRaw, bRaw, cRaw) = mesh.GetTri(i, new Vector3(0, 0, 0)); //raw is in obj space
                Vector3 a = RotateVert(aRaw * scale) + position;
                Vector3 b = RotateVert(bRaw * scale) + position;
                Vector3 c = RotateVert(cRaw * scale) + position;

                Vector3 normal = Utils.Cross(b - a, c - a).Normalized();
                Vector3 edgeAB = b - a;
                Vector3 edgeAC = c - a;
                Vector3 center = (a + b + c) / 3;
                //Console.WriteLine($"Baked Tri {a},{b},{c}");

                var (texA, texB, texC) = mesh.GetUVs(i);

                newBakedTriangles.Add(new Triangle
                {
                    a = a,
                    b = b,
                    c = c,
                    normal = normal,
                    edgeAB = edgeAB,
                    edgeAC = edgeAC,
                    center = center,
                    hostObj = this,
                    texA = texA,
                    texB = texB,
                    texC = texC
                });
            }

            bakedTriangles = newBakedTriangles;
        }
    }

    public class Material
    {
        public string name;
        public Vector3 colour;
        public float specular;
        public float roughness;
        public float metallic;

        public bool hasTexture = false;
        public int albedoOffset = -1, roughnessOffset = -1, metallicOffset = -1;
        public int textureWidth = 0;
        public int textureHeight = 0; //coords array has width and height for RGBA channel img

        public ComputeSharp.Float4[] albedoData;
        public ComputeSharp.Float4[] roughnessData;
        public ComputeSharp.Float4[] metallicData;

        public Material(string nam, Vector3 col, float spec, float ruf, float met = 0.0f)
        {
            name = nam; colour = col; specular = spec; roughness = ruf; metallic = met;
        }

        private ComputeSharp.Float4[] LoadMap(string path)
        {
            if (!File.Exists(path)) return null;
            using (Stream stream = File.OpenRead(path))
            {
                var image = StbImageSharp.ImageResultFloat.FromStream(stream, StbImageSharp.ColorComponents.RedGreenBlueAlpha);
                textureWidth = image.Width;  // Assumes all maps in the PBR set are identical size
                textureHeight = image.Height;
                var data = new ComputeSharp.Float4[textureWidth * textureHeight];

                for (int i = 0; i < textureWidth * textureHeight; i++)
                    data[i] = new ComputeSharp.Float4(
                        image.Data[i *4], 
                        image.Data[i *4 +1], 
                        image.Data[i *4 +2], 
                        image.Data[i *4 +3]);
                return data;
            }
        }

        public void LoadPBRTexture(string path, string prefix)
        {
            string FindFile(string suffix)
            {
                var files = Directory.GetFiles(path, $"{prefix}_{suffix}.*");
                return files.Length > 0 ? files[0] : "";
            }

            albedoData = LoadMap(FindFile("Albedo"));
            roughnessData = LoadMap(FindFile("Roughness"));
            metallicData = LoadMap(FindFile("Metallic"));
        }
    }
    

    public static class MeshLib
    {
        //store path and mesh from it, directly avoid loading already loaded meshes
        private static readonly Dictionary<string, Mesh> meshCache = new Dictionary<string, Mesh>();

        //only accepts .obj files for now
        private static (Mesh, string) LoadMeshFromDisk(string filePath, float scale)
        {
            Mesh mesh = new Mesh();
            string name = "";
            string[] data = File.ReadAllLines(filePath);

            foreach (string line in data)
            {
                string cleanLine = line.Split('#')[0].Trim(); //clean up comments and whitespace
                if (string.IsNullOrWhiteSpace(cleanLine))
                    continue;

                #region Check Line Types
                if (cleanLine.StartsWith("o") || cleanLine.StartsWith("g"))
                {
                    name = $"{cleanLine.Substring(2)}";
                }
                else if (cleanLine.StartsWith("v ") && !cleanLine.StartsWith("vn"))
                {
                    string[] splitLine = cleanLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                    Vector3 vector3 = new Vector3(0, 0, 0);
                    vector3.x = float.Parse(splitLine[1]); 
                    vector3.y = float.Parse(splitLine[2]); //index 0 is just "v"
                    vector3.z = float.Parse(splitLine[3]);

                    mesh.vertices.Add(vector3);
                }
                else if (cleanLine.StartsWith("f")) //each face is three ints referring to
                {                              //indexes of vertices in the vertex list
                    string[] splitLine = cleanLine.Split(' ');
                    for (int i = 1; i < splitLine.Length; i++)
                    {
                        string[] parts = splitLine[i].Split(new[] { ' ', '/' }, StringSplitOptions.RemoveEmptyEntries);
                        int vertIndex = int.Parse(parts[0]) - 1; //obj indexing starts at 1
                        mesh.indices.Add(int.Parse(parts[0]) - 1);

                        if (parts.Length > 1 && !string.IsNullOrEmpty(parts[1])) //if the parts has the uv index (f 1/3/4)
                            mesh.uvIndices.Add(int.Parse(parts[1]) - 1);
                        else
                            mesh.uvIndices.Add(-1);
                    }
                }
                else if (cleanLine.StartsWith("vt "))
                {
                    string[] splitLine = cleanLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    mesh.uvs.Add(new ComputeSharp.Float2(float.Parse(splitLine[1]), float.Parse(splitLine[2])));
                }
                #endregion

                Console.WriteLine($"Loaded {mesh.vertices.Count} vertices & {mesh.indices.Count / 3} triangles");
            }

            return (mesh, name);
        }

        public static Object NewObj(string filePath, Vector3 pos, string name = "Unnamed", float scale = 1f)
        {
            string fullPath = Path.GetFullPath(filePath);
            string meshName = "";
            Mesh mesh;

            if (meshCache.TryGetValue(fullPath, out Mesh cachedMesh))
            {
                Console.WriteLine($"Reusing mesh: {Path.GetFileName(filePath)}");
                mesh = cachedMesh;
            }
            else 
            {
                Console.WriteLine($"Loading new mesh: {Path.GetFileName(filePath)}");
                var result = LoadMeshFromDisk(fullPath, scale);
                mesh = result.Item1;
                meshName = result.Item2;
                meshCache[fullPath] = mesh;
            }

            Object newObj = new Object($"{name}{meshName}", pos, scale);
            newObj.mesh = mesh;

            return newObj;
        }
    }
}
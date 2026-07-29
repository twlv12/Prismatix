using Prismatix.Math;
using Prismatix.Shaders;
using System;
using System.Collections.Generic;
using System.IO;

namespace Prismatix.Geometry
{
    public class Scene
    {
        public List<Object> objects = new List<Object>();
        public List<Lamp> lamps = new List<Lamp>();
        public Camera mainCamera;
        public BoundingVolume rootBVH;

        public Scene(){
            BuildBVH();
        }

        public void AddObject(Object obj){
            obj.BakeAllTris();
            objects.Add(obj); 
        }
        public void AddLamp(Lamp lamp){
            lamps.Add(lamp); }

        public void BuildBVH()
        {
            List<Triangle> listOfAllTris = new List<Triangle>();
            foreach (Object obj in objects){
                foreach (Triangle tri in obj.bakedTriangles){
                    listOfAllTris.Add(tri);
                }
            }

            rootBVH = new BoundingVolume(listOfAllTris, 0);
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
                        colour = tri.hostObj.material.colour
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
    }

    public class Object
    {
        #region Constructor
        public Mesh mesh;
        public string name;
        public Vector3 position;
        public float scale;
        public Material material;
        public List<Triangle> bakedTriangles = new List<Triangle>();
        public Boolean needsPrecomp = true;

        public Object(string nam, Vector3 pos, float scl)
        {
            name = nam; position = pos; scale = scl;
        }
        #endregion


        public void BakeAllTris()
        {
            bakedTriangles.Clear();
            for (int i = 0; i < mesh.indices.Count / 3; i++)
            {
                //Console.WriteLine($"Baking Tri {i}");
                var (aRaw, bRaw, cRaw) = mesh.GetTri(i, new Vector3(0, 0, 0));
                Vector3 a = (aRaw * scale) + position;
                Vector3 b = (bRaw * scale) + position;
                Vector3 c = (cRaw * scale) + position;

                Vector3 normal = Utils.Cross(b - a, c - a).Normalized();
                Vector3 edgeAB = b - a;
                Vector3 edgeAC = c - a;
                Vector3 center = (a + b + c) / 3;
                //Console.WriteLine($"Baked Tri {a},{b},{c}");

                bakedTriangles.Add(new Triangle
                {
                    a = a,
                    b = b,
                    c = c,
                    normal = normal,
                    edgeAB = edgeAB,
                    edgeAC = edgeAC,
                    center = center,
                    hostObj = this,
                });
            }
        }
    }

    public class Material
    {
        public string name;
        public Vector3 colour;
        public float specular;
        public float roughness;

        public Material(string nam, Vector3 col, float spec, float ruf){
            name = nam;
            colour = col;
            specular = spec;
            roughness = ruf;
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
                        mesh.indices.Add(vertIndex);
                    }
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
                Console.WriteLine($"Reusing cached mesh for: {Path.GetFileName(filePath)}");
                mesh = cachedMesh;
            }
            else 
            {
                Console.WriteLine($"Loading new mesh from disk: {Path.GetFileName(filePath)}");
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
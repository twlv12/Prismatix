using Prismatix;
using Prismatix.Math;
using System;
using System.IO;
using System.Collections.Generic;
using SysMath = System.Math;
using System.Diagnostics;

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
        public Vector3 boundsMin = new Vector3();
        public Vector3 boundsMax = new Vector3();

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
        public Vector3 boundsMin;
        public Vector3 boundsMax;

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
                var (a, b, c) = mesh.GetTri(i, position);
                //Console.WriteLine($"Baked Tri {a},{b},{c}");
                Vector3 normal = Utils.Cross(b-a, c-a).Normalized();
                Vector3 edgeAB = b - a;
                Vector3 edgeAC = c - a;
                Vector3 center = (a + b + c) / 3;

                bakedTriangles.Add(new Triangle { 
                    a=a, b=b, c=c, 
                    normal=normal, 
                    edgeAB=edgeAB, edgeAC=edgeAC, 
                    center=center,
                    hostObj=this,
                });
            }
        }
        //only accepts .obj files for now
        public void LoadFromDisk(string filePath)
        {
            mesh = new Mesh();

            string[] data = File.ReadAllLines(filePath);
            foreach (string line in data)
            {
                string cleanLine = line.Split('#')[0].Trim(); //clean up comments and whitespace
                if (string.IsNullOrWhiteSpace(cleanLine))
                    continue;

                #region Check Line Types
                if (cleanLine.StartsWith("o") || cleanLine.StartsWith("g")) {
                    name = $"Unnamed{cleanLine.Substring(2)}"; 
                }

                else if (cleanLine.StartsWith("v ") && !cleanLine.StartsWith("vn")) {
                    string[] splitLine = cleanLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                    Vector3 vector3 = new Vector3(0,0,0);
                    vector3.x = float.Parse(splitLine[1]); //index 0 is just "v"
                    vector3.y = float.Parse(splitLine[2]);
                    vector3.z = float.Parse(splitLine[3]);

                    vector3 *= scale;
                    mesh.vertices.Add(vector3);
                }

                else if (cleanLine.StartsWith("f")) //each face is three ints referring to
                {                              //indexes of vertices in the vertex list
                    string[] splitLine = cleanLine.Split(' ');
                    for (int i = 1; i < splitLine.Length; i++)
                    {
                        string[] parts = splitLine[i].Split(new[] { ' ', '/' }, StringSplitOptions.RemoveEmptyEntries);
                        int vertIndex = int.Parse(parts[0]) -1; //obj indexing starts at 1
                        mesh.indices.Add(vertIndex);
                    }
                }
                #endregion

                //Console.WriteLine($"Loaded {mesh.vertices.Count} vertices & {mesh.indices.Count / 3} triangles");
            }
            BakeAllTris();
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
}
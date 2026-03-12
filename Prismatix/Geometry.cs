using Prismatix;
using Prismatix.Math;
using System;
using System.IO;
using System.Collections.Generic;

namespace Prismatix.Geometry
{
    public class Scene
    {
        public List<Object> objects = new List<Object>();
        public Camera mainCamera;

        public void AddObject(Object obj){
            objects.Add(obj);
        }
    }

    public class Lamp
    {
        #region Lamp Data
        public Vector3 position;
        public float brightness = 10.0f;
        
        public Lamp(Vector3 pos, float lumen){
            position = pos; 
            brightness = lumen;
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
        public (Vector3, Vector3, Vector3) GetTri(int index, Vector3 offset)
        {
            int i = index * 3;
            return (
                offset + vertices[indices[i]],
                offset + vertices[indices[i + 1]],
                offset + vertices[indices[i + 2]]
            );
        }
        #endregion
    }

    public class Object
    {
        #region Constructor
        public Mesh mesh;
        public string name;
        public Vector3 position;
        public float scale;

        public Object(string nam, Vector3 pos, float scl)
        {
            name = nam; position = pos; scale = scl;
        }
        #endregion

        #region Mesh Loading
        //loads a mesh from .obj including name
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
                    name = cleanLine.Substring(2); 
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

                Console.WriteLine($"Loaded {mesh.vertices.Count} vertices & {mesh.indices.Count / 3} triangles");
            }
        }
        #endregion
    }
}
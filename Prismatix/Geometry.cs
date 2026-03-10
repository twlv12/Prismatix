using Prismatix.Math;
using System;
using System.IO;

namespace Prismatix.Geometry
{
    public class Mesh
    {
        public List<Vector3> vertices = new(); //hold all sequential vertex positions
        public List<int> indices = new(); //list of index numbers referring to vertices
        //each 3 ints represents a tri


        //public Mesh(List<Vector3> verts, List<int> tris)
        //{
        //    Vertices = verts;
        //    indices = tris;
        //}

        public (Vector3, Vector3, Vector3) GetTri(int index)
        {
            int i = index * 3;
            return (
                vertices[indices[i]],
                vertices[indices[i + 1]],
                vertices[indices[i + 2]]
            );
        }
    }

    public class Object
    {
        public Mesh mesh;
        public string name;
        public Vector3 position;
        public float scale;

        public void LoadFromDisk(string filePath)
        {
            mesh = new Mesh();

            string[] data = File.ReadAllLines(filePath);
            foreach (string line in data)
            {
                if (line.StartsWith("o")) { name = line.Substring(2); }

                else if (line.StartsWith("v")) {
                    string[] splitLine = line.Split(' ');
                    for (int i = 0; i < splitLine.Count; i++) 
                    { splitLine[i] = splitLine[i].Trim(); }

                    Vector3 vector3 = new Vector3();
                    vector3.x = float.Parse(splitLine[1]);
                    vector3.y = float.Parse(splitLine[2]);
                    vector3.z = float.Parse(splitLine[3]);

                    mesh.vertices.Add(vector3);
                }

                else if (line.StartsWith("f"))
                {
                    string[] splitLine = line.Split(' ');
                    for (int i = 1; i <= 3; i++)
                    { 
                        splitLine[i] = splitLine[i].Trim();
                        mesh.indices.Add(int.Parse(splitLine[i]) - 1);
                    }
                }
            }
        }
    }
}
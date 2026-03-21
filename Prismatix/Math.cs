using System;
using System.Collections;
using Prismatix;
using Prismatix.Geometry;
using SysMath = System.Math;

namespace Prismatix.Math
{
    public struct Vector3 //was previously class, changed to struct for SPEED!!!
    {
        public float x,y,z;

        #region Constructor And Operators
        public Vector3(float X, float Y, float Z){
            this.x = X;
            this.y = Y;
            this.z = Z;
        }

        public override string ToString(){
            return $"Vector3({x},{y},{z})";
        }
        public Vector3 Normalized(){
            float magnitude = (float)SysMath.Sqrt(x*x + y*y + z*z);
            return magnitude>0 ? this / magnitude : new Vector3(0,0,0);
        }
        public float Magnitude(){
            return (float)SysMath.Sqrt(x*x + y*y + z*z);
        }
        public static Vector3 operator +(Vector3 a, Vector3 b) {
            return new Vector3(a.x+b.x, a.y+b.y, a.z+b.z);
        }
        public static Vector3 operator -(Vector3 a, Vector3 b) {
            return new Vector3(a.x-b.x, a.y-b.y, a.z-b.z);
        }
        public static Vector3 operator *(Vector3 a, float multiplier) {
            return new Vector3(a.x*multiplier, a.y*multiplier, a.z*multiplier);
        }
        public static Vector3 operator *(float multiplier, Vector3 a) {
            return a*multiplier; //just use the previous one
        }
        public static Vector3 operator /(Vector3 a, float multiplier) {
            return new Vector3(a.x/multiplier, a.y/multiplier, a.z/multiplier);
        }
        #endregion
    }

    public struct HitInfo
    {
        public Vector3 point;
        public Vector3 normal;
        public float distance;
        public Boolean lit;
        public Material material;
    }

    public struct Triangle
    {
        public Vector3 a, b, c;
        public Vector3 normal;
    }

    public class Image
    { //moved all this stuff out of the rendering loop to clean it up
        #region Image Data & Utilities
        public int width, height;
        public byte[] data; //3 bytes per pixel

        public Image(int w, int h){
            width = w;
            height = h;
            data = new byte[width*height*3];
        }

        public int GetIndexOf(int x, int y){
            return (y*width +x) *3;
        }

        public void SetPixel(int x, int y, Vector3 colour){ //now working with vector3 colours
            int index = GetIndexOf(x,y); //to make stuff easier in the future
            data[index] = (byte)Utils.Clamp(colour.x, 0, 255);
            data[index +1] = (byte)Utils.Clamp(colour.y, 0, 255);
            data[index +2] = (byte)Utils.Clamp(colour.z, 0, 255);
        }

        public void Fill(Vector3 colour){
            for (int i = 0; i < data.Length; i+=3){
                data[i] = (byte)Utils.Clamp(colour.x, 0, 255);
                data[i +1] = (byte)Utils.Clamp(colour.y, 0, 255);
                data[i +2] = (byte)Utils.Clamp(colour.z, 0, 255);
            }
        }
        #endregion
    }

    public static class Utils
    {
        #region Boring Math Functions
        //gives the 2d vector perpendicular to the two input vectors 
        public static Vector3 Cross(Vector3 a, Vector3 b){
            return new Vector3(
            a.y*b.z - a.z*b.y,
            a.z*b.x - a.x*b.z,
            a.x*b.y - a.y*b.x
            );
        }
        public static float Dot(Vector3 a, Vector3 b){
            return a.x*b.x + a.y*b.y + a.z*b.z;
        }
        public static float Remap(float value, float inMin, float inMax, float outMin, float outMax){
            return outMin + (value - inMin) * (outMax - outMin) / (inMax - inMin);
        }
        public static float Clamp(float value, float min, float max){
            if (value < min) {  value = min; }
            if (value > max) { value = max; } 
            return value;
        }
        public static Vector3 Min(Vector3 a, Vector3 b)
        {
            Vector3 result = new Vector3();
            if (a.x < b.x) { result.x = a.x; }
            else { result.x = b.x; }
            if (a.y < b.y) { result.y = a.y; }
            else { result.y = b.y; }
            if (a.z < b.z) { result.z = a.z; }
            else { result.z = b.z; }
            return result;
        }
        public static Vector3 Max(Vector3 a, Vector3 b)
        {
            Vector3 result = new Vector3();
            if (a.x < b.x) { result.x = b.x; }
            else { result.x = a.x; }
            if (a.y < b.y) { result.y = b.y; }
            else { result.y = a.y; }
            if (a.z < b.z) { result.z = b.z; }
            else { result.z = a.z; }
            return result;
        }
        #endregion

        #region Cool Math Functions
        public static HitInfo? GetRayIntersect(Raycast ray, Triangle trig, Geometry.Object obj=null)
        {
            //using Moller-Trumbore algorithm
            Vector3 a = trig.a;
            Vector3 b = trig.b;
            Vector3 c = trig.c;
            Vector3 edgeAB = b - a; //vectors of edges of A
            Vector3 edgeAC = c - a;

            //the dot product is a scalar value that represnts how aligned
            //two vectors are. >0: same direction, <0: opposite direction, 0: perpendicular

            Vector3 rayCrossAC = Cross(ray.direction, edgeAC);

            float determinent = Dot(edgeAB, rayCrossAC);
            if ((float)SysMath.Abs(determinent) < 0.00001f) { return null; }
            //ray is parallel to triangle if near 0
            float invDeterminent = 1.0f / determinent;

            //vector from A to ray start
            Vector3 vertAtoRay = ray.origin - a;

            //barycentric coordinates are a way of expressing a point in a triangle
            // as a vector of the triangles vertices

            Vector3 rayCrossVertAtoRay = Cross(vertAtoRay, edgeAB);

            float barycentricC = Dot(vertAtoRay, rayCrossAC) * invDeterminent;
            if (barycentricC < 0 || barycentricC > 1) { return null; }

            float barycentricB = Dot(ray.direction, rayCrossVertAtoRay) * invDeterminent;
            if (barycentricB < 0 || barycentricB + barycentricC > 1) { return null; }
            //previously forgot to add barycentricC 

            float distance = Dot(edgeAC, rayCrossVertAtoRay) * invDeterminent;
            if (distance < 0) { return null; } //ray goes away from triangle

            HitInfo hitInfo = new HitInfo();
            hitInfo.point = ray.origin + ray.direction * distance;
            hitInfo.normal = trig.normal;
            hitInfo.distance = distance;
            if (obj != null){
                hitInfo.material = obj.material;}
            else{
                hitInfo.material = new Material("white", new Vector3(1, 1, 1), 1, 1);}

                return hitInfo;
        }

        public static bool GetRayHitsBounds(Raycast ray, Vector3 min, Vector3 max)
        {
            //t representing time along the ray
            float tEnterX = (min.x -ray.origin.x) /ray.direction.x;
            float tExitX = (max.x -ray.origin.x) /ray.direction.x;

            if (tEnterX> tExitX){ //flip so enter is larger
                float temp = tEnterX;
                tEnterX = tExitX;
                tExitX = temp;
            }

            float tEnterY = (min.y -ray.origin.y) /ray.direction.y;
            float tExitY = (max.y -ray.origin.y) /ray.direction.y;

            if (tEnterY> tExitY){
                float temp = tEnterY;
                tEnterY = tExitY;
                tExitY = temp;
            }

            if (tEnterX> tExitY || tEnterY> tExitX)
                return false;

            float tEnter = SysMath.Max(tEnterX, tEnterY);
            float tExit = SysMath.Min(tExitX, tExitY);

            float tEnterZ = (min.z - ray.origin.z) /ray.direction.z;
            float tExitZ = (max.z - ray.origin.z) /ray.direction.z;

            if (tEnterZ > tExitZ)
            {
                float temp = tEnterZ;
                tEnterZ = tExitZ;
                tExitZ = temp;
            }

            //final check if thru any
            if (tEnter > tExitZ || tEnterZ > tExit)
                return false;

            return true;
        }
        #endregion
    }
}
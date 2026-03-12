using System;
using System.Collections;
using Prismatix;
using SysMath = System.Math;

namespace Prismatix.Math
{
    public struct Vector3 //was previously class, changed to struct for SPEED!!!
    {
        public float x,y,z;
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
    }

    public struct HitInfo
    {
        public Vector3 point;
        public Vector3 normal;
        public float distance;
    }

    public class Image
    {
        //temp, use for storing image data and methods to modify
    }

    public static class Utils
    {
        //gives the 2d vector perpendicular to the two input vectors 
        public static Vector3 Cross(Vector3 a, Vector3 b){
            return new Vector3(
            a.y * b.z - a.z * b.y,
            a.z * b.x - a.x * b.z,
            a.x * b.y - a.y * b.x
            );
        }
        public static float Dot(Vector3 a, Vector3 b){
            return a.x * b.x + a.y * b.y + a.z * b.z;
        }
        public static float Remap(float value, float inMin, float inMax, float outMin, float outMax){
            return outMin + (value - inMin) * (outMax - outMin) / (inMax - inMin);
        }
        public static float Clamp(float value, float min, float max){
            if (value < min) {  value = min; }
            if (value > max) { value = max; } 
            return value;
        }

        public static HitInfo? GetRayIntersect(Raycast ray, Vector3 a, Vector3 b, Vector3 c)
        {
            //using Moller-Trumbore algorithm
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
            hitInfo.normal = Cross(edgeAB, edgeAC).Normalized(); //normal is perpendicular to triangle
            hitInfo.distance = distance;

            return hitInfo;
        }

    }
}
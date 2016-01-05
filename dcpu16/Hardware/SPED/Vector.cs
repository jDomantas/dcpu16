using System;
using System.Drawing;

namespace dcpu16.Hardware.SPED
{
    class Vector
    {
        public double X { get; }
        public double Y { get; }
        public double Z { get; }

        public Vector(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public Vector Cross(Vector other)
        {
            return new Vector(
                Y * other.Z - Z * other.Y,
                Z * other.X - X * other.Z,
                X * other.Y - Y * other.X);
        }
        
        public static Vector operator +(Vector a, Vector b)
        {
            return new Vector(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        }

        public static Vector operator -(Vector a, Vector b)
        {
            return new Vector(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        }

        public static Vector operator -(Vector a)
        {
            return new Vector(-a.X, -a.Y, -a.Z);
        }

        public static Vector operator *(Vector a, double val)
        {
            return new Vector(a.X * val, a.Y * val, a.Z * val);
        }

        public static Vector operator *(double val, Vector a)
        {
            return a * val;
        }

        public static Vector operator /(Vector a, double val)
        {
            return new Vector(a.X / val, a.Y / val, a.Z / val);
        }
        
        public Point ToScreenCoordinates(int screenWidth, int screenHeight)
        {
            double x = (Y * 1.75 / X) * screenWidth + screenWidth / 2;
            double y = -(Z * 1.75 / X) * screenWidth + screenHeight * 0.4;
            return new Point((int)Math.Round(x), (int)Math.Round(y));
        }

        public double LengthSquared => (X * X + Y * Y + Z * Z);
        public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);
        public Vector Normalized => this / Length;

        public static Vector Zero => new Vector(0, 0, 0);
        public static Vector UnitX => new Vector(1, 0, 0);
        public static Vector UnitY => new Vector(0, 1, 0);
        public static Vector UnitZ => new Vector(0, 0, 1);
    }
}

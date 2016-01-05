using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace dcpu16.Hardware.SPED
{
    class ViewMatrix
    {
        private double[,] Values;

        public ViewMatrix(Vector camera, Vector lookAt, Vector up)
        {
            Values = new double[4, 3];
            Vector direction = (lookAt - camera).Normalized;
            Vector side = direction.Cross(up).Normalized;
            up = side.Cross(direction).Normalized;

            Values[0, 0] = direction.X;
            Values[1, 0] = direction.Y;
            Values[2, 0] = direction.Z;
            Values[0, 1] = side.X;
            Values[1, 1] = side.Y;
            Values[2, 1] = side.Z;
            Values[0, 2] = up.X;
            Values[1, 2] = up.Y;
            Values[2, 2] = up.Z;
            Values[3, 0] = -(Values[0, 0] * camera.X + Values[1, 0] * camera.Y + Values[2, 0] * camera.Z);
            Values[3, 1] = -(Values[0, 1] * camera.X + Values[1, 1] * camera.Y + Values[2, 1] * camera.Z);
            Values[3, 2] = -(Values[0, 2] * camera.X + Values[1, 2] * camera.Y + Values[2, 2] * camera.Z);
        }

        public Vector Transform(Vector v)
        {
            return new Vector(
                v.X * Values[0, 0] + v.Y * Values[1, 0] + v.Z * Values[2, 0] + Values[3, 0],
                v.X * Values[0, 1] + v.Y * Values[1, 1] + v.Z * Values[2, 1] + Values[3, 1],
                v.X * Values[0, 2] + v.Y * Values[1, 2] + v.Z * Values[2, 2] + Values[3, 2]);
        }
    }
}

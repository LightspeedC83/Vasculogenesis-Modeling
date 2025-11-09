using System;
namespace VascularGenerator.DataStructures
{
    public class VascularSegment
    {
        double[] startPoint;
        double[] endPoint;
        public float radius;
        public float flow;
        public float pressure;

        public VascularSegment(double[] startPoint, double[] endPoint, float r, float q, float p)
        {
            this.startPoint = startPoint;
            this.endPoint = endPoint;
            radius = r;
            flow = q;
            pressure = p;
        }
    }
}
using System;
namespace VascularGenerator.DataStructures
{
    public class VascularSegment : IsCopyable<VascularSegment>
    {
        public double[] startPoint;
        public double[] endPoint;
        public double segmentLength;

        public double radius;
        public double flow;
        public double pressureIn;
        public double pressureOut;

        private double dynamicViscosity = 0.0035;

        //constructor that takes all the values and assigns them
        public VascularSegment(double[] startPoint, double[] endPoint, double q, double p1, double p2, double radius)
        {
            this.startPoint = startPoint;
            this.endPoint = endPoint;
            segmentLength = Math.Sqrt(Math.Pow(startPoint[0] - endPoint[0], 2) + Math.Pow(startPoint[1] - endPoint[1], 2)); //segment lengh is in pixels

            flow = q;
            pressureIn = p1;
            pressureOut = p2;
            this.radius = radius;
        }

        //constructor that will calculate the radius, using poiseulle's law, given all the other relevant values
        public VascularSegment(double[] startPoint, double[] endPoint, double q, double p1, double p2)
        {
            this.startPoint = startPoint;
            this.endPoint = endPoint;
            segmentLength = Math.Sqrt(Math.Pow(startPoint[0] - endPoint[0], 2) + Math.Pow(startPoint[1] - endPoint[1], 2)); //segment lengh is in pixels

            double segmentLengthMeters = segmentLength / 100; //we're saying one pixel is one centimeter but for the radius calculation we need it in SI units (meters), so we convert (using 1 pixel = 1cm = 0.01m)

            flow = q;
            pressureIn = p1;
            pressureOut = p2;
            radius = Math.Pow((8 * flow * dynamicViscosity * segmentLengthMeters / (Math.PI * Math.Abs(p1 - p2))), 1.0 / 4.0); //calculated using poiseulle's law with a dynamic viscosity of blood as 0.0035 pascal-seconds

        }

        //constructor that will calculate the pressureIn, using poiseulle's law, given all the other relevant values
        public VascularSegment(double q, double p2, double radius, double[] startPoint, double[] endPoint)
        {
            this.startPoint = startPoint;
            this.endPoint = endPoint;
            segmentLength = Math.Sqrt(Math.Pow(startPoint[0] - endPoint[0], 2) + Math.Pow(startPoint[1] - endPoint[1], 2)); //segment lengh is in pixels

            double segmentLengthMeters = segmentLength / 100; //we're saying one pixel is one centimeter but for the radius calculation we need it in SI units (meters), so we convert (using 1 pixel = 1cm = 0.01m)

            flow = q;
            this.radius = radius;
            pressureOut = p2;
            double deltaPressure = 8 * flow * dynamicViscosity * segmentLengthMeters / (Math.PI * Math.Pow(radius, 4)); //change in pressure from inlet to outlet (p2-p1) -- we know p2<p1 and deltaPressure>0 (assuming flow is nonnegative) meaning we add to get to p1
            pressureIn = pressureOut + deltaPressure;
        }

        public void CalculateLength()
        {
            segmentLength = Math.Sqrt(Math.Pow(startPoint[0] - endPoint[0], 2) + Math.Pow(startPoint[1] - endPoint[1], 2)); //segment lengh is in pixels
        }

        public void RecalculatePressureIn()
        {
            double segmentLengthMeters = segmentLength / 100; //we're saying one pixel is one centimeter but for the radius calculation we need it in SI units (meters), so we convert 
            double deltaPressure = 8 * flow * dynamicViscosity * segmentLengthMeters / (Math.PI * Math.Pow(radius, 4)); //change in pressure from inlet to outlet (p2-p1) -- we know p2<p1 and deltaPressure>0 (assuming flow is nonnegative) meaning we add to get to p1
            pressureIn = pressureOut + deltaPressure;
        }

        public double CalculateVolume()
        {
            return segmentLength*0.01 * Math.PI * Math.Pow(radius, 2);
        }

        public double GetRadius()
        {
            return radius * 10;
        }

        public bool ContainsPoint(double[] point)
        {
            int dim = startPoint.Length;

            if (dim != endPoint.Length || dim != point.Length)
                throw new ArgumentException("All input points must have the same dimension.");

            // Compute axis vector and its squared length
            double axisSquaredLength = 0;
            double[] axis = new double[dim];
            for (int i = 0; i < dim; i++)
            {
                axis[i] = endPoint[i] - startPoint[i];
                axisSquaredLength += axis[i] * axis[i];
            }

            if (axisSquaredLength == 0)
                return false; // Degenerate cylinder

            // Vector from start to point
            double[] startToPoint = new double[dim];
            for (int i = 0; i < dim; i++)
                startToPoint[i] = point[i] - startPoint[i];

            // Project startToPoint onto axis to get parameter t
            double t = 0;
            for (int i = 0; i < dim; i++)
                t += startToPoint[i] * axis[i];
            
            t /= axisSquaredLength;

            // Clamp t between 0 and 1 to stay within the finite cylinder
            if (t < 0 || t > 1)
                return false;

            // Calculate closest point on axis
            double[] closestPoint = new double[dim];
            for (int i = 0; i < dim; i++)
                closestPoint[i] = startPoint[i] + t * axis[i];

            // Calculate squared distance from point to closest point
            double distSquared = 0;
            for (int i = 0; i < dim; i++)
            {
                double diff = point[i] - closestPoint[i];
                distSquared += diff * diff;
            }

            return distSquared <= radius * radius;
        }
        

        //returns an exact copy of this vascular segment object
        public VascularSegment CreateCopy()
        {
            VascularSegment copy = new VascularSegment(
                startPoint: this.startPoint,
                endPoint: this.endPoint,
                q: this.flow,
                p1: this.pressureIn,
                p2: this.pressureOut,
                radius: this.radius
            );
            return copy;
        }
        
        public override string ToString()
        {
            return "Vascular Segment: \n\tstart:" + startPoint + "\n\tend:" + endPoint +"\n\tlength:" + segmentLength + "\n\tpressureIn:" + pressureIn + "\n\tpressureOut:" + pressureOut + "\n\tflow:" + flow + "\n\tradius:" + radius;
        }
    }
}
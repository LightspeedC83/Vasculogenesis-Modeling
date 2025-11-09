using System;

public class VascularSegment
{
    public float radius;
    public float flow;
    public float pressure;

    public VascularSegment(float r, float q, float p)
    {
        radius = r;
        flow = q;
        pressure = p;
    }
}
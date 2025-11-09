# Vasculogenesis Modeling
I'm using C# to generate realistic looking vascular networks!

# Starting with simple model
vasculogenesis is basically a problem of how to supply every "cell" on a plane (or in 3D space) with blood/oxygen. So I'm going to start with a model that represents this. 

the plane will be tiled with cell objects, that have have posisiton and hypoxia fields. hypoxia starts as 0
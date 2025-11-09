# Vasculogenesis Modeling
I'm using C# to generate realistic looking vascular networks!

# Starting with simple model
vasculogenesis is basically a problem of how to supply every "cell" on a plane (or in 3D space) with blood/oxygen. I will be roughly following this paper by W. Schreiner wherein he lays out a "Constrained Constructive Optimization" approach to the problem: [Computer generation of complex arterial tree models by W. Schreiner](https://doi.org/10.1016/0141-5425(93)90046-2)

First I use rejection sampling to get a uniform distribution of N points (recorded as "numberTerminalLocations") on the 
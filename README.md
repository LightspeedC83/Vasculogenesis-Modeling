# Vasculogenesis Modeling
I'm using C# to procedurally generate realistic looking vascular structures! One highly useful application of this would be for surgery simulators that would teach surgeons how to operate in and around realistic vascular systems.

# Project Overview
vasculogenesis is basically a problem of how to supply every "cell" on a plane (or in 3D space) with blood/oxygen. I will be roughly following this paper by W. Schreiner wherein he lays out a "Constrained Constructive Optimization" approach to the problem: [Computer generation of complex arterial tree models by W. Schreiner](https://doi.org/10.1016/0141-5425(93)90046-2)

First I use rejection sampling to get a uniform distribution of N (recorded as "numberTerminalLocations") random points within the perfusion radius. 

ToDo: look into this paper: https://proceedings.sbmac.org.br/sbmac/article/view/2323/2339

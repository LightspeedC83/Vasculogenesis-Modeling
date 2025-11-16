using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Transactions;
using MathNet.Numerics.RootFinding; // for solving the junction pressure when we bifurcate
using VascularGenerator.DataStructures; //importing our custom tree datastructure
using System.IO;

using System.Collections.Generic;
using Newtonsoft.Json;

public class VascularGeneration
{

    //fields
    private bool[,] canvas;
    private bool[,] terminalPointLocations;

    double y;
    int perfusionRadius;
    double terminalFlow;
    double terminalPressure;
    double inletFlow;
    double inletPressure;
    int numberTerminalSegments;
    int numTerminalPointsCreated;
    private double[] inletLocation;
    List<double[]> terminalLocations;



    //constructor
    public VascularGeneration(int perfusionRadius, int numberTerminalSegments, double terminalPressure, double inletPressure, double inletFlow, double y)
    {
        numTerminalPointsCreated = 0;
        terminalPointLocations = new bool[perfusionRadius * 2 +1, perfusionRadius * 2 +1];
        this.numberTerminalSegments = numberTerminalSegments;
        this.y = y;
        this.perfusionRadius = perfusionRadius;
        this.terminalFlow = inletFlow/numberTerminalSegments; //inlet flow should be equally divided amongst the terminal segments 
        this.terminalPressure = terminalPressure; 
        this.inletPressure = inletPressure; 
        this.inletFlow = inletFlow; 
        inletLocation = new double[] { 0, 0 }; //setting the inlet location to the the left corner of the circle

        // terminalLocations = GenerateTerminalPoints(perfusionRadius, numberTerminalSegments); //terminal location is a list of random uniformly distributed points within the perfusion area

        Tree<VascularSegment> tree = GenerateVascularTree(inletLocation, terminalPressure, inletFlow, inletPressure);
        
        //storing JSON
        string json = JsonConvert.SerializeObject(tree, Formatting.Indented);
        string path = "outputs/testJSON.txt";
        Console.WriteLine("testing");
        File.WriteAllText(path, json);
        //loading from JSON
        json = System.IO.File.ReadAllText(path);
        Tree<VascularSegment> treeRoot = JsonConvert.DeserializeObject<Tree<VascularSegment>>(json);
    }


    private Tree<VascularSegment> GenerateVascularTree(double[] inletLocation, double terminalPressure, double inletFlow, double inletPressure)
    {
        //first we create a segment from the inlet location to some random point that carries one terminal flow from the inlet to the terminal location
        double[] firstTerminalPoint = NewTerminalPoint(null);

        // terminalFlow = inletFlow/numTerminalPointsCreated;

        VascularSegment firstVascularSegment = new VascularSegment(
            startPoint: inletLocation,
            endPoint: firstTerminalPoint,
            p1: inletPressure,
            p2: terminalPressure,
            q: terminalFlow
        );


        Tree<VascularSegment> inletSegment = new Tree<VascularSegment>(firstVascularSegment); // the root of the tree

        // now we get a new point, traverse the tree to find the best bifurcation coordinate (ie. the closest point to the new point along all the lines connecting the new tree)
        while (numTerminalPointsCreated < numberTerminalSegments)
        {
            //debugging adn progress stuff
            ExportImage(VisualizeTree(inletSegment), "test_iteration_" + numTerminalPointsCreated);
            Console.WriteLine(inletSegment.GetValue().GetRadius());

            double[] nextTerminalPoint = NewTerminalPoint(inletSegment);
            // terminalFlow = inletFlow/numTerminalPointsCreated;

            //getting the best bifurcation point and the node associated with it
            double[] bestBifurcationPoint = GetBifurcationPointByDistance(inletSegment, nextTerminalPoint);
            Tree<VascularSegment> bestBifurcationPointNode = GetBifurcationNode(inletSegment, bestBifurcationPoint);

            //now that we have the best bifurcation point for the current terminal segment, we will insert a new segment into the tree and rescale everything accordingly
            Tree<VascularSegment> bifurcatedNode = Bifurcate(
                bifurcationNode: bestBifurcationPointNode,
                terminalPoint: nextTerminalPoint,
                bifurcationPoint: bestBifurcationPoint
            );

            //adding it into the tree by overwriting the node to be bifurcatd with it
            bestBifurcationPointNode.SetValue(bifurcatedNode.GetValue());
            bestBifurcationPointNode.SetChildren(bifurcatedNode.GetChildren());

            //Now we bubble up, recalculating radii, pressure, and flow for all the segments above the bifurcation segment
            Tree<VascularSegment> upperNode = bestBifurcationPointNode;
            BubbleUpChanges(upperNode);
            // EnforceGlobalPressureDrop(inletSegment);

        }

        return inletSegment;
    }

    //given a root node, this method will return the point in space that is the best bifurcation node
    public double[] GetBifurcationPointByVolume(Tree<VascularSegment> root, double[] newPoint)
    {
        //first we make a copy of the tree
        Tree<VascularSegment> rootCopy = root.CreateCopy();

        double[] bestPoint = [rootCopy.GetValue().endPoint[0], rootCopy.GetValue().endPoint[1]];
        double bestVolume = Double.MaxValue;
        
        //now we to a test insertion at each possible point and calculate the resultant volume

        List<Tree<VascularSegment>> segmentsToVisit = [root]; // a queue

        while (segmentsToVisit.Count > 0)
        {   
            Tree<VascularSegment> currentSegment = segmentsToVisit[0];
            segmentsToVisit.RemoveAt(0);

            //traversing the current segment
            double[] p1 = currentSegment.GetValue().startPoint; //getting a unit vector pointing from the start point to the end point
            double[] p2 = currentSegment.GetValue().endPoint;
            double[] segmentVector = [(p2[0] - p1[0]) / currentSegment.GetValue().segmentLength, (p2[1] - p1[1]) / currentSegment.GetValue().segmentLength];
            for (double x = 0.1; x < currentSegment.GetValue().segmentLength - 0.1; x += 0.1)
            {
                
                //overwrite the copy with a new clean copy
                rootCopy = root.CreateCopy();

                double[] bifurcationCandidate = [p1[0] + x * segmentVector[0], p1[1] + x * segmentVector[1]]; //getting the bifurcation candidate 
                Tree<VascularSegment> testBifurcationNode = GetBifurcationNode(rootCopy, bifurcationCandidate); //getting the associated node in the copy tree

                //now that we have the candidate bifurcation point for the current segment, we will insert a new segment into the tree copy and rescale everything accordingly
                Tree<VascularSegment> bifurcatedNode = Bifurcate(
                    bifurcationNode: testBifurcationNode,
                    terminalPoint: newPoint,
                    bifurcationPoint: bifurcationCandidate
                );

                //adding it into the tree by overwriting the node to be bifurcatd with it
                testBifurcationNode.SetValue(bifurcatedNode.GetValue());
                testBifurcationNode.SetChildren(bifurcatedNode.GetChildren());

                //Now we bubble up, recalculating radii, pressure, and flow for all the segments above the bifurcation segment
                BubbleUpChanges(testBifurcationNode);
                
                // EnforceGlobalPressureDrop(rootCopy);
                

                //now we calculate the volume of the copied tree.
                double volume = 0.0;
                List<Tree<VascularSegment>> toVisit = [rootCopy];
                while (toVisit.Count > 0)
                {
                    Tree<VascularSegment> current = toVisit[0];
                    toVisit.RemoveAt(0);
                    volume += current.GetValue().CalculateVolume();
                    if (current.GetChildren().Count == 0) { continue; }
                    foreach (Tree<VascularSegment> c in current.GetChildren()) { toVisit.Add(c); }
                }
                
                //if this tree's volume is better, we update the best point
                if (volume < bestVolume)
                {
                    bestVolume = volume;
                    bestPoint = bifurcationCandidate;
                }
            }

            // adding the current segments children to the visiting queue
            List<Tree<VascularSegment>> currentChildren = currentSegment.GetChildren();
            if (currentChildren.Count > 0)
            {
                foreach (Tree<VascularSegment> c in currentChildren)
                {
                    segmentsToVisit.Add(c);
                }
            }
        }

        return bestPoint;
    }

    public Tree<VascularSegment> GetBifurcationNode(Tree<VascularSegment> root, double[] bifurcationPoint)
    {

        List<Tree<VascularSegment>> toVisit = [root];
        while (toVisit.Count > 0)
        {
            Tree<VascularSegment> current = toVisit[0];
            toVisit.RemoveAt(0);
            if (current.GetValue().ContainsPoint(bifurcationPoint)) { return current; }
            if (current.GetChildren().Count == 0) { continue; }
            foreach (Tree<VascularSegment> c in current.GetChildren()) { toVisit.Add(c); }
        }
        return null;
    }
    
    public double[] GetBifurcationPointByDistance(Tree<VascularSegment> root, double[] newPoint)
    {
        //traversing the Tree to find the best bifurcation point minimizing distance 
        List<Tree<VascularSegment>> segmentsToVisit = [root]; // a queue

        double[] bestBifurcationPoint = root.GetValue().startPoint;
        double bestDistance = 2 * perfusionRadius + 1; // starting out with one more than the maximum distance any point inside the perfusion area could be from another
        Tree<VascularSegment> bestBifurcationPointNode = root;

        while (segmentsToVisit.Count > 0)
        {
            Tree<VascularSegment> currentSegment = segmentsToVisit[0];
            segmentsToVisit.RemoveAt(0);

            //traversing the current segment
            double[] p1 = currentSegment.GetValue().startPoint; //getting a unit vector pointing from the start point to the end point
            double[] p2 = currentSegment.GetValue().endPoint;
            double[] segmentVector = [(p2[0] - p1[0]) / currentSegment.GetValue().segmentLength, (p2[1] - p1[1]) / currentSegment.GetValue().segmentLength];
            for (double x = 0.1; x < currentSegment.GetValue().segmentLength - 0.1; x += 0.1)
            {
                double[] bifurcationCandidate = [p1[0] + x * segmentVector[0], p1[1] + x * segmentVector[1]]; //getting the bifurcation candidate 
                double candidateDistance = Math.Sqrt(Math.Pow(newPoint[0] - bifurcationCandidate[0], 2) + Math.Pow(newPoint[1] - bifurcationCandidate[1], 2)); //calculating distance between bifurcation candidate and the target location

                if (candidateDistance < bestDistance) // if the bifucation candidate is better than before
                {
                    bestDistance = candidateDistance;
                    bestBifurcationPoint = bifurcationCandidate;
                    bestBifurcationPointNode = currentSegment;
                }
            }

            // adding the current segments children to the visiting queue
            List<Tree<VascularSegment>> currentChildren = currentSegment.GetChildren();
            if (currentChildren.Count > 0)
            {
                for (int i = 0; i < currentChildren.Count; i++)
                {
                    segmentsToVisit.Add(currentChildren[i]);
                }
            }
        }
        return bestBifurcationPoint;
    }

    //given a node that has had a bifurcation operation preformed on it, this function will update the upstream nodes in the tree to reflect the changes
    public Tree<VascularSegment> BubbleUpChanges(Tree<VascularSegment> upperNode)
    {
        List<Tree<VascularSegment>> bubblingUpNodes = [upperNode]; //creating a queue initially populated with the upper node from our insert operation

        while (bubblingUpNodes.Count > 0)
        {
            //getting the next node from the queue
            Tree<VascularSegment> currentNode = bubblingUpNodes[0];
            bubblingUpNodes.RemoveAt(0);

            //getting the new raidus of the next node from the radii of it's children and the flow as the sum of the flow of the children.
            double radiusCubeSum = 0.0;
            double flowSum = 0.0;
            List<Tree<VascularSegment>> childSegments = currentNode.GetChildren();
            for (int i = 0; i < childSegments.Count; i++)
            {
                radiusCubeSum += Math.Pow(childSegments[i].GetValue().radius, y);
                flowSum += childSegments[i].GetValue().flow;
            }


            double newRadius = Math.Pow(radiusCubeSum, 1.0 / y); // the new radius according to r_parent^y = r_daughter^y + r_daughter^y

            //updating the values
            currentNode.GetValue().radius = newRadius;
            currentNode.GetValue().flow = flowSum;
            currentNode.GetValue().pressureOut = currentNode.GetChildren()[0].GetValue().pressureIn; //pressure at the node is all the same so we'll just sample one of the children for their pressure in

            //updating the pressure 
            currentNode.GetValue().RecalculatePressureIn();

            //adding the parent of the current node to the queue
            if (currentNode.GetParent() != null)
            {
                bubblingUpNodes.Add(currentNode.GetParent());
            }
        }

        //returning the inlet segment (tree root) of the updated tree
        return upperNode.GetRoot();
    }

    //this function takes in a node containing a segment S, it bifurcates S1 at the bifurcationPoint creating Sa, Sb, and Snew. Sa goes from the S's start point to the bifurcation point, Sb goes from the bifurcation point to S's endpoint, and Snew goes from the bifurcation point to the terminal point
    public Tree<VascularSegment> Bifurcate(Tree<VascularSegment> bifurcationNode, double[] terminalPoint, double[] bifurcationPoint)
    {
        //WE have the following known values for Snew
        double sNewFlow = terminalFlow;
        double sNewP2 = terminalPressure;
        double sNewLength = Math.Sqrt(Math.Pow(terminalPoint[0] - bifurcationPoint[0], 2) + Math.Pow(terminalPoint[1] - bifurcationPoint[1], 2)); //segment lengh is in pixels for Snew

        //we have the following known values for Sb
        double sBFlow = bifurcationNode.GetValue().flow;
        double sBP2 = bifurcationNode.GetValue().pressureOut;
        if (bifurcationNode.GetChildren().Count == 0)
        {
            sBFlow = terminalFlow;
            sBP2 = terminalPressure;
        }

        double sBLength = Math.Sqrt(Math.Pow(bifurcationNode.GetValue().endPoint[0] - bifurcationPoint[0], 2) + Math.Pow(bifurcationNode.GetValue().endPoint[1] - bifurcationPoint[1], 2)); //segment lengh is in pixels for Sb

        //we have the following known values for Sa
        double sAFlow = sNewFlow + sBFlow;
        double sAP1 = bifurcationNode.GetValue().pressureIn;
        double sALength = Math.Sqrt(Math.Pow(bifurcationNode.GetValue().startPoint[0] - bifurcationPoint[0], 2) + Math.Pow(bifurcationNode.GetValue().startPoint[1] - bifurcationPoint[1], 2)); //segment lengh is in pixels for Sb

        // the radii of the three segments are related by: r_Sa^y = r_Snew^y + r_Sb^y
        // the pressure at the junction between the three segments is the same: P_Snew_in = P_a_out = P_b_in = P_junction
        // we can use poiseulle's law and the above contstraints to find Pjunction, and then the radii of all junctions

        // segment lenght is in pixels so wee need to convert to meters (using 1 pixel = 1cm = 0.01m)
        sALength = sALength * 0.01;
        sBLength = sBLength * 0.01;
        sNewLength = sNewLength * 0.01;

        const double eta = 0.0035; // dynamic viscosity in Pa seconds
        double K = 8.0 * eta / Math.PI;

        // Function f(Pj) = r_a^y - r_b^y - r_new^y, where each r_i is a function of Pjunction by Poiseuille's law
        Func<double, double> f = Pj =>
        {
            // Avoid division by zero or negative pressure differences
            if (Pj >= sAP1 || Pj <= Math.Max(sBP2, sNewP2)) { return double.NaN; }

            double ra4 = K * sALength * sAFlow / (sAP1 - Pj);
            double rb4 = K * sBLength * sBFlow / (Pj - sBP2);
            double rnew4 = K * sNewLength * sNewFlow / (Pj - sNewP2);

            double ra_y = Math.Pow(ra4, y / 4.0);
            double rb_y = Math.Pow(rb4, y / 4.0);
            double rnew_y = Math.Pow(rnew4, y / 4.0);

            return ra_y - (rb_y + rnew_y);
        };

        // Bracket Pjunction between (max(sBP2, sNewP2) + ε) and (sAP1 - ε)
        double eps = 1e-6;
        double lowerBound = Math.Max(sBP2, sNewP2) + eps;
        double upperBound = sAP1 - eps;

        // Use Math.NET Numerics Brent's method to find Pj (bifurcation junction pressure)
        double Pj = Brent.FindRoot(f, lowerBound, upperBound);

        // Compute the radii from Pjunction
        double rA = Math.Pow(K * sALength * sAFlow / (sAP1 - Pj), 0.25);
        double rB = Math.Pow(K * sBLength * sBFlow / (Pj - sBP2), 0.25);
        double rNew = Math.Pow(K * sNewLength * sNewFlow / (Pj - sNewP2), 0.25);

        // now that we have all the values we need, we can create the segment objects, and structure the tree
        VascularSegment sNew = new VascularSegment(
            startPoint: bifurcationPoint,
            endPoint: terminalPoint,
            p1: Pj,
            p2: terminalPressure,
            q: terminalFlow,
            radius: rNew
        );

        VascularSegment sA = new VascularSegment(
            startPoint: bifurcationNode.GetValue().startPoint,
            endPoint: bifurcationPoint,
            p1: sAP1,
            p2: Pj,
            q: sAFlow,
            radius: rA
        );

        VascularSegment sB = new VascularSegment(
            startPoint: bifurcationPoint,
            endPoint: bifurcationNode.GetValue().endPoint,
            p1: Pj,
            p2: sBP2,
            q: sBFlow,
            radius: rB
        );

        //creating the tree structre of the bifurcation area and returning the root (sA)
        Tree<VascularSegment> upperSegment = new Tree<VascularSegment>(sA);
        Tree<VascularSegment> lowerSegment = new Tree<VascularSegment>(sB);
        Tree<VascularSegment> newSegment = new Tree<VascularSegment>(sNew);
        lowerSegment.SetChildren(bifurcationNode.GetChildren()); // the children of the bifurcation node are now children of the lower segment
        upperSegment.AddChild(lowerSegment);
        upperSegment.AddChild(newSegment);

        return upperSegment;
    }

    private void EnforceGlobalPressureDrop(Tree<VascularSegment> root) {
        const double eta = 0.0035;
        // Step A: compute ΔP_current with terminal pressure clamped
        RecomputePressures(root, inletPressure, terminalPressure, eta, useInletAsBoundary: false);
        double deltaPCurrent = root.GetValue().pressureIn - terminalPressure;
        double deltaPTarget  = inletPressure - terminalPressure;
        if (deltaPCurrent <= 0 || deltaPTarget <= 0) return; // guard

        // Step B: scale all radii so ΔP matches target (ΔP ∝ 1/r^4)
        double s = Math.Pow(deltaPCurrent / deltaPTarget, 0.25);
        ScaleRadii(root, s);

        // Step C: recompute pressures with inlet clamped to inletPressure
        RecomputePressures(root, inletPressure, terminalPressure, eta, useInletAsBoundary: true);
    }
    void ScaleRadii(Tree<VascularSegment> node, double s) {
    var queue = new Queue<Tree<VascularSegment>>();
    queue.Enqueue(node);
    while (queue.Count > 0) {
        var current = queue.Dequeue();
        current.GetValue().radius *= s;
        foreach (var child in current.GetChildren()) {
            queue.Enqueue(child);
        }
    }
}

    public static void RecomputePressures(
        Tree<VascularSegment> root,
        double inletPressure,
        double terminalPressure,
        double eta,
        bool useInletAsBoundary // true: fix inlet; false: fix terminal
    ){
        // 1) Precompute segment resistances
        var R = new Dictionary<int, double>();
        double Rseg(VascularSegment s) {
            double Lm = s.segmentLength * 0.01; // convert px to meters
            return 8.0 * eta * Lm / (Math.PI * Math.Pow(s.radius, 4));
        }
        void FillR(Tree<VascularSegment> n) {
            var s = n.GetValue();
            R[n.GetID()] = Rseg(s);
            foreach (var c in n.GetChildren()) FillR(c);
        }
        FillR(root);

        // 2) Post-order: compute equivalent downstream resistance
        var Req = new Dictionary<int, double>();
        double ComputeReq(Tree<VascularSegment> n) {
            if (n.GetChildren().Count == 0) {
                return Req[n.GetID()] = R[n.GetID()];
            }
            double reqA = ComputeReq(n.GetChildren()[0]);
            double reqB = ComputeReq(n.GetChildren()[1]);
            double reqChildren = 1.0 / (1.0 / reqA + 1.0 / reqB);
            return Req[n.GetID()] = R[n.GetID()] + reqChildren;
        }
        double reqRoot = ComputeReq(root);

        // 3) Set root boundary pressure
        if (useInletAsBoundary) {
            root.GetValue().pressureIn = inletPressure;
        } else {
            // enforce terminal pressure exactly
            root.GetValue().pressureIn = terminalPressure + root.GetValue().flow * reqRoot;
        }

        // 4) Pre-order: propagate pressures down the tree
        void Propagate(Tree<VascularSegment> n) {
            var s = n.GetValue();
            s.pressureOut = s.pressureIn - s.flow * R[n.GetID()];
            foreach (var c in n.GetChildren()) {
                c.GetValue().pressureIn = s.pressureOut; // junction pressure
                Propagate(c);
            }
        }
        Propagate(root);

        // 5) Optional: diagnostics can be added here
    }

    //returns one terminal point candidate, randomly from within the perfusion radius (uniform distribution)
    private double[] NewTerminalPoint(Tree<VascularSegment> workingTree)
    {

        // we will be using rejection sampling, let's now calculate the radius within which no other terminal point should be spawned from a previously placed terminal point
        double exclusionRadius = perfusionRadius / Math.Sqrt(numberTerminalSegments); // comes from pi*(r_exclusion)^2 = pi*(r_perfusion)^2 / numberTerminalSegments
        double exclusionRadiusSqared = Math.Pow(exclusionRadius, 2);

        //terminalPointLocations is a rectangular array we use to store all the terminal points that have been added


        Random random = new Random();
        bool pointFound = false;
        while (!pointFound)
        {
            double randAngle = 2.0 * 3.14159265 * random.NextDouble();
            double randRadius = perfusionRadius * Math.Sqrt(random.NextDouble()); //we use sqrt here because area scales with r^2, taking the sqrt of the random double [0,1] fixes this
            double[] cartesianRandPoint = new double[] { randRadius * Math.Sin(randAngle), randRadius * Math.Cos(randAngle) }; //converting the raidus and angle into a rectangluar coordinate in (x,y) form

            //checking to see if cartesianRandPoint should be rejected because its too close to any other terminal points (this is the part that makes it rejection sampling)
            int[] rectArrLocation = new int[] { (int)(cartesianRandPoint[0] + perfusionRadius), (int)(perfusionRadius - cartesianRandPoint[1]) };
            bool rejected = false;
            for (int x = rectArrLocation[0] - (int)exclusionRadius; x < rectArrLocation[0] + (int)exclusionRadius; x++)
            {
                for (int y = rectArrLocation[1] - (int)exclusionRadius; y < rectArrLocation[1] + (int)exclusionRadius; y++)
                {
                    //checking point validity
                    if (terminalPointLocations.GetLength(0) <= x || terminalPointLocations.GetLength(1) <= y || x < 0 || y < 0)
                    {
                        continue;
                    }

                    if (terminalPointLocations[y, x])
                    {
                        if (Math.Pow(x - rectArrLocation[0], 2) + Math.Pow(y - rectArrLocation[1], 2) < exclusionRadiusSqared) // if the point within the box of lenght 2*exclusionRadius is within the exclusion radius of the new point, the new point is invalid and thus rejected
                        {
                            rejected = true;
                            break;
                        }
                    }
                }

            }

            //If we reject becasue of the location in relation to other terminal points, we don't bother with the next check
            if (rejected)
            {
                continue;
            }

            if (workingTree == null) //if it's the first terminal point, there is no working tree so we just return
            {
                pointFound = true;
                numTerminalPointsCreated++;
                terminalPointLocations[rectArrLocation[1], rectArrLocation[0]] = true; //representing the accpeted point in the exclusion map

                return cartesianRandPoint;
            }
            //checking to see if the terminal point candidate (cartesianRandPoint) is within any of the segments of the workingTree (ie. the tree we're in the process of construction at the time of this function call )
            List<Tree<VascularSegment>> toVisit = [workingTree];

            while (toVisit.Count > 0)
            {
                //deque next node
                Tree<VascularSegment> currentNode = toVisit[0];
                toVisit.RemoveAt(0);

                //checking if the candidate terminal point is inside this segment
                double[] start = currentNode.GetValue().startPoint;
                double[] end = currentNode.GetValue().endPoint;
                double length = currentNode.GetValue().segmentLength;
                double radius = currentNode.GetValue().radius;
                double[] segmentVector = [(end[0] - start[0]) / length, (end[1] - start[1]) / length];

                bool segmentClear = true;
                for (double x = 0; x <= length; x += 0.1)
                {
                    double[] p = [start[0] + x * segmentVector[0], start[1] + x * segmentVector[1]];
                    double distance = Math.Sqrt(Math.Pow(p[0] - cartesianRandPoint[0], 2) + Math.Pow(p[1] - cartesianRandPoint[1], 2));

                    if (distance < radius)
                    {
                        segmentClear = false;
                        rejected = true;
                        break;
                    }
                }

                if (rejected) { break; }

                //enqueing the children of this node
                List<Tree<VascularSegment>> children = currentNode.GetChildren();
                if (children.Count > 0)
                {
                    for (int i = 0; i < children.Count; i++)
                    {
                        toVisit.Add(children[i]);
                    }
                }
            }


            if (!rejected)
            {
                pointFound = true;
                numTerminalPointsCreated++;
                terminalPointLocations[rectArrLocation[1], rectArrLocation[0]] = true; //representing the accpeted point in the exclusion map

                return cartesianRandPoint;
            }
            else { continue; }
        }
        return null;


    }

    private bool[,] VisualizeTree(Tree<VascularSegment> tree)
    {
        int H = perfusionRadius * 2 + 1; // rows (y)
        int W = perfusionRadius * 2 + 1; // cols (x)
        var output = new bool[H, W];
        var toVisit = new List<Tree<VascularSegment>> { tree };
        const double step = 0.5;

        while (toVisit.Count > 0)
        {
            var node = toVisit[0];
            toVisit.RemoveAt(0);
            var seg = node.GetValue();

            double[] start = seg.startPoint;
            double[] end = seg.endPoint;
            double len = seg.segmentLength;
            if (len <= 1e-9) continue; // avoid division by zero

            // Unit direction vector of the segment
            double ux = (end[0] - start[0]) / len;
            double uy = (end[1] - start[1]) / len;

            // Convert radius from meters to pixels (1 px = 0.01 m)
            double r_px = seg.GetRadius();
            if (r_px <= 0) r_px = 0.5; // ensure visibility
            double r2 = r_px * r_px;
            int rPix = (int)Math.Ceiling(r_px);

            for (double k = 0; k <= len; k += step)
            {
                double wx = start[0] + k * ux; // world X (model coordinate)
                double wy = start[1] + k * uy; // world Y

                // Map world coordinates to image indices
                int cx = (int)Math.Round(wx) + perfusionRadius;   // X to [0..W-1]
                int cy = perfusionRadius - (int)Math.Round(wy);   // flip Y to image row

                // Clamp bounding box to image bounds
                int xmin = Math.Max(0, cx - rPix);
                int xmax = Math.Min(W - 1, cx + rPix);
                int ymin = Math.Max(0, cy - rPix);
                int ymax = Math.Min(H - 1, cy + rPix);

                for (int y = ymin; y <= ymax; y++)
                {
                    int dy = y - cy;
                    for (int x = xmin; x <= xmax; x++)
                    {
                        // Skip if already set
                        if (output[y, x]) continue;

                        int dx = x - cx;
                        if (dx * dx + dy * dy <= r2)
                        {
                            output[y, x] = true;
                        }
                    }
                }
            }

            var children = node.GetChildren();
            if (children != null && children.Count > 0)
            {
                toVisit.AddRange(children);
            }
        }

        return output;
    }

    private double CalculateVolume(Tree<VascularSegment> tree)
    {
        double sum = 0.0;
        List<Tree<VascularSegment>> toVisit = [tree];

        while (toVisit.Count > 0)
        {
            Tree<VascularSegment> current = toVisit[0];
            toVisit.RemoveAt(0);

            sum += current.GetValue().CalculateVolume();

            //enqueuing children
            List<Tree<VascularSegment>> children = current.GetChildren();
            if (children.Count > 0)
            {
                foreach (Tree<VascularSegment> child in children)
                {
                    toVisit.Add(child);
                }
            }
        }
        return sum;
    }

    // //uniformly generates a list of terminal points randomly inside the perfucsion area
    // private List<double[]> GenerateTerminalPoints(int perfusionRadius, int numberTerminalSegments)
    // {

    //     //creating numberTerminalSegments number of points uniformly randomly distributed within the perfusion cirlce

    //     // we will be using rejection sampling, let's now calculate the radius within which no other terminal point should be spawned from a previously placed terminal point
    //     double exclusionRadius = perfusionRadius / Math.Sqrt(numberTerminalSegments); // comes from pi*(r_exclusion)^2 = pi*(r_perfusion)^2 / numberTerminalSegments
    //     double exclusionRadiusSqared = Math.Pow(exclusionRadius, 2);
    //     bool[,] terminalPoints = new bool[perfusionRadius * 2, perfusionRadius * 2];

    //     List<double[]> terminalLocations = new List<double[]>(); // initializing the list to store the terminal location points
    //     Random random = new Random();
    //     int iteration = numberTerminalSegments;
    //     while (iteration > 0) // will repeat numberTerminalSegment times
    //     {
    //         double randAngle = 2.0 * 3.14159265 * random.NextDouble();
    //         double randRadius = perfusionRadius * Math.Sqrt(random.NextDouble()); //we use sqrt here because area scales with r^2, taking the sqrt of the random double [0,1] fixes this
    //         double[] cartesianRandPoint = new double[] { randRadius * Math.Sin(randAngle), randRadius * Math.Cos(randAngle) }; //converting the raidus and angle into a rectangluar coordinate in (x,y) form

    //         //checking to see if cartesianRandPoint should be rejected (ie. is it too close to any other points)
    //         int[] rectArrLocation = new int[] { (int)(cartesianRandPoint[0] + perfusionRadius), (int)(perfusionRadius - cartesianRandPoint[1]) };
    //         bool rejected = false;
    //         for (int x = rectArrLocation[0] - (int)exclusionRadius; x < rectArrLocation[0] + (int)exclusionRadius; x++)
    //         {
    //             for (int y = rectArrLocation[1] - (int)exclusionRadius; y < rectArrLocation[1] + (int)exclusionRadius; y++)
    //             {
    //                 //checking point validity
    //                 if (terminalPoints.GetLength(0) <= x || terminalPoints.GetLength(1) <= y || x < 0 || y < 0)
    //                 {
    //                     continue;
    //                 }

    //                 if (terminalPoints[y, x])
    //                 {
    //                     if (Math.Pow(x - rectArrLocation[0], 2) + Math.Pow(y - rectArrLocation[1], 2) < exclusionRadiusSqared) // if the point within the box of lenght 2*exclusionRadius is within the exclusion radius of the new point, the new point is invalid and thus rejected
    //                     {
    //                         rejected = true;
    //                         break;
    //                     }
    //                 }
    //             }
    //             if (rejected) { break; }
    //         }

    //         if (!rejected)
    //         {
    //             terminalPoints[rectArrLocation[1], rectArrLocation[0]] = true; //representing the accpeted point in the exclusion map
    //             //storing the terminal point
    //             terminalLocations.Add(cartesianRandPoint);

    //             iteration--; //decreasing iterator
    //         }

    //     }

    //     //representing the random terminal segment points in the canvas and exporting them
    //     canvas = new bool[perfusionRadius * 2 + 1, perfusionRadius * 2 + 1];
    //     foreach (double[] point in terminalLocations)
    //     {
    //         canvas[perfusionRadius - (int)point[1], (int)point[0] + perfusionRadius] = true;
    //     }
    //     ExportImage(canvas, "terminal_point_generation_test");

    //     return terminalLocations;
    // }


    //exports the canvas as an image
    public void ExportImage(bool[,] canvas, string name)
    {
        Bitmap outputImage = new Bitmap(canvas.GetLength(0), canvas.GetLength(0));
        for (int y = 0; y < canvas.GetLength(1); y++)
        {
            for (int x = 0; x < canvas.GetLength(0); x++)
            {
                if (canvas[y, x] == false)
                {
                    outputImage.SetPixel(x, y, Color.FromArgb(255, 255, 255));
                }
                else
                {
                    outputImage.SetPixel(x, y, Color.FromArgb(0, 0, 0));
                }
            }
        }
        outputImage.Save("outputs/" + name + ".png", ImageFormat.Png);
    }

    static void Main()
    {
        Console.WriteLine("---Starting Program---");

        // Constants for realistic microvascular scale
        double perfusionRadius = 100;                // in pixels (1 px = 1 cm → 1 m radius domain)
        int numberTerminalSegments = 10;
        double terminalPressure = 40;       // 60 mmHg in Pascals
        double inletPressure = 400;           // 100 mmHg in Pascals
        double inletFlow = 1;                  // 500 μL/min in m³/s (approximate)
        double y = 3.0;                               // Murray's law exponent

        VascularGeneration testing = new VascularGeneration(
            perfusionRadius: (int)perfusionRadius,
            numberTerminalSegments: numberTerminalSegments,
            terminalPressure: terminalPressure,
            inletPressure: inletPressure,
            inletFlow: inletFlow,
            y: y
        );
        
        Console.WriteLine("---Program Complete---");


    }

}
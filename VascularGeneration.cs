using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Net;
using System.Net.NetworkInformation;
using System.Security.Cryptography.X509Certificates;
using System.Transactions;
using VascularGenerator.DataStructures; //importing our custom tree datastructure

public class VascularGeneration
{

    //fields
    private bool[,] canvas;

    double y;
    int perfusionRadius;
    double terminalFlow;
    double terminalPressure;
    double inletFlow;
    double inletPressure;

    private double[] inletLocation;
    List<double[]> terminalLocations;



    //constructor
    public VascularGeneration(int perfusionRadius, int numberTerminalSegments, double terminalPressure, double inletPressure, double inletFlow, double y)
    {
        this.y = y;
        this.perfusionRadius = perfusionRadius;
        this.terminalFlow = inletFlow/numberTerminalSegments; //inlet flow should be equally divided amongst the terminal segments 
        this.terminalPressure = terminalPressure; 
        this.inletPressure = inletPressure; 
        this.inletFlow = inletFlow; 
        inletLocation = new double[] { -perfusionRadius, 0 }; //setting the inlet location to the the left corner of the circle

        terminalLocations = GenerateTerminalPoints(perfusionRadius, numberTerminalSegments); //terminal location is a list of random uniformly distributed points within the perfusion area
        
        GenerateVascularTree(inletLocation, terminalLocations, terminalFlow, terminalPressure, inletFlow, inletPressure);
    }

    //uniformly generates a list of terminal points randomly inside the perfucsion area
    private List<double[]> GenerateTerminalPoints(int perfusionRadius, int numberTerminalSegments)
    {

        //creating numberTerminalSegments number of points uniformly randomly distributed within the perfusion cirlce

        // we will be using rejection sampling, let's now calculate the radius within which no other terminal point should be spawned from a previously placed terminal point
        double exclusionRadius = perfusionRadius / Math.Sqrt(numberTerminalSegments); // comes from pi*(r_exclusion)^2 = pi*(r_perfusion)^2 / numberTerminalSegments
        double exclusionRadiusSqared = Math.Pow(exclusionRadius, 2);
        bool[,] terminalPoints = new bool[perfusionRadius * 2, perfusionRadius * 2];

        List<double[]> terminalLocations = new List<double[]>(); // initializing the list to store the terminal location points
        Random random = new Random();
        int iteration = numberTerminalSegments;
        while (iteration > 0) // will repeat numberTerminalSegment times
        {
            double randAngle = 2.0 * 3.14159265 * random.NextDouble();
            double randRadius = perfusionRadius * Math.Sqrt(random.NextDouble()); //we use sqrt here because area scales with r^2, taking the sqrt of the random double [0,1] fixes this
            double[] cartesianRandPoint = new double[] { randRadius * Math.Sin(randAngle), randRadius * Math.Cos(randAngle) }; //converting the raidus and angle into a rectangluar coordinate in (x,y) form

            //checking to see if cartesianRandPoint should be rejected (ie. is it too close to any other points)
            int[] rectArrLocation = new int[] { (int)(cartesianRandPoint[0] + perfusionRadius), (int)(perfusionRadius - cartesianRandPoint[1]) };
            bool rejected = false;
            for (int x = rectArrLocation[0] - (int)exclusionRadius; x < rectArrLocation[0] + (int)exclusionRadius; x++)
            {
                for (int y = rectArrLocation[1] - (int)exclusionRadius; y < rectArrLocation[1] + (int)exclusionRadius; y++)
                {
                    //checking point validity
                    if (terminalPoints.GetLength(0) <= x || terminalPoints.GetLength(1) <= y || x < 0 || y < 0)
                    {
                        continue;
                    }

                    if (terminalPoints[y, x])
                    {
                        if (Math.Pow(x - rectArrLocation[0], 2) + Math.Pow(y - rectArrLocation[1], 2) < exclusionRadiusSqared) // if the point within the box of lenght 2*exclusionRadius is within the exclusion radius of the new point, the new point is invalid and thus rejected
                        {
                            rejected = true;
                            break;
                        }
                    }
                }
                if (rejected) { break; }
            }

            if (!rejected)
            {
                terminalPoints[rectArrLocation[1], rectArrLocation[0]] = true; //representing the accpeted point in the exclusion map
                //storing the terminal point
                terminalLocations.Add(cartesianRandPoint);

                iteration--; //decreasing iterator
            }




        }

        //representing the random terminal segment points in the canvas and exporting them
        canvas = new bool[perfusionRadius * 2 + 1, perfusionRadius * 2 + 1];
        foreach (double[] point in terminalLocations)
        {
            canvas[perfusionRadius - (int)point[1], (int)point[0] + perfusionRadius] = true;
        }
        ExportImage(canvas, "terminal_point_generation_test");

        return terminalLocations;
    }


    private Tree<VascularSegment> GenerateVascularTree(double[] inletLocation, List<double[]> terminalLocations, double terminalFlow, double terminalPressure, double inletFlow, double inletPressure)
    {
        //first we create a segment from the inlet location to some random point that carries one terminal flow from the inlet to the terminal location
        double[] firstTerminalPoint = terminalLocations[0];
        terminalLocations.RemoveAt(0);

        VascularSegment firstVascularSegment = new VascularSegment(
            startPoint: inletLocation,
            endPoint: firstTerminalPoint,
            p1: inletPressure,
            p2: terminalPressure,
            q: terminalFlow
        );


        Tree<VascularSegment> inletSegment = new Tree<VascularSegment>(firstVascularSegment); // the root of the tree

        // now we get a new point, traverse the tree to find the best bifurcation coordinate (ie. the closest point to the new point along all the lines connecting the new tree)
        while (terminalLocations.Count > 0)
        {
            double[] nextTerminalPoint = terminalLocations[0]; //getting the next terminal location
            terminalLocations.RemoveAt(0);

            //traversing the Tree to find the best bifurcation point
            List<Tree<VascularSegment>> segmentsToVisit = [inletSegment]; // a queue

            double[] bestBifurcationPoint = inletSegment.GetValue().startPoint;
            double bestDistance = 2 * perfusionRadius + 1; // starting out with one more than the maximum distance any point inside the perfusion area could be from another
            Tree<VascularSegment> bestBifurcationPointNode = inletSegment;

            while (segmentsToVisit.Count > 0)
            {
                Tree<VascularSegment> currentSegment = segmentsToVisit[0];
                segmentsToVisit.RemoveAt(0);

                //traversing the current segment
                double[] p1 = currentSegment.GetValue().startPoint; //getting a unit vector pointing from the start point to the end point
                double[] p2 = currentSegment.GetValue().endPoint;
                double[] segmentVector = [(p2[0] - p1[0]) / currentSegment.GetValue().segmentLength, (p2[1] - p1[1]) / currentSegment.GetValue().segmentLength];
                for (int x = 1; x < currentSegment.GetValue().segmentLength; x++)
                {
                    double[] bifurcationCandidate = [p1[0] + x * segmentVector[0], p2[1] + x * segmentVector[1]]; //getting the bifurcation candidate 
                    double candidateDistance = Math.Sqrt(Math.Pow(nextTerminalPoint[0] - bifurcationCandidate[0], 2) + Math.Pow(nextTerminalPoint[1] - bifurcationCandidate[1], 2)); //calculating distance between bifurcation candidate and the target location

                    if (candidateDistance < bestDistance) // if the bifucation candidate is better than before
                    {
                        bestDistance = candidateDistance;
                        bestBifurcationPoint = bifurcationCandidate;
                        bestBifurcationPointNode = currentSegment;
                    }
                }

                // adding the current segments children to the visiting queue
                List<Tree<VascularSegment>> currentChildren = currentSegment.GetChildren();
                if (currentChildren != null)
                {
                    for (int i = 0; i < currentChildren.Count; i++)
                    {
                        segmentsToVisit.Add(currentChildren[i]);
                    }
                }
            }

            //now that we have the best bifurcation point for the current terminal segment, we will insert a new segment into the tree and rescale everything accordingly

            //first we break the segment that contains the bifurcation node into two segments
            double[] extraSegmentEndPoint = bestBifurcationPointNode.GetValue().endPoint;
            VascularSegment extraSegment = new VascularSegment( // the first segment, that leads to the rest of the tree
                startPoint: bestBifurcationPoint,
                endPoint: extraSegmentEndPoint,
                radius: bestBifurcationPointNode.GetValue().radius, //will have same radius as the segment that we split into two (this is the lower of those two in the tree)
                p2: bestBifurcationPointNode.GetValue().pressureOut,
                q: bestBifurcationPointNode.GetValue().flow
            );
            Tree<VascularSegment> extraNode = new Tree<VascularSegment>(extraSegment);

            //setting all the children of the best bifurcation node to be children of the new insert node
            List<Tree<VascularSegment>> children = bestBifurcationPointNode.GetChildren();
            if (children != null)
            {
                for (int i = 0; i < children.Count; i++)
                {
                    children[i].SetParent(extraNode);
                }
            }


            //creating a new terminal segment connecting to the next terminal point from the bifurcation point
            VascularSegment newTerminalSegment = new VascularSegment(
                startPoint: bestBifurcationPoint,
                endPoint: nextTerminalPoint,
                p1: extraSegment.pressureIn, //pressure into the next terminal segment will be the same as the pressure into the extra terminal segment, will be the same as the pressure out of the changed bestBifurcationSegmetn
                p2: terminalPressure,
                q: terminalFlow
            );
            Tree<VascularSegment> newTerminalNode = new Tree<VascularSegment>(newTerminalSegment);
            newTerminalNode.SetParent(bestBifurcationPointNode);

            //changing the node that was bestBifurcationNode's endpoint to the bifurcaiton point and updating all it's values
            Tree<VascularSegment> upperNode = bestBifurcationPointNode; //the upper node of the rest of the tree will be the rescaled best BifurcationNode
            VascularSegment upperSegment = upperNode.GetValue();
            upperSegment.endPoint = bestBifurcationPoint;
            upperSegment.CalculateLength(); //recalculating length
            upperSegment.pressureOut = newTerminalSegment.pressureIn; // pressure out will be the same as the pressure into the new terminal segment and the extra segment
            upperSegment.radius = Math.Pow(Math.Pow(newTerminalSegment.radius, y) + Math.Pow(extraSegment.radius, y), 1.0 / y); //murray's law if the y value is 3


            //Now we bubble up, recalculating radii, pressure, and flow for all the segments above the bifurcation segment
            List<Tree<VascularSegment>> bubblingUpNodes = [upperNode]; //creating a queue initially populated with the upper node from our insert operation

            while (bubblingUpNodes.Count > 0)
            {
                //getting the next node from the queue
                Tree<VascularSegment> currentNode = bubblingUpNodes[0];
                bubblingUpNodes.RemoveAt(0);

                //getting the new raidus of the next node from the radii of it's children and the flow as the sum of the flow of the children.
                double radiusCubeSum = 0.0;
                double flowSum = 0.0;
                double pressureSum = 0.0;
                List<Tree<VascularSegment>> childSegments = currentNode.GetChildren();
                for (int i = 0; i < childSegments.Count; i++)
                {
                    radiusCubeSum += Math.Pow(childSegments[i].GetValue().radius, y);
                    flowSum += childSegments[i].GetValue().flow;
                    pressureSum += childSegments[i].GetValue().pressureIn;
                }

                double newRadius = Math.Pow(radiusCubeSum, 1.0 / y); // the new radius according to r_parent^y = r_daughter^y + r_daughter^y
                //updating the values
                currentNode.GetValue().radius = newRadius;
                currentNode.GetValue().flow = flowSum;
                currentNode.GetValue().pressureOut = pressureSum;

                //updating the pressure 
                currentNode.GetValue().RecalculatePressureIn();

                //adding the parent of the current node to the queue
                bubblingUpNodes.Add(currentNode.GetParent());
            }

        }

        return inletSegment;
    }



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

        VascularGeneration testing = new VascularGeneration(
            perfusionRadius: 100, //100 pixel radius
            numberTerminalSegments: 1000,
            terminalPressure: 7999.342104, // 60 mmHg in pascals
            inletPressure: 13332.23684, // 100 mmHg in pascals
            inletFlow: 0.0000083333, // 500 mm/min in m^3/second
            y: 3 //exponent for the r_parent^y = r_daughter^y + r_daughter^y relationship governing radii --> we're using a y value of 3, making the governing equation murray's law
        );
        
        Console.WriteLine("---Program Complete---");


    }

}
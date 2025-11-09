using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;

public class VascularGeneration
{

    //fields
    private bool[,] canvas;

    private int[] inletLocation;
    List<double[]> terminalLocations;

    //constructor
    public VascularGeneration(int perfusionRadius, int numberTerminalSegments)
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
        canvas = new bool[perfusionRadius * 2 +1, perfusionRadius * 2 +1];
        foreach (double[] point in terminalLocations)
        {
            canvas[perfusionRadius-(int)point[1], (int)point[0]+perfusionRadius] = true;
        }
        ExportImage(canvas, "terminal_segment_generation_test");

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
        outputImage.Save("outputs/"+name+".png", ImageFormat.Png);
    }

    static void Main()
    {
        Console.WriteLine("---Starting Program---");
        VascularGeneration testing = new VascularGeneration(100, 500);
    }

}
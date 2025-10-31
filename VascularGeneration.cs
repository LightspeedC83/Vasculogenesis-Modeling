using System;

using System.Drawing;
using System.Drawing.Imaging;

public class VascularGeneration
{

    //fields
    private bool[,] canvas;
    private int size;

    //constructor
    public VascularGeneration(int size = 250)
    {
        this.size = size;
        canvas = new bool[size, size];
        ExportImage(canvas);

    }
    
    //exports the canvas as an image
    public void ExportImage(bool[,] canvas)
    {
        Bitmap outputImage = new Bitmap(size, size);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
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
        outputImage.Save("outputs/testOutput.png", ImageFormat.Png);
    }

    static void Main()
    {
        VascularGeneration testing = new VascularGeneration();
    }

}
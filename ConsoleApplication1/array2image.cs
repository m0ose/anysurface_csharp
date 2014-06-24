/*
 * Copyright 2014. Redfish Group LLC
 * 
 * Camera server 
 * 
 * 
 * 
 * 
 * 
 * */
// REDFISH GROUP MAKES NO REPRESENTATIONS OR WARRANTIES ABOUT THE SUITABILITY OF THE
// SOFTWARE, EITHER EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE
// IMPLIED WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE, OR NON-INFRINGEMENT. REDFISH GROUP SHALL NOT BE LIABLE FOR ANY DAMAGES
// SUFFERED BY LICENSEE AS A RESULT OF USING, MODIFYING OR DISTRIBUTING
// THIS SOFTWARE OR ITS DERIVATIVES.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Diagnostics;
using System.Threading.Tasks;

namespace AnySurfaceWebServer
{
    class array2image
    {
        public void array2Image()
        {

        }
        unsafe public Bitmap ArrayToBitmap( byte* ptr, int width, int height, int len){
            //maybe copying this image twice is not the most efficiant thing in the world
            //  It says this function only takes 1ms to complete. Plenty fast.
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            byte[] arr = new byte[len];
            Marshal.Copy((IntPtr)ptr, arr, 0, len);
            Bitmap barf =  ArrayToBitmap(arr, width, height);
            stopwatch.Stop();
            Console.WriteLine("image copy took {0} ms", stopwatch.ElapsedMilliseconds);
            return barf;

        }

        public Bitmap ArrayToBitmap(byte[] bytes, int width, int height)
        {
            var pixelFormat = PixelFormat.Format24bppRgb;
            var image = new Bitmap(width, height, pixelFormat);
            BitmapData imageData = image.LockBits(new Rectangle(0, 0, image.Width, image.Height), ImageLockMode.ReadWrite, pixelFormat);
            try
            {
                Marshal.Copy(bytes, 0, imageData.Scan0, bytes.Length);
            }
            finally
            {
                image.UnlockBits(imageData);
            }
            //image.Save("C:\\temp\\fart.bmp");
            return image;
        }
    }
}

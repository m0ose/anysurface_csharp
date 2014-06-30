using FlyCapture2Managed;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnySurfaceWebServer
{
    class BrightestPointFinder
    {
        private DateTime lastTime = DateTime.Now;
        private byte[] avgimg = null; 
        public BrightestPointFinder()
        {

        }
        private byte subtr(byte a, byte b)
        {
            return (byte)Math.Max(0, Math.Min(255, a - b));
        }
        public String brightestPoint(ManagedImage img)
        {
            DateTime now = DateTime.UtcNow;
            TimeSpan diff = now - lastTime;
            double diff2 = diff.TotalMilliseconds;

            Double maxIntens = 0;
            uint maxIndex = 0;

            int w = (int)img.cols;
            int h = (int)img.rows;
            int len = w * h;

            if (w <= 0 || h <= 0)
            {
                return "{\"x\":0,\"y\":0,\"i\":0,\"x2\":0,\"y2\":0}";
            }

            unsafe
            {

                array2image pu = new array2image();
                byte[] fu = pu.copyArray(img.data, w, h, 3 * (int)len);
                if (avgimg == null || diff2 > 60*1000)
                {
                    avgimg = fu;
                }
                
                for (uint i = 0; i < len; i += 3)
                {
                    byte r = subtr(fu[i], avgimg[i]);//fu[i] ;
                    byte g = subtr(fu[i+1], avgimg[i+1]);//fu[i + 1] - avgimg[i] ;
                    byte b = subtr(fu[i + 2], avgimg[i + 2]);//fu[i + 2];
                    Double intens = Math.Sqrt(r * r + g * g + b * b);
                    if (intens > maxIntens)
                    {
                        maxIntens = intens;
                        maxIndex = i;
                    }
                }
                //average the image in with the last ones
                for (uint i = 0; i < len; i += 1)
                {
                    avgimg[i] = (byte)Math.Max(0, Math.Min(255, (0.8 * (Double)avgimg[i] + 0.2 * (Double)fu[i])));
                }
            }
            int x = (int)(maxIndex % w);
            int y = (int)(maxIndex / h);//would prefer a floor call here but the language wont allow it. Says it's ambiguous
            float x2 = x / w;
            float y2 = y / h;
            //String json2 = "{\"x\":" + x + ",\"y\":" + y + ",\"i\":" + maxIntens + " }";
            StringBuilder jstr = new StringBuilder();
            jstr.AppendFormat("\"x\":{0},\"y\":{1},\"x2\":{2},\"y2\":{3},\"i\":{4}", x,y,x2,y2,maxIntens);
            String json2 = "{"+jstr.ToString()+"}";
            lastTime = now;
            return json2;
        }
    }
}

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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Threading;
using System.Diagnostics;
using System.Drawing;
using AnySurfaceWebServer;
using System.Drawing.Imaging;


namespace serverAgent
{
    public class Server
    {
        private int port;
        private string name;
        private HttpListener listener;
        private HttpListenerContext context;
        private PGCamWrapper camw;
        private bool EXIT_ON_ERROR = false;

        public Server(int _port, string _name)
        {
            port = _port;
            name = _name;
        }

        public void startCamera()
        {
            camw = new PGCamWrapper();
            camw.cam.StartCapture();
            Console.WriteLine("Start Camera");
        }
        public void stopCamera()
        {
            Console.WriteLine("Stopping Camera");
            camw.cam.StopCapture();
        }

        public void startServer()
        {
            listener = new HttpListener();
            listener.Prefixes.Add("http://*:" + port + "/");
            listener.Start();
            Console.WriteLine(" Start listening...");
            startCamera();
            ThreadPool.QueueUserWorkItem(new WaitCallback(startListening));
        }

        private void startListening(object o)
        {
            while (true)
            {
                //Console.WriteLine("listening...");
                context = listener.GetContext();
                sendResponse();
            }
        }

        private void sendResponse()
        {
            HttpListenerResponse response = context.Response;
            response.Headers["Access-Control-Allow-Origin"] = "*";
            HttpListenerRequest request = context.Request;
            Console.WriteLine("request for {0} from {1}", request.Url, request.UrlReferrer);
            Debug.WriteLine(request.QueryString.ToString());
            //
            // parse url
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            try
           {
                for (int i = 0; i < request.QueryString.Count; i++)
                {
                    var qs1 = request.QueryString.GetKey(i);
                    String qs2 = request.QueryString.Get(i);
                    switch (qs1)
                    {
                        case "gain":
                            int val2 = Convert.ToInt32(qs2);
                            camw.Gain = val2;
                            Console.WriteLine("gain set to {0}", val2);
                            break;
                        case "shutter":
                            int val3 = Convert.ToInt32( qs2);
                            camw.Shutter = val3;
                            Console.WriteLine("shutter set to {0}", val3);
                            break;
                        case "delay":
                            Double val4 = Convert.ToDouble(qs2);
                            camw.Delay = val4;
                            Console.WriteLine(" trigger delay set to {0}", camw.Delay);
                            break;
                        default:
                            break;

                    }
                }
                Console.WriteLine("arguments parsed at " + stopwatch.ElapsedMilliseconds + "ms");

                String stlow = request.RawUrl.ToLower();
                if (stlow.IndexOf("shot.") > 0 || stlow.IndexOf("image.") > 0)
                {
                    // Image Requested
                    //
                    ImageFormat firmat = imgformat4url(stlow);
                    Console.WriteLine("  image format is {0}", firmat.ToString());
                    Debug.WriteLine("shot in da house," + stlow.IndexOf("shot.png"));
                    Image img = camw.getPictureBMP();
                    Console.WriteLine("image gotten at " + stopwatch.ElapsedMilliseconds + "ms");
                    System.IO.Stream output = response.OutputStream;
                    img.Save(output, firmat);//this is the slowest part by far. jpg encoding is faster than png.
                    Console.WriteLine("image save and stream written at " + stopwatch.ElapsedMilliseconds + "ms");
                    output.Close();
                    Console.WriteLine("picture taking took" +stopwatch.ElapsedMilliseconds + "ms" );
                    img.Dispose();
                }
                else if (stlow.IndexOf("brightestpoint") > 0)
                {
                    string bp = camw.brightestPoint();
                    writeTextResponse(response, bp);
                }
                else { 
                //send response 
                    string responseString = "<HTML><BODY><h3> Your connected to<h1> " + name + "</h1></h3>" + request.RawUrl
                        + "<hr>Usage:<li><b><a href='http://127.0.0.1:" + port + "/shot.jpg'>http://127.0.0.1:" + port + "/shot.jpg</a> </b>"
                        + " <li><b><a href='http://127.0.0.1:" + port + "/shot.jpg?shutter=4&gain=0'>http://127.0.0.1:" + port + "/shot.jpg?shutter=4&gain=0</a></b>. Shutter and Gain can be set absolutely"
                        + " <li><b><a href='http://127.0.0.1:" + port + "/shot.jpg?delay=0.002'>http://127.0.0.1:" + port + "/shot.jpg?delay=0.002</a></b>. The delay in seconds afer GPIO trigger signal"
                        + " <li><b><a href='http://127.0.0.1:" + port + "/shot.jpg?shutter=-1&gain=-1&delay=-1'>http://127.0.0.1:" + port + "/shot.jpg?shutter=-1&gain=-1&delay=-1</a></b>. Negative numbers turn on automatic mode"
                        + " <li><b><a href='http://127.0.0.1:" + port + "/?shutter=4&gain=0&delay=0.002'>http://127.0.0.1:" + port + "/?shutter=4&gain=0&delay=0.002</a></b>. You don't need to take a picture to set the parameters "
                        + " <li><b><a href='http://127.0.0.1:" + port + "/shot.png'>http://127.0.0.1:" + port + "/shot.png</a></b>. Different formats based on extension. Supported extensions are jpg, png, and bmp."
                        + " <li><b><a href='http://127.0.0.1:" + port + "/brightestpoint.json'>http://127.0.0.1:" + port + "/brightestpoint.json</a></b>. Find the brightest Point"
                        + " <li><b><a href='http://127.0.0.1:" + port + "/brightestpoint.json?shutter=40&gain=0'>http://127.0.0.1:" + port + "/brightestpoint.json?shutter=40&gain=0</a></b>. Find the brightest Point"


                        + "</BODY></HTML>";
                    writeTextResponse(response, responseString);
                }
            }
            catch (Exception e)
            {
                var es = "<h3>!!!</h3>  " + e;
                errorResponse(response, es);
                if (EXIT_ON_ERROR == true)
                {
                    throw e;
                }
            }
            Console.WriteLine("total response time " + stopwatch.ElapsedMilliseconds + "ms");
            stopwatch.Stop();
        }

        private void writeTextResponse(HttpListenerResponse response, String responseString)
        {
            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
            response.ContentLength64 = buffer.Length;
            System.IO.Stream output = response.OutputStream;
            output.Write(buffer, 0, buffer.Length);
            output.Close();
        }

        private void errorResponse(HttpListenerResponse response, String errorMsg)
        {
            Console.WriteLine("BIG OL ERROR parsing request");
            string responseStringEr = "<HTML><BODY><h3> <h1> ERROR parsing request </h1>" + errorMsg + "</h3></BODY></HTML>";
            byte[] bufferEr = System.Text.Encoding.UTF8.GetBytes(responseStringEr);
            response.ContentLength64 = bufferEr.Length;
            response.StatusCode = 400;
            Debug.WriteLine(response.Headers);
            System.IO.Stream outputEr = response.OutputStream;
            outputEr.Write(bufferEr, 0, bufferEr.Length);
            outputEr.Close();
            return;
        }

        private ImageFormat imgformat4url(String stlow){
            String extens = stlow.Substring(stlow.LastIndexOf('.'));
            extens = extens.Split('?')[0];
            if (extens == ".png")
            {
                return ImageFormat.Png;
            }
            else if (extens == ".jpg")
            {
                return ImageFormat.Jpeg;
            }
            else if( extens == ".bmp" ){
                return ImageFormat.Bmp;
            }
            return ImageFormat.Png;//when in doubt return png
        }
    }
}

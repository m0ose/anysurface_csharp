﻿/*
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
                Console.WriteLine("listening...");
                context = listener.GetContext();
                sendResponse();
            }
        }

        private void sendResponse()
        {
            HttpListenerResponse response = context.Response;
            response.Headers["Access-Control-Allow-Origin"] = "*";
            HttpListenerRequest request = context.Request;
            Debug.WriteLine(request.QueryString.ToString());
            //
            // parse url
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
                            Console.WriteLine("set gain:" + val2);
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

                String stlow = request.RawUrl.ToLower();
                if (stlow.IndexOf("shot.") > 0 || stlow.IndexOf("image.") > 0)
                {
                    Stopwatch stopwatch = new Stopwatch();
                    stopwatch.Start();
                    ImageFormat firmat = imgformat4url(stlow);
                    Debug.WriteLine("shot in da house," + stlow.IndexOf("shot.png"));
                    Image img = camw.getPictureBMP();
                    Console.WriteLine("image gotten at " + stopwatch.ElapsedMilliseconds + "ms");
                    System.IO.Stream output = response.OutputStream;
                    img.Save(output, firmat);//this is the slowest part by far. jpg encoding is faster than png.
                    Console.WriteLine("image save and stream written at " + stopwatch.ElapsedMilliseconds + "ms");
                    output.Close();
                    stopwatch.Stop();
                    Console.WriteLine("picture taking took" +stopwatch.ElapsedMilliseconds + "ms" );
                    img.Dispose();
                }
                else { 
                //send response 
                    string responseString = "<HTML><BODY><h3> Your connected to<h1> " + name + "</h1></h3>" + request.RawUrl
                        + "<hr>Usage:<li>  http://127.0.0.1:8080/shot.png "
                        +" <li>http://127.0.0.1:8080/shot.png?shutter=4&gain=0. Shutter and Gain can be set absolutely"
                        + "<li>  http://127.0.0.1:8080/shot.png?delay=0.002. The delay is used for with GPIO sync cable"
                        + " <li>http://127.0.0.1:8080/shot.png?shutter=-1&gain=-1&delay=-1. Negative numbers turn on auto mode"
                        + " <li>  http://127.0.0.1:8080/?shutter=4&gain=0&delay=0.002. You don't need to take a picture to set the parameters "
                        + "</BODY></HTML>";
                    writeTextResponse(response, responseString);
                }
            }
            catch (Exception e)
            {
                var es = "<h3>!!!</h3>  " + e;
                errorResponse(response, es);
            }
            
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
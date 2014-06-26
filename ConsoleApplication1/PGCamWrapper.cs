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
using System.IO;
using System.Drawing;
using FlyCapture2Managed;
using System.Diagnostics;
using System.Threading;

namespace AnySurfaceWebServer
{
    class PGCamWrapper
    {
        public ManagedCamera cam;
        private int shutter = -990;
        private int gain = -990;
        private double delay = -990;
        private int triggerFails = 0;
        private int MAX_TRIGGER_FAILS = 1;
        private int PHOTO_TIMEOUT = 2000;
        private bool paramChanged = false;
        private ManagedPGRGuid guid;
        private DateTime lastPic = DateTime.UtcNow;

        public PGCamWrapper()
        {
            // initialize the camera
            ManagedBusManager busMgr = new ManagedBusManager();
            uint numCameras = busMgr.GetNumOfCameras();
            Console.WriteLine("Number of cameras detected: {0}", numCameras);
            if (numCameras < 1)
            {
                throw new Exception("No Camera Found");
            }
            cam = new ManagedCamera();
            guid = busMgr.GetCameraFromIndex(0);
            cam.Connect(guid);
            waitForWake();
            setDefaults();
            Console.WriteLine(CameraInfo());
        }

        public String CameraInfo()
        {
            CameraInfo camInfo = cam.GetCameraInfo();
            StringBuilder newStr = new StringBuilder();
            newStr.Append("\n*** CAMERA INFORMATION ***\n");
            newStr.AppendFormat("Serial number - {0}\n", camInfo.serialNumber);
            newStr.AppendFormat("Camera model - {0}\n", camInfo.modelName);
            newStr.AppendFormat("Camera vendor - {0}\n", camInfo.vendorName);
            newStr.AppendFormat("Sensor - {0}\n", camInfo.sensorInfo);
            newStr.AppendFormat("Resolution - {0}\n", camInfo.sensorResolution);
            return newStr.ToString();
        }

        public ManagedImage getPicture()
        {
            //pull one image off of the buffer
            if (paramChanged == true)
            {
                Console.WriteLine("parameter changed, pause for a few pictures");
                _getPicture();
                paramChanged = false;
            }
            ManagedImage res = _getPicture();
            return res;
        }

        private void pauseForShutter(){
            DateTime now = DateTime.UtcNow;
            TimeSpan diff = now - lastPic;
            double diff2 = diff.TotalMilliseconds;
            Console.WriteLine("milliseconds since last pic {0}", diff.TotalMilliseconds);
            if (diff2 < Math.Min(30, Shutter+10))
            {
                Console.WriteLine("sleeping for {0}", 35 - diff2 );
                Thread.Sleep((int)(35 - diff2));
            }
            lastPic = now;
        }

        private ManagedImage _getPicture()
        {
            pauseForShutter();

            ManagedImage rawImage = new ManagedImage();
            ManagedImage convertedImage = new ManagedImage();

            try
            {
                cam.RetrieveBuffer(rawImage);//get the actual image
                rawImage.Convert(PixelFormat.PixelFormatBgr, convertedImage);
            }
            catch (FC2Exception e)
            {
                String et = e.Type.ToString();
                Console.WriteLine(et);
                Debug.WriteLine(cam);
                if (et == "Timeout" || et == "TriggerFailed")
                {
                    Console.WriteLine("Turning off trigger beacuse of error {0}", e.ToString());
                    triggerFails++;
                    if (triggerFails >= 1)
                    {
                        restartCamera();
                    //    triggerFails = 0;
                    }
                    //throw new Exception("Trigger failed. Make sure GPOI pin is connected and working. Then restart server. Until then No trigger will be used");
                }
                else
                {
                    restartCamera();
                }
            }

            return convertedImage;
        }

        public void restartCamera()
        {
            Console.WriteLine("Restarting camera");
            try
            {
                cam.StopCapture();
            }
            catch (Exception e)
            {}
            cam.Disconnect();
            Thread.Sleep(200);
            cam = new ManagedCamera();
            ManagedBusManager busMgr = new ManagedBusManager();
            guid = busMgr.GetCameraFromIndex(0);
            cam.Connect(guid);
            waitForWake();
            cam.StartCapture();
            setDefaults();
            Thread.Sleep(200);
        }

        public Image getPictureBMP()
        {

            ManagedImage convertedImage = getPicture();
            uint datalength = convertedImage.cols * convertedImage.rows;
            int cols = (int)convertedImage.cols;
            int rows = (int)convertedImage.rows;
            //Bitmap fart = new Bitmap(cols, rows);
            //byte[] wart = new byte[datalength];
            if (cols <= 0 || rows <= 0)
            {
                return new Bitmap(1, 1);
            }
            Image fu = null;
            unsafe
            {
                array2image pu = new array2image();
                fu = pu.ArrayToBitmap(convertedImage.data, cols, rows, 3 * (int)datalength);
            }
            return fu;
        }

        public void setDefaults()
        {
            CameraProperty autoExposure = cam.GetProperty(PropertyType.AutoExposure);
            CameraProperty whiteBalance = cam.GetProperty(PropertyType.WhiteBalance);
            CameraProperty fps = cam.GetProperty(PropertyType.FrameRate);

            fps.valueA = 60;
            autoExposure.autoManualMode = false;
            whiteBalance.autoManualMode = false;
            cam.SetProperty(autoExposure);
            cam.SetProperty(whiteBalance);
            cam.SetProperty(fps);
            Shutter = -1;
            Gain = -1;
            FC2Config config = cam.GetConfiguration();
            config.grabTimeout = PHOTO_TIMEOUT;// seconds before image reception fails
            cam.SetConfiguration(config);
        }

        public Boolean waitForWake()
        {
            // Wait for camera to complete power-up
            const uint k_powerVal = 0x80000000;
            const Int32 k_millisecondsToSleep = 100;
            const uint k_cameraPower = 0x610;
            uint regVal = 0;
            uint count = 0;
            do
            {
                System.Threading.Thread.Sleep(k_millisecondsToSleep);
                count++;
                regVal = cam.ReadRegister(k_cameraPower);

            } while ((regVal & k_powerVal) == 0 && count < 100);
            return true;
        }

        public String brightestPoint()
        {
            Double maxIntens = 0;
            uint maxIndex = 0;

             ManagedImage img = getPicture();
             int w = (int)img.cols;
             int h = (int)img.rows;
             int len = w * h;


             if (w <= 0 || h <= 0 )
             {
                 return "{x:0,y:0,i:0}";
             }

             unsafe
             {

                 array2image pu = new array2image();
                 byte[] fu = pu.copyArray(img.data, w, h, 3 * (int)len);
                 for (uint i = 0; i < len; i += 3)
                 {
                     byte r = fu[i];
                     byte g = fu[i + 1];
                     byte b = fu[i + 2];
                     Double intens = Math.Sqrt(r * r + g * g + b * b);
                     if (intens > maxIntens)
                     {
                         maxIntens = intens;
                         maxIndex = i;
                     }
                 }
             }
             int x = (int)(maxIndex % w);
             int y = (int)(maxIndex / h);//would prefer a floor call here but the language wont allow it. Says it's ambiguous
             String json2 = "{ x:" + x + " , y:" + y + ", i:" + maxIntens + " }";
             return json2;
        }

        // These setters/gettters seem a little like repeated code. 
        //
        public int Shutter
        {
            set
            {
                if (value < 0 && shutter != -1)
                {
                    CameraProperty shut = cam.GetProperty(PropertyType.Shutter);
                    shut.autoManualMode = true;
                    cam.SetProperty(shut);
                    shutter = -1;
                    paramChanged = true;
                }
                else if (value != shutter)
                {
                    CameraProperty shut = cam.GetProperty(PropertyType.Shutter);
                    shut.autoManualMode = false;
                    shut.valueA = (uint)value;
                    cam.SetProperty(shut);
                    shutter = value;
                    paramChanged = true;
                }
            }
            get
            {
                return shutter;
            }
        }

        public int Gain
        {
            set
            {
                if (value < 0 && gain != -1)
                {
                    CameraProperty gn = cam.GetProperty(PropertyType.Gain);
                    gn.autoManualMode = true;
                    cam.SetProperty(gn);
                    gain = -1;
                    paramChanged = true;
                }
                else if (value != gain)
                {
                    CameraProperty gn = cam.GetProperty(PropertyType.Gain);
                    gn.autoManualMode = false;
                    gn.valueA = (uint)value;
                    cam.SetProperty(gn);
                    gain = value;
                    Console.WriteLine("gain set to {0}", gain);
                    paramChanged = true;
                }
            }
            get
            {
                return gain;
            }
        }

        public double Delay
        {
            set
            {
                if ((value < 0 && delay != -1.0) || triggerFails >= MAX_TRIGGER_FAILS)
                {
                    if (delay != -1.0)
                    {
                        CameraProperty gn1 = cam.GetProperty(PropertyType.TriggerMode);
                        gn1.onOff = false;//I think this is OFF
                        cam.SetProperty(gn1);

                        CameraProperty prop = cam.GetProperty(PropertyType.TriggerDelay);
                        prop.absControl = false;
                        prop.autoManualMode = true;
                        prop.onOff = false;
                        cam.SetProperty(prop);
                        delay = -1.0;
                        paramChanged = true;
                    }

                }
                else if (delay != value)
                {

                    CameraProperty gn1 = cam.GetProperty(PropertyType.TriggerMode);
                    gn1.onOff = true;//I think this is ON
                    cam.SetProperty(gn1);

                    CameraProperty prop = cam.GetProperty(PropertyType.TriggerDelay);
                    prop.absControl = true;
                    prop.autoManualMode = false;
                    prop.onOff = true;
                    prop.absValue = (float)value;
                    cam.SetProperty(prop);
                    delay = value;
                    paramChanged = true;
                }
            }
            get
            {
                return delay;
            }

        }

    }
}

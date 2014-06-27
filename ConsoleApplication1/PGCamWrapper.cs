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
        private int triggerFails = 2;
        private int MAX_TRIGGER_FAILS = 5;
        private int PHOTO_TIMEOUT = 2000;
        private bool paramChanged = false;
        private ManagedPGRGuid guid;
        private DateTime lastPic = DateTime.UtcNow;
        private BrightestPointFinder brightFind = new BrightestPointFinder();

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
            newStr.AppendFormat("{0}",getModeInfo());
            return newStr.ToString();
        }

        public String getModeInfo()
        {
            StringBuilder res = new StringBuilder();

            CameraProperty tm = cam.GetProperty(PropertyType.TriggerMode);
            CameraProperty td = cam.GetProperty(PropertyType.TriggerDelay);
            CameraProperty tmp = cam.GetProperty(PropertyType.Temperature);
            CameraProperty fps = cam.GetProperty(PropertyType.FrameRate);
            CameraProperty sh = cam.GetProperty(PropertyType.Shutter);

            res.AppendFormat("trigger mode:   abs control:{0} auto:{1} on:{2}\n", tm.absControl, tm.autoManualMode, tm.onOff);
            res.AppendFormat(" trigger delay: {0}, {1}\n", td.absControl, td.absValue);
            res.AppendFormat(" temperature: {0}, {1}\n", tmp.absValue, tmp.valueA);
            res.AppendFormat(" framerate:{0}, {1}\n", fps.valueA, fps.absValue);
            res.AppendFormat(" shutter:{0}, {1}\n", sh.valueA, fps.absValue);


            return res.ToString();
        }

        public ManagedImage getPicture()
        {
            //pull one image off of the buffer
            if (paramChanged == true)
            {
                Console.WriteLine("parameter changed, pause for a few pictures");
                _getPicture();
                Thread.Sleep(120);
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
            var shitter = Math.Max(Shutter, 20);
            if (diff2 <shitter)
            {
                Console.WriteLine("sleeping for {0}", shitter  - diff2);
                Thread.Sleep((int)(shitter  - diff2));
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
                cam.WaitForBufferEvent(rawImage,0);// Oooh, this is a much safer way to get the image
                //cam.RetrieveBuffer(rawImage);//get the actual image
                rawImage.Convert(PixelFormat.PixelFormatBgr, convertedImage);
            }
            catch (FC2Exception e)
            {
                String et = e.Type.ToString();
                Console.WriteLine(et);
                Debug.WriteLine(cam);
                if (et == "Timeout" || et == "TriggerFailed")
                {
                    Console.WriteLine("\n\nTIMEOUT ERROR {0} \n\n", e.ToString());
                    //if (Delay > 0)
                    {
                        triggerFails++;
                        if (triggerFails > MAX_TRIGGER_FAILS)
                        {
                            Delay = -1;
                        }
                    }
                    restartCamera();
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
            Console.WriteLine(getModeInfo());
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
            Delay = -1;
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
             ManagedImage img = getPicture();
             return brightFind.brightestPoint( img);
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
                    Thread.Sleep(90);//takes a little while
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
                    //if (delay != -1.0)
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

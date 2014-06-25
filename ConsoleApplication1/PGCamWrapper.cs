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

namespace AnySurfaceWebServer
{
    class PGCamWrapper
    {
        public ManagedCamera cam;
        private int shutter;
        private int gain;
        private double delay;
        private int triggerFails = 0;
        private int MAX_TRIGGER_FAILS = 1;
        private int PHOTO_TIMEOUT = 1000;

        public PGCamWrapper()
        {
            shutter = gain = -1;
            delay = -1;
            // initialize the camera
            ManagedBusManager busMgr = new ManagedBusManager();
            uint numCameras = busMgr.GetNumOfCameras();
            Console.WriteLine("Number of cameras detected: {0}", numCameras);
            if (numCameras < 1)
            {
                throw new Exception("No Camera Found");
            }
            cam = new ManagedCamera();
            ManagedPGRGuid guid = busMgr.GetCameraFromIndex(0);
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
                if (et == "Timeout" || et == "TriggerFailed")
                {
                    Console.WriteLine("Turning off trigger beacuse of error {0}", e.ToString());
                    triggerFails++;
                }
            }

            return convertedImage;
        }

        public Image getPictureBMP()
        {
            ManagedImage convertedImage = getPicture();
            uint datalength = convertedImage.cols * convertedImage.rows;
            int cols = (int)convertedImage.cols;
            int rows = (int)convertedImage.rows;
            //Bitmap fart = new Bitmap(cols, rows);
            //byte[] wart = new byte[datalength];
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
            config.grabTimeout = PHOTO_TIMEOUT;//5 seconds before image recive fails
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
        // These setters/gettters seem a little like repeated code. 
        //
        public int Shutter
        {
            set
            {
                if (value < 0)
                {
                    CameraProperty shut = cam.GetProperty(PropertyType.Shutter);
                    shut.autoManualMode = true;
                    cam.SetProperty(shut);
                    shutter = -1;
                }
                else if (value != shutter)
                {
                    CameraProperty shut = cam.GetProperty(PropertyType.Shutter);
                    shut.autoManualMode = false;
                    shut.valueA = (uint)value;
                    cam.SetProperty(shut);
                    shutter = value;
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
                if (value < 0)
                {
                    CameraProperty gn = cam.GetProperty(PropertyType.Gain);
                    gn.autoManualMode = true;
                    cam.SetProperty(gn);
                }
                else if (value != gain)
                {
                    CameraProperty gn = cam.GetProperty(PropertyType.Gain);
                    gn.autoManualMode = false;
                    gn.valueA = (uint)value;
                    cam.SetProperty(gn);
                    gain = value;
                    Console.WriteLine("gain set to {0}", gain);
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
                if (value < 0 || triggerFails >= MAX_TRIGGER_FAILS)
                {
                    CameraProperty gn1 = cam.GetProperty(PropertyType.TriggerMode);
                    gn1.onOff = false;//I think this is OFF
                    cam.SetProperty(gn1);

                    CameraProperty prop = cam.GetProperty(PropertyType.TriggerDelay);
                    prop.absControl = false;
                    prop.autoManualMode = true;
                    prop.onOff = false;
                    cam.SetProperty(prop);
                    delay = -1;
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
                }
            }
            get
            {
                return delay;
            }

        }

    }
}

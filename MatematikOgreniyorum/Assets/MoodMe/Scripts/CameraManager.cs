﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MoodMe
{
    public class CameraManager : MonoBehaviour
    {
        [Header("Video Source")]
        public int DeviceIndex = 0;
        public GameObject WebcamPlane;
        public Texture2D WebcamTexture;
        public RenderTexture VideoTexure;


        //Main buffer texture
        private WebCamTexture CameraTexture;
        public static Texture ExportWebcamTexture;

        private const int _width = 640, _height = 480;

        //Webcam ready state
        static bool _webcamSet = false;

        public static Color32[] GetPixels { get; private set; }

        public static Texture2D GetTexture { get { return (Texture2D)ExportWebcamTexture; } }


        // Start is called before the first frame update
        void Start()
        {
            try
            {
                Debug.Log("DEVICES LIST");
                for (int cameraIndex = 0; cameraIndex < WebCamTexture.devices.Length; cameraIndex++)
                {
                    Debug.Log(cameraIndex + " name " + WebCamTexture.devices[cameraIndex].name + " isFrontFacing " + WebCamTexture.devices[cameraIndex].isFrontFacing);
                }

                // Automatically select front-facing camera on Android
                if (Application.platform == RuntimePlatform.Android)
                {
                    bool foundFrontCamera = false;
                    for (int i = 0; i < WebCamTexture.devices.Length; i++)
                    {
                        if (WebCamTexture.devices[i].isFrontFacing)
                        {
                            DeviceIndex = i;
                            foundFrontCamera = true;
                            Debug.Log("Front-facing camera selected: " + WebCamTexture.devices[i].name);
                            break;
                        }
                    }
                    if (!foundFrontCamera)
                    {
                        Debug.LogWarning("No front-facing camera found. Using default device.");
                    }
                }

                // Clamp device index to valid range
                DeviceIndex = Mathf.Clamp(DeviceIndex, 0, WebCamTexture.devices.Length - 1);

                string camName = WebCamTexture.devices[DeviceIndex].name;
                Debug.Log("Cam Name: " + camName);
                CameraTexture = new WebCamTexture(camName, _width, _height, 30);
            }
            catch (Exception)
            {
                Debug.Log("Camera not ready");
            }

            if (VideoTexure == null)
            {
                CameraTexture.Play();
                StartCoroutine(WaitForWebCamAndInitialize(CameraTexture));
            }
            else
            {
                ExportWebcamTexture = new Texture2D(VideoTexure.width, VideoTexure.height, TextureFormat.RGBA32, false);
                _webcamSet = true;
            }
        }
        public static bool WebcamReady
        {
            get
            {
                return _webcamSet;
            }
        }



        private IEnumerator WaitForWebCamAndInitialize(WebCamTexture _webCamTexture)
        {
            while (_webCamTexture.width < 100)
                yield return null;
            //Debug.Log("******** Camera Texture size now is " + CameraTexture.width + " x " + CameraTexture.height);
            GetPixels = new Color32[CameraTexture.width * CameraTexture.height];
            ExportWebcamTexture = new Texture2D(CameraTexture.width, CameraTexture.height, TextureFormat.RGBA32, false);
            _webcamSet = true;

        }

        // Update is called once per frame
        void Update()
        {
            if (VideoTexure != null)
            {
                RenderTexture.active = VideoTexure;
                ((Texture2D)WebcamTexture).ReadPixels(new Rect(0, 0, _width, _height), 0, 0, false);
                ((Texture2D)WebcamTexture).Apply();
                RenderTexture.active = null;
                GetPixels = ((Texture2D)WebcamTexture).GetPixels32();
                Graphics.CopyTexture(WebcamTexture, ExportWebcamTexture);
            }
            else
            {
                if (WebcamReady)
                {
                    //Get the next frame from the webcam
                    bool waitForCamera = true;
                    while (waitForCamera)
                    {
                        try
                        {
                            //Store to RGBA buffer
                            CameraTexture.GetPixels32(GetPixels);
                            //Update the Webcam plane texture
                            ((Texture2D)WebcamTexture).SetPixels32(GetPixels);
                            ((Texture2D)WebcamTexture).Apply();
                            Graphics.CopyTexture(WebcamTexture, ExportWebcamTexture);
                            waitForCamera = false;
                        }
                        catch (Exception ex)
                        {
                            Debug.Log("Camera not running " + ex.Message);
                        }
                    }
                }
            }
        }
    }
}

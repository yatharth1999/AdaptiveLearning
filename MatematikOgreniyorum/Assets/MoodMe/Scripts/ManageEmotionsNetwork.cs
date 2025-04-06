using UnityEngine;
using Unity.Sentis;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MoodMe
{
    public class ManageEmotionsNetwork : MonoBehaviour
    {
        public ModelAsset EmotionsNetwork;
        public int ImageNetworkWidth = 48;
        public int ImageNetworkHeight = 48;
        [Range(1, 4)] public int ChannelCount = 1;
        public bool Process;

        public float[] GetCurrentEmotionValues
        {
            get { return DetectedEmotions.Values.ToArray(); }
        }

        private Worker worker;
        private static Dictionary<string, float> DetectedEmotions;
        private string[] EmotionsLabelFull = { "Angry", "Disgusted", "Scared", "Happy", "Sad", "Surprised", "Neutral" };
        private string[] EmotionsLabel = { "Neutral", "Surprised", "Sad" }; //Free Package


        void Start()
        {
            var runtimeModel = ModelLoader.Load(EmotionsNetwork);
            worker = new Worker(runtimeModel, BackendType.GPUCompute);

            // Initialize DetectedEmotions dictionary
            DetectedEmotions = new Dictionary<string, float>();
            foreach (string key in EmotionsLabelFull)
            {
                DetectedEmotions.Add(key, 0);
            }
        }

        void Update()
        {
            if (!Process)
            {
                Debug.Log("Processing not enabled, skipping Update.");
                return;
            }
            Process = true;

            if (FaceDetection.OutputCrop == null || FaceDetection.OutputCrop.Length != (ImageNetworkWidth * ImageNetworkHeight))
            {
                if (FaceDetection.IsFaceDetectionRunning)
                {
                    Debug.LogWarning("OutputCrop is null or has incorrect dimensions, skipping processing.");
                }
                return;
            }

            // Create the input texture and assign pixel data
            Texture2D croppedTexture = null;
            try
            {
                croppedTexture = new Texture2D(ImageNetworkWidth, ImageNetworkHeight, TextureFormat.R8, false);
                Color32[] rgba = FaceDetection.OutputCrop;
                croppedTexture.SetPixels32(rgba);
                croppedTexture.Apply();

                // Convert texture to tensor with NHWC layout
                var transform = new TextureTransform().SetTensorLayout(TensorLayout.NHWC).SetDimensions(ImageNetworkWidth, ImageNetworkHeight, ChannelCount);
                using (var inputTensor = TextureConverter.ToTensor(croppedTexture, transform))
                {

                    // Run inference
                    worker.Schedule(inputTensor);

                    using (var outputTensor = worker.PeekOutput() as Tensor<float>)
                    {
                        if (outputTensor == null)
                        {
                            Debug.LogError("Output tensor is null. Model inference failed.");
                            return;
                        }

                        using (var clonedTensor = outputTensor.ReadbackAndClone())
                        {
                            float[] results = clonedTensor.AsReadOnlyNativeArray().ToArray();
                            for (int i = 0; i < results.Length; i++)
                            {
                                if (i < EmotionsLabel.Length)
                                {
                                    DetectedEmotions[EmotionsLabel[i]] = results[i];
                                }
                            }
                        }
                    }
                }
            }
            finally
            {
                if (croppedTexture != null)
                {
                    DisposeTexture(croppedTexture);
                }
            }
        }

        private void OnDisable()
        {
            worker?.Dispose();
            worker = null;
            Debug.Log("Disposed of worker on disable.");
        }

        private void OnDestroy()
        {
            worker?.Dispose();
            Debug.Log("Worker disposed on destroy.");
        }

        private void DisposeTexture(Texture2D texture)
        {
            if (texture != null)
            {
                Destroy(texture);
                texture = null;
            }
        }
    }
}
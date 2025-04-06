using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MoodMe;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.Collections;
using Unity.Jobs;

namespace MoodMe
{
    public class EmotionsManager : MonoBehaviour
    {
        [Header("Input")]
        public ManageEmotionsNetwork EmotionNetworkManager;
        public FaceDetection FaceDetectorManager;

        [Header("Performance")]
        [Range(1, 60)]
        public int ProcessEveryNFrames = 15;

        [Header("Processing")]
        public bool FilterAllZeros = true;
        [Range(0, 29f)]
        public int Smoothing;

        [Header("Emotions")]
        public bool TestMode = false;
        [Range(0, 1f)]
        public float Angry;
        [Range(0, 1f)]
        public float Disgust;
        [Range(0, 1f)]
        public float Happy;
        [Range(0, 1f)]
        public float Neutral;
        [Range(0, 1f)]
        public float Sad;
        [Range(0, 1f)]
        public float Scared;
        [Range(0, 1f)]
        public float Surprised;

        public static float EmotionIndex;
        public static MoodMeEmotions.MDMEmotions Emotions;
        private static MoodMeEmotions.MDMEmotions CurrentEmotions;

        public static WebCamTexture CameraTexture;

        private EmotionsInterface _emotionNN;
        private bool _bufferProcessed = false;
        private int NFramePassed;

        void Start()
        {
            _emotionNN = new EmotionsInterface(EmotionNetworkManager, FaceDetectorManager);
        }

        void OnDestroy()
        {
            _emotionNN = null;
            Debug.Log("EmotionsManager destroyed and resources cleared.");
        }

        void LateUpdate()
        {
            if (!TestMode)
            {
                if (CameraManager.WebcamReady)
                {
                    NFramePassed = (NFramePassed + 1) % ProcessEveryNFrames;
                    if (NFramePassed == 0)
                    {
                        try
                        {
                            _emotionNN.ProcessFrame();
                            _bufferProcessed = true;
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError("Error processing frame: " + ex.Message);
                            _bufferProcessed = false;
                        }

                        if (_bufferProcessed)
                        {
                            _bufferProcessed = false;
                            if (!(_emotionNN.DetectedEmotions.AllZero && FilterAllZeros))
                            {
                                CurrentEmotions = _emotionNN.DetectedEmotions;
                                Emotions = Filter(Emotions, CurrentEmotions, Smoothing);

                                Angry = Emotions.angry;
                                Disgust = Emotions.disgust;
                                Happy = Emotions.happy;
                                Neutral = Emotions.neutral;
                                Sad = Emotions.sad;
                                Scared = Emotions.scared;
                                Surprised = Emotions.surprised;
                            }
                            else
                            {
                                Debug.Log("Filtered frame due to all emotions being zero.");
                            }
                        }
                        else
                        {
                            Emotions.Error = true;
                            Debug.LogError("Buffer processing failed, setting error flag.");
                        }
                    }
                }
            }
            else
            {
                Emotions.angry = Angry;
                Emotions.disgust = Disgust;
                Emotions.happy = Happy;
                Emotions.neutral = Neutral;
                Emotions.sad = Sad;
                Emotions.scared = Scared;
                Emotions.surprised = Surprised;
                Debug.Log("Test Mode is ON: Using manually set emotion values.");
            }

            EmotionIndex = Mathf.Clamp01((Surprised * 3+ Neutral * 2 - Sad * 2f + 1f) / 3f);
        
        }

        // Smoothing function
        MoodMeEmotions.MDMEmotions Filter(MoodMeEmotions.MDMEmotions target, MoodMeEmotions.MDMEmotions source, int SmoothingGrade)
        {
            float targetFactor = SmoothingGrade / 30f;
            float sourceFactor = (30 - SmoothingGrade) / 30f;

            target.angry = target.angry * targetFactor + source.angry * sourceFactor;
            target.disgust = target.disgust * targetFactor + source.disgust * sourceFactor;
            target.happy = target.happy * targetFactor + source.happy * sourceFactor;
            target.neutral = target.neutral * targetFactor + source.neutral * sourceFactor;
            target.sad = target.sad * targetFactor + source.sad * sourceFactor;
            target.scared = target.scared * targetFactor + source.scared * sourceFactor;
            target.surprised = target.surprised * targetFactor + source.surprised * sourceFactor;

            return target;
        }
    }
}
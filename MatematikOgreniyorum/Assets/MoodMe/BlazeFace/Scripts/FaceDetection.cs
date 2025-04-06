using System;
using System.Linq;
using System.Threading;
using Unity.Mathematics;
// using Unity.VisualScripting.Antlr3.Runtime;
using Unity.Sentis;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using System.Collections.Generic;
// using Unity.VisualScripting;

namespace MoodMe
{
    public class FaceDetection : MonoBehaviour
    {
        #region Serialized Fields
        [Header("Preview Components")]
        [SerializeField] public bool BoundingBoxes = true;
        [SerializeField] public FacePreview[] facePreviews;
        [SerializeField] public bool CropFacePreview = true;
        [SerializeField] public RawImage PreviewCropPlane;


        [Header("Model Assets")]
        [SerializeField] public ModelAsset faceDetector;

        [SerializeField] public TextAsset anchorsCSV;

        [Header("Export Settings")]
        [SerializeField] public int ExportCropWidth = 48;
        [SerializeField] public int ExportCropHeight = 48;
        [SerializeField] private float scaleFactor = 2f;

        [Header("Detection Settings")]
        [SerializeField] public float iouThreshold = 0.3f;
        [SerializeField] public float scoreThreshold = 0.5f;
        #endregion

        #region Constants
        private const int k_NumAnchors = 896;
        private const int k_NumKeypoints = 6;
        private const int detectorInputSize = 128;
        #endregion

        #region Private Fields
        private float[,] m_Anchors;
        private Worker m_FaceDetectorWorker;
        private Tensor<float> m_DetectorInput;
#if UNITY_2023_2_OR_NEWER
        private Awaitable m_DetectAwaitable;
#else
        private Task m_DetectTask;
#endif
        private float m_TextureWidth;
        private float m_TextureHeight;
        private Color32[] croppedFaceData;
        private CancellationTokenSource _cancellationTokenSource;

        #endregion

        #region Public Properties
        public bool Process;
        public static Color32[] OutputCrop;
        public static bool IsFaceDetectionRunning { get; private set; } = false;
        #endregion

        #region Unity Lifecycle Methods
        public async void Start()
        {
            if (!Process) return;

            _cancellationTokenSource = new CancellationTokenSource();
            CancellationToken token = _cancellationTokenSource.Token;

            // Initialize Anchors
            m_Anchors = BlazeUtils.LoadAnchors(anchorsCSV.text, k_NumAnchors);

            // Load and compile the face detection model
            var faceDetectorModel = ModelLoader.Load(faceDetector);

            // Create FunctionalGraph and add input
            var graph = new FunctionalGraph();
            var input = graph.AddInput(faceDetectorModel, 0);

            // Perform forward operation and keep outputs in place
            var outputs = Functional.Forward(faceDetectorModel, 2 * input - 1);
            var boxes = outputs[0];
            var scores = outputs[1];

            // Prepare anchors data
            var anchorsData = new float[k_NumAnchors * 4];
            Buffer.BlockCopy(m_Anchors, 0, anchorsData, 0, anchorsData.Length * sizeof(float));
            var anchors = Functional.Constant(new TensorShape(k_NumAnchors, 4), anchorsData);

            // Apply Non-Maximum Suppression filtering
            var idx_scores_boxes = BlazeUtils.NMSFiltering(boxes, scores, anchors, detectorInputSize, iouThreshold, scoreThreshold);

            // Compile final model using NMS output
            faceDetectorModel = graph.Compile(idx_scores_boxes.Item1, idx_scores_boxes.Item2, idx_scores_boxes.Item3);

            // Assign compiled model to worker
            m_FaceDetectorWorker = new Worker(faceDetectorModel, BackendType.GPUCompute);

            // Initialize the detector input tensor
            m_DetectorInput = new Tensor<float>(new TensorShape(1, detectorInputSize, detectorInputSize, 3));
            while (!token.IsCancellationRequested)
            {
                if (CameraManager.WebcamReady)
                {
                    try
                    {
#if UNITY_2023_2_OR_NEWER
                        m_DetectAwaitable = Detect(CameraManager.ExportWebcamTexture, token);
                        await m_DetectAwaitable;
#else
                        m_DetectTask = Detect(CameraManager.ExportWebcamTexture, token);
                        await m_DetectTask;
#endif
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
                else
                {
#if UNITY_2023_2_OR_NEWER
                        await Awaitable.NextFrameAsync();
#else
                    await Task.Yield();

#endif
                }
            }
            m_FaceDetectorWorker.Dispose();
            m_DetectorInput.Dispose();
        }
        #endregion


        #region Detection
#if UNITY_2023_2_OR_NEWER
        async Awaitable Detect(Texture texture, CancellationToken token)
#else
        async Task Detect(Texture texture, CancellationToken token)
#endif
        {
            if (texture == null)
            {
                Debug.LogWarning("Detect: Texture is null. Skipping detection.");
                return;
            }

            if (texture is WebCamTexture webCamTexture && !webCamTexture.isPlaying)
            {
                Debug.LogWarning("Detect: WebCamTexture is not playing. Skipping detection.");
                return;
            }

            m_TextureWidth = texture.width;
            m_TextureHeight = texture.height;

            var size = Mathf.Max(texture.width, texture.height);
            var scale = size / (float)detectorInputSize;

            // Create transformation matrix
            var M = CreateTransformationMatrix(texture, size, scale);

            // Validate the input tensor and transform matrix
            if (m_DetectorInput == null || M.Equals(default(float2x3)))
            {
                Debug.LogError("Detect: Invalid input tensor or transformation matrix.");
                return;
            }


            BlazeUtils.SampleImageAffine(texture, m_DetectorInput, M);

            // Run face detection model
            m_FaceDetectorWorker.Schedule(m_DetectorInput);

            // Use the proper Readback method depending on Unity version
#if UNITY_2023_2_OR_NEWER
            using var outputIndices = await (m_FaceDetectorWorker.PeekOutput(0) as Tensor<int>).ReadbackAndCloneAsync();
            using var outputScores = await (m_FaceDetectorWorker.PeekOutput(1) as Tensor<float>).ReadbackAndCloneAsync();
            using var outputBoxes = await (m_FaceDetectorWorker.PeekOutput(2) as Tensor<float>).ReadbackAndCloneAsync();
#else
            using var outputIndices = (m_FaceDetectorWorker.PeekOutput(0) as Tensor<int>).ReadbackAndClone();
            await Task.Yield();
            using var outputScores = (m_FaceDetectorWorker.PeekOutput(1) as Tensor<float>).ReadbackAndClone();
            await Task.Yield();
            using var outputBoxes = (m_FaceDetectorWorker.PeekOutput(2) as Tensor<float>).ReadbackAndClone();
#endif

            // Process the top face only
            if (outputIndices.shape.length > 0)
            {
                int i = 0; // Index for the top face
                if (token.IsCancellationRequested) return;

                // Activate only the first face preview
                if (BoundingBoxes)
                {
                    facePreviews[i].SetActive(true);
                }

                var idx = outputIndices[i];
                var anchorPosition = detectorInputSize * new float2(m_Anchors[idx, 0], m_Anchors[idx, 1]);

                // Calculate bounding box and other properties
                var boxStart = anchorPosition + new float2(outputBoxes[0, i, 0], outputBoxes[0, i, 1]);
                var box_ImageSpace = BlazeUtils.mul(M, boxStart);
                var boxEnd = anchorPosition + new float2(outputBoxes[0, i, 0] + 0.5f * outputBoxes[0, i, 2], outputBoxes[0, i, 1] + 0.5f * outputBoxes[0, i, 3]);
                var boxTopRight_ImageSpace = BlazeUtils.mul(M, boxEnd);
                var boxSize = 2f * (boxTopRight_ImageSpace - box_ImageSpace);

                int noseIndex = 2;
                var noseOffset = new float2(outputBoxes[0, i, 4 + 2 * noseIndex + 0], outputBoxes[0, i, 4 + 2 * noseIndex + 1]);
                var nosePosition_ImageSpace = BlazeUtils.mul(M, anchorPosition + noseOffset);

                if (BoundingBoxes)
                {
                    facePreviews[i].SetBoundingBox(true, ImageToWorld(box_ImageSpace), boxSize / texture.height);

                    for (var j = 0; j < k_NumKeypoints; j++)
                    {
                        var keypointOffset = new float2(outputBoxes[0, i, 4 + 2 * j + 0], outputBoxes[0, i, 4 + 2 * j + 1]);
                        var keypoint_ImageSpace = BlazeUtils.mul(M, anchorPosition + keypointOffset);
                        facePreviews[i].SetKeypoint(j, true, ImageToWorld(keypoint_ImageSpace));
                    }
                }
                else
                {
                    // Hide bounding box and keypoints if BoundingBoxes is false
                    if (BoundingBoxes)
                    {
                        facePreviews[i].SetBoundingBox(false, Vector3.zero, Vector2.zero);
                        for (var j = 0; j < k_NumKeypoints; j++)
                        {
                            facePreviews[i].SetKeypoint(j, false, Vector3.zero);
                        }
                    }

                }

                // Calculate crop dimensions and set the crop plane
                var boxWidth = Mathf.Abs(boxTopRight_ImageSpace.x - box_ImageSpace.x) * scaleFactor;
                var boxHeight = Mathf.Abs(boxTopRight_ImageSpace.y - box_ImageSpace.y) * scaleFactor;

                var adjustedX = nosePosition_ImageSpace.x - boxWidth / 2f;
                var adjustedY = nosePosition_ImageSpace.y - boxHeight / 2f;
                var texturePosition = new Vector2(adjustedX, adjustedY);
                var textureSize = new Vector2(boxWidth, boxHeight);

                CropAndResizeFace(texture, texturePosition, textureSize);
            }
            else
            {
                if (BoundingBoxes)
                {
                    // No faces detected, disable previews
                    foreach (var preview in facePreviews)
                    {
                        preview.SetActive(false);
                    }
                }
            }

#if UNITY_2023_2_OR_NEWER
                        await Awaitable.NextFrameAsync();
#else
            await Task.Yield();

#endif
        }
        #endregion

        #region Helper Methods
        private float2x3 CreateTransformationMatrix(Texture texture, int size, float scale)
        {
            return BlazeUtils.mul(
                BlazeUtils.TranslationMatrix(0.5f * (new Vector2(texture.width, texture.height) + new Vector2(-size, size))),
                BlazeUtils.ScaleMatrix(new Vector2(scale, -scale)));
        }

        Vector3 ImageToWorld(Vector2 position)
        {
            return (position - 0.5f * new Vector2(m_TextureWidth, m_TextureHeight)) / m_TextureHeight;
        }
        #endregion

        #region Face Processing Methods
        void CropAndResizeFace(Texture texture, Vector2 position, Vector2 size)
        {
            Texture2D inputTexture = texture as Texture2D;
            if (inputTexture == null && texture is WebCamTexture webcamTex)
            {
                inputTexture = new Texture2D(webcamTex.width, webcamTex.height, TextureFormat.RGBA32, false);
                inputTexture.SetPixels(webcamTex.GetPixels());
                inputTexture.Apply();
            }

            if (inputTexture == null)
            {
                Debug.LogError("Input texture could not be converted to Texture2D.");
                return;
            }

            int x = Mathf.Clamp(Mathf.FloorToInt(position.x), 0, inputTexture.width - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt(position.y), 0, inputTexture.height - 1);
            int width = Mathf.Clamp(Mathf.FloorToInt(size.x), 1, inputTexture.width - x);
            int height = Mathf.Clamp(Mathf.FloorToInt(size.y), 1, inputTexture.height - y);

            if (width <= 0 || height <= 0)
            {
                Debug.LogWarning("Invalid crop dimensions. Width and height must be greater than zero.");
                return;
            }

            Color[] croppedPixels = inputTexture.GetPixels(x, y, width, height);
            Texture2D croppedTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            croppedTexture.SetPixels(croppedPixels);
            croppedTexture.Apply();

            Texture2D resizedTexture = new Texture2D(ExportCropWidth, ExportCropHeight, TextureFormat.RGBA32, false);
            for (int i = 0; i < ExportCropHeight; i++)
            {
                for (int j = 0; j < ExportCropWidth; j++)
                {
                    float u = j / (float)ExportCropWidth;
                    float v = i / (float)ExportCropHeight;
                    Color color = croppedTexture.GetPixelBilinear(u, v);
                    resizedTexture.SetPixel(j, i, color);
                }
            }
            resizedTexture.Apply();
            Destroy(croppedTexture);

            // Update the PreviewCropPlane only if CropFacePreview is enabled
            if (CropFacePreview && PreviewCropPlane != null)
            {
                var currentTexture = PreviewCropPlane.texture;
                if (currentTexture != null)
                {
                    Destroy(currentTexture);
                }
                PreviewCropPlane.texture = resizedTexture;
            }

            // Store the processed crop for further analysis, regardless of CropFacePreview setting
            OutputCrop = resizedTexture.GetPixels32();
            croppedFaceData = OutputCrop;

            IsFaceDetectionRunning = true;
        }
        #endregion

        #region Cleanup Methods
        void OnDestroy()
        {

#if UNITY_2023_2_OR_NEWER
    m_DetectAwaitable?.Cancel(); // Cancel Awaitable task explicitly
#else
            if (m_DetectTask != null && !m_DetectTask.IsCompleted)
            {
                _cancellationTokenSource.Cancel(); // Notify the task to stop via the token
            }
#endif
            IsFaceDetectionRunning = false;

            // Cancel any ongoing cancellation tokens
            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = null;
            }

            // Dispose the preview texture if it exists
            if (PreviewCropPlane != null && PreviewCropPlane.texture != null)
            {
                Destroy(PreviewCropPlane.texture);
                PreviewCropPlane.texture = null;
            }
        }
        #endregion
    }
}

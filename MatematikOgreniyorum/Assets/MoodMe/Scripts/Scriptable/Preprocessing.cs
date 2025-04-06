using System.Linq;
using Unity.Burst;
// using Unity.VisualScripting;
using UnityEngine;

namespace MoodMe
{
    public class Preprocessing
    {
        public enum ValueType
        {
            Color32,
            Linear,
            LinearNormalized
        }

        public enum OrientationType
        {
            Source,
            CW90,
            Upsidedown,
            ACW90,
            XMirrored,
            YMirrored
        }

        public static Color32[] InputImage, OutputImage;
        public TextureFormat InputFormat, OutputFormat;

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast)]
        private static void Preprocess32(int InputWidth, int InputHeight, int OutputWidth, int OutputHeight, OrientationType OutputOrientation)
        {
            if (InputImage == null)
            {
                Debug.LogError("InputImage is not initialized. Ensure it is assigned before preprocessing.");
                return;
            }

            Debug.Log($"Starting Preprocess32 with InputWidth={InputWidth}, InputHeight={InputHeight}, OutputWidth={OutputWidth}, OutputHeight={OutputHeight}, Orientation={OutputOrientation}");


            if ((InputWidth == OutputWidth) && (InputHeight == OutputHeight) && (OutputOrientation == OrientationType.Source)) return;

            int square = (OutputHeight * OutputWidth);
            OutputImage = new Color32[square];

            Debug.Log($"Initialized OutputImage array with {square} elements.");


            int i = 0;
            float xFactor = InputWidth / (float)OutputWidth;
            float yFactor = InputHeight / (float)OutputHeight;
            Debug.Log($"Scaling factors calculated: _xFactor={xFactor}, _yFactor={yFactor}");


            for (int y = 0; y < OutputHeight; y++)
            {
                for (int x = 0; x < OutputWidth; x++)
                {
                    int srcX = Mathf.FloorToInt(x * xFactor);
                    int srcY = Mathf.FloorToInt(y * yFactor);
                    int pixelIndex = srcY * InputWidth + srcX;

                    switch (OutputOrientation)
                    {
                        case OrientationType.Upsidedown:
                            OutputImage[square - i - 1] = InputImage[pixelIndex];
                            Color32 pixel = InputImage[pixelIndex];

                            // Set alpha to 255
                            OutputImage[i] = new Color32(pixel.r, pixel.g, pixel.b, 255);
                            break;
                        case OrientationType.ACW90:
                            OutputImage[(OutputWidth - x - 1) * OutputHeight + y] = InputImage[pixelIndex];
                            break;
                        case OrientationType.CW90:
                            OutputImage[(OutputWidth - y - 1) + (x * OutputHeight)] = InputImage[pixelIndex];
                            break;
                        default:
                            OutputImage[i] = InputImage[pixelIndex];
                            break;
                    }
                    i++;
                }
            }
            Debug.Log($"Transformed OutputImage sample (first 30 pixels): {string.Join(", ", OutputImage.Take(30).Select(p => $"({p.r}, {p.g}, {p.b}, {p.a})"))}");


        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast)]
        private static float[] PreprocessFloats(int InputWidth, int InputHeight, int OutputWidth, int OutputHeight, TextureFormat OutputFormat, OrientationType OutputOrientation, ValueType OutputType)
        {
            int outputChannels = (OutputFormat == TextureFormat.RGBA32) ? 4 : (OutputFormat == TextureFormat.RGB24) ? 3 : 1;
            float[] outputArray = new float[OutputWidth * OutputHeight * outputChannels];
            Debug.Log($"PreprocessFloats: Initialized _outputArray with {outputChannels} channels.");


            Preprocess32(InputWidth, InputHeight, OutputWidth, OutputHeight, OutputOrientation);


            float normalizer = (OutputType == ValueType.Linear) ? 255f : 128f;
            float offset = (OutputType == ValueType.Linear) ? 0f : 127f;
            Debug.Log($"Using normalizer {normalizer} and offset {offset} for ValueType {OutputType}.");


            int j = 0;
            for (int i = 0; i < OutputImage.Length; i++)
            {
                outputArray[j] = (OutputImage[i].r - offset) / normalizer;   // R
                outputArray[j + 1] = (OutputImage[i].g - offset) / normalizer; // G
                outputArray[j + 2] = (OutputImage[i].b - offset) / normalizer; // B

                // No alpha channel needed for Sentis model input
                j += outputChannels;
            }

            Debug.Log("Preprocessed data (first 10 values in Sentis): " + string.Join(", ", outputArray.Take(10)));

            return outputArray;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast)]
        public static float[] Preprocess(int InputWidth, int InputHeight, int OutputWidth, int OutputHeight, TextureFormat OutputFormat, OrientationType OutputOrientation, ValueType OutputType)
        {
            return PreprocessFloats(InputWidth, InputHeight, OutputWidth, OutputHeight, OutputFormat, OutputOrientation, OutputType);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast)]
        public static Color32[] Preprocess(int InputWidth, int InputHeight, int OutputWidth, int OutputHeight, OrientationType OutputOrientation)
        {
            Preprocess32(InputWidth, InputHeight, OutputWidth, OutputHeight, OutputOrientation);
            return OutputImage;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Burst;
using UnityEngine;

namespace MoodMe
{
    public class FaceDetectorPostProcessing
    {
        [Serializable]
        public struct FaceInfo
        {
            public float x1;
            public float y1;
            public float x2;
            public float y2;
            public float score;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast)]
        public static List<FaceInfo> Predict(int width, int height, float[] scores, float[] boxes, float threshold, float iou_threshold = 0.3f, int top_k = -1)
        {
            //Debug.Log("Starting Predict function...");
            //Debug.Log("Max Score" + scores.Max());
            //Debug.Log("detection threshold" + threshold);

            List<FaceInfo> result_picked_box = new List<FaceInfo>();
            List<float> probs = new List<float>();
            List<bool> mask = new List<bool>();
            List<float> newScore = new List<float>();

            // Filtering scores above the threshold
            for (int i = 0; i < scores.Length; i++)
            {
                //Debug.Log(scores[i]);
                if (i % 2 == 1)
                {
                    newScore.Add(scores[i]);
                  //Debug.Log(scores[i]);   
                    if (scores[i] > .2f)
                    {
                        Debug.Log("score detected over thresh: " + scores[i]);
                        probs.Add(scores[i]);
                        mask.Add(true);
                    }
                    else
                    {
                        mask.Add(false);
                    }
                }
            }

            Debug.Log($"Predict: Number of scores above threshold: {probs.Count}");

            if (probs.Count == 0)
            {
                Debug.LogWarning("Predict: No scores above threshold, returning empty list.");
                return result_picked_box; // Exit if no valid scores
            }

            // Initialize box_probs array
            float[,] box_probs = new float[probs.Count, 5];
            int k = 0;
            for (int j = 0; j < boxes.Length / 4; j++)
            {
                if (mask[j] == true)
                {
                    box_probs[k, 0] = boxes[(j * 4) + 0];
                    box_probs[k, 1] = boxes[(j * 4) + 1];
                    box_probs[k, 2] = boxes[(j * 4) + 2];
                    box_probs[k, 3] = boxes[(j * 4) + 3];
                    box_probs[k, 4] = probs[k];
                    k++;
                }
            }

            Debug.Log($"Predict: Number of boxes after threshold filtering: {k}");

            List<List<float>> newBox_probs = HardNMS(box_probs, iou_threshold, top_k);
            Debug.Log($"Predict: Number of boxes after NMS: {newBox_probs.Count}");

            foreach (var box in newBox_probs)
            {
                box[0] *= width;
                box[1] *= height;
                box[2] *= width;
                box[3] *= height;

                result_picked_box.Add(new FaceInfo
                {
                    x1 = box[0],
                    y1 = box[1],
                    x2 = box[2],
                    y2 = box[3],
                    score = box[4]
                });
            }

            Debug.Log($"Predict: Final number of picked boxes: {result_picked_box.Count}");
            return result_picked_box;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast)]
        public static List<List<float>> HardNMS(float[,] box_probs, float iou_threshold, int top_k = -1, int candidate_size = 200)
        {
            Debug.Log("Starting HardNMS...");

            List<float> scores = new List<float>();
            List<int> picked = new List<int>();
            List<List<float>> boxes = new List<List<float>>();
            List<List<float>> result = new List<List<float>>();

            for (int i = 0; i < box_probs.GetLength(0); i++)
            {
                boxes.Add(new List<float> { box_probs[i, 0], box_probs[i, 1], box_probs[i, 2], box_probs[i, 3] });
                scores.Add(box_probs[i, 4]);
            }

            Debug.Log($"HardNMS: Initial number of boxes: {boxes.Count}");

            var indexes = scores
                .Select((score, index) => new KeyValuePair<float, int>(score, index))
                .OrderByDescending(x => x.Key)
                .Select(x => x.Value)
                .ToList();

            while (indexes.Count > 0)
            {
                List<int> newIndexes = new List<int>();
                int current = indexes[0];
                picked.Add(current);

                Debug.Log($"HardNMS: Picking box index {current} with score {scores[current]}");

                if (top_k > 0 && picked.Count >= top_k) break;

                var current_box = boxes[current];
                indexes.RemoveAt(0);

                var rest_boxes = indexes.Select(idx => boxes[idx]).ToList();
                var iouValues = IouOf(rest_boxes, new List<List<float>> { current_box });

                for (int i = 0; i < indexes.Count; i++)
                {
                    if (iouValues[i] <= iou_threshold)
                    {
                        newIndexes.Add(indexes[i]);
                    }
                }

                indexes = newIndexes;
                Debug.Log($"HardNMS: Remaining boxes after IoU filtering: {indexes.Count}");
            }

            foreach (int idx in picked)
            {
                result.Add(new List<float>
                {
                    box_probs[idx, 0],
                    box_probs[idx, 1],
                    box_probs[idx, 2],
                    box_probs[idx, 3],
                    box_probs[idx, 4]
                });
            }

            Debug.Log($"HardNMS: Final picked boxes count: {result.Count}");
            return result;
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast)]
        public static List<float> IouOf(List<List<float>> box0, List<List<float>> box1, float eps = 1E-5f)
        {
            Debug.Log("Starting IoU calculations...");

            List<float> iouResults = new List<float>();
            for (int i = 0; i < box0.Count; i++)
            {
                var leftTop = new List<float>
                {
                    Math.Max(box0[i][0], box1[0][0]),
                    Math.Max(box0[i][1], box1[0][1])
                };

                var rightBottom = new List<float>
                {
                    Math.Min(box0[i][2], box1[0][2]),
                    Math.Min(box0[i][3], box1[0][3])
                };

                var overlapArea = AreaOf(new List<List<float>> { leftTop }, new List<List<float>> { rightBottom })[0];
                var area0 = AreaOf(new List<List<float>> { box0[i].Take(2).ToList() }, new List<List<float>> { box0[i].Skip(2).ToList() })[0];
                var area1 = AreaOf(new List<List<float>> { box1[0].Take(2).ToList() }, new List<List<float>> { box1[0].Skip(2).ToList() })[0];

                float iou = overlapArea / (area0 + area1 - overlapArea + eps);
                iouResults.Add(iou);

                Debug.Log($"IoU: Box {i} with IoU value {iou}");
            }

            return iouResults;
        }

        public static List<float> AreaOf(List<List<float>> left_top, List<List<float>> right_bottom)
        {
            List<float> areaResults = new List<float>();

            for (int i = 0; i < left_top.Count; i++)
            {
                float width = Math.Max(0, right_bottom[i][0] - left_top[i][0]);
                float height = Math.Max(0, right_bottom[i][1] - left_top[i][1]);
                areaResults.Add(width * height);

                Debug.Log($"AreaOf: width={width}, height={height}, area={width * height}");
            }

            return areaResults;
        }
    }
}
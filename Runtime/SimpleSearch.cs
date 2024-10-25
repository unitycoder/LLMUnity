using System;
using System.Collections.Generic;
using System.Linq;
using System.IO.Compression;
using UnityEngine;

namespace LLMUnity
{
    [DefaultExecutionOrder(-2)]
    public class SimpleSearch : SearchMethod
    {
        protected SortedDictionary<int, float[]> embeddings = new SortedDictionary<int, float[]>();
        protected Dictionary<int, List<(int, float)>> incrementalSearchCache = new Dictionary<int, List<(int, float)>>();

        protected override void AddInternal(int key, float[] embedding)
        {
            embeddings[key] = embedding;
        }

        protected override void RemoveInternal(int key)
        {
            embeddings.Remove(key);
        }

        public static float DotProduct(float[] vector1, float[] vector2)
        {
            if (vector1.Length != vector2.Length)
            {
                throw new ArgumentException("Vector lengths must be equal for dot product calculation");
            }
            float result = 0;
            for (int i = 0; i < vector1.Length; i++)
            {
                result += vector1[i] * vector2[i];
            }
            return result;
        }

        public static float InverseDotProduct(float[] vector1, float[] vector2)
        {
            return 1 - DotProduct(vector1, vector2);
        }

        public static float[] InverseDotProduct(float[] vector1, float[][] vector2)
        {
            float[] results = new float[vector2.Length];
            for (int i = 0; i < vector2.Length; i++)
            {
                results[i] = InverseDotProduct(vector1, vector2[i]);
            }
            return results;
        }

        protected override (int[], float[]) SearchInternal(float[] embedding, int k)
        {
            float[] unsortedDistances = InverseDotProduct(embedding, embeddings.Values.ToArray());
            var sortedLists = embeddings.Keys.Zip(unsortedDistances, (first, second) => new { First = first, Second = second })
                .OrderBy(item => item.Second)
                .ToList();
            int kmax = k == -1 ? sortedLists.Count : Math.Min(k, sortedLists.Count);
            int[] results = new int[kmax];
            float[] distances = new float[kmax];
            for (int i = 0; i < kmax; i++)
            {
                results[i] = sortedLists[i].First;
                distances[i] = sortedLists[i].Second;
            }
            return (results, distances);
        }

        public override int IncrementalSearch(float[] embedding)
        {
            int key = nextIncrementalSearchKey++;
            float[] unsortedDistances = InverseDotProduct(embedding, embeddings.Values.ToArray());
            incrementalSearchCache[key] = embeddings.Keys.Zip(unsortedDistances, (first, second) => (first, second))
                .OrderBy(item => item.second)
                .ToList();
            return key;
        }

        public override (int[], float[], bool) IncrementalFetchKeys(int fetchKey, int k)
        {
            if (!incrementalSearchCache.ContainsKey(fetchKey)) throw new Exception($"There is no IncrementalSearch cached with this key: {fetchKey}");

            bool completed;
            List<(int, float)> sortedLists;
            if (k == -1)
            {
                sortedLists = incrementalSearchCache[fetchKey];
                completed = true;
            }
            else
            {
                sortedLists = incrementalSearchCache[fetchKey].GetRange(0, k);
                incrementalSearchCache[fetchKey].RemoveRange(0, k);
                completed = incrementalSearchCache[fetchKey].Count == 0;
            }
            if (completed) IncrementalSearchComplete(fetchKey);

            int[] results = new int[sortedLists.Count];
            float[] distances = new float[sortedLists.Count];
            for (int i = 0; i < sortedLists.Count; i++)
            {
                results[i] = sortedLists[i].Item1;
                distances[i] = sortedLists[i].Item2;
            }
            return (results.ToArray(), distances.ToArray(), completed);
        }

        public override void IncrementalSearchComplete(int fetchKey)
        {
            incrementalSearchCache.Remove(fetchKey);
        }

        protected override void ClearInternal()
        {
            embeddings.Clear();
            incrementalSearchCache.Clear();
        }

        protected override void SaveInternal(ZipArchive archive)
        {
            ArchiveSaver.Save(archive, embeddings, "SimpleSearch_embeddings");
            ArchiveSaver.Save(archive, incrementalSearchCache, "SimpleSearch_incrementalSearchCache");
        }

        protected override void LoadInternal(ZipArchive archive)
        {
            embeddings = ArchiveSaver.Load<SortedDictionary<int, float[]>>(archive, "SimpleSearch_embeddings");
            incrementalSearchCache = ArchiveSaver.Load<Dictionary<int, List<(int, float)>>>(archive, "SimpleSearch_incrementalSearchCache");
        }
    }
}
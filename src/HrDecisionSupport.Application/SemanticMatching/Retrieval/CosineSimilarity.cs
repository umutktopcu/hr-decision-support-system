using System;

namespace HrDecisionSupport.Application.SemanticMatching.Retrieval;

public static class CosineSimilarity
{
    /// <summary>
    /// Calculates the cosine similarity between two vectors.
    /// Returns a value between -1.0 and 1.0.
    /// Throws ArgumentException if vectors have mismatched dimensions, are empty, contain NaN/Infinity, or have zero magnitude.
    /// </summary>
    public static double Calculate(float[] vectorA, float[] vectorB)
    {
        if (vectorA == null || vectorB == null)
            throw new ArgumentNullException(vectorA == null ? nameof(vectorA) : nameof(vectorB));

        if (vectorA.Length == 0 || vectorB.Length == 0)
            throw new ArgumentException("Vectors cannot be empty.");

        if (vectorA.Length != vectorB.Length)
            throw new ArgumentException($"Dimension mismatch: {vectorA.Length} vs {vectorB.Length}");

        double dotProduct = 0.0;
        double magnitudeA = 0.0;
        double magnitudeB = 0.0;

        for (int i = 0; i < vectorA.Length; i++)
        {
            float a = vectorA[i];
            float b = vectorB[i];

            if (float.IsNaN(a) || float.IsInfinity(a))
                throw new ArgumentException($"vectorA contains invalid value at index {i}: {a}");

            if (float.IsNaN(b) || float.IsInfinity(b))
                throw new ArgumentException($"vectorB contains invalid value at index {i}: {b}");

            double aD = a;
            double bD = b;

            dotProduct += aD * bD;
            magnitudeA += aD * aD;
            magnitudeB += bD * bD;
        }

        magnitudeA = Math.Sqrt(magnitudeA);
        magnitudeB = Math.Sqrt(magnitudeB);

        if (magnitudeA == 0.0 || magnitudeB == 0.0)
        {
            throw new ArgumentException("Vectors cannot have zero magnitude.");
        }

        var result = dotProduct / (magnitudeA * magnitudeB);
        
        // Clamp to [-1.0, 1.0] to handle floating point inaccuracies
        return Math.Clamp(result, -1.0, 1.0);
    }
}

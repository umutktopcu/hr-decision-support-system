using System;
using HrDecisionSupport.Application.SemanticMatching.Retrieval;
using Xunit;

namespace HrDecisionSupport.Tests.Application.SemanticMatching.Retrieval;

public class CosineSimilarityTests
{
    [Fact]
    public void Calculate_IdenticalVectors_ReturnsOne()
    {
        var vecA = new float[] { 1f, 0f };
        var vecB = new float[] { 1f, 0f };

        var result = CosineSimilarity.Calculate(vecA, vecB);

        Assert.Equal(1.0, result, 5);
    }

    [Fact]
    public void Calculate_OrthogonalVectors_ReturnsZero()
    {
        var vecA = new float[] { 1f, 0f };
        var vecB = new float[] { 0f, 1f };

        var result = CosineSimilarity.Calculate(vecA, vecB);

        Assert.Equal(0.0, result, 5);
    }

    [Fact]
    public void Calculate_OppositeVectors_ReturnsNegativeOne()
    {
        var vecA = new float[] { 1f, 0f };
        var vecB = new float[] { -1f, 0f };

        var result = CosineSimilarity.Calculate(vecA, vecB);

        Assert.Equal(-1.0, result, 5);
    }

    [Fact]
    public void Calculate_NonNormalizedSameDirection_ReturnsOne()
    {
        var vecA = new float[] { 2f, 0f };
        var vecB = new float[] { 10f, 0f };

        var result = CosineSimilarity.Calculate(vecA, vecB);

        Assert.Equal(1.0, result, 5);
    }

    [Fact]
    public void Calculate_ZeroVector_ThrowsArgumentException()
    {
        var vecA = new float[] { 1f, 1f };
        var vecB = new float[] { 0f, 0f };

        Assert.Throws<ArgumentException>(() => CosineSimilarity.Calculate(vecA, vecB));
    }

    [Fact]
    public void Calculate_DimensionMismatch_ThrowsArgumentException()
    {
        var vecA = new float[] { 1f, 0f };
        var vecB = new float[] { 1f, 0f, 0f };

        Assert.Throws<ArgumentException>(() => CosineSimilarity.Calculate(vecA, vecB));
    }

    [Fact]
    public void Calculate_EmptyVectors_ThrowsArgumentException()
    {
        var vecA = Array.Empty<float>();
        var vecB = Array.Empty<float>();

        Assert.Throws<ArgumentException>(() => CosineSimilarity.Calculate(vecA, vecB));
    }

    [Fact]
    public void Calculate_VectorContainsNaN_ThrowsArgumentException()
    {
        var vecA = new float[] { float.NaN, 0f };
        var vecB = new float[] { 1f, 0f };

        Assert.Throws<ArgumentException>(() => CosineSimilarity.Calculate(vecA, vecB));
    }

    [Fact]
    public void Calculate_VectorContainsInfinity_ThrowsArgumentException()
    {
        var vecA = new float[] { float.PositiveInfinity, 0f };
        var vecB = new float[] { 1f, 0f };

        Assert.Throws<ArgumentException>(() => CosineSimilarity.Calculate(vecA, vecB));
    }

    [Fact]
    public void Calculate_ClampBounds_PreventsFloatingPointOverflow()
    {
        // For identical vectors, floating point rounding might occasionally produce 1.0000000000000002.
        // We simulate a case that would cause slight inaccuracies if floats were kept at float precision.
        // The implementation already clamps to [-1.0, 1.0].
        var vecA = new float[] { 0.1f, 0.2f, 0.3f };
        var vecB = new float[] { 0.1f, 0.2f, 0.3f };
        
        var result = CosineSimilarity.Calculate(vecA, vecB);
        
        Assert.InRange(result, -1.0, 1.0);
        Assert.Equal(1.0, result, 10);
    }
}

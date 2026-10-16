using System;

using Shouldly;

using TurboTurbo.Runtime.Environment;

using Xunit;

namespace TurboTurboTests
{
    public class BuiltinWindProfileTests
    {
        [Fact]
        public void Evaluate_StaysWithinRange()
        {
            for (var t = 0f; t < 1000f; t += 0.37f)
            {
                var speed = BuiltinWindProfile.Evaluate(t);

                float.IsNaN(speed).ShouldBeFalse();
                speed.ShouldBeGreaterThanOrEqualTo(0f);
                speed.ShouldBeLessThanOrEqualTo(BuiltinWindProfile.MaxSpeed + 1e-4f);
            }
        }

        [Theory]
        [InlineData(0.0f, 1.0f)]
        [InlineData(1.0f, 2.0f)]
        [InlineData(2.0f, 3.0f)]
        [InlineData(3.0f, 4.0f)]
        [InlineData(4.0f, 5.0f)]
        public void Evaluate_FollowsTruncatedRayleighDistribution(float lo, float hi)
        {
            const int sampleCount = 200000;
            var scale = BuiltinWindProfile.ReferenceMeanSpeed * 2.0 / Math.Sqrt(Math.PI);
            double Cdf(double v) => 1.0 - Math.Exp(-(v / scale) * (v / scale));

            var inRange = 0;
            for (var i = 0; i < sampleCount; i++)
            {
                // a step that doesn't line up with either noise layer
                var speed = BuiltinWindProfile.Evaluate(i * 7.31f);
                if (speed >= lo && speed < hi) inRange++;
            }

            var expected = (Cdf(hi) - Cdf(lo)) / Cdf(BuiltinWindProfile.MaxSpeed);
            ((double)inRange / sampleCount).ShouldBe(expected, tolerance: 0.015);
        }

        [Fact]
        public void Evaluate_IsSmooth()
        {
            // the wind drifts; neighbouring samples must not jump
            var previous = BuiltinWindProfile.Evaluate(0f);
            for (var t = 0.1f; t < 500f; t += 0.1f)
            {
                var speed = BuiltinWindProfile.Evaluate(t);

                Math.Abs(speed - previous).ShouldBeLessThan(0.2f);
                previous = speed;
            }
        }

        [Theory]
        [InlineData(0f, 0f, 0f, -1f)]
        [InlineData(90f, -1f, 0f, 0f)]
        [InlineData(180f, 0f, 0f, 1f)]
        [InlineData(270f, 1f, 0f, 0f)]
        public void WorldDirection_MatchesGameConvention(float degrees, float x, float y, float z)
        {
            var direction = BuiltinWindProfile.WorldDirection(degrees);

            direction.x.ShouldBe(x, tolerance: 1e-4f);
            direction.y.ShouldBe(y, tolerance: 1e-4f);
            direction.z.ShouldBe(z, tolerance: 1e-4f);
        }

        [Fact]
        public void WorldDirection_IsUnitLength()
        {
            for (var degrees = 0f; degrees < 360f; degrees += 15f)
            {
                BuiltinWindProfile.WorldDirection(degrees).magnitude.ShouldBe(1f, tolerance: 1e-4f);
            }
        }
    }
}

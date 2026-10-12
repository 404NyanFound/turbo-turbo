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
                speed.ShouldBeGreaterThanOrEqualTo(BuiltinWindProfile.MeanSpeed - BuiltinWindProfile.Amplitude - 1e-4f);
                speed.ShouldBeLessThanOrEqualTo(BuiltinWindProfile.MeanSpeed + BuiltinWindProfile.Amplitude + 1e-4f);
            }
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

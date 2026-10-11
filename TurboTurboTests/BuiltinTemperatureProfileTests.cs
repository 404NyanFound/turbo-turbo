using Shouldly;

using TurboTurbo.Runtime.Environment;

using Xunit;

namespace TurboTurboTests
{
    public class BuiltinTemperatureProfileTests
    {
        private const float Mean = BuiltinTemperatureProfile.MeanCelsius;
        private const float Swing = BuiltinTemperatureProfile.SwingCelsius;

        private static float Clear(float hour) =>
            BuiltinTemperatureProfile.Evaluate(hour, cloud: 0f, rain: 0f, wind: 0f, thunder: 0f);

        [Fact]
        public void ClearAtPeak_IsMeanPlusSwing()
        {
            Clear(BuiltinTemperatureProfile.PeakHour).ShouldBe(Mean + Swing, tolerance: 0.01f);
        }

        [Fact]
        public void ClearAtTrough_IsMeanMinusSwing()
        {
            Clear(BuiltinTemperatureProfile.PeakHour - 12f).ShouldBe(Mean - Swing, tolerance: 0.01f);
        }

        [Fact]
        public void ClearSixHoursFromPeak_IsMean()
        {
            Clear(BuiltinTemperatureProfile.PeakHour + 6f).ShouldBe(Mean, tolerance: 0.01f);
            Clear(BuiltinTemperatureProfile.PeakHour - 6f).ShouldBe(Mean, tolerance: 0.01f);
        }

        [Fact]
        public void CloudDampsTheSwing()
        {
            var damped = Swing * (1f - BuiltinTemperatureProfile.CloudSwingDamping);

            BuiltinTemperatureProfile.Evaluate(BuiltinTemperatureProfile.PeakHour, 1f, 0f, 0f, 0f)
                .ShouldBe(Mean + damped, tolerance: 0.01f);
            BuiltinTemperatureProfile.Evaluate(BuiltinTemperatureProfile.PeakHour - 12f, 1f, 0f, 0f, 0f)
                .ShouldBe(Mean - damped, tolerance: 0.01f);
        }

        [Fact]
        public void RainCools()
        {
            BuiltinTemperatureProfile.Evaluate(BuiltinTemperatureProfile.PeakHour, 0f, 1f, 0f, 0f)
                .ShouldBe(Mean + Swing - BuiltinTemperatureProfile.RainCoolingCelsius, tolerance: 0.01f);
        }

        [Fact]
        public void WindCools()
        {
            BuiltinTemperatureProfile.Evaluate(BuiltinTemperatureProfile.PeakHour, 0f, 0f, 1f, 0f)
                .ShouldBe(Mean + Swing - BuiltinTemperatureProfile.WindCoolingCelsius, tolerance: 0.01f);
        }

        [Fact]
        public void WindCoolsMoreWhenRaining()
        {
            var cooling = BuiltinTemperatureProfile.WindCoolingCelsius * (1f + BuiltinTemperatureProfile.WindRainCoupling);

            BuiltinTemperatureProfile.Evaluate(BuiltinTemperatureProfile.PeakHour, 0f, 1f, 1f, 0f)
                .ShouldBe(Mean + Swing - BuiltinTemperatureProfile.RainCoolingCelsius - cooling, tolerance: 0.01f);
        }

        [Fact]
        public void ThunderCools()
        {
            BuiltinTemperatureProfile.Evaluate(BuiltinTemperatureProfile.PeakHour, 0f, 0f, 0f, 1f)
                .ShouldBe(Mean + Swing - BuiltinTemperatureProfile.ThunderCoolingCelsius, tolerance: 0.01f);
        }

        [Fact]
        public void InputsAreClamped()
        {
            BuiltinTemperatureProfile.Evaluate(BuiltinTemperatureProfile.PeakHour, 2f, 2f, 2f, 2f)
                .ShouldBe(BuiltinTemperatureProfile.Evaluate(BuiltinTemperatureProfile.PeakHour, 1f, 1f, 1f, 1f),
                    tolerance: 0.01f);

            BuiltinTemperatureProfile.Evaluate(BuiltinTemperatureProfile.PeakHour, -1f, -1f, -1f, -1f)
                .ShouldBe(BuiltinTemperatureProfile.Evaluate(BuiltinTemperatureProfile.PeakHour, 0f, 0f, 0f, 0f),
                    tolerance: 0.01f);
        }
    }
}

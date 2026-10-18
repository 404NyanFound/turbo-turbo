using TurboTurbo;

namespace TestBench
{
    public static class SpoolMode
    {
        public static void Run(bool once, float rampS)
        {
            var options = new SpoolBench.Options { RampS = rampS };
            options.Validate();
            var bench = new SpoolBench(options);
            var ambientK = PhysicsConstants.ReferenceAmbientK;

            var fields = new BenchScreen.Field[]
            {
                new BenchScreen.Field
                {
                    Label = "Ramp time [s]",
                    Description = "Time for the engine to accelerate from idle to rated speed while the governor holds " +
                                  "the rack fully open. Smaller steps take proportionally less time.",
                    Default = SpoolBench.Options.DefaultRampS,
                    Get = () => options.RampS,
                    Set = v => options.RampS = v
                },
                new BenchScreen.Field
                {
                    Label = "Spool scale",
                    Description = "Multiplier on each profile's spool-up time constant.",
                    Default = options.SpoolScale,
                    Get = () => options.SpoolScale,
                    Set = v => options.SpoolScale = v
                },
            };

            new BenchScreen("TurboTurbo spool TestBench", fields,
                _ => bench.Report(ambientK), () => options.Validate()).Run(once);
        }
    }
}

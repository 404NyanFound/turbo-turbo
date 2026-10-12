using UnityEngine;

namespace TurboTurbo.WorkBench
{
    public struct ExhaustSignalSet
    {
        public float Lambda;

        public float RpmNorm;

        public float ExhaustEnergy;

        public float Vapour;

        public float MassFlow;

        public float GasDensity;

        public bool EngineOn;

        public Vector3 LocoVelocity;

        public Vector3 Wind;

        public void Apply(SmokeParticles smoke, ShimmerParticles shimmer)
        {
            if (smoke != null)
            {
                smoke.lambda = Lambda;
                smoke.rpmNorm = RpmNorm;
                smoke.exhaustEnergy = ExhaustEnergy;
                smoke.vapour = Vapour;
                smoke.massFlow = MassFlow;
                smoke.gasDensity = GasDensity;
                smoke.engineOn = EngineOn;
                smoke.locoVelocity = LocoVelocity;
                smoke.windVelocity = Wind;
            }

            if (shimmer != null)
            {
                shimmer.enabled = EngineOn;
                if (EngineOn)
                {
                    shimmer.SetFlow(ExhaustEnergy);
                    shimmer.massFlow = MassFlow;
                    shimmer.gasDensity = GasDensity;
                    shimmer.locoVelocity = LocoVelocity;
                    shimmer.windVelocity = Wind;
                }
            }
        }
    }
}

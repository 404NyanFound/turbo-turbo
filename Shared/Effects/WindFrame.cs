using System.Collections.Generic;

using UnityEngine;

namespace TurboTurbo.Effects
{
    /// <summary>
    /// A reference frame that follows the ambient wind. Intended for use with particle systems that should be affected
    /// by wind drag. Runs before the default execution order to ensure the frame updates before emitters emit particles
    /// into it.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class WindFrame : MonoBehaviour
    {
        /// <summary>Squared offset [m] from the parent at which the frame is moved back to the parent's origin.</summary>
        private const float RecenterDistanceSquared = 5000f * 5000f;

        /// <summary>World wind velocity [m/s] the frame drifts with.</summary>
        public Vector3 wind;

        private readonly HashSet<ParticleSystem> _systems = new HashSet<ParticleSystem>();
        private ParticleSystem.Particle[] _buffer;

        /// <summary>Distance [m] the frame has drifted from its parent's origin since it last re-centred.</summary>
        public Vector3 Offset => transform.localPosition;

        /// <summary>Creates a frame at the origin of <paramref name="parent"/>, or of the world if null.</summary>
        public static WindFrame Create(Transform parent)
        {
            var go = new GameObject("WindFrame");
            go.transform.SetParent(parent, worldPositionStays: false);
            return go.AddComponent<WindFrame>();
        }

        /// <summary>
        /// Simulates <paramref name="ps"/> in this frame and tracks it, so re-centering keeps its particles in place.
        /// Binding an already bound system has no further effect.
        /// </summary>
        public void Bind(ParticleSystem ps)
        {
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Custom;
            main.customSimulationSpace = transform;

            _systems.Add(ps);
        }

        /// <summary>Stops tracking <paramref name="ps"/>.</summary>
        public void Unbind(ParticleSystem ps)
        {
            _systems.Remove(ps);
        }

        /// <summary>Converts a world position into this frame, as manually emitted particles require.</summary>
        public Vector3 ToSimPosition(Vector3 worldPosition)
        {
            return transform.InverseTransformPoint(worldPosition);
        }

        /// <summary>
        /// Velocity [m/s] relative to the air for a world velocity. Since the frame moves with the air and is not
        /// rotated, this is also the velocity in this frame.
        /// </summary>
        public Vector3 AirVelocity(Vector3 worldVelocity)
        {
            return worldVelocity - wind;
        }

        private void Update()
        {
            transform.position += wind * Time.deltaTime;

            if (Offset.sqrMagnitude > RecenterDistanceSquared)
            {
                Recenter(Offset);
            }
        }

        private void Recenter(Vector3 offset)
        {
            transform.localPosition = Vector3.zero;

            _systems.RemoveWhere(s => s == null);
            foreach (var ps in _systems)
            {
                var max = ps.main.maxParticles;
                if (_buffer == null || _buffer.Length < max) _buffer = new ParticleSystem.Particle[max];

                var n = ps.GetParticles(_buffer);
                for (var j = 0; j < n; j++)
                {
                    _buffer[j].position += offset;
                }
                ps.SetParticles(_buffer, n);
            }
        }
    }
}

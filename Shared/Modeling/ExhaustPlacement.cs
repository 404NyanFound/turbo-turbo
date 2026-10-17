using UnityEngine;

namespace TurboTurbo.Modeling
{
    public static class ExhaustPlacement
    {
        /// <summary>Parents an emitter to the car and orients it for smoke emission.</summary>
        public static void AttachTo(Transform emitter, Transform parent)
        {
            emitter.SetParent(parent, worldPositionStays: false);
            emitter.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        }

        public static void PlaceAt(Transform emitter, Vector3 exhaustPosition, Transform parent, Vector3 offset)
        {
            // probably not the easiest way, but hey, it seems to work even under the heaviest of derailments.
            // if ever you wanted to test if the exhaust emits in the right direction even when the loco is upside down
            // boy have I got you covered
            AttachTo(emitter, parent);
            emitter.localPosition = parent.InverseTransformPoint(exhaustPosition) + offset;
        }
    }
}

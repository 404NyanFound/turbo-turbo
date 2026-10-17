using System;
using System.Globalization;

using UnityEngine;

namespace TurboTurbo.Modeling
{
    internal static class ColorHex
    {
        /// <summary>
        /// Parses a hex colour into a Unity <see cref="Color"/>.
        /// Returns magenta if the colour cannot be parsed.
        /// </summary>
        public static Color Parse(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return Color.magenta;

            if (hex[0] == '#') hex = hex.Substring(1);
            if ((hex.Length != 6 && hex.Length != 8)
                || !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
            {
                return Color.magenta;
            }

            if (hex.Length == 6)
            {
                return new Color(((value >> 16) & 0xFF) / 255f, ((value >> 8) & 0xFF) / 255f,
                    (value & 0xFF) / 255f, 1f);
            }

            return new Color(((value >> 24) & 0xFF) / 255f, ((value >> 16) & 0xFF) / 255f,
                ((value >> 8) & 0xFF) / 255f, (value & 0xFF) / 255f);
        }

        public static string Format(Color color)
        {
            var r = (byte)Math.Round(Mathf.Clamp(color.r, 0f, 1f) * 255f);
            var g = (byte)Math.Round(Mathf.Clamp(color.g, 0f, 1f) * 255f);
            var b = (byte)Math.Round(Mathf.Clamp(color.b, 0f, 1f) * 255f);
            var a = (byte)Math.Round(Mathf.Clamp(color.a, 0f, 1f) * 255f);
            return $"{r:X2}{g:X2}{b:X2}{a:X2}";
        }
    }
}

// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Windows.Media;

namespace Microsoft.Common.Wpf.Media {
    public static class ColorExtensions {
        public static float GetBrightness(this Color color) {
            return color.GetDrawingColor().GetBrightness();
        }

        public static float GetHue(this Color color) {
            return color.GetDrawingColor().GetHue();
        }

        public static float GetSaturation(this Color color) {
            return color.GetDrawingColor().GetSaturation();
        }

        private static System.Drawing.Color GetDrawingColor(this Color color) => System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B);
    }
}

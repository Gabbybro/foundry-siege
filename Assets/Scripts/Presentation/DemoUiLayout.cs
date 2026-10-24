using UnityEngine;

namespace SiegeDemo
{
    // Screen-space contract in GUI (top-left) coordinates, independent of OnGUI event order.
    internal static class DemoUiLayout
    {
        internal static Rect BuildStrip(float width) => new Rect(16, 111, Mathf.Max(160, width - 356), 34);
        internal static Rect ActivePanel(float width) => new Rect(width - 324, 111, 308, 72);
        internal static Rect FeedbackPanel(float width) => new Rect((width - Mathf.Min(680, width - 48)) * .5f, 190, Mathf.Min(680, width - 48), 42);
        internal static Rect[] BlockingRects(float width, float height, bool modal, bool hasFeedback)
        {
            if (modal) return new[] { new Rect(0, 0, width, height) };
            var regions = new Rect[hasFeedback ? 6 : 5];
            regions[0] = new Rect(0, 0, width, 110);
            regions[1] = new Rect(0, height - 100, width, 100);
            regions[2] = BuildStrip(width);
            regions[3] = ActivePanel(width);
            regions[4] = new Rect(16, 151, 280, 25);
            if (hasFeedback) regions[5] = FeedbackPanel(width);
            return regions;
        }
    }
}

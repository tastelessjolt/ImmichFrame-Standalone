using Avalonia.Media;
using Avalonia.Media.Imaging;
using System;

namespace ImmichFrame.Models
{
    public class UiImage : IDisposable
    {
        public Bitmap? Image { get; set; }
        public Bitmap? ThumbhashImage { get; set; }
        public Stretch ImageStretch { get; set; }
        public bool ShowDarkGradient { get; set; }
        public bool ShowStretchedThumbhash { get; set; }
        public bool ShowBlurredDuplicate { get; set; }

        public void Dispose()
        {
            Image?.Dispose();
            ThumbhashImage?.Dispose();
        }
    }
}

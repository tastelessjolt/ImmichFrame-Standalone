using Avalonia.Media;
using Avalonia.Media.Imaging;
using System;
using System.Threading;

namespace ImmichFrame.Models
{
    public class UiImage : IDisposable
    {
        private int disposed;

        public Bitmap? Image { get; set; }
        public Bitmap? ThumbhashImage { get; set; }
        public Stretch ImageStretch { get; set; }
        public bool ShowDarkGradient { get; set; }
        public bool ShowStretchedThumbhash { get; set; }
        public bool ShowBlurredDuplicate { get; set; }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
                return;

            var image = Image;
            var thumbhashImage = ThumbhashImage;
            Image = null;
            ThumbhashImage = null;

            image?.Dispose();
            if (!ReferenceEquals(image, thumbhashImage))
                thumbhashImage?.Dispose();
        }
    }
}

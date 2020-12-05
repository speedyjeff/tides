using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Utilities;

namespace wintideclock
{
    public class WritableBitmap
    {
        public WritableBitmap(int width, int height)
        {
            Width = width;
            Height = height;

            // create a bitmap and return the WritableGraphics handle
            UnderlyingImage = new Bitmap(width, height);
            var g = System.Drawing.Graphics.FromImage(UnderlyingImage);
            Canvas = new GraphicsCanvas(null, g, width, height);
        }

        public ICanvas Canvas { get; private set; }

        public int Height { get; private set; }

        public int Width { get; private set; }

        #region internal
        internal Bitmap UnderlyingImage;
        #endregion
    }
}

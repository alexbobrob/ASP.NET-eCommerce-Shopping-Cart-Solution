﻿using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;

namespace Smartstore.Core.Content.Media.Imaging
{
    public class ImageWrapper : Disposable, IImage
    {
        private readonly bool _disposeStream;

        public ImageWrapper(Stream stream, Size size, IImageFormat format, BitDepth bitDepth = BitDepth.Bit24, bool disposeStream = true)
        {
            Guard.NotNull(stream, nameof(stream));
            Guard.NotNull(format, nameof(format));

            InStream = stream;
            Format = format;
            Size = size;
            SourceSize = size;
            BitDepth = bitDepth;
            _disposeStream = disposeStream;
        }

        #region IImageInfo

        /// <inheritdoc/>
        public Size Size { get; }

        /// <inheritdoc/>
        public BitDepth BitDepth { get; set; } = BitDepth.Bit24;

        /// <inheritdoc/>
        public IImageFormat Format { get; set; }

        #endregion

        public Stream InStream { get; }

        /// <inheritdoc/>
        public Size SourceSize { get; set; }

        /// <inheritdoc/>
        public Task<IImage> SaveAsync(string path)
        {
            Guard.NotEmpty(path, nameof(path));

            var di = new DirectoryInfo(Path.GetDirectoryName(path));
            if (!di.Exists)
            {
                di.Create();
            }

            using var stream = File.OpenWrite(path);
            return SaveAsync(stream);
        }

        /// <inheritdoc/>
        public async Task<IImage> SaveAsync(Stream stream)
        {
            Guard.NotNull(stream, nameof(stream));

            if (stream.CanSeek)
            {
                stream.SetLength(0);
            }

            await InStream.CopyToAsync(stream);

            if (stream.CanSeek)
            {
                stream.Position = 0;
            }

            if (InStream.CanSeek)
            {
                InStream.Position = 0;
            }

            return this;
        }

        protected override void OnDispose(bool disposing)
        {
            if (disposing && _disposeStream)
                InStream.Dispose();
        }
    }
}

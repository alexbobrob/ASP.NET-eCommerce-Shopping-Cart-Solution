﻿using System;
using System.Diagnostics;
using System.IO;
using System.Drawing;
using Smartstore.Utilities;
using Smartstore.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Threading.Tasks;
using Smartstore.IO;
using Smartstore.Imaging;

namespace Smartstore.Core.Content.Media.Imaging
{
    public class DefaultImageProcessor : IImageProcessor
    {
        private static long _totalProcessingTime;

        private readonly IEventPublisher _eventPublisher;

        public DefaultImageProcessor(IImageFactory imageFactory, IEventPublisher eventPublisher)
        {
            Factory = imageFactory;
            _eventPublisher = eventPublisher;
        }

        public ILogger Logger { get; set; } = NullLogger.Instance;

        public IImageFactory Factory { get; }

        public async Task<ProcessImageResult> ProcessImageAsync(ProcessImageQuery query, bool disposeOutput = true)
        {
            Guard.NotNull(query, nameof(query));

            ValidateQuery(query);

            var watch = new Stopwatch();
            long len;
            IProcessableImage image = null;

            try
            {
                watch.Start();

                var source = query.Source;

                // Load source
                if (source is byte[] b)
                {
                    using var memStream = new MemoryStream(b);
                    image = await Factory.LoadAsync(memStream);
                    len = b.LongLength;
                }
                else if (source is Stream s)
                {
                    image = await Factory.LoadAsync(s);
                    len = s.Length;
                }
                else if (source is string str)
                {
                    str = NormalizePath(str);
                    image = Factory.Load(str);
                    len = (new FileInfo(str)).Length;
                }
                else if (source is IFile file)
                {
                    using (var fs = file.OpenRead())
                    {
                        image = await Factory.LoadAsync(fs);
                        len = file.Length;
                    }
                }
                else
                {
                    throw new ProcessImageException("Invalid source type '{0}' in query.".FormatInvariant(query.Source.GetType().FullName), query);
                }

                var sourceFormat = image.Format;

                // Pre-process event
                await _eventPublisher.PublishAsync(new ImageProcessingEvent(query, image));

                var result = new ProcessImageResult
                {
                    Query = query,
                    SourceFormat = image.Format,
                    Image = image,
                    DisposeImage = disposeOutput
                };

                // >>>>>> Core processing
                ProcessImageCore(query, image, out var fxApplied);

                result.HasAppliedVisualEffects = fxApplied;

                // Post-process event
                await _eventPublisher.PublishAsync(new ImageProcessedEvent(query, result));

                result.ProcessTimeMs = watch.ElapsedMilliseconds;

                return result;
            }
            catch (Exception ex)
            {
                throw new ProcessImageException(query, ex);
            }
            finally
            {
                if (query.DisposeSource && query.Source is IDisposable source)
                {
                    source.Dispose();
                }

                watch.Stop();
                _totalProcessingTime += watch.ElapsedMilliseconds;
            }
        }

        /// <summary>
        /// Processes the loaded image. Inheritors should NOT save the image, this is done by the caller. 
        /// </summary>
        /// <param name="query">Query</param>
        /// <param name="image">Image instance</param>
        /// <param name="fxApplied">
        /// Should be true if any effect has been applied that potentially changes the image visually (like background color, contrast, sharpness etc.).
        /// Resize and compression quality does NOT count as FX.
        /// </param>
        protected virtual void ProcessImageCore(ProcessImageQuery query, IProcessableImage image, out bool fxApplied)
        {
            bool fxAppliedInternal = false;

            // Resize
            var size = query.MaxWidth != null || query.MaxHeight != null
                ? new Size(query.MaxWidth ?? 0, query.MaxHeight ?? 0)
                : Size.Empty;

            image.Transform(transformer =>
            {
                if (!size.IsEmpty)
                {
                    transformer.Resize(new ResizeOptions
                    {
                        Size = size,
                        Mode = ProcessImageQuery.ConvertScaleMode(query.ScaleMode),
                        Position = ProcessImageQuery.ConvertAnchorPosition(query.AnchorPosition)
                    });
                }

                if (query.BackgroundColor.HasValue())
                {
                    transformer.BackgroundColor(ColorTranslator.FromHtml(query.BackgroundColor));
                    fxAppliedInternal = true;
                }
            });

            fxApplied = fxAppliedInternal;

            // Format
            if (query.Format != null)
            {
                var requestedFormat = query.Format as IImageFormat;

                if (requestedFormat is null && query.Format is string queryFormat)
                {
                    requestedFormat = Factory.FindFormatByExtension(queryFormat.ToLowerInvariant());
                }

                if (requestedFormat != null && requestedFormat.DefaultMimeType != image.Format.DefaultMimeType)
                {
                    image.Format = requestedFormat;
                }
            }

            // Quality
            if (query.Quality.HasValue && image.Format is IJpegFormat jpegFormat)
            {
                // TODO: (core) Apply ProcessImageQuery.Quality to Png and Gif somehow.
                jpegFormat.Quality = query.Quality.Value;
            }
        }

        protected virtual string NormalizePath(string path)
        {
            if (path.IsWebUrl())
            {
                throw new NotSupportedException($"Remote images cannot be processed: Path: {path}");
            }

            if (!PathHelper.IsAbsolutePhysicalPath(path))
            {
                path = CommonHelper.MapPath(path);
            }

            return path;
        }

        private static void ValidateQuery(ProcessImageQuery query)
        {
            if (query.Source == null)
            {
                throw new ArgumentException("During image processing 'ProcessImageQuery.Source' must not be null.", nameof(query));
            }
        }

        public long TotalProcessingTimeMs => _totalProcessingTime;
    }
}
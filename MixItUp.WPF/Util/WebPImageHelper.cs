using MixItUp.Base.Util;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.IO;

namespace MixItUp.WPF.Util
{
    /// <summary>
    /// Transcodes WebP images to PNG so WPF's WIC-based BitmapImage can decode them (Velora serves
    /// all of its emote, badge, and avatar assets as WebP). A representative animation frame is
    /// extracted from the RIFF container and decoded standalone so animated assets retain the
    /// same poster selection as the animation pipeline.
    /// </summary>
    public static class WebPImageHelper
    {
        private const int RiffHeaderLength = 12;

        /// <summary>
        /// Returns PNG-encoded bytes when the input is a WebP image; anything else (including WebP
        /// that fails to decode) is returned unchanged for the normal WPF decode path to handle.
        /// </summary>
        public static byte[] TranscodeToPngIfWebP(byte[] bytes)
        {
            if (!IsWebP(bytes))
            {
                return bytes;
            }

            try
            {
                byte[] staticWebP = ExtractRepresentativeAnimationFrame(bytes) ?? bytes;
                using (SixLabors.ImageSharp.Image image = SixLabors.ImageSharp.Image.Load(staticWebP))
                using (MemoryStream output = new MemoryStream())
                {
                    image.SaveAsPng(output);
                    return output.ToArray();
                }
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "Failed to transcode WebP image to PNG: " + ex);
                return bytes;
            }
            finally
            {
                // See DecodeAnimation: return ImageSharp's pooled buffers rather than letting them
                // sit in the working set until the pool's lazy trim.
                SixLabors.ImageSharp.Configuration.Default.MemoryAllocator.ReleaseRetainedResources();
            }
        }

        public static bool IsWebP(byte[] bytes)
        {
            return bytes != null && bytes.Length > RiffHeaderLength &&
                bytes[0] == (byte)'R' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F' && bytes[3] == (byte)'F' &&
                bytes[8] == (byte)'W' && bytes[9] == (byte)'E' && bytes[10] == (byte)'B' && bytes[11] == (byte)'P';
        }

        /// <summary>
        /// Decodes an animated WebP into ready-to-render premultiplied-BGRA frames, each ANMF frame
        /// decoded STANDALONE on a cleared canvas at its declared offset. This deliberately ignores
        /// the container's blend/dispose flags and background color: Velora's raid emotes declare
        /// full-canvas alpha-blend frames over an opaque-white background, which spec-literal
        /// compositors (AnimatedImage.Wpf via libwebp's anim decoder) accumulate into a white slab,
        /// while each individual frame is a complete picture. Frames are downscaled to
        /// <paramref name="maxDimension"/> to bound memory. Returns null when the bytes are not an
        /// animated WebP or any frame fails to decode (callers fall back to the static pipeline).
        /// </summary>
        public static WebPAnimation DecodeAnimation(byte[] bytes, int maxDimension)
        {
            if (!IsWebP(bytes))
            {
                return null;
            }

            try
            {
                int canvasWidth = 0;
                int canvasHeight = 0;
                List<WebPAnimationFrame> frames = new List<WebPAnimationFrame>();

                int offset = RiffHeaderLength;
                while (offset + 8 <= bytes.Length)
                {
                    string fourCC = ReadFourCC(bytes, offset);
                    int size = ReadInt32LE(bytes, offset + 4);
                    int dataStart = offset + 8;
                    if (size < 0 || dataStart + size > bytes.Length)
                    {
                        return null;
                    }

                    if (fourCC == "VP8 " || fourCC == "VP8L")
                    {
                        // A still image.
                        return null;
                    }

                    if (fourCC == "VP8X" && size >= 10)
                    {
                        canvasWidth = ReadUInt24LE(bytes, dataStart + 4) + 1;
                        canvasHeight = ReadUInt24LE(bytes, dataStart + 7) + 1;
                    }

                    if (fourCC == "ANMF" && size >= 16)
                    {
                        // ANMF stores the frame offset halved and the duration in milliseconds.
                        int frameX = ReadUInt24LE(bytes, dataStart) * 2;
                        int frameY = ReadUInt24LE(bytes, dataStart + 3) * 2;
                        int duration = ReadUInt24LE(bytes, dataStart + 12);

                        byte[] standaloneWebP = BuildStaticWebPFromFrame(bytes, dataStart, size);
                        if (standaloneWebP == null)
                        {
                            return null;
                        }

                        frames.Add(new WebPAnimationFrame()
                        {
                            X = frameX,
                            Y = frameY,
                            // Browsers treat <=10ms as unspecified and play at 100ms; mirror that.
                            DurationMilliseconds = (duration <= 10) ? 100 : duration,
                            StandaloneWebP = standaloneWebP,
                        });
                    }

                    offset = dataStart + size + (size & 1);
                }

                if (frames.Count < 2)
                {
                    return null;
                }

                WebPAnimation animation = new WebPAnimation();
                animation.RepresentativeFrameIndex = SelectRepresentativeFrameIndex(frames);
                foreach (WebPAnimationFrame frame in frames)
                {
                    using (SixLabors.ImageSharp.Image<Rgba32> frameImage = SixLabors.ImageSharp.Image.Load<Rgba32>(frame.StandaloneWebP))
                    {
                        int width = Math.Max(canvasWidth, frame.X + frameImage.Width);
                        int height = Math.Max(canvasHeight, frame.Y + frameImage.Height);

                        // Most frames cover the whole canvas at (0,0); compositing those onto a
                        // separate canvas would only copy the frame, so downscale the decoded frame
                        // in place and allocate a canvas just for partial/offset frames. This keeps
                        // the (large, native-resolution) transients to one image per frame.
                        SixLabors.ImageSharp.Image<Rgba32> composed = frameImage;
                        bool composedOnCanvas = false;
                        try
                        {
                            if (frame.X != 0 || frame.Y != 0 || frameImage.Width != width || frameImage.Height != height)
                            {
                                composed = new SixLabors.ImageSharp.Image<Rgba32>(width, height);
                                composedOnCanvas = true;
                                composed.Mutate(c => c.DrawImage(frameImage, new SixLabors.ImageSharp.Point(frame.X, frame.Y), 1f));
                            }

                            if (Math.Max(width, height) > maxDimension)
                            {
                                double scale = (double)maxDimension / Math.Max(width, height);
                                composed.Mutate(c => c.Resize(Math.Max(1, (int)(width * scale)), Math.Max(1, (int)(height * scale))));
                            }

                            if (animation.Width == 0)
                            {
                                animation.Width = composed.Width;
                                animation.Height = composed.Height;
                            }
                            else if (composed.Width != animation.Width || composed.Height != animation.Height)
                            {
                                composed.Mutate(c => c.Resize(animation.Width, animation.Height));
                            }

                            frame.PremultipliedBgraPixels = ToPremultipliedBgra(composed);
                            frame.StandaloneWebP = null;
                        }
                        finally
                        {
                            if (composedOnCanvas)
                            {
                                composed.Dispose();
                            }
                        }
                    }
                    animation.TotalDurationMilliseconds += frame.DurationMilliseconds;
                }

                animation.Frames = frames;
                return animation;
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, "Failed to decode animated WebP: " + ex);
                return null;
            }
            finally
            {
                // ImageSharp pools its (unmanaged) pixel buffers and only trims them lazily, which
                // reads as hundreds of MB of lingering working set after an emote wall. Decodes here
                // are sporadic, so returning the pool immediately is the better trade.
                SixLabors.ImageSharp.Configuration.Default.MemoryAllocator.ReleaseRetainedResources();
            }
        }

        // WebP has no flagged poster frame, so the freeze/static target defaults to the SECOND-TO-
        // LAST frame: the resting pose of most animations, skipping the final frame in case it is
        // a lead-in back into the loop. When that frame is a near-blank fade (Velora's raid emotes
        // wind down through few-hundred-byte frames vs multi-KB art frames), the most detailed
        // frame is used instead so the emote does not freeze invisible.
        private static int SelectRepresentativeFrameIndex(List<WebPAnimationFrame> frames)
        {
            int largest = 0;
            for (int i = 1; i < frames.Count; i++)
            {
                if (frames[i].StandaloneWebP.Length > frames[largest].StandaloneWebP.Length)
                {
                    largest = i;
                }
            }

            int poster = Math.Max(frames.Count - 2, 0);
            return (frames[poster].StandaloneWebP.Length * 4 >= frames[largest].StandaloneWebP.Length) ? poster : largest;
        }

        // WPF's Pbgra32 expects premultiplied color channels; ImageSharp gives straight alpha.
        private static byte[] ToPremultipliedBgra(SixLabors.ImageSharp.Image<Rgba32> image)
        {
            byte[] pixels = new byte[image.Width * image.Height * 4];
            int i = 0;
            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    foreach (ref Rgba32 pixel in accessor.GetRowSpan(y))
                    {
                        byte a = pixel.A;
                        pixels[i++] = (byte)(pixel.B * a / 255);
                        pixels[i++] = (byte)(pixel.G * a / 255);
                        pixels[i++] = (byte)(pixel.R * a / 255);
                        pixels[i++] = a;
                    }
                }
            });
            return pixels;
        }

        /// <summary>True when the WebP container carries an animation (ANIM/ANMF chunks) rather than a still image.</summary>
        public static bool IsAnimatedWebP(byte[] bytes)
        {
            if (!IsWebP(bytes))
            {
                return false;
            }

            int offset = RiffHeaderLength;
            while (offset + 8 <= bytes.Length)
            {
                string fourCC = ReadFourCC(bytes, offset);
                int size = ReadInt32LE(bytes, offset + 4);
                int dataStart = offset + 8;
                if (size < 0 || dataStart + size > bytes.Length)
                {
                    return false;
                }

                if (fourCC == "VP8 " || fourCC == "VP8L")
                {
                    return false;
                }

                if (fourCC == "ANIM" || fourCC == "ANMF")
                {
                    return true;
                }

                offset = dataStart + size + (size & 1);
            }
            return false;
        }

        /// <summary>
        /// Walks the RIFF container and, for an animated WebP, rebuilds one ANMF frame as a
        /// standalone static WebP. Returns null when the image is not animated (or is malformed),
        /// in which case the original bytes should be decoded directly.
        /// </summary>
        private static byte[] ExtractRepresentativeAnimationFrame(byte[] bytes)
        {
            // Same poster-frame rule as SelectRepresentativeFrameIndex: the second-to-last frame,
            // unless it is a near-blank fade frame (tiny bitstream), then the most detailed one.
            int previousFrameStart = -1;
            int previousFrameSize = 0;
            int previousBitstreamLength = -1;
            int lastFrameStart = -1;
            int lastFrameSize = 0;
            int lastBitstreamLength = -1;
            int largestFrameStart = -1;
            int largestFrameSize = 0;
            int largestBitstreamLength = -1;

            int offset = RiffHeaderLength;
            while (offset + 8 <= bytes.Length)
            {
                string fourCC = ReadFourCC(bytes, offset);
                int size = ReadInt32LE(bytes, offset + 4);
                int dataStart = offset + 8;
                if (size < 0 || dataStart + size > bytes.Length)
                {
                    break;
                }

                if (fourCC == "VP8 " || fourCC == "VP8L")
                {
                    // A still image - no frame extraction needed.
                    return null;
                }

                if (fourCC == "ANMF")
                {
                    int bitstreamLength = MeasureFrameBitstream(bytes, dataStart, size);
                    previousBitstreamLength = lastBitstreamLength;
                    previousFrameStart = lastFrameStart;
                    previousFrameSize = lastFrameSize;
                    lastBitstreamLength = bitstreamLength;
                    lastFrameStart = dataStart;
                    lastFrameSize = size;
                    if (bitstreamLength > largestBitstreamLength)
                    {
                        largestBitstreamLength = bitstreamLength;
                        largestFrameStart = dataStart;
                        largestFrameSize = size;
                    }
                }

                offset = dataStart + size + (size & 1);
            }

            if (lastFrameStart < 0)
            {
                return null;
            }

            int posterFrameStart = (previousFrameStart >= 0) ? previousFrameStart : lastFrameStart;
            int posterFrameSize = (previousFrameStart >= 0) ? previousFrameSize : lastFrameSize;
            int posterBitstreamLength = (previousFrameStart >= 0) ? previousBitstreamLength : lastBitstreamLength;

            bool usePosterFrame = (long)posterBitstreamLength * 4 >= largestBitstreamLength;
            return BuildStaticWebPFromFrame(bytes, usePosterFrame ? posterFrameStart : largestFrameStart, usePosterFrame ? posterFrameSize : largestFrameSize);
        }

        /// <summary>Returns the byte length of a frame's VP8/VP8L bitstream, or -1 when it has none.</summary>
        private static int MeasureFrameBitstream(byte[] bytes, int frameStart, int frameSize)
        {
            if (frameSize < 16)
            {
                return -1;
            }

            int offset = frameStart + 16;
            int frameEnd = frameStart + frameSize;
            while (offset + 8 <= frameEnd)
            {
                string fourCC = ReadFourCC(bytes, offset);
                int size = ReadInt32LE(bytes, offset + 4);
                int dataStart = offset + 8;
                if (size < 0 || dataStart + size > frameEnd)
                {
                    return -1;
                }

                if (fourCC == "VP8 " || fourCC == "VP8L")
                {
                    return size;
                }

                offset = dataStart + size + (size & 1);
            }
            return -1;
        }

        // ANMF payload: frame X u24, frame Y u24, (width-1) u24, (height-1) u24, duration u24 and a
        // flags byte (16 bytes total), followed by an optional ALPH chunk and the VP8/VP8L bitstream.
        private static byte[] BuildStaticWebPFromFrame(byte[] bytes, int frameStart, int frameSize)
        {
            if (frameSize < 16)
            {
                return null;
            }

            int width = ReadUInt24LE(bytes, frameStart + 6) + 1;
            int height = ReadUInt24LE(bytes, frameStart + 9) + 1;

            ArraySegment<byte>? alpha = null;
            ArraySegment<byte>? bitstream = null;
            string bitstreamFourCC = null;

            int offset = frameStart + 16;
            int frameEnd = frameStart + frameSize;
            while (offset + 8 <= frameEnd && bitstream == null)
            {
                string fourCC = ReadFourCC(bytes, offset);
                int size = ReadInt32LE(bytes, offset + 4);
                int dataStart = offset + 8;
                if (size < 0 || dataStart + size > frameEnd)
                {
                    return null;
                }

                if (fourCC == "ALPH")
                {
                    alpha = new ArraySegment<byte>(bytes, dataStart, size);
                }
                else if (fourCC == "VP8 " || fourCC == "VP8L")
                {
                    bitstreamFourCC = fourCC;
                    bitstream = new ArraySegment<byte>(bytes, dataStart, size);
                }

                offset = dataStart + size + (size & 1);
            }

            if (bitstream == null)
            {
                return null;
            }

            // VP8L carries alpha natively; a lossy VP8 frame with an alpha plane needs a VP8X header
            // (alpha flag set) ahead of the ALPH + VP8 chunks.
            bool writeAlpha = alpha.HasValue && bitstreamFourCC == "VP8 ";

            int contentLength = 4 + PaddedChunkLength(bitstream.Value.Count);
            if (writeAlpha)
            {
                contentLength += PaddedChunkLength(10) + PaddedChunkLength(alpha.Value.Count);
            }

            using (MemoryStream output = new MemoryStream())
            {
                WriteFourCC(output, "RIFF");
                WriteInt32LE(output, contentLength);
                WriteFourCC(output, "WEBP");

                if (writeAlpha)
                {
                    WriteFourCC(output, "VP8X");
                    WriteInt32LE(output, 10);
                    output.WriteByte(0x10);    // VP8X flags: alpha only
                    output.WriteByte(0);
                    output.WriteByte(0);
                    output.WriteByte(0);
                    WriteUInt24LE(output, width - 1);
                    WriteUInt24LE(output, height - 1);
                    WriteChunk(output, "ALPH", alpha.Value);
                }

                WriteChunk(output, bitstreamFourCC, bitstream.Value);
                return output.ToArray();
            }
        }

        private static int PaddedChunkLength(int payloadLength)
        {
            return 8 + payloadLength + (payloadLength & 1);
        }

        private static void WriteChunk(MemoryStream output, string fourCC, ArraySegment<byte> payload)
        {
            WriteFourCC(output, fourCC);
            WriteInt32LE(output, payload.Count);
            output.Write(payload.Array, payload.Offset, payload.Count);
            if ((payload.Count & 1) == 1)
            {
                output.WriteByte(0);
            }
        }

        private static string ReadFourCC(byte[] bytes, int offset)
        {
            return new string(new char[] { (char)bytes[offset], (char)bytes[offset + 1], (char)bytes[offset + 2], (char)bytes[offset + 3] });
        }

        private static int ReadInt32LE(byte[] bytes, int offset)
        {
            return bytes[offset] | (bytes[offset + 1] << 8) | (bytes[offset + 2] << 16) | (bytes[offset + 3] << 24);
        }

        private static int ReadUInt24LE(byte[] bytes, int offset)
        {
            return bytes[offset] | (bytes[offset + 1] << 8) | (bytes[offset + 2] << 16);
        }

        private static void WriteFourCC(MemoryStream output, string fourCC)
        {
            for (int i = 0; i < 4; i++)
            {
                output.WriteByte((byte)fourCC[i]);
            }
        }

        private static void WriteInt32LE(MemoryStream output, int value)
        {
            output.WriteByte((byte)(value & 0xFF));
            output.WriteByte((byte)((value >> 8) & 0xFF));
            output.WriteByte((byte)((value >> 16) & 0xFF));
            output.WriteByte((byte)((value >> 24) & 0xFF));
        }

        private static void WriteUInt24LE(MemoryStream output, int value)
        {
            output.WriteByte((byte)(value & 0xFF));
            output.WriteByte((byte)((value >> 8) & 0xFF));
            output.WriteByte((byte)((value >> 16) & 0xFF));
        }
    }

    /// <summary>A decoded animated WebP: fixed-size premultiplied-BGRA frames plus timing.</summary>
    public class WebPAnimation
    {
        public int Width { get; set; }

        public int Height { get; set; }

        public int TotalDurationMilliseconds { get; set; }

        /// <summary>
        /// The poster/freeze frame: the last frame, unless the animation ends on a near-blank fade
        /// frame, in which case the most detailed frame (WebP flags no poster frame of its own).
        /// </summary>
        public int RepresentativeFrameIndex { get; set; }

        public List<WebPAnimationFrame> Frames { get; set; } = new List<WebPAnimationFrame>();
    }

    public class WebPAnimationFrame
    {
        public int X { get; set; }

        public int Y { get; set; }

        public int DurationMilliseconds { get; set; }

        public byte[] StandaloneWebP { get; set; }

        public byte[] PremultipliedBgraPixels { get; set; }
    }
}

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;

namespace Microsoft.CST.OpenSource.MultiExtractor
{
    public class FileEntry
    {
        /// <summary>
        /// Constructs a FileEntry object from a Stream.  If passthroughStream is set to true it will directly use inputStream.
        /// If passthroughStream is false it will copy the full contents of passthroughStream to our internal stream and 
        ///   attempt to reset the position of inputstream.
        /// The finalizer for this class Disposes inputStream.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="parentPath"></param>
        /// <param name="inputStream"></param>
        /// <param name="parent"></param>
        /// <param name="passthroughStream"></param>
        public FileEntry(string name, string parentPath, Stream inputStream, FileEntry? parent = null, bool passthroughStream = false)
        {
            Parent = parent;
            Name = name;
            if (string.IsNullOrEmpty(parentPath))
            {
                FullPath = Name;
            }
            else
            {
                FullPath = $"{parentPath}:{name}";
            }
            ParentPath = parentPath;
            if (inputStream == null)
            {
                throw new ArgumentNullException(nameof(inputStream));
            }
            
            if (passthroughStream)
            {
                Content = inputStream;
            }

            // Back with a temporary filestream, this is optimized to be cached in memory when possible automatically
            Content = new FileStream(Path.GetTempFileName(), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite, 4096, FileOptions.DeleteOnClose);
            long? initialPosition = null;
            if (inputStream.CanSeek)
            {
                initialPosition = inputStream.Position;
                inputStream.Position = 0;
            }
            inputStream.CopyTo(Content);
            if (inputStream.CanSeek)
            {
                inputStream.Position = initialPosition ?? 0;
            }
            Content.Position = 0;
        }
        public string ParentPath { get; set; }
        public string FullPath { get; set; }
        public FileEntry? Parent { get; set; }
        public string Name { get; set; }
        public Stream Content { get; set; }

        ~FileEntry()
        {
            Content?.Dispose();
        }
    }
}

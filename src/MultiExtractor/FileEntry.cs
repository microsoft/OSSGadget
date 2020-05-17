// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;

namespace Microsoft.CST.OpenSource.MultiExtractor
{
    public class FileEntry
    {
        public FileEntry(string name, string parentPath, Stream inputStream, bool passthroughStream = false)
        {
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
        public string Name { get; set; }
        public Stream Content { get; set; }

    }
}

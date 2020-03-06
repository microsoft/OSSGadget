// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;

namespace Microsoft.CST.OpenSource.Shared
{
    public class FileEntry
    {
        public FileEntry(string name, string parentPath, Stream content)
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
            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }
            Content = new MemoryStream();
            if (content.CanSeek)
            {
                content.Position = 0;
            }
            content.CopyTo(Content);
        }

        public string FullPath { get; set; }
        public string Name { get; set; }
        public MemoryStream Content { get; set; }

    }
}

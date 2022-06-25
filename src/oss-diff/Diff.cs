// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Microsoft.CST.OpenSource.DiffTool
{
    public class Diff
    {
        public enum LineType
        {
            None,
            Added,
            Removed,
            Context
        }
        public int startLine1 { get; set; } = -1;
        public int endLine1 { get { return startLine1 == -1 ? -1 : Math.Max(startLine1, startLine1 + text1.Count - 1); } }
        public int startLine2 { get; set; } = -1;
        public int endLine2 { get { return startLine2 == -1 ? -1 : Math.Max(startLine2, startLine2 + text2.Count - 1); } }
        public List<string> beforeContext { get; private set; } = new List<string>();
        public List<string> text1 { get; private set; } = new List<string>();
        public List<string> text2 { get; private set; } = new List<string>();
        public List<string> afterContext { get; private set; } = new List<string>();
        public LineType lastLineType { get; private set; } = LineType.None;

        public void AddBeforeContext(string context)
        {
            beforeContext.Add(context);
            lastLineType = LineType.Context;
        }
        public void AddAfterContext(string context)
        {
            afterContext.Add(context);
            lastLineType = LineType.Context;
        }
        public void AddText1(string content)
        {
            text1.Add(content);
            lastLineType = LineType.Removed;
        }
        public void AddText2(string content)
        {
            text2.Add(content);
            lastLineType = LineType.Added;
        }
    }
}

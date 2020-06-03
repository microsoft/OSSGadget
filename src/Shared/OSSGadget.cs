using System;
using Microsoft.CST.OpenSource.Shared;

namespace Microsoft.CST.OpenSource
{
    public class OSSGadget
    {
        /// <summary>
        /// Logger for this class
        /// </summary>
        public static NLog.ILogger Logger { get; set; } = NLog.LogManager.GetCurrentClassLogger();

        public OSSGadget()
        {
            CommonInitialization.Initialize();
        }
    }
}

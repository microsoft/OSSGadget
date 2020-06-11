using Microsoft.CST.OpenSource.Shared;

namespace Microsoft.CST.OpenSource
{
    public class OSSGadget
    {
        public OSSGadget()
        {
            CommonInitialization.Initialize();
        }

        /// <summary>
        ///     Logger for this class
        /// </summary>
        public static NLog.ILogger Logger { get; set; } = NLog.LogManager.GetCurrentClassLogger();
    }
}
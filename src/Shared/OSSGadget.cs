using Microsoft.CST.OpenSource.Shared;

namespace Microsoft.CST.OpenSource
{
    public class OSSGadget
    {
        #region Public Constructors

        public OSSGadget()
        {
            CommonInitialization.Initialize();
        }

        #endregion Public Constructors

        #region Public Properties

        /// <summary>
        /// Logger for this class
        /// </summary>
        public static NLog.ILogger Logger { get; set; } = NLog.LogManager.GetCurrentClassLogger();

        #endregion Public Properties
    }
}
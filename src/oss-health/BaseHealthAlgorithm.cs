// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using Microsoft.CST.OpenSource.Shared;
using System.Threading.Tasks;

namespace Microsoft.CST.OpenSource.Health
{
    /// <summary>
    /// Abstract base class for health algorithms
    /// </summary>
    internal abstract class BaseHealthAlgorithm
    {
        #region Protected Fields

        /// <summary>
        /// Logger for each of the subclasses
        /// </summary>
        protected static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        #endregion Protected Fields

        #region Public Constructors

        public BaseHealthAlgorithm()
        {
            CommonInitialization.OverrideEnvironmentVariables(this);
        }

        #endregion Public Constructors

        #region Public Methods

        public static double Clamp(double value, double min = 0, double max = 100)
        {
            return (value < min) ? min : (value > max) ? max : value;
        }

        /// <summary>
        /// Wrapper for Github calls for each component of the overall health
        /// </summary>
        /// <returns></returns>
        public abstract Task<HealthMetrics> GetHealth();

        #endregion Public Methods
    }
}
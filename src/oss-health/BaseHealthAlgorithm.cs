// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using Microsoft.CST.OpenSource.Helpers;
using System.Threading.Tasks;

namespace Microsoft.CST.OpenSource.Health
{
    /// <summary>
    ///     Abstract base class for health algorithms
    /// </summary>
    internal abstract class BaseHealthAlgorithm
    {
        public BaseHealthAlgorithm()
        {
            EnvironmentHelper.OverrideEnvironmentVariables(this);
        }

        public static double Clamp(double value, double min = 0, double max = 100)
        {
            return (value < min) ? min : (value > max) ? max : value;
        }

        /// <summary>
        ///     Wrapper for Github calls for each component of the overall health
        /// </summary>
        /// <returns> </returns>
        public abstract Task<HealthMetrics> GetHealth();

        /// <summary>
        ///     Logger for each of the subclasses
        /// </summary>
        protected static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
    }
}

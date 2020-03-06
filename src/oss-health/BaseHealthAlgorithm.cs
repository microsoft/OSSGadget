// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Microsoft.CST.OpenSource.Shared;

namespace Microsoft.CST.OpenSource.Health
{
    /// <summary>
    /// Abstract base class for health algorithms
    /// </summary>
    abstract class BaseHealthAlgorithm
    {
        /// <summary>
        /// Logger for each of the subclasses
        /// </summary>
        protected static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        public BaseHealthAlgorithm()
        {
            CommonInitialization.OverrideEnvironmentVariables(this);
        }

        /// <summary>
        /// Wrapper for Github calls for each component of the overall health
        /// </summary>
        /// <returns></returns>
        public abstract Task<HealthMetrics> GetHealth();

        public static double Clamp(double value, double min = 0, double max = 100)
        {
            return (value < min) ? min : (value > max) ? max : value;
        }
    }
}

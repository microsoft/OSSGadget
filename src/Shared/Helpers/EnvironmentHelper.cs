// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Helpers
{
    using System;
    using System.Reflection;

    public class EnvironmentHelper
    {
        public static NLog.ILogger Logger { get; set; } = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        ///     Overrides instance members of the <paramref name="targetObject"/> starting with ENV_ with the values of the respective environment variables, if set.
        ///     Allows users to easily override fields like API endpoint roots. Only strings are supported.
        /// </summary>
        /// <param name="targetObject"> Examine this object (using reflection) </param>
        public static void OverrideEnvironmentVariables(object targetObject)
        {
            foreach (PropertyInfo propertyInfo in targetObject.GetType().GetProperties())
            {
                if (propertyInfo.PropertyType == typeof(string) &&
                    propertyInfo.Name.StartsWith("ENV_") &&
                    propertyInfo.Name.Length > 4)
                {
                    string? bareName = propertyInfo.Name[4..];

                    string? value = Environment.GetEnvironmentVariable(bareName);
                    if (value != null)
                    {
                        Logger.Debug("Assiging value of {0} to {1}", bareName, propertyInfo.Name);
                        propertyInfo.SetValue(targetObject, value);
                    }
                }
            }
        }
    }
}

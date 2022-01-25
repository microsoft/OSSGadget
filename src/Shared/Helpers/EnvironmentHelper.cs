// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Helpers
{
    using System;
    using System.Reflection;

    public class EnvironmentHelper
    {
        public static NLog.ILogger Logger { get; set; } = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        ///     Overrides static members starting with ENV_ with the respective environment variables.Allows
        ///     users to easily override fields like API endpoint roots. Only strings are supported.
        /// </summary>
        /// <param name="targetObject"> Examine this object (using reflection) </param>
        public static void OverrideEnvironmentVariables(object targetObject)
        {
            foreach (FieldInfo fieldInfo in targetObject.GetType().GetFields(BindingFlags.Static |
                                                                       BindingFlags.Public |
                                                                       BindingFlags.NonPublic))
            {
                if (fieldInfo.FieldType.FullName == "System.String" &&
                    fieldInfo.Name.StartsWith("ENV_") &&
                    fieldInfo.Name.Length > 4)
                {
                    string? bareName = fieldInfo.Name[4..];

                    string? value = Environment.GetEnvironmentVariable(bareName);
                    if (value != null)
                    {
                        Logger.Debug("Assiging value of {0} to {1}", bareName, fieldInfo.Name);
                        fieldInfo.SetValue(null, value);
                    }
                }
            }
        }
    }
}

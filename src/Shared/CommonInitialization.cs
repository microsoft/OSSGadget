// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Shared
{
    using System;
    using System.IO;
    using System.Net.Http;
    using System.Net.Sockets;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;

    public static class CommonInitialization
    {
        /// <summary>
        ///     Prevent being initialized multiple times.
        /// </summary>
        private static bool Initialized = false;

        public static NLog.ILogger Logger { get; set; } = NLog.LogManager.GetCurrentClassLogger();

        /// <summary>
        ///     Initializes common infrastructure, like logging.
        /// </summary>
        public static void Initialize()
        {
            // Only allow initialization once
            if (Initialized)
            {
                return;
            }

            Initialized = true;
        }

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
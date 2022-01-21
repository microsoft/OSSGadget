// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

namespace Microsoft.CST.OpenSource.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Static helper methods for validating parameters.
    /// </summary>
    public static class Check
    {
        /// <summary>
        /// Checks that the given parameter is not null, empty, or whitespace.
        /// </summary>
        /// <param name="paramName">The name of the parameter. Can be retrieved by using nameof(parameter).</param>
        /// <param name="value">The value of the parameter.</param>
        /// <returns>The value of the parameter if it passes the validation.</returns>
        /// <exception cref="ArgumentNullException">The parameter value is null, empty, or whitespace.</exception>
        public static string NotEmptyOrWhitespace(string paramName, string? value)
        {
            // The parameterName can't be null so explicitly check (can't call EnsureIsNotNullOrWhitespace in this case!)
            if (string.IsNullOrWhiteSpace(paramName))
            {
                throw new ArgumentNullException(nameof(paramName));
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentNullException(value);
            }

            return value;
        }

        /// <summary>
        /// Checks that the given parameter is not null.
        /// </summary>
        /// <typeparam name="T">The type of the parameter.</typeparam>
        /// <param name="paramName">The name of the parameter. Can be retrieved by using nameof(parameter).</param>
        /// <param name="value">The value of the parameter.</param>
        /// <param name="message">An optional message that will be included as apart of the exception if the value is null.</param>
        /// <returns>The value of the parameter if it passes the validation.</returns>
        /// <exception cref="ArgumentNullException">The parameter value is null.</exception>
        public static T NotNull<T>(string paramName, T? value, string? message = null)
        {
            NotEmptyOrWhitespace(nameof(paramName), paramName);
            message = message ?? $"Value cannot be null. (Parameter '{paramName}')";

            if (value == null)
            {
                throw new ArgumentNullException(paramName, message);
            }

            return value;
        }

        /// <summary>
        /// Check that the given parameter is in the enum.
        /// </summary>
        /// <typeparam name="T">The enum type of the parameter.</typeparam>
        /// <param name="paramName">The name of the parameter. Can be retrieved by using nameof(parameter).</param>
        /// <param name="value">The value of the parameter.</param>
        /// <returns>The value of the parameter if it passes the validation.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The parameter value is not apart of the enum.</exception>
        public static T IsInEnum<T>(string paramName, T value)
            where T : Enum
        {
            NotEmptyOrWhitespace(nameof(paramName), paramName);
            NotNull(paramName, value);

            if (!Enum.IsDefined(value.GetType(), value))
            {
                throw new ArgumentOutOfRangeException(
                    paramName,
                    $"The value is {value} is not a valid value of {value.GetType()}.");
            }

            return value;
        }

        /// <summary>
        /// Ensures that the provided enumerable is not null and contains at least one item.
        /// </summary>
        /// <typeparam name="T">The type of object contained in the enumerable.</typeparam>
        /// <param name="parameterName">Parameter name (embedded in exception if thrown).</param>
        /// <param name="parameterValue">The value to validate.</param>
        /// <exception cref="ArgumentNullException"> Enumerable object is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Enumerable does not contain at least 1 object.</exception>
        public static void NotNullOrEmpty<T>(
            string parameterName,
            IEnumerable<T>? parameterValue)
        {
            NotNullAndHasMinimumCount(parameterName, parameterValue, 1);
        }

        /// <summary>
        /// Ensures that the provided enumerable is not null and contains at least minimumCount elements.
        /// </summary>
        /// <typeparam name="T">The type of object contained in the enumerable.</typeparam>
        /// <param name="parameterName">Parameter name (embedded in exception if thrown).</param>
        /// <param name="parameterValue">The value to validate.</param>
        /// <param name="minimumCount">The minimum number of items required in the enumerable.</param>
        /// <exception cref="ArgumentNullException">Enumerable object is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Enumerable does not contain at least <paramref name="minimumCount"/> number of objects.</exception>
        public static void NotNullAndHasMinimumCount<T>(
            string parameterName,
            IEnumerable<T>? parameterValue,
            int minimumCount)
        {
            NotEmptyOrWhitespace(nameof(parameterName), parameterName);

            if (parameterValue == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (parameterValue.Count() < minimumCount)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    $"{parameterName} does not contain at least {minimumCount} elements.");
            }
        }
    }
}
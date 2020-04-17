// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// This file was derived from the packageurl-dotnet package available at
// https://github.com/package-url/packageurl-dotnet.

// MIT License
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Microsoft.CST.OpenSource.Shared
{
    /// <summary>
    /// Provides an object representation of a Package URL and easy access to its parts.
    ///
    /// A purl is a URL composed of seven components:
    /// scheme:type/namespace/name@version?qualifiers#subpath
    ///
    /// Components are separated by a specific character for unambiguous parsing.
    /// A purl must NOT contain a URL Authority i.e. there is no support for username,
    /// password, host and port components. A namespace segment may sometimes look
    /// like a host but its interpretation is specific to a type.
    ///
    /// To read full-spec, visit <a href="https://github.com/package-url/purl-spec">https://github.com/package-url/purl-spec</a>
    /// </summary>
    public sealed class PackageURL : IEquatable<PackageURL>
    {
        private static readonly Regex s_typePattern = new Regex("^[a-zA-Z][a-zA-Z0-9.+-]+$", RegexOptions.Compiled);

        /// <summary>
        /// The PackageURL scheme constant.
        /// </summary>
        public string Scheme { get; private set; } = "pkg";

        /// <summary>
        /// The package "type" or package "protocol" such as nuget, npm, nuget, gem, pypi, etc.
        /// </summary>
        public string Type { get; private set; }

        /// <summary>
        /// The name prefix such as a Maven groupid, a Docker image owner, a GitHub user or organization.
        /// </summary>
        public string Namespace { get; private set; }

        /// <summary>
        /// The name of the package.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// The version of the package.
        /// </summary>
        public string Version { get; private set; }

        /// <summary>
        /// Extra qualifying data for a package such as an OS, architecture, a distro, etc.
        /// <summary>
        public SortedDictionary<string, string> Qualifiers { get; private set; }

        /// <summary>
        /// Extra subpath within a package, relative to the package root.
        /// </summary>
        public string Subpath { get; private set; }

        /// <summary>
        /// Constructs a new PackageURL object by parsing the specified string.
        /// </summary>
        /// <param name="purl">A valid package URL string to parse.</param>
        /// <exception cref="FormatException">Thrown when parsing fails.</exception>
        public PackageURL(string purl)
        {
            Parse(purl);
        }

        /// <summary>
        /// Constructs a new PackageURL object by specifying only the required
        /// parameters necessary to create a valid PackageURL.
        /// </summary>
        /// <param name="type">Type of package (i.e. nuget, npm, gem, etc).</param>
        /// <param name="name">Name of the package.</param>
        /// <exception cref="FormatException">Thrown when parsing fails.</exception>
        public PackageURL(string type, string name) : this(type, null, name, null, null, null)
        {
        }

        /// <summary>
        /// Constructs a new PackageURL object.
        /// </summary>
        /// <param name="type">Type of package (i.e. nuget, npm, gem, etc).</param>
        /// <param name="namespace">Namespace of package (i.e. group, owner, organization).</param>
        /// <param name="name">Name of the package.</param>
        /// <param name="version">Version of the package.</param>
        /// <param name="qualifiers"><see cref="SortedDictionary{string, string}"/> of key/value pair qualifiers.</param>
        /// @param qualifiers an array of key/value pair qualifiers
        /// @param subpath the subpath string
        /// <exception cref="FormatException">Thrown when parsing fails.</exception>
        public PackageURL(string type, string @namespace, string name, string version, SortedDictionary<string, string> qualifiers, string subpath)
        {
            Type = ValidateType(type);
            Namespace = ValidateNamespace(@namespace);
            Name = ValidateName(name);
            Version = version;
            Qualifiers = qualifiers;
            Subpath = ValidateSubpath(subpath);
        }

        /// <summary>
        /// Returns a canonicalized representation of the purl.
        /// </summary>
        public override string ToString()
        {
            var purl = new StringBuilder();
            purl.Append(Scheme).Append(':');
            if (Type != null)
            {
                purl.Append(Type);
            }
            purl.Append('/');
            if (Namespace != null)
            {
                purl.Append(WebUtility.UrlEncode(Namespace));
                purl.Append('/');
            }
            if (Name != null)
            {
                purl.Append(Name);
            }
            if (Version != null)
            {
                purl.Append('@').Append(Version);
            }
            if (Qualifiers != null && Qualifiers.Count > 0)
            {
                purl.Append("?");
                foreach (var pair in Qualifiers)
                {
                    purl.Append(pair.Key.ToLower());
                    purl.Append('=');
                    purl.Append(pair.Value);
                    purl.Append('&');
                }
                purl.Remove(purl.Length - 1, 1);
            }
            if (Subpath != null)
            {
                purl.Append("#").Append(Subpath);
            }
            return purl.ToString();
        }

        public string ToStringFilename()
        {
            var invalidChars = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
            return Regex.Replace(this.ToString(), "[" + Regex.Escape(invalidChars) + "]", "-");
        }

        private void Parse(string purl)
        {
            if (purl == null || string.IsNullOrWhiteSpace(purl))
            {
                throw new FormatException("Invalid purl: Contains an empty or null value");
            }

            Uri uri;
            try
            {
                uri = new Uri(purl);
            }
            catch (UriFormatException e)
            {
                throw new FormatException("Invalid purl: " + e.Message);
            }

            // Check to ensure that none of these parts are parsed. If so, it's an invalid purl.
            if (!string.IsNullOrEmpty(uri.UserInfo) || uri.Port != -1)
            {
                throw new FormatException("Invalid purl: Contains parts not supported by the purl spec");
            }

            if (uri.Scheme != "pkg")
            {
                throw new FormatException("The PackageURL scheme is invalid");
            }

            // This is the purl (minus the scheme) that needs parsed.
            string remainder = purl.Substring(4);

            if (remainder.Contains("#"))
            { // subpath is optional - check for existence
                int index = remainder.LastIndexOf("#");
                Subpath = ValidateSubpath(remainder.Substring(index + 1));
                remainder = remainder.Substring(0, index);
            }

            if (remainder.Contains("?"))
            { // qualifiers are optional - check for existence
                int index = remainder.LastIndexOf("?");
                Qualifiers = ValidateQualifiers(remainder.Substring(index + 1));
                remainder = remainder.Substring(0, index);
            }

            if (remainder.Contains("@"))
            { // version is optional - check for existence
                int index = remainder.LastIndexOf("@");
                Version = remainder.Substring(index + 1);
                remainder = remainder.Substring(0, index);
            }

            // The 'remainder' should now consist of the type, an optional namespace, and the name

            // Strip zero or more '/' ('type')
            remainder = remainder.Trim('/');

            string[] firstPartArray = remainder.Split('/');
            if (firstPartArray.Length < 2)
            { // The array must contain a 'type' and a 'name' at minimum
                throw new FormatException("Invalid purl: Does not contain a minimum of a 'type' and a 'name'");
            }

            Type = ValidateType(firstPartArray[0]);
            Name = ValidateName(firstPartArray[^1]);

            // Test for namespaces
            if (firstPartArray.Length > 2)
            {
                var @namespace = new StringBuilder();
                int i;
                for (i = 1; i < firstPartArray.Length - 2; ++i)
                {
                    @namespace.Append(firstPartArray[i] + ',');
                }
                @namespace.Append(firstPartArray[i]);

                Namespace = ValidateNamespace(@namespace.ToString());
            }
        }

        private static string ValidateType(string type)
        {
            if (type == null || !s_typePattern.IsMatch(type))
            {
                throw new FormatException("The PackageURL type specified is invalid");
            }
            return type.ToLower();
        }

        private string ValidateNamespace(string @namespace)
        {
            if (@namespace == null)
            {
                return null;
            }
            if (Type == "vsm" || Type == "cran")
            {
                return WebUtility.UrlDecode(@namespace);
            }

            return WebUtility.UrlDecode(@namespace.ToLower());
        }

        private string ValidateName(string name)
        {
            if (name == null)
            {
                throw new FormatException("The PackageURL name specified is invalid");
            }
            if (Type == "pypi")
            {
                name = name.Replace('_', '-');
            }
            if (Type == "cocoapods" || Type == "cpan" || Type == "vsm" || Type == "cran")
            {
                return name;
            }
            return name.ToLower();
        }

        private static SortedDictionary<string, string> ValidateQualifiers(string qualifiers)
        {
            var list = new SortedDictionary<string, string>();
            string[] pairs = qualifiers.Split('&');
            foreach (var pair in pairs)
            {
                if (pair.Contains("="))
                {
                    string[] kvpair = pair.Split('=');
                    list.Add(kvpair[0], kvpair[1]);
                }
            }
            return list;
        }

        private static string ValidateSubpath(string subpath) => subpath?.Trim('/'); // leading and trailing slashes always need to be removed

        public override int GetHashCode()
        {
            return HashCode.Combine(Scheme, Type, Namespace, Name, Version, Qualifiers, Subpath);
        }

        public bool Equals(PackageURL obj)
        {
            return obj is PackageURL uRL &&
                   Scheme == uRL.Scheme &&
                   Type == uRL.Type &&
                   Namespace == uRL.Namespace &&
                   Name == uRL.Name &&
                   Version == uRL.Version &&
                   EqualityComparer<SortedDictionary<string, string>>.Default.Equals(Qualifiers, uRL.Qualifiers) &&
                   Subpath == uRL.Subpath;
        }
    }
}
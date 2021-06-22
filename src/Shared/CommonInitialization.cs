// Copyright (c) Microsoft Corporation. Licensed under the MIT License.

using System;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.CST.OpenSource.Shared
{
    public static class CommonInitialization
    {
        /// <summary>
        ///     Static HttpClient for use in all HTTP connections.
        /// </summary>
        public static HttpClient? WebClient { get; private set; } = null;

        /// <summary>
        ///     User Agent string, when needed to connect to external resources.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0044:Add readonly modifier", Justification = "Modified through reflection.")]
        private static string ENV_HTTPCLIENT_USER_AGENT = "OSSDL";

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

            // This is needed due to .NET 5 bug #47267 which should be fixed in .NET 6.
            // https://github.com/dotnet/runtime/issues/47267
            static async ValueTask<Stream> IPv4ConnectAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
            {
                // By default, we create dual-mode sockets:
                // Socket socket = new Socket(SocketType.Stream, ProtocolType.Tcp);

                Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
                {
                    NoDelay = true
                };

                try
                {
                    await socket.ConnectAsync(context.DnsEndPoint, cancellationToken).ConfigureAwait(false);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            }

            // Initialize the static HttpClient
            #pragma warning disable CA2000 // Held onto by WebClient
            var handler = new SocketsHttpHandler()
            {
                AllowAutoRedirect = true,
                UseCookies = false,
                MaxAutomaticRedirections = 5,
                PooledConnectionIdleTimeout = TimeSpan.FromSeconds(30),
                PooledConnectionLifetime = TimeSpan.FromSeconds(30),
                ConnectCallback = IPv4ConnectAsync
            };
            #pragma warning restore CA2000

            //if (handler.SupportsAutomaticDecompression)
            //{
            //    handler.AutomaticDecompression = System.Net.DecompressionMethods.All;
            //}
            
            WebClient = new HttpClient(handler);
            WebClient.Timeout = TimeSpan.FromSeconds(120);
            
            
            WebClient.DefaultRequestHeaders.UserAgent.ParseAdd(ENV_HTTPCLIENT_USER_AGENT);

            // @TODO Does this actually run? Is it necessary?
            System.Runtime.Loader.AssemblyLoadContext.Default.Unloading += (_) =>
            {
                if (WebClient != null)
                {
                    WebClient.Dispose();
                    WebClient = null;
                }
                Initialized = false;
            };

            Initialized = true;
        }

        /// <summary>
        ///     Overrides static members starting with ENV_ with the respective environment variables.Allows
        ///     users to easily override fields like API endpoint roots. Only strings are supported.
        /// </summary>
        /// <param name="targetObject"> Examine this object (using reflection) </param>
        public static void OverrideEnvironmentVariables(object targetObject)
        {
            foreach (var fieldInfo in targetObject.GetType().GetFields(BindingFlags.Static |
                                                                       BindingFlags.Public |
                                                                       BindingFlags.NonPublic))
            {
                if (fieldInfo.FieldType.FullName == "System.String" &&
                    fieldInfo.Name.StartsWith("ENV_") &&
                    fieldInfo.Name.Length > 4)
                {
                    var bareName = fieldInfo.Name.Substring(4);

                    var value = Environment.GetEnvironmentVariable(bareName);
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
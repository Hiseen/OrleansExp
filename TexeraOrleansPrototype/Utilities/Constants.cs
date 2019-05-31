using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

namespace TexeraUtilities
{
    static public class Constants
    {
        public const string OperatorAssemblyPathPrefix = "Engine.OperatorImplementation.Operators";
        public const string ControllerAssemblyPathPrefix = "Engine.Controller";
        public const int batchSize = 1000;
        public const int num_scan = 10;
        public const int max_retries = 60;
        public const bool conditions_on = false;
        public const bool ordered_on = true;
        public const string delivery = "RPC";
        public static string connectionString = "server=10.138.0.5;uid=orleans-backend;pwd=orleans-0519-2019;database=orleans;SslMode=none";
    }
}
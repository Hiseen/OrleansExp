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
        public const int batchSize = 100;
        public const int num_scan = 2;
        public const int max_retries = 60;
        public const bool conditions_on = false;
        public const bool ordered_on = true;
        public const string delivery = "RPC";
    }
}
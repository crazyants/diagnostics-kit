﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LowLevelDesign.Diagnostics.Commons.Models
{
    public class LogRecord
    {
        public String LoggerName;

        public String LogLevel;

        public DateTime TimeUtc;

        public int ProcessId;

        public String ProcessName;

        public int ThreadId;

        public String Server;

        public String ApplicationPath;

        public String Identity;

        public String CorrelationId;

        public String Message;

        public String ExceptionType;

        public String ExceptionMessage;

        public String ExceptionAdditionalInfo;

        /// <summary>
        /// Additional field which describe the log being collected, such as for web logs:
        /// url, referer or exception data.
        /// </summary>
        public Dictionary<String, Object> AdditionalFields { get; set; }

        /// <summary>
        /// This field will be used only for logs generated from Performance Counters data.
        /// </summary>
        public Dictionary<String, float> PerformanceData { get; set; }
    }
}

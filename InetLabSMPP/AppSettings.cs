using System;
using System.Collections.Generic;
using System.Text;

namespace InetLabSMPP
{
    public class AppSettings
    {
        public string LogFolder { get; set; }
        public string SrcAddr { get; set; }
        public string SystemType { get; set; }
        public string HostName { get; set; }
        public string Port { get; set; }
        public string SystemId { get; set; }
        public string Password { get; set; }
        public bool Ssl { get; set; }
        public string AddrTon { get; set; }
        public string AddrNpi { get; set; }
        public string SubmitMode { get; set; }
        public string DataCoding { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Text;

namespace InetLabSMPP
{
    public class SMSModel
    {
        public string Message { get; set; }

        public string Telephone { get; set; }

        public string DeliverableStatus { get; set; }

        public string WS_Error_ID { get; set; }

        public string WS_Error_MSG { get; set; }

        public string Message_ID { get; set; }
        public string DestAdrTON { get; set; }
        public string DestAdrNPI { get; set; }          

    }
}


using System;
using System.Collections.Generic;
using System.Text;

namespace InetLabSMPP.Enum
{
      /// <summary>
        /// Identifies the way how to send multi-part text in the SUBMIT_SM PDU.
        /// </summary>
        public enum SubmitMode
        {
            /// <summary>
            /// Send user data in the short_message field and concatenation options in user data header.
            /// </summary>
            ShortMessage = 1,

            /// <summary>
            /// Send user data in the message_payload field and concatenation options in TLV paramaters
            /// </summary>
            Payload = 2,

            /// <summary>
            /// Send user data in the short_message field and concatenation options in TLV paramaters
            /// </summary>
            ShortMessageWithSAR = 3
        }
    }


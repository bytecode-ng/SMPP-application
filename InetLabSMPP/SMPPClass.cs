using Inetlab.SMPP;
using Inetlab.SMPP.Builders;
using Inetlab.SMPP.Common;
using Inetlab.SMPP.Logging;
using Inetlab.SMPP.PDU;
using InetLabSMPP.Enum;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace InetLabSMPP
{
    public class SMPPClass
    {
        private readonly SmppClient _client;
        private readonly string _SrcAddr;
        private readonly string _SystemType;
        private readonly string _hostName;
        private readonly int _port;
        private readonly string _systemId;
        private readonly string _password;
        private readonly bool _ssl;
        private readonly string _AddrTon;
        private readonly string _AddrNpi;

        private readonly string _SubmitMode;
        private readonly string _DataCoding;

        private readonly MessageComposer _messageComposer;
        private readonly ILog _log;

        private void client_evEnquireLink(object sender, EnquireLink data)
        {
            // _log.Info("EnquireLink received");
        }

        private void client_evUnBind(object sender, UnBind data)
        {
            _log.Info("UnBind request received");
        }

        private void ClientOnRecoverySucceeded(object sender, BindResp data)
        {
            _log.Info("Connection has been recovered.");

            //Sync(this, () =>
            //{
            //    bConnect.Enabled = false;
            //    bDisconnect.Enabled = true;
            //    bSubmit.Enabled = true;
            //    cbReconnect.Enabled = false;
            //});

        }

        private void client_evDisconnected(object sender)
        {
            _log.Info("SmppClient disconnected");

            //Sync(this, () =>
            //{
            //    bConnect.Enabled = true;
            //    bDisconnect.Enabled = false;
            //    bSubmit.Enabled = false;
            //    cbReconnect.Enabled = true;


            //});

        }

        private void OnCertificateValidation(object sender, CertificateValidationEventArgs args)
        {
            //accept all certificates
            args.Accepted = true;
        }

        private void OnFullMessageReceived(object sender, MessageEventHandlerArgs args)
        {
            _log.Info("Full message received From: {0}, To: {1}, Text: {2}", args.GetFirst<DeliverSm>().SourceAddress, args.GetFirst<DeliverSm>().DestinationAddress, args.Text);
        }

        private void OnFullMessageTimeout(object sender, MessageEventHandlerArgs args)
        {
            _log.Info("Incomplete message received From: {0}, Text: {1}", args.GetFirst<DeliverSm>().SourceAddress, args.Text);
        }



        public SMPPClass(IOptions<AppSettings> settings)
        {
            // Get values from AppSettings.
            _SrcAddr = settings.Value.SrcAddr;
            _SystemType = settings.Value.SystemType;
            _hostName = settings.Value.HostName;
            _port = Convert.ToInt32(settings.Value.Port);
            _systemId = settings.Value.SystemId;
            _password = settings.Value.Password;
            _ssl = settings.Value.Ssl;
            _AddrTon =settings.Value.AddrTon;
            _AddrNpi = settings.Value.AddrNpi;
           _SubmitMode = settings.Value.SubmitMode;
           _DataCoding = settings.Value.DataCoding;

           _client = new SmppClient();
           _client.ResponseTimeout = TimeSpan.FromSeconds(60);
            _client.EnquireLinkInterval = TimeSpan.FromSeconds(20);

            _client.evDisconnected += new DisconnectedEventHandler(client_evDisconnected);
            _client.evDeliverSm += new DeliverSmEventHandler(client_evDeliverSm);
            _client.evEnquireLink += new EnquireLinkEventHandler(client_evEnquireLink);
            _client.evUnBind += new UnBindEventHandler(client_evUnBind);
            _client.evDataSm += new DataSmEventHandler(client_evDataSm);
            _client.evRecoverySucceeded += ClientOnRecoverySucceeded;

            _client.evServerCertificateValidation += OnCertificateValidation;


            _messageComposer = new MessageComposer();
            _messageComposer.evFullMessageReceived += OnFullMessageReceived;
            _messageComposer.evFullMessageTimeout += OnFullMessageTimeout;

            _log = LogManager.GetLogger(GetType().Name);

            //this.client.evDeliverSm += client_evDeliverSm;
            //this.client.evDataSm += client_evDataSm;
        }


        public async Task Main(SMSModel sReturnValue)
        {
            try
            {
                await Connect();
                if (_client.Status == ConnectionStatus.Open)
                    await Bind();
                string sMsgbox = sReturnValue.Message;
                string sNumber = sReturnValue.Telephone;
                if (!string.IsNullOrEmpty(sNumber.Trim()) & !string.IsNullOrEmpty(sMsgbox.Trim()))
                {
                    string sValue = await sendSMS(sNumber, sMsgbox, sReturnValue);
                    if ((sValue.Trim() ?? "") == "SEND")
                    {
                        sReturnValue.DeliverableStatus = "S";
                        sReturnValue.WS_Error_ID = "0";
                        sReturnValue.WS_Error_MSG = "";
                    }
                }
               await Disconnect();
            }
            catch (Exception ex)
            {
                sReturnValue.WS_Error_ID = "-5";
                sReturnValue.WS_Error_MSG = sReturnValue.WS_Error_MSG + Environment.NewLine + ex.Message.Replace("Inetlab.", "SMSModel.") + Environment.NewLine; // & sError
                _log.Error(ex + "");
            }
        }

        private async Task<string> sendSMS(string sNumber, string sMessageText, SMSModel sReturnValue)
        {
            string sValue = "";
            try
            {
                DataCodings coding = GetDataCoding();
                SubmitMode mode = GetSubmitMode();
                if (_client.Status != ConnectionStatus.Bound)
                {
                    sReturnValue.WS_Error_ID = "-3";
                    sReturnValue.WS_Error_MSG = "Error : Before sending messages, please connect to SMSC." + ConnectionStatus.Bound;
                    sValue = "Error : Before sending messages, please connect to SMSC.";
                    return sValue;
                }

                var sourceAddress = new SmeAddress(_SrcAddr, (AddressTON)byte.Parse(_AddrTon), (AddressNPI)byte.Parse(_AddrNpi));

                var destinationAddress = new SmeAddress(sNumber, (AddressTON)byte.Parse(sReturnValue.DestAdrTON), (AddressNPI)byte.Parse(sReturnValue.DestAdrNPI));

                _log.Info("Submit message To: {0}. Text: {1}", sNumber, sMessageText);

               
                ISubmitSmBuilder builder = SMS.ForSubmit()
                 .From(sourceAddress)
                 .To(destinationAddress)
                 .Coding(coding)
                 .Text(sMessageText)
                //Or you can set data 
                //.Data(HexStringToByteArray("024A3A6949A59194813D988151A004004D215D2690568698A22820C49A4106288A126A8A22C2A820C22820C2A82342AC30820C4984106288A12628A22C2A420800"))

                //Apply italic style for all text  (mobile device should support basic EMS)
                //.Set(delegate(SubmitSm sm)
                //         {
                //             sm.UserDataPdu.Headers.Add(
                //                 InformationElementIdentifiers.TextFormatting, new byte[] {0, 1, ToByte("00100011")});
                //         })

                // Set ValidityPeriod expired in 2 days
                .ExpireIn(TimeSpan.FromDays(2))

                //Request delivery receipt
                .DeliveryReceipt();
                //Add custom TLV parameter
                //.AddParameter(0x1403, "free")

                //Change SubmitSm sequence to your own number.
                //.Set(delegate(SubmitSm sm) { sm.Sequence = _client.SequenceGenerator.NextSequenceNumber();})

               // List<Inetlab.SMPP.PDU.SubmitSmResp> resp = _client.Submit(req);
                switch (mode)
                {
                    case SubmitMode.Payload:
                        builder.MessageInPayload();
                        break;
                    case SubmitMode.ShortMessageWithSAR:
                        builder.Concatenation(ConcatenationType.SAR);
                        break;
                }

                
                    IList<SubmitSmResp> resp = await _client.SubmitAsync(builder);
                    if (resp.All(x => x.Header.Status == CommandStatus.ESME_ROK))
                    {
                        _log.Info("Submit succeeded. MessageIds: {0}", string.Join(",", resp.Select(x => x.MessageId)));
                    }
                    else
                    {
                        _log.Warn("Submit failed. Status: {0}", string.Join(",", resp.Select(x => x.Header.Status.ToString())));
                    }
                
                // When you received success result, you can later query message status on SMSC 
                if (resp.Count > 0 && resp[0].Header.Status == CommandStatus.ESME_ROK)
                {
                    _log.Info("QuerySm for message " + resp[0].MessageId);
                    QuerySmResp qresp = await _client.QueryAsync(resp[0].MessageId, destinationAddress);
                    sReturnValue.Message_ID = resp[0].MessageId;
                    sValue = "SEND";
                }
            }
            catch (Exception ex)
            {
                sValue = "0 - " + sReturnValue.WS_Error_MSG + Environment.NewLine + ex.Message;
            }
            return sValue;
        }

        private async Task SubmitSingleMessage(string MessageText, string SrcAddr, string SrcAdrTON, string SrcAdrNPI, string DestAdr, string DestAdrTON, string DestAdrNPI)
        {
            DataCodings coding = GetDataCoding();



            var sourceAddress = new SmeAddress(SrcAddr, (AddressTON)byte.Parse(SrcAdrTON), (AddressNPI)byte.Parse(SrcAdrNPI));

            var destinationAddress = new SmeAddress(DestAdr, (AddressTON)byte.Parse(DestAdrTON), (AddressNPI)byte.Parse(DestAdrNPI));

            _log.Info("Submit message To: {0}. Text: {1}", DestAdr, MessageText);


            ISubmitSmBuilder builder = SMS.ForSubmit()
                .From(sourceAddress)
                .To(destinationAddress)
                .Coding(coding)
                .Text(MessageText)
                //Or you can set data 
                //.Data(HexStringToByteArray("024A3A6949A59194813D988151A004004D215D2690568698A22820C49A4106288A126A8A22C2A820C22820C2A82342AC30820C4984106288A12628A22C2A420800"))

                //Apply italic style for all text  (mobile device should support basic EMS)
                //.Set(delegate(SubmitSm sm)
                //         {
                //             sm.UserDataPdu.Headers.Add(
                //                 InformationElementIdentifiers.TextFormatting, new byte[] {0, 1, ToByte("00100011")});
                //         })

                // Set ValidityPeriod expired in 2 days
                .ExpireIn(TimeSpan.FromDays(2))

                //Request delivery receipt
                .DeliveryReceipt();
            //Add custom TLV parameter
            //.AddParameter(0x1403, "free")

            //Change SubmitSm sequence to your own number.
            //.Set(delegate(SubmitSm sm) { sm.Sequence = _client.SequenceGenerator.NextSequenceNumber();})

            SubmitMode mode = GetSubmitMode();
            switch (mode)
            {
                case SubmitMode.Payload:
                    builder.MessageInPayload();
                    break;
                case SubmitMode.ShortMessageWithSAR:
                    builder.Concatenation(ConcatenationType.SAR);
                    break;
            }

            try
            {
                IList<SubmitSmResp> resp = await _client.SubmitAsync(builder);

                if (resp.All(x => x.Header.Status == CommandStatus.ESME_ROK))
                {
                    _log.Info("Submit succeeded. MessageIds: {0}", string.Join(",", resp.Select(x => x.MessageId)));
                }
                else
                {
                    _log.Warn("Submit failed. Status: {0}", string.Join(",", resp.Select(x => x.Header.Status.ToString())));
                }
            }
            catch (Exception ex)
            {
                _log.Error("Submit failed. Error: {0}", ex.Message);
            }

            // When you received success result, you can later query message status on SMSC 
            // if (resp.Count > 0 && resp[0].Status == CommandStatus.ESME_ROK)
            // {
            //     _log.Info("QuerySm for message " + resp[0].MessageId);
            //     QuerySmResp qresp = _client.Query(resp[0].MessageId,
            //         srcTon, srcNpi,srcAdr);
            // }
        }

        private async Task SubmitMultiMessage(string MessageText, string[] dstAddresses, string SrcAdrTON, string SrcAdrNPI,string SrcAdr,string DestAdrTON, string DestAdrNPI)
        {
            DataCodings coding = GetDataCoding();

            byte srcTon = byte.Parse(SrcAdrTON);
            byte srcNpi = byte.Parse(SrcAdrNPI);
            string srcAdr = SrcAdr;
            byte dstTon = byte.Parse(DestAdrTON);
            byte dstNpi = byte.Parse(DestAdrNPI);

            ISubmitMultiBuilder builder = SMS.ForSubmitMulti()
                .From(srcAdr, (AddressTON)srcTon, (AddressNPI)srcNpi)
                .Coding(coding)
                .Text(MessageText)
                //Request delivery receipt
                .DeliveryReceipt();

            foreach (var dstAddress in dstAddresses)
            {
                if (dstAddress == null || dstAddress.Trim().Length == 0) continue;

                builder.To(dstAddress.Trim(), (AddressTON)dstTon, (AddressNPI)dstNpi);
            }

            _log.Info("Submit message to several addresses: {0}. Text: {1}", string.Join(", ", dstAddresses), MessageText);


            SubmitMode mode = GetSubmitMode();
            switch (mode)
            {
                case SubmitMode.Payload:
                    builder.MessageInPayload();
                    break;
                case SubmitMode.ShortMessageWithSAR:
                    builder.Concatenation(ConcatenationType.SAR);
                    break;
            }



            try
            {
                IList<SubmitMultiResp> resp = await _client.SubmitAsync(builder);

                if (resp.All(x => x.Header.Status == CommandStatus.ESME_ROK))
                {
                    _log.Info("Submit succeeded. MessageIds: {0}", string.Join(",", resp.Select(x => x.MessageId)));
                }
                else
                {
                    _log.Warn("Submit failed. Status: {0}", string.Join(",", resp.Select(x => x.Header.Status.ToString())));
                }
            }
            catch (Exception ex)
            {
                _log.Error("Submit failed. Error: {0}", ex.Message);
            }

        }




        private async Task SubmitBatchMessages(string MessageText, string SrcAdr, string SrcAdrTON, string SrcAdrNPI, string DestAdr, string DestAdrTON, string DestAdrNPI, string RepeatTimes)
        {
            var sourceAddress = new SmeAddress(SrcAdr, (AddressTON)byte.Parse(SrcAdrTON), (AddressNPI)byte.Parse(SrcAdrNPI));

            var destinationAddress = new SmeAddress(DestAdr, (AddressTON)byte.Parse(DestAdrTON), (AddressNPI)byte.Parse(DestAdrNPI));


            string messageText = MessageText;

            SubmitMode mode = GetSubmitMode();

            DataCodings coding = GetDataCoding();

            int count = int.Parse(RepeatTimes);

            _log.Info("Submit message batch. Count: {0}. Text: {1}", count, messageText);

            // bulk sms test
            List<SubmitSm> batch = new List<SubmitSm>();
            for (int i = 0; i < count; i++)
            {
                ISubmitSmBuilder builder = SMS.ForSubmit()
                    .Text(messageText)
                    .From(sourceAddress)
                    .To(destinationAddress)
                    .Coding(coding);

                switch (mode)
                {
                    case SubmitMode.Payload:
                        builder.MessageInPayload();
                        break;
                    case SubmitMode.ShortMessageWithSAR:
                        builder.Concatenation(ConcatenationType.SAR);
                        break;
                }

                batch.AddRange(builder.Create(_client));

            }




            try
            {
                Stopwatch watch = Stopwatch.StartNew();

                var resp = (await _client.SubmitAsync(batch)).ToArray();

                watch.Stop();

                if (resp.All(x => x.Header.Status == CommandStatus.ESME_ROK))
                {
                    _log.Info("Batch sending completed. Submitted: {0}, Elapsed: {1} ms, Performance: {2} m/s", batch.Count, watch.ElapsedMilliseconds, batch.Count * 1000f / watch.ElapsedMilliseconds);
                }
                else
                {
                    var wrongStatuses = resp.Where(x => x.Header.Status != CommandStatus.ESME_ROK)
                        .Select(x => x.Header.Status).Distinct();

                    _log.Warn("Submit failed. Wrong Status: {0}", string.Join(", ", wrongStatuses));
                }
            }
            catch (Exception ex)
            {
                _log.Error("Submit failed. Error: {0}", ex.Message);
            }




        }

        private SubmitMode GetSubmitMode()
        {
            return (SubmitMode)System.Enum.Parse(typeof(SubmitMode), _SubmitMode);
        }

        private DataCodings GetDataCoding()
        {
            return (DataCodings)System.Enum.Parse(typeof(DataCodings), _DataCoding);
        }




        private void client_evDataSm(object sender, DataSm data)
        {
            _log.Info("DataSm received : Sequence: {0}, SourceAddress: {1}, DestAddress: {2}, Coding: {3}, Text: {4}",
                data.Header.Sequence, data.SourceAddress, data.DestinationAddress, data.DataCoding, data.GetMessageText(_client.EncodingMapper));
        }




        private async Task Connect()
        {
            try
            {
                if (_client.Status == ConnectionStatus.Closed)
                {
                    _log.Info("Connecting to " + _hostName);

                    _client.EsmeAddress = new SmeAddress("", (AddressTON)Convert.ToByte(_AddrTon), (AddressNPI)Convert.ToByte(_AddrNpi));
                    _client.SystemType = _SystemType;

                    _client.ConnectionRecovery = true;
                    _client.ConnectionRecoveryDelay = TimeSpan.FromSeconds(3);


                    if (_ssl)
                    {
                        _client.EnabledSslProtocols = SslProtocols.Default;
                        _client.ClientCertificates.Clear();
                        _client.ClientCertificates.Add(new X509Certificate2("client.p12", "12345"));
                    }
                    else
                    {
                        _client.EnabledSslProtocols = SslProtocols.None;
                    }


                    bool bSuccess = await _client.ConnectAsync(_hostName, Convert.ToInt32(_port));

                    if (bSuccess)
                    {
                        _log.Info("SmppClient connected");

                        await Bind();
                    }
                }
             } catch(Exception ex) {

                _log.Error("Error in Connect(): " + ex.Message.ToString());
            }
        }
           
        

    



        private async Task Bind()
        {
            _log.Info("Bind client with SystemId: {0}", _systemId);

            // can be set in appsettings
            ConnectionMode mode = ConnectionMode.Transceiver;
           
            BindResp resp = await _client.BindAsync(_systemId, _password, mode);

            switch (resp.Header.Status)
            {
                case CommandStatus.ESME_ROK:
                    _log.Info("Bind succeeded: Status: {0}, SystemId: {1}", resp.Header.Status, resp.SystemId);

                    break;
                default:
                    _log.Warn("Bind failed: Status: {0}", resp.Header.Status);

                    await Disconnect();
                    break;
            }
        }


        private async Task UnBind()
        {
            _log.Info("Unbind SmppClient");
            UnBindResp resp = await _client.UnbindAsync();

            switch (resp.Header.Status)
            {
                case CommandStatus.ESME_ROK:
                    _log.Info("UnBind succeeded: Status: {0}", resp.Header.Status);
                    break;
                default:
                    _log.Warn("UnBind failed: Status: {0}", resp.Header.Status);
                    await _client.DisconnectAsync();
                    break;
            }

        }



        private async Task Disconnect()
        {
            _log.Info("Disconnect from SMPP server");

            if (_client.Status == ConnectionStatus.Bound)
            {
                await UnBind();
            }

            if (_client.Status == ConnectionStatus.Open)
            {
                await _client.DisconnectAsync();
            }
        }


        private void client_evDeliverSm(object sender, DeliverSm data)
        {
            try
            {
                //Check if we received Delivery Receipt
                if (data.MessageType == MessageTypes.SMSCDeliveryReceipt)
                {
                    //Get MessageId of delivered message
                    string messageId = data.Receipt.MessageId;
                    MessageState deliveryStatus = data.Receipt.State;

                    _log.Info("Delivery Receipt received: {0}", data.Receipt.ToString());
                }
                else
                {

                    // Receive incoming message and try to concatenate all parts
                    if (data.Concatenation != null)
                    {
                        _messageComposer.AddMessage(data);

                        _log.Info("DeliverSm part received: Sequence: {0}, SourceAddress: {1}, Concatenation ( {2} )" +
                                " Coding: {3}, Text: {4}",
                                data.Header.Sequence, data.SourceAddress, data.Concatenation, data.DataCoding, _client.EncodingMapper.GetMessageText(data));
                    }
                    else
                    {
                        _log.Info("DeliverSm received : Sequence: {0}, SourceAddress: {1}, Coding: {2}, Text: {3}",
                            data.Header.Sequence, data.SourceAddress, data.DataCoding, _client.EncodingMapper.GetMessageText(data));
                    }

                    // Check if an ESME acknowledgement is required
                    if (data.Acknowledgement != SMEAcknowledgement.NotRequested)
                    {
                        // You have to clarify with SMSC support what kind of information they request in ESME acknowledgement.

                        string messageText = data.GetMessageText(_client.EncodingMapper);

                        var smBuilder = SMS.ForSubmit()
                            .From(data.DestinationAddress)
                            .To(data.SourceAddress)
                            .Coding(data.DataCoding)
                            .Concatenation(ConcatenationType.UDH8bit, _client.SequenceGenerator.NextReferenceNumber())
                            .Set(m => m.MessageType = MessageTypes.SMEDeliveryAcknowledgement)
                            .Text(new Receipt
                            {
                                DoneDate = DateTime.Now,
                                State = MessageState.Delivered,
                                //  MessageId = data.Response.MessageId,
                                ErrorCode = "0",
                                SubmitDate = DateTime.Now,
                                Text = messageText.Substring(0, Math.Min(20, messageText.Length))
                            }.ToString()
                            );



                        _client.SubmitAsync(smBuilder).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex)
            {
                data.Response.Header.Status = CommandStatus.ESME_RX_T_APPN;
                _log.Error(ex, "Failed to process DeliverSm");
            }
        }


    }
}


using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Configuration;
using System.Net.Mail;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Caching;
using log4net;
using Microsoft.SqlServer.Server;
using SendGrid;

namespace SQLDataSyncSender
{
    internal class ServerResponse
    {
        public HttpStatusCode StatusCode { get; set; }
        public string Message { get; set; }
    }

    public class SenderService
    {
        private string _sendToken = ConfigurationSettings.AppSettings["sendtoken"];
        private readonly ILog _log = LogManager.GetLogger(typeof(SenderService));
        private enum ProcessedState
        {
            Pending = 0,
            InProcess = 1,
            Success = 2,
            Failure = 3,
            FailureIgnore = 4
        }

        private void ResetErrors()
        {
            //forces anything set to failure status to be reset and retried
            //if the receiver was down, this saves having to manually intervene to sort it out and reset
            if(Convert.ToBoolean(ConfigurationSettings.AppSettings["alwaysreseterrors"]))
            {
                _log.Debug("Resetting any endpoints + messages in error");
                DataAccess.ExecuteSqlCommand("usp_ResetAllErrors", CommandType.StoredProcedure);
            }
        }

        public void ProcessQueue()
        {
            string url = "UNKNOWN";
            try
            {
                ResetErrors();
                int batchSize = Convert.ToInt32(ConfigurationSettings.AppSettings["batchsize"]);
                _log.Debug("Send process starts - batch size: " + batchSize);
                //read n items off queue
                for (var i = 0; i < batchSize; i++)
                {
                    using (var dr = DataAccess.GetSqlDataReader("usp_GetNextDataItem"))
                    {
                        if (dr.Read())
                        {
                            Int64 syncProcessingId = dr.GetInt64(0);
                            //send it   
                            bool ok = false;
                            url = string.Format("{0}/processmessage/{1}/{2}/{3}", dr["EndpointUrl"],
                                _sendToken,
                                dr["SystemName"], dr["EndpointDescription"]);
                            byte[] dataToSend = Encoding.UTF8.GetBytes(dr["SyncPackage"].ToString());
                            ServerResponse response = new ServerResponse();
                            _log.Debug("Sending POST to " + url);
                            //quick 3 attempts with 3s pause between each in case of connectivity issue
                            for (var attempts = 1; attempts <= 3; attempts++)
                            {
                                response = GetWebResponse(url, dataToSend);
                                if (response.StatusCode == HttpStatusCode.OK)
                                {
                                    //update processed state to done
                                    SetProcessedState(syncProcessingId, ProcessedState.Success);
                                    _log.Debug("Sent successfully");
                                    ok = true;
                                    break;
                                }
                                Thread.Sleep(3000);
                            }
                            if (!ok)
                            {
                                _log.Debug("Failed to send after 3 attempts - " + response.Message);
                                //it failed 3 times, we need to log and then park this system until we fix it
                                SetProcessedState(syncProcessingId, ProcessedState.Failure, response.Message);
                                SendFailureEmail(url,response.Message);
                            }
                        }
                        else
                        {
                            //nothing to do
                            _log.Debug("No further messages to send");
                            break;
                        }
                    }

                }
            }
            catch (Exception ex)
            {
                _log.Error(ex.Message, ex);
                SendFailureEmail(url, ex.Message);
            }
            finally
            {
                //finished
                _log.Debug("Processing completed");    
            }            
        }

        private ServerResponse GetWebResponse(string url, byte[] dataToSend)
        {
            try
            {
                var webRequest = (HttpWebRequest) WebRequest.Create(url);
                webRequest.Method = "POST";
                webRequest.ContentType = "text/xml; encoding='utf-8'";
                webRequest.ContentLength = dataToSend.Length;
                using (Stream dataStream = webRequest.GetRequestStream())
                {
                    dataStream.Write(dataToSend, 0, dataToSend.Length);
                }

                using (var resp = (HttpWebResponse) webRequest.GetResponse())
                {
                    return new ServerResponse {StatusCode = resp.StatusCode};
                }
            }
            catch (Exception ex)
            {
                return new ServerResponse {StatusCode = HttpStatusCode.InternalServerError, Message = ex.Message};
            }

        }

        private void SetProcessedState(long syncProcessingId, ProcessedState state, string info = "")
        {
            var parms = new List<SqlParameter>
                        {
                            new SqlParameter {ParameterName = "@SyncProcessingId", Value = syncProcessingId},
                            new SqlParameter {ParameterName = "@State", Value = state}
                        };
            if (info != "")
            {
                parms.Add(new SqlParameter {ParameterName = "@Info", Value = info});
            }
            DataAccess.ExecuteSqlCommand("usp_SetProcessedState", CommandType.StoredProcedure, parms);
        }

        private void SendFailureEmail(string url, string errorMessage)
        {
            try
            {
                SendGridMessage myMessage = new SendGridMessage();
                var sendFailsTo = ConfigurationManager.AppSettings["sendgridfailureto"];
                foreach (var sendFailTo in sendFailsTo.Split(Convert.ToChar(";")))
                {
                    if (!string.IsNullOrEmpty(sendFailTo))
                    {
                        myMessage.AddTo(sendFailTo);        
                    }
                }
                myMessage.From = new MailAddress(ConfigurationManager.AppSettings["sendgridfrom"], "SQL Data Sync Service");
                myMessage.Subject = "Failed to send message to receiver";

                myMessage.Text = string.Format("Attempted to send message to url {0}, received error {1}",url,errorMessage);

                var credentials = new NetworkCredential(ConfigurationManager.AppSettings["sendgriduser"], ConfigurationManager.AppSettings["sendgridpassword"]);
                var transportWeb = new Web(credentials);
                transportWeb.DeliverAsync(myMessage);
            }
            catch (Exception ex)
            {
                _log.Error("Could not send failure email",ex);
            }
            
        }


    }
}

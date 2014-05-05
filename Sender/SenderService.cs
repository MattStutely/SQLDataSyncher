using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Configuration;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Caching;
using log4net;

namespace SQLDataSyncSender
{
    internal class ServerResponse
    {
        public HttpStatusCode StatusCode { get; set; }
        public string Message { get; set; }
    }

    public class SenderService
    {
        private static string _sendToken = ConfigurationSettings.AppSettings["sendtoken"];
        private readonly ILog _log = LogManager.GetLogger(typeof(SenderService));
        private enum ProcessedState
        {
            Pending = 0,
            InProcess = 1,
            Success = 2,
            Failure = 3
        }

        public void ProcessQueue()
        {
            try
            {
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
                            string url = string.Format("{0}/processmessage/{1}/{2}/{3}", dr["EndpointUrl"], _sendToken,
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
                            }
                        }
                        else
                        {
                            //nothing to do
                            break;
                        }
                    }

                }
            }
            catch (Exception ex)
            {
                _log.Error(ex.Message, ex);
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


    }
}

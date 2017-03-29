using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Net;
using System.Net.Mail;
using System.Runtime.Remoting.Services;
using System.Threading;
using log4net;
using SendGrid;

namespace SQLDataSyncProcessor
{
    public class ProcessorService
    {
        private enum ProcessedState
        {
            Pending = 0,
            InProcess = 1,
            Success = 2,
            Failure = 3
        }

        private Dictionary<string,bool> _identityInsertCache = new Dictionary<string, bool>();
        private readonly ILog _log = LogManager.GetLogger(typeof(ProcessorService));

        private void ResetErrors()
        {
            //forces anything set to failure status to be reset and retried
            //if the receiver was down, this saves having to manually intervene to sort it out and reset
            if (Convert.ToBoolean(ConfigurationSettings.AppSettings["alwaysreseterrors"]))
            {
                _log.Debug("Resetting any endpoints + messages in error");
                DataAccess.ExecuteSqlAgainstReceiverDb("usp_ResetAllErrors", CommandType.StoredProcedure);
            }
        }

        public void ProcessQueue()
        {
            Int64 syncProcessingId = 0;
            try
            {
                ResetErrors();
                int batchSize = Convert.ToInt32(ConfigurationSettings.AppSettings["batchsize"]);
                _log.Debug("Starting processing - batch size: " + batchSize);
                //read n items off queue
                for (var i = 0; i < batchSize; i++)
                {
                    using (
                        var dr = DataAccess.GetSqlDataReaderFromReceiverDb("usp_GetNextDataItem",
                            CommandType.StoredProcedure))
                    {
                        if (dr.Read())
                        {
                            syncProcessingId = dr.GetInt64(0);
                            string sql = dr["SqlStatement"].ToString();
                            string tableName = dr["TableName"].ToString();
                            string conStr = dr["DbConStr"].ToString();
                            _log.Debug("Processing syncitem #" + syncProcessingId + " to table " + tableName);
                            //check if table name needs identity insert or not
                            if (!_identityInsertCache.ContainsKey(tableName + "_" + conStr))
                            {
                                _identityInsertCache.Add(tableName + "_" + conStr, CheckIfTableNeedsIdentityInsert(conStr, tableName));
                            }
                            bool identityInsert = _identityInsertCache[tableName + "_" + conStr];
                            bool isUpdate = !sql.StartsWith("DELETE");
                            if (isUpdate)
                            {
                                _log.Debug("Message is INSERT/UPDATE");
                                if (identityInsert)
                                {
                                    _log.Debug("Table requires identity insert");
                                }
                                else
                                {
                                    _log.Debug("Table DOES NOT requires identity insert");
                                }
                            }
                            else
                            {
                                _log.Debug("Message is DELETE");
                            }

                            //send it   
                            bool ok = false;
                            //quick 3 attempts with 3s pause between each in case of connectivity issue
                            _log.Debug("Executing SQL against target Db");
                            for (var attempts = 1; attempts <= 3; attempts++)
                            {
                                if (DataAccess.ExecuteSqlAgainstApplicationDb(conStr, tableName, identityInsert, sql))
                                {
                                    SetProcessedState(syncProcessingId, ProcessedState.Success);
                                    ok = true;
                                    _log.Debug("SQL executed successfully");
                                    syncProcessingId = 0;
                                    break;
                                }
                                Thread.Sleep(3000);
                            }
                            if (!ok)
                            {
                                //it failed 3 times, we need to log and then park this system until we fix it
                                _log.Error("Failed to process after 3 attempts - " + DataAccess.LastErrorMessage);
                                SetProcessedState(syncProcessingId, ProcessedState.Failure, DataAccess.LastErrorMessage);
                                SendFailureEmail(tableName,DataAccess.LastErrorMessage);
                            }

                        }
                        else
                        {
                            //nothing left
                            break;
                        }
                    }

                }
            }
            catch (Exception ex)
            {
                _log.Error(ex.Message, ex);
                if(syncProcessingId!=0)
                {
                    SetProcessedState(syncProcessingId, ProcessedState.Failure, DataAccess.LastErrorMessage);
                }
                
                SendFailureEmail("Unknown", ex.Message);
            }
            finally
            {
                //finished
                _log.Debug("Processing completed");    
            }
        }


        private bool CheckIfTableNeedsIdentityInsert(string conStr, string table)
        {
            bool isIdentity;
            using(SqlDataReader dr = DataAccess.GetSqlDataReaderFromApplicationDb(conStr,"SELECT OBJECTPROPERTY(OBJECT_ID(N'" + table + "'), 'TableHasIdentity')",CommandType.Text))
            {
                dr.Read();
                isIdentity = ((int)dr[0] == 1);
            }
            return isIdentity;
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
                parms.Add(new SqlParameter { ParameterName = "@Info", Value = info });
            }
            DataAccess.ExecuteSqlAgainstReceiverDb("usp_SetProcessedState", CommandType.StoredProcedure, parms);
        }

        private void SendFailureEmail(string tableName, string errorMessage)
        {
            try
            {
                var myMessage = new SendGridMessage
                {
                    From = new MailAddress(ConfigurationManager.AppSettings["sendgridfrom"], "SQL Data Sync Service")
                };
                var sendFailsTo = ConfigurationManager.AppSettings["sendgridfailureto"];
                foreach (var sendFailTo in sendFailsTo.Split(Convert.ToChar(";")))
                {
                    if (!string.IsNullOrEmpty(sendFailTo))
                    {
                        myMessage.AddTo(sendFailTo);
                    }
                }
                myMessage.Subject = "Failed to execute SQL against target DB";

                myMessage.Text = string.Format("Attempted to save to table {0}, received error {1}", tableName, errorMessage);

                var credentials = new NetworkCredential(ConfigurationManager.AppSettings["sendgriduser"], ConfigurationManager.AppSettings["sendgridpassword"]);
                var transportWeb = new Web(credentials);
                transportWeb.DeliverAsync(myMessage).Wait(60000);
            }
            catch (Exception ex)
            {
                _log.Error("Could not send failure email", ex);
            }

        }


    }
}

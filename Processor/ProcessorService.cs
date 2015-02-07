using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Runtime.Remoting.Services;
using System.Threading;
using log4net;

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

        public void ProcessQueue()
        {
            try
            {
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
                            Int64 syncProcessingId = dr.GetInt64(0);
                            string sql = dr["SqlStatement"].ToString();
                            string tableName = dr["TableName"].ToString();
                            string conStr = dr["DbConStr"].ToString();
                            _log.Debug("Processing syncitem #" + syncProcessingId + " to table " + tableName);
                            //check if table name needs identity insert or not
                            if (!_identityInsertCache.ContainsKey(tableName))
                            {
                                _identityInsertCache.Add(tableName, CheckIfTableNeedsIdentityInsert(conStr, tableName));
                            }
                            bool identityInsert = _identityInsertCache[tableName];
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
                                    break;
                                }
                                Thread.Sleep(3000);
                            }
                            if (!ok)
                            {
                                //it failed 3 times, we need to log and then park this system until we fix it
                                _log.Debug("Failed to process after 3 attempts - " + DataAccess.LastErrorMessage);
                                SetProcessedState(syncProcessingId, ProcessedState.Failure, DataAccess.LastErrorMessage);
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


    }
}

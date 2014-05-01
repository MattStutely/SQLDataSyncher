using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLDataSyncProcessor
{
    public static class DataAccess
    {

        private static SqlConnection GetReceiverConnection()
        {
            string dbConStr = ConfigurationSettings.AppSettings["dbconstr"];
            return new SqlConnection(dbConStr);
        }

        private static SqlConnection GetSqlConnection(string connectionString)
        {
            string dbConStr = connectionString;
            return new SqlConnection(dbConStr);
        }

        private static SqlDataReader GetSqlDataReader(SqlCommand cm, CommandType commandType, List<SqlParameter> parms = null, Int32 timeOut = 180)
        {
            try
            {
                if (cm.Connection.State != ConnectionState.Open) { cm.Connection.Open(); }
                cm.CommandType = commandType;
                cm.CommandTimeout = timeOut;
                if (parms != null)
                {
                    foreach (var parm in parms)
                    {
                        cm.Parameters.Add(parm);
                    }
                }
                return cm.ExecuteReader(CommandBehavior.CloseConnection);
            }
            catch (Exception ex)
            {
                LastErrorMessage = ex.Message;
                throw ex;
            }

        }

        public static SqlDataReader GetSqlDataReaderFromReceiverDb(string commandText, CommandType commandType, List<SqlParameter> parms = null, Int32 timeOut = 180)
        {
            using (SqlCommand cm = new SqlCommand(commandText, GetReceiverConnection()))
            {
                return GetSqlDataReader(cm, commandType, parms, timeOut);
            }
        }

        public static SqlDataReader GetSqlDataReaderFromApplicationDb(string connectionString, string commandText, CommandType commandType, List<SqlParameter> parms = null, Int32 timeOut = 180)
        {
            using (SqlCommand cm = new SqlCommand(commandText, GetSqlConnection(connectionString)))
            {
                return GetSqlDataReader(cm, commandType, parms, timeOut);
            }
        }

        private static void ExecuteSqlCommand(SqlCommand cm, string commandText,
            CommandType commandType = CommandType.Text, List<SqlParameter> parms = null, Int32 timeOut = 600)
        {
            if (cm.Connection.State != ConnectionState.Open)
            {
                cm.Connection.Open();
            }
            cm.CommandText = commandText;
            cm.CommandType = commandType;
            cm.CommandTimeout = timeOut;

            if (parms != null)
            {
                foreach (var parm in parms)
                {
                    cm.Parameters.Add(parm);
                }
            }
            cm.ExecuteNonQuery();
        }

        public static void ExecuteSqlAgainstReceiverDb(string commandText, CommandType commandType = CommandType.Text, List<SqlParameter> parms = null, Int32 timeOut = 600)
        {
            using (SqlCommand cm = new SqlCommand(commandText, GetReceiverConnection()))
            {
                ExecuteSqlCommand(cm, commandText, commandType, parms, timeOut);
            }
        }

        public static bool ExecuteSqlAgainstApplicationDb(string connectionString, string tableName, bool identityInsert, string commandText, CommandType commandType = CommandType.Text, List<SqlParameter> parms = null, Int32 timeOut = 600)
        {
            try
            {
                using (SqlCommand cm = new SqlCommand(commandText, GetSqlConnection(connectionString)))
                {
                    if (identityInsert && commandText.StartsWith("IF"))
                    {
                        commandText = "SET IDENTITY_INSERT " + tableName + " ON; " + commandText + "; SET IDENTITY_INSERT " +
                                      tableName + " OFF; ";
                    }
                    ExecuteSqlCommand(cm, commandText, commandType, parms, timeOut);
                }
                return true;
            }
            catch (Exception ex)
            {
                LastErrorMessage = ex.Message;
                return false;
            }
            
        }

        public static string LastErrorMessage { get; set; }
    }
}

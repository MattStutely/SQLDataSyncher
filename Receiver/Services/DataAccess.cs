using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services
{
    public static class DataAccess
    {

        private static SqlConnection GetSqlConnection()
        {
            string dbConStr = ConfigurationSettings.AppSettings["dbconstr"];
            return new SqlConnection(dbConStr);
        }

        public static SqlDataReader GetSqlDataReader(string commandText, List<SqlParameter> parms = null, Int32 timeOut = 180)
        {
            using (SqlCommand cm = new SqlCommand(commandText, GetSqlConnection()))
            {
                if (cm.Connection.State != ConnectionState.Open) { cm.Connection.Open(); }
                cm.CommandType = CommandType.StoredProcedure;
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
        }

        public static void ExecuteSqlCommand(string commandText, CommandType commandType = CommandType.Text, List<SqlParameter> parms = null, Int32 timeOut = 600)
        {
            using (SqlCommand cm = new SqlCommand(commandText, GetSqlConnection()))
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
        }
    }
}

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Xml;

namespace Services
{
    public class ReceiverService 
    {
        public HttpStatusCode ProcessMessage(string system, string endpoint,string messageToProcess)
        {
            try
            {
                //put the message into the db for processing
                int endpointId;
                List<SqlParameter> parms;
                parms = new List<SqlParameter>
                        {
                            new SqlParameter {ParameterName = "@System", Value = system},
                            new SqlParameter {ParameterName = "@Endpoint", Value = endpoint}
                        };

                using (var dr = DataAccess.GetSqlDataReader("usp_GetEndpoint", parms))
                {
                    if (dr.Read())
                    {
                        endpointId = (int)dr[0];
                    }
                    else
                    {
                        //no system or endpoint set up
                        return HttpStatusCode.NotFound;
                    }
                }
                //now write message to database

                //parse the message XML, then if wrong an exception is thrown
                XmlDocument package = new XmlDocument();
                package.LoadXml(messageToProcess);
                var syncId = Convert.ToInt64(package.SelectSingleNode("SyncPackage/SyncId").InnerText);
                var datestamp = Convert.ToDateTime(package.SelectSingleNode("SyncPackage/SyncDatestamp").InnerText);
                var tableName = package.SelectSingleNode("SyncPackage/TableName").InnerText;
                var sqlStatement = HttpUtility.HtmlDecode(package.SelectSingleNode("SyncPackage/SQLStatement").InnerText);

                parms = new List<SqlParameter>
                        {
                            new SqlParameter {ParameterName = "@SyncId", Value = syncId},
                            new SqlParameter {ParameterName = "@EndpointId", Value = endpointId},
                            new SqlParameter {ParameterName = "@Datestamp", Value = datestamp},
                            new SqlParameter {ParameterName = "@TableName", Value = tableName},
                            new SqlParameter {ParameterName = "@SQL", Value = sqlStatement},
                        };

                DataAccess.ExecuteSqlCommand("usp_WriteSyncPackage", CommandType.StoredProcedure, parms);

                return HttpStatusCode.OK;
            }
            catch (Exception ex)
            {
                //log it
                return HttpStatusCode.InternalServerError;
            }
            

        }




    }
}

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
using log4net;

namespace Services
{
    public class ReceiverService 
    {
        private readonly ILog _log = LogManager.GetLogger(typeof(ReceiverService));

        public HttpStatusCode EndpointExistsCheck(string system, string endpoint)
        {
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
                    return HttpStatusCode.Found;
                }
                else
                {
                    //no system or endpoint set up#
                    _log.Error(string.Format("Endpoint not configured for processing {0}/{1}",system,endpoint));
                    return HttpStatusCode.NotFound;
                }
            }
        }

        public HttpStatusCode ProcessMessage(string system, string endpoint,string messageToProcess)
        {
            try
            {
                _log.Debug("Processing messsage");
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
                        //no system or endpoint set up#
                        _log.Error("Endpoint not configured for processing");
                        return HttpStatusCode.NotFound;
                    }
                }
                //now write message to database

                _log.Debug("Writing message to db");
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
                _log.Error(ex.Message,ex);
                return HttpStatusCode.InternalServerError;
            }
            

        }

    }
}

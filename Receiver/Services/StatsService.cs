using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net;

namespace Services
{
    public class StatsService
    {
        private readonly ILog _log = LogManager.GetLogger(typeof(StatsService));

        public List<StatsData> GetStats()
        {
            List<StatsData> data = new List<StatsData>();
            using (var dr = DataAccess.GetSqlDataReader("usp_Stats"))
            {
                while (dr.Read())
                {
                    data.Add(new StatsData
                    {
                        SystemName = dr["SystemName"].ToString(),
                        EndpointName = dr["EndpointName"].ToString(),
                        IsActive = (bool)dr["IsActive"],
                        QueueSize = (int)dr["QueueSize"],
                        LastMessageReceived = (DateTime?)dr["LastMessageReceived"],
                        LastMessageProcessed = (DateTime?)dr["LastMessageProcessed"],
                        LastError =  (DateTime?)dr["LastError"],
                    });
                }
            }
            return data;
        }
    }

    public class StatsData
    {
        public string SystemName { get; set; }
        public string EndpointName{ get; set; }
        public bool IsActive{ get; set; }
        public int QueueSize{ get; set; }
        public DateTime? LastMessageReceived{ get; set; }
        public DateTime? LastMessageProcessed{ get; set; }
        public DateTime?  LastError{ get; set; }
    }
}

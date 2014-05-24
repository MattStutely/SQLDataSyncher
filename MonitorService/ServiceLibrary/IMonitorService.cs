using System.ServiceModel;
using System.ServiceModel.Web;

namespace ServiceLibrary
{
    [ServiceContract]
    public interface IMonitorService
    {
        [OperationContract]
        [WebGet(ResponseFormat = WebMessageFormat.Xml, UriTemplate = "GetSyncStats.xml")]
        string GetSyncStatsXml();

        [OperationContract]
        [WebGet(ResponseFormat = WebMessageFormat.Json, UriTemplate = "GetSyncStats.xml")]
        string GetSyncStatsJson();


    }
}
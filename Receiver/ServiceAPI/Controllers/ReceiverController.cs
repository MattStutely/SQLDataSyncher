using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Web.Http;
using log4net;
using Services;

namespace ServiceAPI
{
    public class ReceiverController : ApiController
    {
        private readonly ILog _log = LogManager.GetLogger(typeof(ReceiverController));
        private static List<string> _acceptTokens = ConfigurationSettings.AppSettings["accepttoken"].Split(',').ToList();
        private static string _statsToken = ConfigurationSettings.AppSettings["statstoken"];

        [HttpGet]
        [Route("status/{token}")]
        public List<StatsData> GetStatus(string token)
        {
            _log.Debug("Request for stats");
            //validate token
            if (token != _statsToken)
            {
                _log.Error("Token sent (" + token + ") is not valid");
                throw new UnauthorizedAccessException();
            }
            var statsService = new StatsService();
            return statsService.GetStats();
        }


        [HttpPost]
        [Route("processmessage/{token}/{system}/{endpoint}")]
        public HttpResponseMessage ProcessMessage(HttpRequestMessage message, string token, string system, string endpoint)
        {
            _log.Debug(string.Format("Message received for processing {0}/{1}",system,endpoint));
            //validate token
            if (!_acceptTokens.Contains(token))
            {
                _log.Error("Token sent (" + token + ") is not valid");
                throw new UnauthorizedAccessException();
            }
            var receiverService = new ReceiverService();
            var messageContent = message.Content.ReadAsStringAsync().Result;
            _log.Debug("Message content has been parsed successfully");
            var statusCode = receiverService.ProcessMessage(system, endpoint, messageContent);
            _log.Debug("Returning status code " + (int) statusCode);
            return Request.CreateResponse(statusCode);
        }
    }

}

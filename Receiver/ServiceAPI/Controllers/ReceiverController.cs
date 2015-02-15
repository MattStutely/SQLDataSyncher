using System;
using System.Collections.Generic;
using System.Configuration;
using System.Net.Http;
using System.Web.Http;
using log4net;
using Services;

namespace ServiceAPI
{
    public class ReceiverController : ApiController
    {
        private readonly ILog _log = LogManager.GetLogger(typeof(ReceiverController));
        private static string _acceptToken = ConfigurationSettings.AppSettings["accepttoken"];
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

        [HttpGet]
        [Route("endpointexists/{token}/{system}/{endpoint}")]
        public HttpResponseMessage EndpointExistsCheck(string token, string system, string endpoint)
        {
            //if this can be hit without error, then its alive
            _log.Debug(string.Format("Request to check endpoint alive at {0}/{1}",system,endpoint));
            if (token != _acceptToken)
            {
                _log.Error("Token sent (" + token + ") is not valid");
                throw new UnauthorizedAccessException();
            }
            var receiverService = new ReceiverService();
            var statusCode = receiverService.EndpointExistsCheck(system, endpoint);
            _log.Debug("Returning status code " + (int)statusCode);
            return Request.CreateResponse(statusCode);
        }

    [HttpPost]
        [Route("processmessage/{token}/{system}/{endpoint}")]
        public HttpResponseMessage ProcessMessage(HttpRequestMessage message, string token, string system, string endpoint)
        {
            _log.Debug(string.Format("Message received for processing {0}/{1}",system,endpoint));
            //validate token
            if (token != _acceptToken)
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

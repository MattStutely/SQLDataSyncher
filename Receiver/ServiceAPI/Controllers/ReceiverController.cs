using System;
using System.Configuration;
using System.Net.Http;
using System.Web.Http;
using Services;

namespace ServiceAPI
{
    public class ReceiverController : ApiController
    {
        private static string _acceptToken = ConfigurationSettings.AppSettings["accepttoken"];

        [HttpGet]
        [Route("status")]
        public void GetStatus()
        {
            
        }


    [HttpPost]
        [Route("processmessage/{token}/{system}/{endpoint}")]
        public int ProcessMessage(HttpRequestMessage message, string token, string system, string endpoint)
        {
            //validate token
            if (token != _acceptToken)
            {
                throw new UnauthorizedAccessException();
            }
            var receiverService = new ReceiverService();
            var messageContent = message.Content.ReadAsStringAsync().Result;
            var statusCode = receiverService.ProcessMessage(system, endpoint, messageContent);
            return (int) statusCode;
        }
    }

}

using System;
using System.Net.Http;
using System.Web.Http;
using Services;

namespace ServiceAPI
{
    public class ReceiverController : ApiController
    {

        [HttpPost]
        //[GetResultsAuthenticationFilter]
        [Route("processmessage/{system}/{endpoint}")]
        public int ProcessMessage(HttpRequestMessage message, string system, string endpoint)
        {
            try
            {
                var receiverService = new ReceiverService();
                var messageContent = message.Content.ReadAsStringAsync().Result;
                var statusCode = receiverService.ProcessMessage(system, endpoint, messageContent);
                return (int)statusCode;
            }
            catch (Exception ex)
            {
                return 500;
            }
        }

    }
}

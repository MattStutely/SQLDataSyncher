using System;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;

namespace SQLDataSyncReceiver.ServiceAPI
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public class BasicAuthenticationFilter : AuthorizationFilterAttribute
    {

        public override void OnAuthorization(HttpActionContext actionContext)
        {
            var identity = ParseAuthorizationHeader(actionContext);
            if (identity == null)
            {
                DenyAccess(actionContext);
                return;
            }


            if (!IsUserAuthorised(identity.Name, identity.Password, actionContext))
            {
                DenyAccess(actionContext);
                return;
            }

            var principal = new GenericPrincipal(identity, null);

            Thread.CurrentPrincipal = principal;

            // inside of ASP.NET this is required
            //if (HttpContext.Current != null)
            //    HttpContext.Current.User = principal;

            base.OnAuthorization(actionContext);
            
        }

        protected virtual bool IsUserAuthorised(string username, string password, HttpActionContext actionContext)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                return false;

            password = GetMd5Hash(password);
            return true;
        }

        protected virtual BasicAuthenticationIdentity ParseAuthorizationHeader(HttpActionContext actionContext)
        {
            string authHeader = null;
            var auth = actionContext.Request.Headers.Authorization;
            if (auth != null && auth.Scheme == "Basic")
                authHeader = auth.Parameter;

            if (string.IsNullOrEmpty(authHeader))
                return null;

            authHeader = Encoding.Default.GetString(Convert.FromBase64String(authHeader));

            var tokens = authHeader.Split(':');
            if (tokens.Length < 2)
                return null;

            return new BasicAuthenticationIdentity(tokens[0], tokens[1]);
        }

        protected void DenyAccess(HttpActionContext actionContext)
        {
            actionContext.Response = actionContext.Request.CreateResponse(HttpStatusCode.Unauthorized);
        }

        protected static string GetMd5Hash(string value)
        {
            var md5Hasher = MD5.Create();
            byte[] data = md5Hasher.ComputeHash((Encoding.Default.GetBytes(value)));
            var sBuilder = new StringBuilder();
            for (var i = 0; i < data.Length; i++)
            {
                sBuilder.Append(data[i].ToString("x2"));
            }
            return sBuilder.ToString();
        }

    }


    public class BasicAuthenticationIdentity : GenericIdentity
    {
        public BasicAuthenticationIdentity(string name, string password)
            : base(name, "Basic")
        {
            this.Password = password;
        }

        public string Password { get; set; }
    }
}
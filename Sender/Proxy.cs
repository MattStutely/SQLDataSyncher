using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Configuration;

/// <summary>
///  Authenticates to the proxy server using the webappssvcacc account.
/// </summary>
public class ServiceAccountProxy : IWebProxy
{
    private IWebProxy innerProxy;
    private ICredentials credentials;

    private ServiceAccountProxy(IWebProxy innerProxy)
    {
        this.innerProxy = innerProxy;
        // todo put these settings in the configuration class
        this.credentials = new NetworkCredential(
            ConfigurationManager.AppSettings["proxy.username"],
            ConfigurationManager.AppSettings["proxy.password"]
        );
    }

    /// <summary>
    ///  Returns the URI of a proxy.
    /// </summary>
    /// <param name="destination">
    ///  A URI that specifies the requested Internet resource.
    /// </param>
    /// <returns>
    ///  The URI of the proxy server for this request.
    /// </returns>
    public Uri GetProxy(Uri destination)
    {
        return this.innerProxy.GetProxy(destination);
    }

    /// <summary>
    ///  Indicates that the proxy should not be used for the specified host.
    /// </summary>
    /// <param name="host">
    ///  The Uri of the host to check for proxy use.
    /// </param>
    /// <returns>
    ///  true if the proxy is to be bypassed, otherwise false.
    /// </returns>
    public bool IsBypassed(Uri host)
    {
        return this.innerProxy.IsBypassed(host);
    }

    /// <summary>
    ///  Returns a new <see cref="ICredentials" /> instance containing the user name
    ///  and password to use to connect to the proxy.
    /// </summary>
    public ICredentials Credentials
    {
        get { return this.credentials; }
        set { /* do nothing */ }
    }

    /// <summary>
    ///  Sets up the default web proxy to be an instance of this class.
    /// </summary>
    public static void SetDefault()
    {
        bool bypassCustom;
        if (bool.TryParse(ConfigurationManager.AppSettings["proxy.nocustom"], out bypassCustom))
        {
            if (bypassCustom)
            {
                return;
            }
        }

        if (!(WebRequest.DefaultWebProxy is ServiceAccountProxy))
        {
            WebRequest.DefaultWebProxy = new ServiceAccountProxy(WebRequest.DefaultWebProxy);
        }
    }
}

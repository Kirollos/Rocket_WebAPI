/*
    See LICENSE file for license info.
*/

using System;
using System.Threading;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Serialization;
using Rocket.API;
using Rocket.Unturned;
using Rocket.Unturned.Logging;
using Rocket.Unturned.Plugins;
using Rocket.Unturned.Player;
using SDG.Unturned;
using UnityEngine;
using SDG;

namespace WebAPIPlugin
{
    public class HTTPServer
    {
        public HttpListener listener;
        public List<string> WhitelistIPs;

        public HTTPServer()
        {
            listener = new HttpListener();
            listener.Prefixes.Add("http://*:" + WebAPI.dis.Configuration.Port + "/");
            listener.Start();

            WhitelistIPs = new List<string>() { };
            if(WebAPI.dis.Configuration.WhitelistIPs.Count > 0)
            {
                foreach(var ip in WebAPI.dis.Configuration.WhitelistIPs)
                {
                    WhitelistIPs.Add(ip.IP);
                }
            }

            AsyncCallback callback = null;
            callback = ar =>
                 {
                     this.ProcessClient(
                     listener.EndGetContext(ar)
                     );
                     Logger.LogWarning("done");
                     listener.BeginGetContext(callback, null);
                 };
            listener.BeginGetContext(callback, null);
        }

        public void ProcessClient(HttpListenerContext client)
        {
            if(this.WhitelistIPs.Count > 0 && !this.WhitelistIPs.Contains(client.Request.RemoteEndPoint.Address.ToString()))
            {
                Write(client, "Error: Access denied.\r\nYour IP is not whitelisted.", HttpStatusCode.Forbidden);
            }
            string path = client.Request.Url.AbsolutePath.Trim();
            if(path == "/api" || path == "/api/")
            {
                Write(client, "Valid area, but under construction.");
            }
            else if(path.StartsWith("/api/") && path != "/api/")
            {
                uAPI.Parse(client, path);
            }
            else
            {
                Write(client, "You are in the wrong place.", HttpStatusCode.Forbidden);
            } 
            client.Response.OutputStream.Close();
        }

        public static void Write(HttpListenerContext client, string message, HttpStatusCode sc = HttpStatusCode.OK)
        {
            client.Response.StatusCode = (int)sc;
            client.Response.ContentLength64 = message.Length;
            client.Response.ContentType = "text/html";
            client.Response.OutputStream.Write(new UTF8Encoding().GetBytes(message), 0, message.Length);
        }
    }
}

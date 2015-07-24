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
        public static Dictionary<string, DateTime> sessions = new Dictionary<string, DateTime>() { };

        private Dictionary<string, string> extensions = new Dictionary<string, string>()
        {
            { "htm", "text/html" },
            { "html", "text/html" },
            { "js", "application/javascript"},
            { "json", "application/javascript"},
            { "xml", "text/xml" },
            { "txt", "text/plain" },
            { "css", "text/css" },
            { "png", "image/png" },
            { "gif", "image/gif" },
            { "jpg", "image/jpg" },
            { "jpeg", "image/jpeg" },
            { "zip", "application/zip"}
        };

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
                     if (!listener.IsListening)
                         return;
                     new Thread(() =>
                     {
                         this.ProcessClient(
                         listener.EndGetContext(ar)
                         );
                     }).Start();
                     listener.BeginGetContext(callback, null);
                 };
            listener.BeginGetContext(callback, null);
        }

        public void ProcessClient(HttpListenerContext client)
        {
            string path = client.Request.Url.AbsolutePath.Trim();
            if(this.WhitelistIPs.Count > 0 && !this.WhitelistIPs.Contains(client.Request.RemoteEndPoint.Address.ToString()))
            {
                Write(client, "Error: Access denied.\r\nYour IP is not whitelisted.", HttpStatusCode.Forbidden);
                client.Response.OutputStream.Close();
                Logger.LogWarning("WebAPI Warning: Client (" + client.Request.RemoteEndPoint.ToString() + ") failed to send request to " + path + ".");
                return;
            }
            client.Response.Headers.Add("Access-Control-Allow-Origin: *");
            client.Response.Headers.Add("Access-Control-Allow-Credentials: true");
            if(path == "/api" || path == "/api/")
            {
                Write(client, "Valid area, but under construction.");
            }
            else if(path.StartsWith("/api/") && path != "/api/")
            {
                uAPI.Parse(client, path);
            }
            else if(path == "/panel" || path.StartsWith("/panel/"))
            {
                string f = "", t = "text/html";

                if(path == "/panel" || path == "/panel/")
                {
                    //f = "index.html";
                    client.Response.Headers.Add("Location: /panel/index.html");
                    Write(client, "", HttpStatusCode.Redirect);
                    client.Response.OutputStream.Close();
                    return;
                }
                else
                {
                    f = path.Remove(0, "/panel".Length);
                }

                foreach (var ext in extensions)
                {
                    if (path.EndsWith(ext.Key))
                    {
                        t = ext.Value;
                        break;
                    }
                    t = "text/plain";
                }

                try
                {
                    Write(client, File.ReadAllBytes(WebAPI.WebPanelFiles + f), HttpStatusCode.OK, t);
                }
                catch
                {
                    Write(client, "File not found.", HttpStatusCode.NotFound);
                    return;
                }
            }
            else
            {
                Write(client, "You are in the wrong place.", HttpStatusCode.Forbidden);
            } 
            client.Response.OutputStream.Close();
            Logger.Log("Client ("+client.Request.RemoteEndPoint.ToString()+") requested " + path);
        }

        public static void Write(HttpListenerContext client, string message, HttpStatusCode sc = HttpStatusCode.OK, string ContentType = "text/html")
        {
            client.Response.StatusCode = (int)sc;
            client.Response.ContentLength64 = message.Length;
            client.Response.ContentType = ContentType;
            client.Response.OutputStream.Write(new UTF8Encoding().GetBytes(message), 0, message.Length);
        }

        public static void Write(HttpListenerContext client, byte[] message, HttpStatusCode sc = HttpStatusCode.OK, string ContentType = "text/html")
        {
            client.Response.StatusCode = (int)sc;
            client.Response.ContentLength64 = message.Length;
            client.Response.ContentType = ContentType;
            client.Response.OutputStream.Write(message, 0, message.Length);
        }

        /*
         * Idea based on (http://stackoverflow.com/questions/730268/unique-random-string-generation)
         */

        public static string GenerateSessionID(HttpListenerContext client)
        {
            StringBuilder final = new StringBuilder();
            final.Append(client.Request.RemoteEndPoint.Address.ToString() + "|");
            final.Append(DateTime.Now);
            int len = final.Length;
            System.Random random = new System.Random();

            for(;final.Length < len + 10;)
            {
                final.Append(Convert.ToChar(Convert.ToInt32(Math.Floor(26*random.NextDouble() + 65))));
            }
            return final.ToString();
        }
    }
}

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
// JSON.NET
using Newtonsoft.Json;

namespace WebAPIPlugin
{
    public class uAPI
    {
        public static Dictionary<string, object> responsejson = new Dictionary<string, object>() { };
        private static string response {get{return JsonConvert.SerializeObject(responsejson);}}

        public static void Parse(HttpListenerContext client, string path)
        {
            responsejson.Clear();
            if(WebAPI.dis.Configuration.AuthType == "HTTPAuth")
            {
                if(!client.Request.Headers.AllKeys.Contains("Authorization"))
                {
                    /*{
                        "status": "error",
                        "response": {
                            "error": "No password entered."
                        }
                    }*/
                    responsejson.Add("status", "error");
                    responsejson.Add("response", new Dictionary<string, string>() { { "error", "No password entered." } });
                    HTTPServer.Write(client, response);
                    return;
                }
                if (client.Request.Headers.GetValues("Authorization")[0] != WebAPI.dis.Configuration.UserPass)
                {
                    /*{
                        "status": "error",
                        "response": {
                            "error": "Password Incorrect."
                        }
                    }*/
                    responsejson.Add("status", "error");
                    responsejson.Add("response", new Dictionary<string, string>() { { "error", "Password Incorrect." } });
                    HTTPServer.Write(client, response);
                    return;
                }
            }
            else if (WebAPI.dis.Configuration.AuthType == "GETQuery")
            {
                string pass = uAPI.GetFromQuery(client.Request.Url.Query, "auth");
                if(pass != null)
                {
                    if(pass != WebAPI.dis.Configuration.UserPass)
                    {
                        /*{
                            "status": "error",
                            "response": {
                                "error": "Password Incorrect."
                            }
                        }*/
                        responsejson.Add("status", "error");
                        responsejson.Add("response", new Dictionary<string, string>() { { "error", "Password Incorrect." } });
                        HTTPServer.Write(client, response);
                        return;
                    }
                }
                else
                {
                    /*{
                        "status": "error",
                        "response": {
                            "error": "No password entered."
                        }
                    }*/
                    responsejson.Add("status", "error");
                    responsejson.Add("response", new Dictionary<string, string>() { { "error", "No password entered." } });
                    HTTPServer.Write(client, response);
                    return;
                }
            }
            string[] request = path.Remove(0, "/api/".Length).Split(new[]{'/'});

            if(request[0] == "players")
            {
                if(request.Length >= 2 && request[1] == "list")
                {
                    /*{
                        "status": "success",
                        "response": {
                            "players": [
                                {
                                    all data in RocketPlayer
                                },
                                {
                                    all data in RocketPlayer
                                },
                                ....
                            ]
                        }
                    }*/
                    responsejson.Add("status", "success");
                    List<Dictionary<string,object>> finalarr = new List<Dictionary<string,object>>() { };
                    var plist = Steam.Players;
                    foreach(var player in plist)
                    {
                        Dictionary<string, object> pa = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) { };
                        Dictionary<string, object> finalpa = new Dictionary<string, object>() { };
                        RocketPlayer p = RocketPlayer.FromSteamPlayer(player);
                        pa.Add("SteamID", p.CSteamID.ToString());
                        pa.Add("SteamName", p.SteamName);
                        pa.Add("CharacterName", p.CharacterName);
                        pa.Add("Ping", player.ping);
                        pa.Add("Bleeding", p.Bleeding);
                        pa.Add("Broken", p.Broken);
                        pa.Add("Dead", p.Dead);
                        pa.Add("Experience", p.Experience);
                        pa.Add("Freezing", p.Freezing);
                        pa.Add("Health", p.Health);
                        pa.Add("Hunger", p.Hunger);
                        pa.Add("Infection", p.Infection);
                        pa.Add("IsAdmin", p.IsAdmin);
                        pa.Add("IsPro", p.IsPro);
                        pa.Add("Position", new Dictionary<string, float>() { 
                                {"X", p.Position.x},
                                {"Y", p.Position.y},
                                {"Z", p.Position.z}
                            });
                        pa.Add("Rotation", p.Rotation);
                        pa.Add("Stamina", p.Stamina);
                        pa.Add("SteamGroupID", p.SteamGroupID.ToString());
                        pa.Add("Thirst", p.Thirst);

                        if((request.Length == 3 && (request[2] == "all" || String.IsNullOrEmpty(request[2]))) || request.Length == 2)
                            finalarr.Add(pa);
                        else if(request.Length == 3)
                        {
                            var fields = request[2].Trim().Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries);
                            foreach(var field in fields)
                            {
                                if (!pa.ContainsKey(field))
                                    continue;
                                finalpa.Add(field, pa[field]);
                            }
                            finalarr.Add(finalpa);
                        }
                    }
                    responsejson.Add("response", new Dictionary<string, List<Dictionary<string, object>>>(){{"players", finalarr}});
                    HTTPServer.Write(client, response);
                }
            }
            else if(request[0] == "server")
            {
                if(request.Length >= 2 && request[1] == "info")
                {
                    responsejson.Add("status", "success");
                    Dictionary<string, object> sa = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) { };
                    Dictionary<string, object> finalsa = new Dictionary<string, object>() { };
                    sa.Add("InstanceName", Steam.InstanceName);
                    sa.Add("IsPassworded", !String.IsNullOrEmpty(Steam.serverPassword));
                    sa.Add("IsPVP", Steam.isPvP);
                    sa.Add("SecurityType", Steam.security.ToString());
                    sa.Add("Map", Steam.map);
                    sa.Add("Players", Steam.Players.Count);
                    sa.Add("MaxPlayers", Steam.maxPlayers);
                    sa.Add("Mode", Steam.mode.ToString());
                    sa.Add("Name", Steam.serverName);
                    if ((request.Length == 3 && (request[2] == "all" || String.IsNullOrEmpty(request[2]))) || request.Length == 2)
                        responsejson.Add("response", new Dictionary<string, object>() { {"serverinfo", sa} });
                    else if(request.Length == 3)
                    {
                        var fields = request[2].Trim().Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var field in fields)
                        {
                            if (!sa.ContainsKey(field))
                                continue;
                            finalsa.Add(field, sa[field]);
                        }
                        responsejson.Add("response", new Dictionary<string, object>() { { "serverinfo", finalsa } });
                    }
                    HTTPServer.Write(client, response);
                }
            }
        }

        public static string GetFromQuery(string query, string name)
        {
            if (query.Length < 1)
                return null;
            query = query.Remove(0, 1); // remove the ?
            string[] queryarr = query.Trim().Split(new[] { "&" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var k in queryarr) Logger.Log(k);
            List<string> queryarr_ = queryarr.ToList<string>();
            foreach (var k in queryarr_) Logger.LogWarning(k);
            foreach (var element in queryarr_)
            {
                if (element.Contains(name) && element.Contains('='))
                {
                    return element.Trim().Split(new[] { '=' })[1];
                }
            }
            return null;
        }
    }
}

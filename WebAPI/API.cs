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
using Rocket.Core.Logging;
using Rocket.Core.Plugins;
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
            
            string[] request = path.Remove(0, "/api/".Length).Split(new[]{'/'});

            if(request[0] == "newsession")
            {
                if(request.Length == 1 || (request.Length == 2 && String.IsNullOrEmpty(request[1]))) // ONLY /api/newsession/
                {
                    foreach(var session in HTTPServer.sessions)
                    {
                        if(session.Key.StartsWith(client.Request.RemoteEndPoint.Address.ToString()+"|"))
                        {
                            if (session.Value.AddMinutes(30) > DateTime.Now)
                            {
                                /*{
                                    "status": "error",
                                    "response": {
                                        "error": "Session has not expired yet."
                                    }
                                }*/
                                responsejson.Add("status", "error");
                                responsejson.Add("response", new Dictionary<string, string>() { { "error", "Session has not expired yet." } });
                                HTTPServer.Write(client, response);
                                return;
                            }
                            else
                            {
                                // Expired, let's give them a chance to re-generate.
                                HTTPServer.sessions.Remove(session.Key);
                                break;
                            }
                        }
                    }

                    if (!AllowAccess(client, true)) // Make sure they enter the correct password
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

                    string sessionid = HTTPServer.GenerateSessionID(client);
                    DateTime tiem = DateTime.Now;
                    HTTPServer.sessions.Add(sessionid, tiem);

                    /*{
                        "status": "success",
                        "response": {
                            "sessionid": "blablabla",
                            "GeneratedAt": "blablabla",
                            "ExpiresAt": "blablabla",
                        }
                    }*/
                    responsejson.Add("status", "success");
                    responsejson.Add("response", new Dictionary<string, string>() { 
                        {"sessionid", sessionid},
                        {"GeneratedAt", tiem.ToString()},
                        {"ExpiresAt", tiem.AddMinutes(30).ToString()}
                    });
                    HTTPServer.Write(client, response);
                    return;
                }
            }

            if (!AllowAccess(client)) return;

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
                        UnturnedPlayer p = UnturnedPlayer.FromSteamPlayer(player);
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
                        pa.Add("Groups", Rocket.Core.R.Permissions.GetGroups(p, true));
                        pa.Add("Permissions", p.GetPermissions());

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
                else if(request[1] == "kick")
                {
                    UnturnedPlayer p = null;
                    if (request.Length >= 3)
                    {
                        string _cn, _sn, _sid, _rs;
                        switch (request[2])
                        {
                            case "steamid":
                                p = UnturnedPlayer.FromName(request[3]);
                                if (p == null)
                                {
                                    responsejson.Add("status", "error");
                                    responsejson.Add("response", new Dictionary<string, string>() { { "error", "Player not found." } });
                                    HTTPServer.Write(client, response);
                                    break;
                                }
                                _cn = p.CharacterName;
                                _sn = p.SteamName;
                                _sid = p.CSteamID.ToString();
                                _rs = request.Length == 5 ? request[4] : "Kicked via WebAPI";
                                p.Kick(_rs);
                                responsejson.Add("status", "success");
                                responsejson.Add("response", new Dictionary<string, string>()
                                {
                                    { "kCharacterName", _cn },
                                    { "kSteamName", _sn },
                                    { "kSteamID", _sid },
                                    { "kReason", _rs }
                                });
                                HTTPServer.Write(client, response);
                                break;
                            case "steamname":
                                p = UnturnedPlayer.FromName(request[3]);
                                if (p == null)
                                {
                                    responsejson.Add("status", "error");
                                    responsejson.Add("response", new Dictionary<string, string>() { { "error", "Player not found." } });
                                    HTTPServer.Write(client, response);
                                    break;
                                }
                                _cn = p.CharacterName;
                                _sn = p.SteamName;
                                _sid = p.CSteamID.ToString();
                                _rs = request.Length == 5 ? request[4] : "Kicked via WebAPI";
                                p.Kick(_rs);
                                responsejson.Add("status", "success");
                                responsejson.Add("response", new Dictionary<string, string>()
                                {
                                    { "kCharacterName", _cn },
                                    { "kSteamName", _sn },
                                    { "kSteamID", _sid },
                                    { "kReason", _rs }
                                });
                                HTTPServer.Write(client, response);
                                break;
                            case "charactername":
                                p = UnturnedPlayer.FromName(request[3]);
                                if (p == null)
                                {
                                    responsejson.Add("status", "error");
                                    responsejson.Add("response", new Dictionary<string, string>() { { "error", "Player not found." } });
                                    HTTPServer.Write(client, response);
                                    break;
                                }
                                _cn = p.CharacterName;
                                _sn = p.SteamName;
                                _sid = p.CSteamID.ToString();
                                _rs = request.Length == 5 ? request[4] : "Kicked via WebAPI";
                                p.Kick(_rs);
                                responsejson.Add("status", "success");
                                responsejson.Add("response", new Dictionary<string, string>()
                                {
                                    { "kCharacterName", _cn },
                                    { "kSteamName", _sn },
                                    { "kSteamID", _sid },
                                    { "kReason", _rs }
                                });
                                HTTPServer.Write(client, response);
                                break;
                            case "id":
                                p = UnturnedPlayer.FromSteamPlayer(Steam.Players[int.Parse(request[3])]);
                                if (p == null)
                                {
                                    responsejson.Add("status", "error");
                                    responsejson.Add("response", new Dictionary<string, string>() { { "error", "Player not found." } });
                                    HTTPServer.Write(client, response);
                                    break;
                                }
                                _cn = p.CharacterName;
                                _sn = p.SteamName;
                                _sid = p.CSteamID.ToString();
                                _rs = request.Length == 5 ? request[4] : "Kicked via WebAPI";
                                p.Kick(_rs);
                                responsejson.Add("status", "success");
                                responsejson.Add("response", new Dictionary<string, string>()
                                {
                                    { "kCharacterName", _cn },
                                    { "kSteamName", _sn },
                                    { "kSteamID", _sid },
                                    { "kReason", _rs }
                                });
                                HTTPServer.Write(client, response);
                                break;
                            default:
                                responsejson.Add("status", "error");
                                responsejson.Add("response", new Dictionary<string, string>() { { "error", "Invalid type. Supported types: steamname/steamid/charactername/id" } });
                                HTTPServer.Write(client, response);
                                break;
                        }
                    }
                    else
                    {
                        responsejson.Add("status", "error");
                        responsejson.Add("response", new Dictionary<string, string>() { { "error", "Missing Parameters." } });
                        HTTPServer.Write(client, response);
                    }
                }
                else if (request[1] == "ban")
                {
                    UnturnedPlayer p = null;
                    if (request.Length >= 3)
                    {
                        string _cn, _sn, _sid, _rs, _du;
                        switch (request[2])
                        {
                            case "steamid":
                                p = UnturnedPlayer.FromName(request[3]);
                                if (p == null)
                                {
                                    responsejson.Add("status", "error");
                                    responsejson.Add("response", new Dictionary<string, string>() { { "error", "Player not found." } });
                                    HTTPServer.Write(client, response);
                                    break;
                                }
                                _cn = p.CharacterName;
                                _sn = p.SteamName;
                                _sid = p.CSteamID.ToString();
                                _rs = request.Length == 6 ? request[5] : "Banned via WebAPI";
                                _du = request[4];
                                p.Ban(_rs, uint.Parse(_du));
                                responsejson.Add("status", "success");
                                responsejson.Add("response", new Dictionary<string, string>()
                                {
                                    { "bCharacterName", _cn },
                                    { "bSteamName", _sn },
                                    { "bSteamID", _sid },
                                    { "bDuration", _du },
                                    { "bReason", _rs }
                                });
                                HTTPServer.Write(client, response);
                                break;
                            case "steamname":
                                p = UnturnedPlayer.FromName(request[3]);
                                if (p == null)
                                {
                                    responsejson.Add("status", "error");
                                    responsejson.Add("response", new Dictionary<string, string>() { { "error", "Player not found." } });
                                    HTTPServer.Write(client, response);
                                    break;
                                }
                                _cn = p.CharacterName;
                                _sn = p.SteamName;
                                _sid = p.CSteamID.ToString();
                                _rs = request.Length == 6 ? request[5] : "Banned via WebAPI";
                                _du = request[4];
                                p.Ban(_rs, uint.Parse(_du));
                                responsejson.Add("status", "success");
                                responsejson.Add("response", new Dictionary<string, string>()
                                {
                                    { "bCharacterName", _cn },
                                    { "bSteamName", _sn },
                                    { "bSteamID", _sid },
                                    { "bDuration", _du },
                                    { "bReason", _rs }
                                });
                                HTTPServer.Write(client, response);
                                break;
                            case "charactername":
                                p = UnturnedPlayer.FromName(request[3]);
                                if (p == null)
                                {
                                    responsejson.Add("status", "error");
                                    responsejson.Add("response", new Dictionary<string, string>() { { "error", "Player not found." } });
                                    HTTPServer.Write(client, response);
                                    break;
                                }
                                _cn = p.CharacterName;
                                _sn = p.SteamName;
                                _sid = p.CSteamID.ToString();
                                _rs = request.Length == 6 ? request[5] : "Banned via WebAPI";
                                _du = request[4];
                                p.Ban(_rs, uint.Parse(_du));
                                responsejson.Add("status", "success");
                                responsejson.Add("response", new Dictionary<string, string>()
                                {
                                    { "bCharacterName", _cn },
                                    { "bSteamName", _sn },
                                    { "bSteamID", _sid },
                                    { "bDuration", _du },
                                    { "bReason", _rs }
                                });
                                HTTPServer.Write(client, response);
                                break;
                            case "id":
                                p = UnturnedPlayer.FromSteamPlayer(Steam.Players[int.Parse(request[3])]);
                                if (p == null)
                                {
                                    responsejson.Add("status", "error");
                                    responsejson.Add("response", new Dictionary<string, string>() { { "error", "Player not found." } });
                                    HTTPServer.Write(client, response);
                                    break;
                                }
                                _cn = p.CharacterName;
                                _sn = p.SteamName;
                                _sid = p.CSteamID.ToString();
                                _rs = request.Length == 6 ? request[5] : "Banned via WebAPI";
                                _du = request[4];
                                p.Ban(_rs, uint.Parse(_du));
                                responsejson.Add("status", "success");
                                responsejson.Add("response", new Dictionary<string, string>()
                                {
                                    { "bCharacterName", _cn },
                                    { "bSteamName", _sn },
                                    { "bSteamID", _sid },
                                    { "bDuration", _du },
                                    { "bReason", _rs }
                                });
                                HTTPServer.Write(client, response);
                                break;
                            default:
                                responsejson.Add("status", "error");
                                responsejson.Add("response", new Dictionary<string, string>() { { "error", "Invalid type. Supported types: steamname/steamid/charactername/id" } });
                                HTTPServer.Write(client, response);
                                break;
                        }
                    }
                    else
                    {
                        responsejson.Add("status", "error");
                        responsejson.Add("response", new Dictionary<string, string>() { { "error", "Missing Parameters." } });
                        HTTPServer.Write(client, response);
                    }
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
            List<string> queryarr_ = queryarr.ToList<string>();
            
            foreach (var element in queryarr_)
            {
                if (element.Contains(name) && element.Contains('='))
                {
                    return element.Trim().Split(new[] { '=' })[1];
                }
            }
            return null;
        }

        public static string GetFromCookie(string cookiestr, string name)
        {
            if (cookiestr.Length < 1)
                return null;
            string[] cookiearr = cookiestr.Trim().Split(new[] { ";" }, StringSplitOptions.RemoveEmptyEntries);
            List<string> cookiearr_ = cookiearr.ToList<string>();

            foreach(var element in cookiearr_)
            {
                if(element.Contains(name) && element.Contains('='))
                {
                    return element.Trim().Split(new[] { '=' })[1].Trim(new[] {' ', '\t'});
                }
            }
            return null;
        }

        public static bool AllowAccess(HttpListenerContext client, bool nocookie = false)
        {
            if (!nocookie)
            {
                if (client.Request.Headers.AllKeys.Contains("Cookie"))
                {
                    string sessionid = GetFromCookie(client.Request.Headers.GetValues("Cookie")[0], "sessionid");

                    if (HTTPServer.sessions.ContainsKey(sessionid))
                    {
                        if (HTTPServer.sessions[sessionid].AddMinutes(30) < DateTime.Now)
                        {
                            /*{
                                "status": "error",
                                "response": {
                                    "error": "Session expired."
                                }
                            }*/
                            responsejson.Add("status", "error");
                            responsejson.Add("response", new Dictionary<string, string>() { { "error", "Session Expired." } });
                            HTTPServer.Write(client, response);
                            HTTPServer.sessions.Remove(sessionid);
                            return false;
                        }

                        if (!sessionid.StartsWith(client.Request.RemoteEndPoint.Address.ToString() + "|"))
                        {
                            /*{
                                "status": "error",
                                "response": {
                                    "error": "Session refused."
                                }
                            }*/
                            responsejson.Add("status", "error");
                            responsejson.Add("response", new Dictionary<string, string>() { { "error", "Session refused." } });
                            HTTPServer.Write(client, response);
                            return false;
                        }
                        return true;
                    }
                    /* else { no problem, let them try with pass vvvv } */
                }
            }

            if (WebAPI.dis.Configuration.Instance.AuthType == "HTTPAuth")
            {
                if (!client.Request.Headers.AllKeys.Contains("Authorization"))
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
                    return false;
                }
                if (client.Request.Headers.GetValues("Authorization")[0] != WebAPI.dis.Configuration.Instance.UserPass)
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
                    return false;
                }
            }
            else if (WebAPI.dis.Configuration.Instance.AuthType == "GETQuery")
            {
                string pass = uAPI.GetFromQuery(client.Request.Url.Query, "auth");
                if (pass != null)
                {
                    if (pass != WebAPI.dis.Configuration.Instance.UserPass)
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
                        return false;
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
                    return false;
                }
            }
            return true;
        }
    }
}

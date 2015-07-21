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
    public class WebAPIConfiguration : IRocketPluginConfiguration
    {
        public bool Enabled;
        public short Port;
        public string UserPass;
        public string AuthType;
        public int maxconnections;
        public bool WebPanel;

        [XmlArrayItem(ElementName = "IP")] // WhitelistIP was not good ok
        public List<WebAPI_WhitelistIP> WhitelistIPs;

        public IRocketPluginConfiguration DefaultConfiguration
        {
            get
            {
                return new WebAPIConfiguration()
                {
                    Enabled = false,
                    Port = 0,
                    UserPass = "user:pass",
                    AuthType = "GETQuery",
                    maxconnections = 5,
                    WebPanel = false,
                    WhitelistIPs = new List<WebAPI_WhitelistIP>() {}
                };
            }
        }
    }
    public class WebAPI_WhitelistIP
    {
        [XmlText()]
        public string IP;
        WebAPI_WhitelistIP() { IP = ""; }
    }
}

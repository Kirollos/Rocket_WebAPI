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

namespace WebAPIPlugin
{
    public class WebAPI : RocketPlugin<WebAPIConfiguration>
    {
        public static string WebPanelFiles { get { return Path.GetFullPath(".") + "/WebPanelFiles/"; } }
        public static WebAPI dis = null;
        public HTTPServer httpserver;

        protected override void Load()
        {
            dis = this;
            if(!this.Configuration.Instance.Enabled)
            {
                Logger.Log("WebAPI set to disabled.");
                return;
            }

            httpserver = new HTTPServer();

            if(this.Configuration.Instance.WebPanel)
            {
                if (!Directory.Exists(WebPanelFiles))
                    Directory.CreateDirectory(WebPanelFiles);
            }
            
        }

        protected override void Unload()
        {
            if(this.Configuration.Instance.Enabled)
            {
                httpserver.listener.Stop();
                httpserver.listener.Abort();
            }
        }
    }
}

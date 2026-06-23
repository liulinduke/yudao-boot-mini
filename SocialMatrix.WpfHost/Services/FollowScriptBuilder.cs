using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SocialMatrix.WpfHost.Services
{
    public class FollowScriptBuilder
    {
        private readonly string _accountId;
        private readonly string _configJson;

        public FollowScriptBuilder(string accountId, string? configJson)
        {
            _accountId = accountId ?? "";
            _configJson = configJson ?? "{}";
        }

        public string Build()
        {
            var config = ParseConfig();
            config["accountId"] = _accountId;

            var scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scripts", "followPageScript.js");
            if (!File.Exists(scriptPath))
            {
                scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Scripts", "followPageScript.js");
            }
            if (!File.Exists(scriptPath))
            {
                throw new FileNotFoundException("关注脚本文件不存在", scriptPath);
            }

            var template = File.ReadAllText(scriptPath);
            return template.Replace("__FOLLOW_CONFIG_JSON__", JsonConvert.SerializeObject(config));
        }

        private JObject ParseConfig()
        {
            try
            {
                var root = JObject.Parse(_configJson);
                var actionConfig = root["actionConfig"] as JObject;
                if (actionConfig != null)
                {
                    if (root["targetUrl"] != null && actionConfig["targetUrl"] == null)
                    {
                        actionConfig["targetUrl"] = root["targetUrl"];
                    }
                    return actionConfig;
                }
                return root;
            }
            catch
            {
                return new JObject();
            }
        }
    }
}

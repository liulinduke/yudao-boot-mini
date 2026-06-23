using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SocialMatrix.WpfHost.Services
{
    public class DeepProfileCollectScriptBuilder
    {
        private readonly string _accountId;
        private readonly string _configJson;

        public DeepProfileCollectScriptBuilder(string accountId, string? configJson)
        {
            _accountId = accountId ?? "";
            _configJson = configJson ?? "{}";
        }

        public string Build()
        {
            var config = ParseConfig();
            config["accountId"] = _accountId;

            var scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Scripts", "deepProfileCollectScript.js");
            if (!File.Exists(scriptPath))
            {
                scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Scripts", "deepProfileCollectScript.js");
            }
            if (!File.Exists(scriptPath))
            {
                throw new FileNotFoundException("深度采集脚本文件不存在", scriptPath);
            }

            var template = File.ReadAllText(scriptPath);
            return template.Replace("__DEEP_PROFILE_CONFIG_JSON__", JsonConvert.SerializeObject(config));
        }

        private JObject ParseConfig()
        {
            try
            {
                return JObject.Parse(_configJson);
            }
            catch
            {
                return new JObject();
            }
        }
    }
}

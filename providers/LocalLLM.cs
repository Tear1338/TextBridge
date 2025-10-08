using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using TextBridge.Core;

namespace TextBridge.Providers;

public class Localllm : ITranslator
{
    private readonly Settings cfg;

    public string name => "LocalLLM";

    public Localllm(Settings config)
    {
        cfg = config;
    }

    private string stripthinking(string text)
    {
        var result = text;
        result = Regex.Replace(result, @"<think>.*?</think>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"<thinking>.*?</thinking>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"<thought>.*?</thought>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        return result.Trim();
    }

    public async Task<string> translate(string text, string targetlang)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            client.DefaultRequestHeaders.Clear();

            var key = cfg.getkey();
            if (!string.IsNullOrEmpty(key))
            {
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {key}");
            }

            var payload = new
            {
                model = cfg.model,
                messages = new object[]
                {
                    new { role = "system", content = cfg.getfullsystemprompt() },
                    new { role = "user", content = Settings.getuserprompt(text, targetlang) }
                },
                temperature = 0.4,
                max_tokens = 150
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var url = $"http://{cfg.localip}:{cfg.localport}/v1/chat/completions";
            var response = await client.PostAsync(url, content);
            var responsecontent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return text;

            var result = JsonSerializer.Deserialize<JsonElement>(responsecontent);

            if (result.TryGetProperty("error", out _))
                return text;

            if (!result.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
                return text;

            var translation = choices[0].GetProperty("message").GetProperty("content").GetString()?.Trim();
            if (translation != null)
            {
                translation = stripthinking(translation);
            }
            return translation ?? text;
        }
        catch
        {
            return text;
        }
    }
}

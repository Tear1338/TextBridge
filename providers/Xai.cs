using System.Net.Http;
using System.Text;
using System.Text.Json;
using TextBridge.Core;

namespace TextBridge.Providers;

public class Xai : ITranslator
{
    private readonly Settings cfg;

    public string name => "Grok";

    public Xai(Settings config)
    {
        cfg = config;
    }

    public async Task<string> translate(string text, string targetlang)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {cfg.getkey()}");

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
            var response = await client.PostAsync("https://api.x.ai/v1/chat/completions", content);
            var responsecontent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return text;

            var result = JsonSerializer.Deserialize<JsonElement>(responsecontent);

            if (result.TryGetProperty("error", out _))
                return text;

            if (!result.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
                return text;

            var translation = choices[0].GetProperty("message").GetProperty("content").GetString()?.Trim();
            return translation ?? text;
        }
        catch
        {
            return text;
        }
    }
}

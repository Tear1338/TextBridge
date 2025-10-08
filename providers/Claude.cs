using System.Net.Http;
using System.Text;
using System.Text.Json;
using TextBridge.Core;

namespace TextBridge.Providers;

public class Claude : ITranslator
{
    private readonly Settings cfg;

    public string name => "Claude";

    public Claude(Settings config)
    {
        cfg = config;
    }

    public async Task<string> translate(string text, string targetlang)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("x-api-key", cfg.getkey());
            client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

            var payload = new
            {
                model = cfg.model,
                max_tokens = 150,
                temperature = 0.4,
                system = cfg.getfullsystemprompt(),
                messages = new[]
                {
                    new { role = "user", content = Settings.getuserprompt(text, targetlang) }
                }
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PostAsync("https://api.anthropic.com/v1/messages", content);
            var responsecontent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return text;

            var result = JsonSerializer.Deserialize<JsonElement>(responsecontent);

            if (result.TryGetProperty("error", out _))
                return text;

            if (!result.TryGetProperty("content", out var contentarray) || contentarray.GetArrayLength() == 0)
                return text;

            var translation = contentarray[0].GetProperty("text").GetString()?.Trim();
            return translation ?? text;
        }
        catch
        {
            return text;
        }
    }
}

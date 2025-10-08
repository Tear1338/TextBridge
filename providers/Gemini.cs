using System.Net.Http;
using System.Text;
using System.Text.Json;
using TextBridge.Core;

namespace TextBridge.Providers;

public class Gemini : ITranslator
{
    private readonly Settings cfg;

    public string name => "Gemini";

    public Gemini(Settings config)
    {
        cfg = config;
    }

    public async Task<string> translate(string text, string targetlang)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            client.DefaultRequestHeaders.Clear();

            var fullprompt = cfg.getfullsystemprompt() + "\n\n" + Settings.getuserprompt(text, targetlang);

            var payload = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = fullprompt }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.4,
                    maxOutputTokens = 150
                }
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{cfg.model}:generateContent?key={cfg.getkey()}";
            var response = await client.PostAsync(url, content);
            var responsecontent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return text;

            var result = JsonSerializer.Deserialize<JsonElement>(responsecontent);

            if (result.TryGetProperty("error", out _))
                return text;

            if (!result.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
                return text;

            var translation = candidates[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString()?.Trim();
            return translation ?? text;
        }
        catch
        {
            return text;
        }
    }
}

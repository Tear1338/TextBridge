using System.Text.Json;

namespace TextBridge.Core;

public class Settings
{
    public Dictionary<string, string> apikeys { get; set; } = new()
    {
        {"Grok", ""},
        {"OpenAI", ""},
        {"Claude", ""},
        {"Gemini", ""},
        {"FantasyAI", ""},
        {"LocalLLM", ""}
    };

    public string provider { get; set; } = "LocalLLM";
    public string model { get; set; } = "custom-model";
    public string localip { get; set; } = "127.0.0.1";
    public string localport { get; set; } = "11434";
    public bool enableincoming { get; set; } = true;
    public bool enableoutgoing { get; set; } = true;
    public string intarget { get; set; } = "english";
    public string outtarget { get; set; } = "english";
    public bool doshout { get; set; } = true;
    public bool dochat { get; set; } = true;
    public bool dowhisper { get; set; } = false;
    public string custominstructions { get; set; } = "";

    public static string systemprompt => "You are a professional translator. Translate the provided text accurately and naturally based on the prompt into the target language. Return ONLY the translated text with no explanations, notes, or additional commentary.";

    public static string getuserprompt(string text, string targetlang)
    {
        return $"translate the following text to {targetlang}. only return the translated text, nothing else: {text}";
    }

    public string getfullsystemprompt()
    {
        var prompt = systemprompt;
        if (!string.IsNullOrEmpty(custominstructions))
        {
            prompt += " " + custominstructions;
        }
        return prompt;
    }

    public static Dictionary<string, List<string>> providermodels { get; set; } = new()
    {
        {"Grok", new List<string> { "grok-3-mini", "grok-4-fast-reasoning", "grok-4-fast-non-reasoning", "grok-code-fast-1" }},
        {"OpenAI", new List<string> { "gpt-4o-mini", "gpt-4.1-mini", "gpt-4.1-nano"}},
        {"Claude", new List<string> { "claude-3-haiku-20240307", "claude-3-5-haiku-20241022", "claude-sonnet-4-20250514", "claude-sonnet-4-5-20250929" }},
        {"Gemini", new List<string> { "gemini-2.0-flash", "gemini-2.0-flash-lite", "gemini-2.5-flash", "gemini-2.5-flash-lite" }},
        {"FantasyAI", new List<string> { "gpt-4o", "claude-3-5-sonnet-20241022", "deepseek-v4" }},
        {"LocalLLM", new List<string> { "custom-model" }}
    };

    public static List<string> languages { get; set; } = new()
    {
        "english", "spanish", "french", "german", "italian", "portuguese", "dutch",
        "russian", "chinese", "japanese", "korean", "arabic", "turkish", "polish",
        "swedish", "danish", "norwegian", "finnish", "greek", "czech", "hungarian"
    };

    public string getkey() => apikeys.ContainsKey(provider) ? apikeys[provider] : "";
    public void setkey(string key) { if (apikeys.ContainsKey(provider)) apikeys[provider] = key; }

    private static readonly string configpath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TextBridge", "config.json"
    );

    public static Settings load()
    {
        try
        {
            if (File.Exists(configpath))
            {
                var json = File.ReadAllText(configpath);
                var settings = JsonSerializer.Deserialize<Settings>(json) ?? new Settings();

                foreach (var provider in providermodels.Keys)
                {
                    if (!settings.apikeys.ContainsKey(provider))
                        settings.apikeys[provider] = "";
                }

                return settings;
            }
        }
        catch { }
        return new Settings();
    }

    public void save()
    {
        try
        {
            var dir = Path.GetDirectoryName(configpath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(configpath, json);
        }
        catch { }
    }
}

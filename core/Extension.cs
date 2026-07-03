using Xabbo;
using Xabbo.GEarth;
using Xabbo.Messages;
using Xabbo.Messages.Flash;
using TextBridge.Providers;
using System.IO;

namespace TextBridge.Core;

public class Extension : GEarthExtension
{
    public event Action<string>? logevent;
    public bool translationenabled { get; set; } = false;

    private Settings cfg;
    private ITranslator? handler;
    private static string logpath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TextBridge", "crash.log");
    private readonly Dictionary<string, DateTime> recentoutgoing = new();

    public Extension(Settings config) : base(new GEarthOptions
    {
        Name = "TextBridge",
        Description = "Real-time AI translation",
        Author = "QDave",
        Version = "1.0.0",
        ShowDeleteButton = true,
        ShowLeaveButton = true
    })
    {
        try
        {
            cfg = config;
            updatehandler();
        }
        catch (Exception ex)
        {
            var msg = $"extension initialization failed: {ex.Message} | {ex.StackTrace}";
            logevent?.Invoke(msg);
            writelog(msg);
            throw;
        }
    }

    private static void writelog(string msg)
    {
        try
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            File.AppendAllText(logpath, $"[{timestamp}] {msg}\n");
        }
        catch
        {
        }
    }

    public void updateconfig(Settings config)
    {
        cfg = config;
        updatehandler();
    }

    private void updatehandler()
    {
        handler = cfg.provider switch
        {
            "Grok" => new Xai(cfg),
            "OpenAI" => new Openai(cfg),
            "Claude" => new Claude(cfg),
            "Gemini" => new Gemini(cfg),
            "FantasyAI" => new Fantasyai(cfg),
            "LocalLLM" => new Localllm(cfg),
            _ => new Xai(cfg)
        };
    }

    private void cleanupoldmessages()
    {
        lock (recentoutgoing)
        {
            var cutoff = DateTime.Now.AddSeconds(-5);
            var toremove = recentoutgoing.Where(x => x.Value < cutoff).Select(x => x.Key).ToList();
            foreach (var key in toremove)
            {
                recentoutgoing.Remove(key);
            }
        }
    }

    protected override void OnConnected(ConnectedEventArgs e)
    {
        try
        {
            base.OnConnected(e);
            logevent?.Invoke($"connected to {e.Host}:{e.Port}");
            Intercepted += handleintercept;
        }
        catch (Exception ex)
        {
            var msg = $"OnConnected crashed: {ex.Message} | {ex.StackTrace}";
            logevent?.Invoke(msg);
            writelog(msg);
        }
    }

    private void handleintercept(Intercept e)
    {
        try
        {
            if (!translationenabled) return;

            if (cfg.enableincoming && e.Is([In.Chat, In.Shout, In.Whisper]))
            {
                var chattype = e.Is(In.Chat) ? "chat" : e.Is(In.Shout) ? "shout" : "whisper";

                if (chattype == "chat" && !cfg.dochat) return;
                if (chattype == "shout" && !cfg.doshout) return;
                if (chattype == "whisper" && !cfg.dowhisper) return;

                e.Packet.Position = 0;
                var userid = e.Packet.Read<int>();
                var message = e.Packet.Read<string>();

                cleanupoldmessages();

                bool isownmessage = false;
                lock (recentoutgoing)
                {
                    isownmessage = recentoutgoing.ContainsKey(message);
                }

                if (isownmessage)
                {
                    return;
                }

                var header = e.Packet.Header;
                var clienttype = e.Packet.Client;
                var symbol = chattype == "chat" ? "c" : chattype == "shout" ? "s" : "w";

                Task.Run(async () =>
                {
                    try
                    {
                        if (handler != null)
                        {
                            var translated = await handler.translate(message, cfg.intarget);

                            using var newpacket = new Packet(header, clienttype);
                            newpacket.Write(userid);

                            if (translated != null && translated != message)
                            {
                                newpacket.Write(translated);
                                Send(newpacket);
                                logevent?.Invoke($"← {symbol} | {message} → {translated}");
                            }
                            else
                            {
                                newpacket.Write(message);
                                Send(newpacket);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        var msg = $"incoming translation failed: {ex.Message}";
                        logevent?.Invoke(msg);
                        writelog(msg);
                    }
                });

                e.Block();
            }

            if (cfg.enableoutgoing && e.Is([Out.Chat, Out.Shout, Out.Whisper]))
            {
                var chattype = e.Is(Out.Chat) ? "chat" : e.Is(Out.Shout) ? "shout" : "whisper";

                if (chattype == "chat" && !cfg.dochat) return;
                if (chattype == "shout" && !cfg.doshout) return;
                if (chattype == "whisper" && !cfg.dowhisper) return;

                e.Packet.Position = 0;

                string? recipient = null;
                string message;
                int param1;
                int? param2 = null;

                if (e.Is(Out.Whisper))
                {
                    var fullmessage = e.Packet.Read<string>();
                    param1 = e.Packet.Read<int>();

                    var spaceindex = fullmessage.IndexOf(' ');
                    if (spaceindex > 0)
                    {
                        recipient = fullmessage.Substring(0, spaceindex);
                        message = fullmessage.Substring(spaceindex + 1);
                    }
                    else
                    {
                        recipient = fullmessage;
                        message = "";
                    }
                }
                else if (e.Is(Out.Chat))
                {
                    message = e.Packet.Read<string>();
                    param1 = e.Packet.Read<int>();
                    param2 = e.Packet.Read<int>();
                }
                else
                {
                    message = e.Packet.Read<string>();
                    param1 = e.Packet.Read<int>();
                }

                var header = e.Packet.Header;
                var clienttype = e.Packet.Client;
                var symbol = chattype == "chat" ? "c" : chattype == "shout" ? "s" : "w";
                var iswhisper = e.Is(Out.Whisper);
                var ischat = e.Is(Out.Chat);

                Task.Run(async () =>
                {
                    try
                    {
                        if (handler != null)
                        {
                            var translated = await handler.translate(message, cfg.outtarget);

                            using var newpacket = new Packet(header, clienttype);

                            var finalmessage = (translated != null && translated != message) ? translated : message;

                            if (iswhisper)
                            {
                                newpacket.Write(recipient + " " + finalmessage);
                                newpacket.Write(param1);
                            }
                            else if (ischat)
                            {
                                newpacket.Write(finalmessage);
                                newpacket.Write(param1);
                                newpacket.Write(param2!.Value);
                            }
                            else
                            {
                                newpacket.Write(finalmessage);
                                newpacket.Write(param1);
                            }

                            Send(newpacket);

                            lock (recentoutgoing)
                            {
                                recentoutgoing[finalmessage] = DateTime.Now;
                            }

                            if (translated != null && translated != message)
                            {
                                logevent?.Invoke($"→ {symbol} | {message} → {translated}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        var msg = $"outgoing translation failed: {ex.Message}";
                        logevent?.Invoke(msg);
                        writelog(msg);
                    }
                });

                e.Block();
            }
        }
        catch (Exception ex)
        {
            var msg = $"intercept crashed: {ex.Message} | {ex.StackTrace}";
            logevent?.Invoke(msg);
            writelog(msg);
        }
    }
}

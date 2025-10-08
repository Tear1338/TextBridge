namespace TextBridge.Core;

public interface ITranslator
{
    string name { get; }
    Task<string> translate(string text, string targetlang);
}

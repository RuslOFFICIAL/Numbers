// Thanks to @zxjljgugvj for giving an idea for this!

using System.Globalization;
using System.Runtime.CompilerServices;
using Numbers_Windows.Resources;

class LanguageManager
{
    public static void LoadSystemLanguage()
    {
        Console.WriteLine(Strings.LanguageOptionsPrompt.Replace("\\n", Environment.NewLine).Replace("\\t", Constants.ConsoleTab));
        Console.Write(Strings.LanguagePrompt);
        string? lang = Console.ReadLine()?.ToLowerInvariant();

        switch (lang)
        {
            case "uk":
            case "en":
            case "es":
                Console.WriteLine(Strings.LanguageSettingPrompt
                    .Replace("{lang}", lang)
                    .Replace("\\n", Environment.NewLine));
                Strings.Culture = new CultureInfo(lang);
                break;
            default:
                // Neutral language is English.
                Console.WriteLine(Strings.LanguageNeutralPrompt
                    .Replace("{lang}", lang)
                    .Replace("\\n", Environment.NewLine));
                Strings.Culture = new CultureInfo("en");
                break;
        };
    }

	public static class Constants
	{
		public const string ConsoleTab = "\t";
	}
}
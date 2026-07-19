// Thanks to @zxjljgugvj for giving an idea for this!

using System.Globalization;
using Numbers_Mobile.Resources;

class LanguageManager
{
    public static bool LoadSystemLanguage(string lang)
    {
        switch (lang.ToLowerInvariant())
        {
            case "uk":
            case "en":
            case "es":
                Strings.Culture = new CultureInfo(lang);
                return true;
            default:
                // Neutral language is English.
                Strings.Culture = new CultureInfo("en");
                return false;
        };
    }
}
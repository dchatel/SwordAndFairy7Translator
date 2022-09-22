using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SwordAndFairy7Translator
{
    public class Language
    {
        public string Code { get; set; } = null!;
        public string Name { get; set; } = null!;
    }

    public interface ITranslatorService
    {
        public string Name { get; }
        public IEnumerable<Language>? Languages { get; }
        public string? TargetLanguage { get; }
        public Task<bool> TestAsync(string? overrideLanguage = null);
        public Task<IEnumerable<string>> TranslateAsync(string from, params string[] texts);
        bool IsValid { get; }
    }

    public class TranslatorServiceHelper
    {
        private static IEnumerable<ITranslatorService>? translatorServices;

        private static ITranslatorService? translator;
        public static ITranslatorService? Translator
        {
            get
            {
                if (translator is null)
                {
                    if (Settings.Default.TranslateService is not null)
                    {
                        translator = TranslatorServices.SingleOrDefault(
                            service => service.Name == Settings.Default.TranslateService
                            );
                    }
                }
                return translator;
            }
            set
            {
                translator = value;
                Settings.Default.TranslateService = translator?.Name;
            }
        }
        public static IEnumerable<ITranslatorService> TranslatorServices
        {
            get
            {
                if (translatorServices is null)
                {
                    translatorServices = new List<ITranslatorService>
                    {
                        new NoneTranslator(),
                        new AzureTranslator(),
                        new DeepLTranslator(),
                    };
                }
                return translatorServices;
            }
        }

        public static int EstimateCaracterCount(params string[] texts)
        {
            var allMStrings = texts
                .Select(text => GetMStrings(text))
                .ToArray();

            var toTranslateMStrings = allMStrings
                .SelectMany(x => x)
                .Where(x => !Regex.IsMatch(x.Text, @"<[^>]+>") && Regex.IsMatch(x.Text, @"\p{L}"))
                .ToArray();

            return toTranslateMStrings.Sum(x => x.Text.Length);
        }

        public class MString
        {
            public string Text { get; set; } = null!;
            public MString(string text) { Text = text; }
        }

        public static List<MString> GetMStrings(string str)
        {
            var ms = Regex.Matches(str, "(<[^>]+>)+").ToArray();
            var index = 0;
            List<MString> ts = new();
            foreach (Match m in ms)
            {
                if (m.Index > index)
                    ts.Add(new(str[index..m.Index]));
                ts.Add(new(m.Value));
                index = m.Index + m.Length;
            }
            if (str.Length > index)
                ts.Add(new(str[index..]));
            return ts;
        }
    }

}

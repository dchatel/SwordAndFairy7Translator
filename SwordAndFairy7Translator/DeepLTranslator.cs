using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using DeepL;
using PostSharp.Patterns.Model;

namespace SwordAndFairy7Translator
{
    [NotifyPropertyChanged]
    public class DeepLTranslator : ITranslatorService, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null!)
            => PropertyChanged?.Invoke(this, new(propertyName));

        private IEnumerable<Language>? languages;

        [SafeForDependencyAnalysis]
        public string? TargetLanguage
        {
            get => Settings.Default.TargetLanguage;
            set
            {
                Settings.Default.TargetLanguage = value;
                OnPropertyChanged();
                Task.Run(async () => IsValid = await TestAsync());
            }
        }

        public string Name => "DeepL";

        public bool IsValid { get; private set; }

        [SafeForDependencyAnalysis]
        public string? Key
        {
            get => Settings.Default.DeepLAPIKey;
            set
            {
                Settings.Default.DeepLAPIKey = value;
                OnPropertyChanged(nameof(Languages));
                Task.Run(async () => IsValid = await TestAsync());
            }
        }
        [SafeForDependencyAnalysis]
        public bool UseFreeApi
        {
            get => Settings.Default.DeepLUseFreeApi;
            set
            {
                Settings.Default.DeepLUseFreeApi = value;
                OnPropertyChanged(nameof(Languages));
                Task.Run(async () => IsValid = await TestAsync());
            }
        }

        [SafeForDependencyAnalysis]
        public IEnumerable<Language>? Languages
        {
            get
            {
                if (languages is null)
                {
                    Task.Run(async () =>
                    {
                        using var deepl = new DeepLClient(Key, UseFreeApi);
                        if (!await TestAsync(overrideLanguage: "en")) return;
                        var langs = await deepl.GetSupportedLanguagesAsync();
                        languages = langs
                            .Select(l => new Language
                            {
                                Code = l.LanguageCode.ToLower(),
                                Name = l.Name,
                            });
                    });
                }
                return languages;
            }
        }

        public async Task<bool> TestAsync(string? overrideLanguage = null)
        {
            using var deepl = new DeepLClient(Key, UseFreeApi);
            try
            {
                await deepl.TranslateAsync("Hello, World!", overrideLanguage ?? Settings.Default.TargetLanguage);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<IEnumerable<string>> TranslateAsync(string from, params string[] texts)
        {
            if (string.IsNullOrEmpty(Key)) return Enumerable.Empty<string>();

            // split strings in substrings with or without html tags
            var allMStrings = texts
                .Select(text => TranslatorServiceHelper.GetMStrings(text))
                .ToArray();

            // Filter strings with html tags and those who don't have text to translate
            var toTranslateMStrings = allMStrings
                .SelectMany(x => x)
                .Where(x => !Regex.IsMatch(x.Text, @"<[^>]+>") && Regex.IsMatch(x.Text, @"\p{L}"))
                .ToArray();

            // Create Chunks of 40k characters max
            var chunks = new List<List<TranslatorServiceHelper.MString>>
            {
                new()
            };
            foreach (var str in toTranslateMStrings)
            {
                if (chunks.Last().Count >= 50 || chunks.Last().Sum(x => x.Text.Length) + str.Text.Length > 5000)
                    chunks.Add(new());
                chunks.Last().Add(str);
            }

            using var deepl = new DeepLClient(Key, UseFreeApi);
            // Translate using Azure
            foreach (var body in chunks)
            {
                try
                {
                    var tr = await deepl.TranslateAsync(body.Select(b => b.Text), Settings.Default.TargetLanguage);
                    foreach (var (t, ms) in tr.Zip(body))
                    {
                        ms.Text = t.Text;
                    }
                }
                catch (Exception e)
                {
                    MessageBox.Show(e.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            // Rebuild strings
            var translations = allMStrings
                .Select(x => string.Join("", x.Select(t => t.Text)))
                .ToArray();

            return translations;
        }
    }
}

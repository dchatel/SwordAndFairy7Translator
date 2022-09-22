using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PostSharp.Patterns.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Windows;

namespace SwordAndFairy7Translator;

[NotifyPropertyChanged]
public class AzureTranslator : ITranslatorService
{
    private static readonly string endpoint = "https://api.cognitive.microsofttranslator.com/";

    private IEnumerable<Language>? languages;

    [SafeForDependencyAnalysis]
    public string? Key
    {
        get => Settings.Default.AzureAPIKey; set
        {
            Settings.Default.AzureAPIKey = value;
            Task.Run(async () => IsValid = await TestAsync());
        }
    }
    [SafeForDependencyAnalysis]
    public string? Region
    {
        get => Settings.Default.AzureRegion; set
        {
            Settings.Default.AzureRegion = value;
            Task.Run(async () => IsValid = await TestAsync());
        }
    }

    public string Name => "Microsoft Azure";
    public bool IsValid { get; private set; }

    [SafeForDependencyAnalysis]
    public string? TargetLanguage
    {
        get => Settings.Default.TargetLanguage;
        set
        {
            Settings.Default.TargetLanguage = value;
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
                    using var client = new HttpClient();
                    using var request = new HttpRequestMessage
                    {
                        Method = HttpMethod.Get,
                        RequestUri = new Uri("https://api.cognitive.microsofttranslator.com/languages?api-version=3.0"),
                    };
                    var response = await client.SendAsync(request).ConfigureAwait(false);
                    var result = await response.Content.ReadAsStringAsync();

                    var json = JObject.Parse(result);
                    Dictionary<string, Language> langs = JsonConvert.DeserializeObject<Dictionary<string, Language>>(json["translation"]!.ToString())!;
                    foreach (var (k, v) in langs)
                        v.Code = k;
                    languages = langs.Values.AsEnumerable();
                });
            }
            return languages;
        }
    }
    public async Task<IEnumerable<string>> TranslateAsync(string from, params string[] textToTranslate)
    {
        if (string.IsNullOrEmpty(Key))
            throw new NullReferenceException(nameof(Key));
        if (string.IsNullOrEmpty(Region))
            throw new NullReferenceException(nameof(Region));

        // split strings in substrings with or without html tags
        var allMStrings = textToTranslate
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
            if (chunks.Last().Count >= 1000 || chunks.Last().Sum(x => x.Text.Length) + str.Text.Length > 40000)
                chunks.Add(new());
            chunks.Last().Add(str);
        }

        // Translate using Azure
        foreach (var body in chunks)
        {
            var requestBody = JsonConvert.SerializeObject(body);

            using var client = new HttpClient();
            using var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri($"{endpoint}translate?api-version=3.0&from={from}&to={Settings.Default.TargetLanguage}"),
                Content = new StringContent(requestBody, Encoding.UTF8, "application/json"),
            };
            request.Headers.Add("Ocp-Apim-Subscription-Key", Key);
            request.Headers.Add("Ocp-Apim-Subscription-Region", Region);

            var response = await client.SendAsync(request).ConfigureAwait(false);
            var result = await response.Content.ReadAsStringAsync();

            var json = JToken.Parse(result);
            if (json.Type == JTokenType.Object)
            {

                if (((JObject)json).ContainsKey("error"))
                    throw new ApplicationException((string)json["error"]!["message"]!);
            }
            else if (json.Type == JTokenType.Array)
            {
                var tr = json.Select(j => j["translations"]![0]!["text"]!.Value<string>()!);
                foreach (var (t, ms) in tr.Zip(body))
                {
                    ms.Text = t;
                }
            }
        }

        // Rebuild strings
        var translations = allMStrings
            .Select(x => string.Join("", x.Select(t => t.Text)))
            .ToArray();

        return translations;
    }

    public async Task<bool> TestAsync(string? overrideLanguage = null)
    {
        try
        {
            _ = await TranslateAsync(from: overrideLanguage ?? Settings.Default.TargetLanguage, "Hello, World!");
            return true;
        }
        catch
        {
            return false;
        }
    }
}

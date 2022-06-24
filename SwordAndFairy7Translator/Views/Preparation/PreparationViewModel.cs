using CsvHelper;
using CsvHelper.Configuration;
using Ethereality;
using Microsoft.Toolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace SwordAndFairy7Translator.Views.Preparation;

public class PreparationViewModel
{
    static IEnumerable<T> CsvRead<T>(string path)
    {
        if (!File.Exists(path)) return Enumerable.Empty<T>();
        CsvConfiguration csvConfiguration = new(System.Globalization.CultureInfo.CurrentCulture)
        {
            Quote = '"',
            Delimiter = ",",
            HeaderValidated = null,
            MissingFieldFound = null,
        };
        using var reader = new StreamReader(path);
        using var csv = new CsvReader(reader, csvConfiguration);
        return csv.GetRecords<T>().ToList();
    }

    public string? TaskName { get; private set; }
    public ICommand OnLoadedCommand { get; }
    public int Progress { get; private set; }
    public int Maximum { get; private set; }
    public bool IsIndeterminate { get; private set; }
    public IEnumerable<ExtendedLocale> ExtendedLocales { get; private set; } = null!;
    public IEnumerable<ExtendedLocale> NewLocales { get; private set; } = null!;

    public PreparationViewModel()
    {
        OnLoadedCommand = new AsyncRelayCommand(OnLoaded);
    }

    private async Task OnLoaded()
    {
        TaskName = "Extracting localization files";
        await Task.Run(() =>
        {
            Clean();
            UnrealTools.ExtractAny("*FZBWKSJW.ufont", "Pal7");
            UnrealTools.ExtractAny("*Game.locres", "extracted");
            UnrealTools.LocresExport(@"extracted/Pal7/Content/Localization/Game/en/Game.locres", "en.extracted.csv");
            UnrealTools.LocresExport(@"extracted/Pal7/Content/Localization/Game/zh-Hans/Game.locres", "zh-Hans.extracted.csv");
            UnrealTools.LocresExport(@"extracted/Pal7/Content/Localization/Game/zh-Hant-TW/Game.locres", "zh-Hant-TW.extracted.csv");

            if (UnrealTools.ExtractMod())
                UnrealTools.LocresExport(@"Pal7/Content/Localization/Game/en/Game.locres", "en.modded.csv");
        });

        TaskName = "Reading localization files...";
        ExtendedLocales = GetExtendedLocales();

        TaskName = "Translating new strings...";
        NewLocales = ExtendedLocales
            .Where(el => string.IsNullOrEmpty(el.Modded))
            .ToArray();
        foreach (var el in NewLocales)
        {
            el.Modded = el.en;
        }
        var english = NewLocales
            .Select(l => l.en!)
            .ToArray();
        if (english.Length > 0)
        {
            if (MessageBox.Show(
                $"{english.Length} new strings have been found (approx. {TranslatorServiceHelper.EstimateCaracterCount(english)} characters\n" +
                $"Do you want to automatically translate them ?",
                "Proceed ?",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                var translated = await TranslatorServiceHelper.Translator!.TranslateAsync("en", english);
                foreach (var (el, t) in NewLocales.Zip(translated))
                {
                    el.Modded = t;
                }
            }
        }
        Dialog.CloseDialog(true);
    }

    private static void Clean()
    {
        if (Directory.Exists("extracted"))
            Directory.Delete("extracted", recursive: true);
        if (Directory.Exists("modded"))
            Directory.Delete("modded", recursive: true);
        var csvs = Directory.GetFiles(".", "*.csv");
        foreach (var csv in csvs)
            File.Delete(csv);
    }

    private struct InternalProgress
    {
        public int Progress { get; init; }
        public int Maximum { get; init; }
    }

    private static IEnumerable<ExtendedLocale> GetExtendedLocales()
    {
        var dic = new Dictionary<string, ExtendedLocale>();
        var enlocale = CsvRead<Locale>("en.extracted.csv");
        var zhHanslocale = CsvRead<Locale>("zh-Hans.extracted.csv");
        var zhHantTWlocale = CsvRead<Locale>("zh-Hant-TW.extracted.csv");
        var moddedlocale = CsvRead<Locale>("en.modded.csv");

        foreach (var locale in enlocale)
        {
            dic[locale.key] = new ExtendedLocale
            {
                key = locale.key,
                en = locale.source,
            };
        }

        foreach (var locale in zhHanslocale)
        {
            if (dic.TryGetValue(locale.key, out ExtendedLocale? extendedLocale))
                extendedLocale.zh = locale.source;
        }

        foreach (var locale in zhHantTWlocale)
        {
            if (dic.TryGetValue(locale.key, out ExtendedLocale? extendedLocale))
                extendedLocale.zhtw = locale.source;
        }

        foreach (var locale in moddedlocale)
        {
            if (dic.TryGetValue(locale.key, out ExtendedLocale? extendedLocale))
                extendedLocale.Modded = locale.source;
        }

        return dic.Values.ToArray();
    }
}

using PostSharp.Patterns.Model;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SwordAndFairy7Translator
{
    public class Locale
    {
        public string key { get; set; } = null!;
        public string? source { get; set; }
        public string? target { get; set; }
    }

    [NotifyPropertyChanged]
    public class ExtendedLocale
    {
        private string? translated_zh;
        private string? translated_zhtw;
        private string? translated_en;

        public string key { get; set; } = null!;
        public string? en { get; set; }
        [SafeForDependencyAnalysis]
        public string? Translated_en
        {
            get
            {
                if (string.IsNullOrEmpty(en)) return null;
                if (string.IsNullOrEmpty(translated_en))
                {
                    Task.Run(async () =>
                    {
                        translated_en = (await TranslatorServiceHelper.Translator!.TranslateAsync("en", en)).Single();
                    });
                    return en;
                }
                return translated_en;
            }
        }
        public string? zh { get; set; }
        [SafeForDependencyAnalysis]
        public string? Translated_zh
        {
            get
            {
                if (string.IsNullOrEmpty(zh)) return null;
                if (string.IsNullOrEmpty(translated_zh))
                {
                    Task.Run(async () =>
                    {
                        translated_zh = (await TranslatorServiceHelper.Translator!.TranslateAsync("zh-Hans", zh)).Single();
                    });
                    return zh;
                }
                return translated_zh;
            }
        }
        public string? zhtw { get; set; }
        [SafeForDependencyAnalysis]
        public string? Translated_zhtw
        {
            get
            {
                if (string.IsNullOrEmpty(zhtw)) return null;
                if (string.IsNullOrEmpty(translated_zhtw))
                {
                    Task.Run(async () =>
                    {
                        translated_zhtw = (await TranslatorServiceHelper.Translator!.TranslateAsync("zh-Hant", zhtw)).Single();
                    });
                    return zhtw;
                }
                return translated_zhtw;
            }
        }
        public string? Modded { get; set; }
    }
}

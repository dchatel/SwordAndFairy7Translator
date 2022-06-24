using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SwordAndFairy7Translator
{
    public class NoneTranslator : ITranslatorService
    {
        public string Name => "None";

        public IEnumerable<Language>? Languages => null;

        public Task<bool> TestAsync()
        {
            return Task.FromResult(true);
        }

        public Task<IEnumerable<string>> TranslateAsync(string from, params string[] texts)
        {
            return Task.FromResult(texts.AsEnumerable());
        }
    }
}

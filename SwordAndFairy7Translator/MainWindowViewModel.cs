using Ethereality;
using Microsoft.Toolkit.Mvvm.Input;
using SwordAndFairy7Translator.Views.Preparation;
using SwordAndFairy7Translator.Views.ProgressDialog;
using SwordAndFairy7Translator.Views.StartupDialog;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace SwordAndFairy7Translator
{
    public class MainWindowViewModel
    {
        private string? searchText;
        private string? replacement;

        public IEnumerable<ExtendedLocale> ExtendedLocales { get; set; } = null!;
        public ExtendedLocale? SelectedLocale { get; set; }

        public ICommand OnLoadedCommand { get; }
        public ICommand PakCommand { get; }
        public ICommand ReplaceCommand { get; }

        public string? SearchText
        {
            get => searchText;
            set
            {
                searchText = value;
                Search();
            }
        }
        public string? Replacement
        {
            get => replacement;
            set
            {
                replacement = value;
                Search();
            }
        }
        public IEnumerable<ExtendedLocale>? SearchResults { get; set; }

        public MainWindowViewModel()
        {
            OnLoadedCommand = new AsyncRelayCommand(OnLoaded);
            PakCommand = new AsyncRelayCommand(Pak);
            ReplaceCommand = new RelayCommand(Replace);
        }

        private void Replace()
        {
            if (searchText is null) return;

            var locales = ExtendedLocales
                .Where(el => !string.IsNullOrEmpty(el.Modded) &&
                    Regex.IsMatch(el.Modded, searchText, RegexOptions.IgnoreCase))
                .ToArray();

            foreach (var el in locales)
            {
                el.Modded = Regex.Replace(el.Modded!, searchText, replacement ?? "", RegexOptions.IgnoreCase);
            }
            Search();
        }

        private async void Search()
        {
            if (string.IsNullOrEmpty(SearchText)) { SearchResults = ExtendedLocales; return; }
            try
            {
                SearchResults = await Task.Run(() => ExtendedLocales
                    .Where(el => el.Modded is not null && Regex.IsMatch(el.Modded, SearchText, RegexOptions.IgnoreCase))
                    .ToArray()).WaitAsync(new System.TimeSpan(0, 0, 5));
            }
            catch
            {
            }
        }

        private async Task Pak()
        {
            var dialog = new ProgressDialog { DataContext = "Packing mod, please wait..." };
            var t = Dialog.Show(dialog);
            await Task.Run(() => UnrealTools.Pak(ExtendedLocales));
            Dialog.CloseDialog(true);
            await t;
        }

        private async Task OnLoaded()
        {
            //if (!StartupDialogViewModel.CheckInstallation())
            {
                var vm = new StartupDialogViewModel();
                var dialog = new StartupDialog { DataContext = vm };
                if (!await Dialog.Show(dialog) is bool b && b)
                {
                    App.Current.Shutdown();
                    return;
                }
                vm.SaveConfiguration();
            }

            {
                var vm = new PreparationViewModel();
                var dialog = new PreparationView { DataContext = vm };
                await Dialog.Show(dialog);
                ExtendedLocales = vm.ExtendedLocales;
                SearchResults = vm.NewLocales;
            }
        }
    }
}

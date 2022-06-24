using Ethereality;
using Microsoft.Toolkit.Mvvm.Input;
using Ookii.Dialogs.Wpf;
using PostSharp.Patterns.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace SwordAndFairy7Translator.Views.StartupDialog;

public class StartupDialogViewModel:INotifyPropertyChanged
{
    private const string UnrealLocresDownloadPath = "https://github.com/akintos/UnrealLocres/releases/download/1.1.1/UnrealLocres.exe";
    private const string UnrealPakToolDownloadPath = "https://github.com/allcoolthingsatoneplace/UnrealPakTool/releases/download/4.25.3/UnrealPakTool.zip";
    private string? targetLanguage;

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string propertyName = null!)
        => PropertyChanged?.Invoke(this, new(propertyName));

    public static bool CheckInstallation(StartupDialogViewModel? vm = null)
    {
        if (!Directory.Exists("Binaries/Win64")) return false;
        if (!File.Exists("UnrealLocres.exe")) return false;
        if (string.IsNullOrEmpty(vm?.Pal7Folder ?? Settings.Default.Pal7Folder)) return false;
        if (string.IsNullOrEmpty(vm?.ModName ?? Settings.Default.ModName)) return false;
        if (string.IsNullOrEmpty(vm?.TargetLanguage ?? Settings.Default.TargetLanguage)) return false;
        return true;
    }

    public string? Pal7Folder { get; set; }
    public string? ModName { get; set; }
    public string? TargetLanguage
    {
        get => targetLanguage;
        set
        {
            targetLanguage = value;
            if (string.IsNullOrEmpty(ModName) ||
                ModName.StartsWith("Pal7Translation_"))
                ModName = $"Pal7Translation_{TargetLanguage}";
        }
    }

    public string? TaskName { get; set; }
    [SafeForDependencyAnalysis]
    public IEnumerable<ITranslatorService> TranslatorServices
        => TranslatorServiceHelper.TranslatorServices;
    [SafeForDependencyAnalysis]
    public ITranslatorService? TranslatorService
    {
        get => TranslatorServiceHelper.Translator;
        set
        {
            TranslatorServiceHelper.Translator = value;
            OnPropertyChanged();
        }
    }
    public IEnumerable<Language>? Languages { get; private set; }

    public ICommand OnLoadedCommand { get; }
    public ICommand SelectPal7FolderCommand { get; }
    public ICommand AcceptCommand { get; }

    public StartupDialogViewModel()
    {
        TargetLanguage = Settings.Default.TargetLanguage;
        Pal7Folder = Settings.Default.Pal7Folder;
        ModName = Settings.Default.ModName;

        OnLoadedCommand = new AsyncRelayCommand(OnLoaded);
        SelectPal7FolderCommand = new RelayCommand(SelectPal7Folder);
        AcceptCommand = new AsyncRelayCommand(Accept, () => CheckInstallation(this));

        ((INotifyPropertyChanged)this).PropertyChanged += StartupDialogViewModel_PropertyChanged;
    }

    private void StartupDialogViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        ((IRelayCommand)AcceptCommand).NotifyCanExecuteChanged();
        //if (e.PropertyName == nameof(TranslatorService))
        //{
        //    Languages = await TranslatorServiceHelper.Translator!.GetLanguagesAsync();
        //}
    }

    private async Task Accept()
    {
        if (!CheckInstallation(this)) { MessageBox.Show("Not all fields are set."); return; }
        if (!await TranslatorServiceHelper.Translator!.TestAsync()) { MessageBox.Show("Your Azure Subscription key is invalid"); return; }
        Dialog.CloseDialog(true);
    }

    private void SelectPal7Folder()
    {
        var ofd = new VistaFolderBrowserDialog
        {
            UseDescriptionForTitle = true,
            Description = "Select Sword & Fairy 7 installation folder",
        };
        if (ofd.ShowDialog() is bool b && b)
        {
            Pal7Folder = ofd.SelectedPath;
        }
    }

    public void SaveConfiguration()
    {
        Settings.Default.TranslateService = TranslatorService?.Name;
        Settings.Default.TargetLanguage = TargetLanguage;
        Settings.Default.Pal7Folder = Pal7Folder;
        Settings.Default.ModName = ModName;
        Settings.Default.Save();
    }

    private async Task OnLoaded()
    {
        //if (TranslatorServiceHelper.Translator is not null)
        //    Languages = await TranslatorServiceHelper.Translator.GetLanguagesAsync();

        if (!Directory.Exists("Binaries/Win64"))
        {
            TaskName = "Installing UnrealPakTool...";
            using (var client = new HttpClient())
            {
                using var fs = new FileStream("unrealpaktool.zip", FileMode.Create);
                using var stream = await client.GetStreamAsync(UnrealPakToolDownloadPath);

                await stream.CopyToAsync(fs);
            }

            ZipFile.ExtractToDirectory("unrealpaktool.zip", "Binaries/Win64");
            File.Delete("unrealpaktool.zip");
            var cryptoContents = await File.ReadAllTextAsync("Binaries/Win64/UnrealPakTool/Crypto.json");
            cryptoContents = cryptoContents.Replace("Your Base64 key here", "5Tvr287IKZXzcDO2LP2CQ+AsEMA6sS6mgvZU3IBfQAk=");
            await File.WriteAllTextAsync("Binaries/Win64/UnrealPakTool/Crypto.json", cryptoContents);
        }
        if (!File.Exists("UnrealLocres.exe"))
        {
            TaskName = "Installing UnrealLocres";
            using var client = new HttpClient();
            using var fs = new FileStream("UnrealLocres.exe", FileMode.Create);
            using var stream = await client.GetStreamAsync(UnrealLocresDownloadPath);

            await stream.CopyToAsync(fs);
        }

        TaskName = null;
    }
}

using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SwordAndFairy7Translator
{
    public static class UnrealTools
    {
        static readonly CsvConfiguration csvConfig = new(System.Globalization.CultureInfo.InvariantCulture)
        {
            Quote = '"',
            Delimiter = ","
        };

        public static bool ExtractMod()
        {
            var pakfile = $@"{Settings.Default.Pal7Folder}\Pal7\Content\Paks\~mods\{Settings.Default.ModName}.pak";
            if (!File.Exists(pakfile)) return false;
            var basedir = AppDomain.CurrentDomain.BaseDirectory;
            var args = $@"{basedir}\Binaries\Win64\UnrealPakTool\UnrealPak.exe -Extract '{pakfile}' '{basedir}Pal7' -cryptokeys='{basedir}Binaries\Win64\UnrealPakTool\Crypto.json'";
            Process.Start(new ProcessStartInfo($@"powershell.exe", args)
            {
                CreateNoWindow = true,
                WorkingDirectory = basedir,
            })!.WaitForExit();
            FileSystem.MoveDirectory("Pal7/Localization", "Pal7/Content/Localization", true);
            FileSystem.MoveDirectory("Pal7/UI", "Pal7/Content/UI", true);
            return true;
        }

        public static void ExtractAny(string pattern, string extractDir)
        {
            var basedir = AppDomain.CurrentDomain.BaseDirectory;
            var args = $@"ls '{Settings.Default.Pal7Folder}\Pal7\Content\Paks\*.pak'|foreach{{{basedir}Binaries\Win64\UnrealPakTool\UnrealPak.exe -Extract $_.FullName '{basedir}{extractDir}' -Filter='{pattern}' -cryptokeys='{basedir}Binaries\Win64\UnrealPakTool\Crypto.json'}}";
            Process.Start(new ProcessStartInfo("powershell.exe", args)
            {
                CreateNoWindow = true,
                WorkingDirectory = basedir,
            })!.WaitForExit();
        }

        public static void LocresExport(string input, string output)
        {
            var basedir = AppDomain.CurrentDomain.BaseDirectory;
            var args = @$"{basedir}UnrealLocres.exe export '{input}' -o {output}";
            Process.Start(new ProcessStartInfo("powershell.exe", args)
            {
                CreateNoWindow = true,
                WorkingDirectory = basedir,
            })!.WaitForExit();
        }

        public static void LocresImport(IEnumerable<Locale> locales)
        {
            var basedir = AppDomain.CurrentDomain.BaseDirectory;

            using (var writer = new StreamWriter("modded.csv"))
            using (var csvWriter = new CsvWriter(writer, csvConfig))
                csvWriter.WriteRecords(locales);

            Directory.CreateDirectory(@$"{basedir}\Pal7\Content\Localization\Game\en\");
            Process.Start(new ProcessStartInfo(
                $"powershell.exe",
                $@"{basedir}/UnrealLocres.exe import extracted\Pal7\Content\Localization\Game\en\Game.locres modded.csv -o Pal7/Content/Localization/Game/en/Game.locres")
            {
                CreateNoWindow = true,
                WorkingDirectory = basedir,
            })!.WaitForExit();
        }

        public static void Pak(IEnumerable<ExtendedLocale> extendedLocales)
        {
            string output = string.Empty;

            var locales = from x in extendedLocales
                          select new Locale
                          {
                              key = x.key,
                              source = x.en,
                              target = x.Modded,
                          };

            UnrealTools.LocresImport(locales);

            using (var writer = new StreamWriter($"Binaries/Win64/UnrealPakTool/list.txt"))
                writer.WriteLine(@"..\..\..\Pal7\*");

            Process.Start(new ProcessStartInfo(
                $"powershell.exe",
                $"Binaries/Win64/UnrealPakTool/UnrealPak.exe '{Settings.Default.Pal7Folder}/Pal7/Content/Paks/~mods/{Settings.Default.ModName}.pak' -Create='list.txt' -compress")
            {
                CreateNoWindow = true,
                WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
            })!.WaitForExit();
            File.Delete($"Binaries/Win64/UnrealPakTool/list.txt");

            File.Copy($"{Settings.Default.Pal7Folder}/Pal7/Content/Paks/dat1-WindowsNoEditor.sig",
                $"{Settings.Default.Pal7Folder}/Pal7/Content/Paks/~mods/{Settings.Default.ModName}.sig", overwrite: true);
        }
    }
}

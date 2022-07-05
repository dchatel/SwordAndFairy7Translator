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
            var pakfile = Path.Combine(
                Settings.Default.Pal7Folder,
                "Pal7", "Content", "Paks", "~mods", $"{Settings.Default.ModName}.pak");
            if (!File.Exists(pakfile)) return false;
            var basedir = AppDomain.CurrentDomain.BaseDirectory;
            var unrealpaktoolpath = Path.Combine(basedir, "Binaries", "Win64", "UnrealPakTool");
            var args = $@"{Path.Combine(unrealpaktoolpath, "UnrealPak.exe")} -Extract '{pakfile}' '{basedir}Pal7' -cryptokeys='{Path.Combine(unrealpaktoolpath, "Crypto.json")}'";
            Process.Start(new ProcessStartInfo($@"powershell.exe", args)
            {
                CreateNoWindow = true,
                WorkingDirectory = basedir,
            })!.WaitForExit();
            FileSystem.MoveDirectory(Path.Combine("Pal7", "Localization"), Path.Combine("Pal7", "Content", "Localization"), true);
            FileSystem.MoveDirectory(Path.Combine("Pal7", "UI"), Path.Combine("Pal7", "Content", "UI"), true);
            return true;
        }

        public static void ExtractAny(string pattern, string extractDir)
        {
            var basedir = AppDomain.CurrentDomain.BaseDirectory;
            var args = $@"ls '{Path.Combine(Settings.Default.Pal7Folder, "Pal7", "Content", "Paks", "*.pak")}'|foreach{{{Path.Combine(basedir, "Binaries", "Win64", "UnrealPakTool", "UnrealPak.exe")} -Extract $_.FullName '{Path.Combine(basedir, extractDir)}' -Filter='{pattern}' -cryptokeys='{Path.Combine(basedir, "Binaries", "Win64", "UnrealPakTool", "Crypto.json")}'}}";
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

            Directory.CreateDirectory(Path.Combine(basedir, "Pal7", "Content", "Localization", "Game", "en"));
            Process.Start(new ProcessStartInfo(
                $"powershell.exe",
                $@"{Path.Combine(basedir, "UnrealLocres.exe")} import {Path.Combine("extracted", "Pal7", "Content", "Localization", "Game", "en", "Game.locres")} modded.csv -o {Path.Combine("Pal7", "Content", "Localization", "Game", "en", "Game.locres")}")
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

            using (var writer = new StreamWriter(Path.Combine("Binaries", "Win64", "UnrealPakTool", "list.txt")))
                writer.WriteLine(Path.Combine("..", "..", "..", "Pal7", "*"));

            Process.Start(new ProcessStartInfo(
                $"powershell.exe",
                $"{Path.Combine("Binaries", "Win64", "UnrealPakTool", "UnrealPak.exe")} '{Path.Combine(Settings.Default.Pal7Folder, "Pal7", "Content", "Paks", "~mods", $"{Settings.Default.ModName}.pak")}' -Create='list.txt' -compress")
            {
                CreateNoWindow = true,
                WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
            })!.WaitForExit();
            File.Delete(Path.Combine("Binaries", "Win64", "UnrealPakTool", "list.txt"));

            File.Copy(Path.Combine(Settings.Default.Pal7Folder, "Pal7", "Content", "Paks", "dat1-WindowsNoEditor.sig"),
                Path.Combine(Settings.Default.Pal7Folder, "Pal7", "Content", "Paks", "~mods", $"{Settings.Default.ModName}.sig"), overwrite: true);
        }
    }
}

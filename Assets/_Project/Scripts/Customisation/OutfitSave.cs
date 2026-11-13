using System;
using System.IO;
using UnityEngine;

namespace CloseTheDeal.Customisation
{
    /// <summary>
    /// The player's outfit and their runs-started count, kept on this PC in one small JSON file.
    /// Choices are stored by slot and option asset name, not by list position, so reordering or
    /// adding options in the catalog doesn't scramble an old save. A missing, unreadable or
    /// unknown-version file loads as the default outfit with nothing on screen; an unreadable
    /// one is kept beside it as ".corrupt" for a bug report.
    /// The path is Application.persistentDataPath/outfit.json, or the one given after
    /// -outfitSave on the command line (tests use this so they never touch a real save).
    /// </summary>
    public static class OutfitSave
    {
        /// <summary>Raise when the file layout changes, and teach Load the old one.</summary>
        public const int CurrentVersion = 1;

        const string FileName = "outfit.json";
        const string PathArg = "-outfitSave";

        [Serializable]
        sealed class Choice
        {
            public string slot;
            public string option;
        }

        [Serializable]
        sealed class FileData
        {
            public int version;
            public int runsStarted;
            public Choice[] choices;
        }

        /// <summary>What a load produced, for the fitting room to start from.</summary>
        public readonly struct Loaded
        {
            public readonly OutfitLoadout Loadout;
            public readonly int RunsStarted;
            public readonly bool FromFile;

            public Loaded(OutfitLoadout loadout, int runsStarted, bool fromFile)
            {
                Loadout = loadout;
                RunsStarted = runsStarted;
                FromFile = fromFile;
            }
        }

        static string _pathOverride;

        public static string FilePath
        {
            get
            {
                if (_pathOverride == null)
                    _pathOverride = ReadPathArg() ?? string.Empty;
                return _pathOverride.Length > 0 ? _pathOverride : Path.Combine(Application.persistentDataPath, FileName);
            }
        }

        public static Loaded Load(OutfitCatalog catalog)
        {
            FileData data = Read();
            if (data == null)
                return new Loaded(catalog.Default, 0, false);

            int runs = Mathf.Max(0, data.runsStarted);
            OutfitLoadout loadout = catalog.Default;
            if (data.choices != null)
            {
                foreach (Choice choice in data.choices)
                    loadout = Apply(catalog, loadout, choice);
            }

            return new Loaded(catalog.Sanitise(loadout, runs), runs, true);
        }

        /// <summary>Writes the outfit, keeping the runs count already on disk.</summary>
        public static void SaveOutfit(OutfitCatalog catalog, OutfitLoadout loadout)
        {
            FileData existing = Read();
            Write(catalog, loadout, existing != null ? Mathf.Max(0, existing.runsStarted) : 0);
        }

        /// <summary>Adds one to the runs count, keeping the outfit already on disk. Returns the new count.</summary>
        public static int AddRunStarted(OutfitCatalog catalog)
        {
            Loaded current = Load(catalog);
            int runs = current.RunsStarted + 1;
            Write(catalog, current.Loadout, runs);
            return runs;
        }

        // ---- File ----------------------------------------------------------------------------

        static OutfitLoadout Apply(OutfitCatalog catalog, OutfitLoadout loadout, Choice choice)
        {
            if (choice == null)
                return loadout;

            for (int s = 0; s < catalog.SlotCount; s++)
            {
                OutfitSlot slot = catalog.Slot(s);
                if (slot == null || slot.name != choice.slot)
                    continue;

                for (int o = 0; o < slot.OptionCount; o++)
                {
                    if (slot.IsValidOption(o) && slot.Option(o).name == choice.option)
                        return loadout.With(s, o);
                }
            }

            return loadout;
        }

        static FileData Read()
        {
            string path = FilePath;
            if (!File.Exists(path))
                return null;

            try
            {
                var data = JsonUtility.FromJson<FileData>(File.ReadAllText(path));
                if (data != null && data.version >= 1 && data.version <= CurrentVersion)
                    return data;

                Debug.LogWarning($"[Outfit] {path} is from an unknown save version; starting from the default outfit.");
                return null;
            }
            catch (Exception e) when (e is ArgumentException || e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning($"[Outfit] Couldn't read {path} ({e.Message}); starting from the default outfit. The file was kept as .corrupt.");
                KeepCorrupt(path);
                return null;
            }
        }

        static void Write(OutfitCatalog catalog, OutfitLoadout loadout, int runsStarted)
        {
            var data = new FileData
            {
                version = CurrentVersion,
                runsStarted = runsStarted,
                choices = new Choice[catalog.SlotCount]
            };

            for (int s = 0; s < catalog.SlotCount; s++)
            {
                OutfitSlot slot = catalog.Slot(s);
                int option = loadout.Get(s);
                data.choices[s] = new Choice
                {
                    slot = slot != null ? slot.name : string.Empty,
                    option = slot != null && slot.IsValidOption(option) ? slot.Option(option).name : string.Empty
                };
            }

            string path = FilePath;
            string temp = path + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(temp, JsonUtility.ToJson(data, true));
                if (File.Exists(path))
                    File.Replace(temp, path, null);
                else
                    File.Move(temp, path);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                // The outfit still applies for this session; only keeping it for next launch failed.
                Debug.LogWarning($"[Outfit] Couldn't save to {path} ({e.Message}). This outfit won't be there next launch.");
            }
        }

        static void KeepCorrupt(string path)
        {
            try
            {
                File.Copy(path, path + ".corrupt", true);
                File.Delete(path);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning($"[Outfit] Couldn't set the unreadable save aside ({e.Message}).");
            }
        }

        static string ReadPathArg()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == PathArg)
                    return args[i + 1];
            }

            return null;
        }
    }
}

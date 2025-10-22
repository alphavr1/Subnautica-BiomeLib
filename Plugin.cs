using BepInEx;
using BiomeLib.BiomeLib;
using Nautilus.Assets;
using Nautilus.Assets.PrefabTemplates;
using Nautilus.FMod;
using Nautilus.Handlers;
using Nautilus.Utility;
using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace BiomeLib
{
    [BepInPlugin("com.Violet.BiomeLib", "BiomeLib", "1.1.0")]
    [BepInDependency("com.snmodding.nautilus", BepInDependency.DependencyFlags.HardDependency)]
    public class Plugin : BaseUnityPlugin
    {
        private readonly List<BiomeDefinition> loadedBiomes = new List<BiomeDefinition>();
        private readonly HashSet<string> registeredPrefabIds = new HashSet<string>();
        private readonly HashSet<string> registeredSpawnIds = new HashSet<string>();

        private void Awake()
        {
            Logger.LogInfo("[BiomeLib] Initializing...");

            string pluginsDir = Paths.PluginPath;
            string biomeLibDir = Path.Combine(pluginsDir, "BiomeLib", "CustomBiomes");

            Directory.CreateDirectory(biomeLibDir);

            List<string> biomeFolders = new List<string> { biomeLibDir };

            foreach (string modFolder in Directory.GetDirectories(pluginsDir))
            {
                string customBiomeFolder = Path.Combine(modFolder, "CustomBiomes");
                if (Directory.Exists(customBiomeFolder))
                {
                    biomeFolders.Add(customBiomeFolder);
                    Logger.LogInfo($"[BiomeLib] Found CustomBiomes in: {Path.GetFileName(modFolder)}");
                }
            }

            foreach (string folder in biomeFolders)
            {
                LoadBiomesFromFolder(folder);
            }

            StartCoroutine(WaitForPlayerAndSpawnBiomes());
        }

        private void LoadBiomesFromFolder(string folder)
        {
            foreach (string file in Directory.GetFiles(folder, "*.json"))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var def = JsonConvert.DeserializeObject<BiomeDefinition>(json);

                    if (def == null)
                        throw new System.Exception("Invalid or empty biome definition JSON.");

                    RegisterCustomAudio(def, folder);

                    RegisterBiome(def);
                    loadedBiomes.Add(def);

                    Logger.LogInfo($"[BiomeLib] Registered biome: {def.Id}");
                }
                catch (System.Exception ex)
                {
                    Logger.LogError($"[BiomeLib] Failed to load {Path.GetFileName(file)}: {ex.Message}");
                }
            }
        }

        private void RegisterCustomAudio(BiomeDefinition def, string biomeFolderPath)
        {
            try
            {
                var soundSource = new ModFolderSoundSource(biomeFolderPath, Assembly.GetExecutingAssembly());
                var builder = new FModSoundBuilder(soundSource);

                if (def.UseCustomMusic && !string.IsNullOrEmpty(def.CustomMusic))
                {
                    string musicEventId = $"biome_{def.Id}_music";
                    builder.CreateNewEvent(musicEventId, Nautilus.Utility.AudioUtils.BusPaths.Music)
                           .SetModeMusic(true)
                           .SetMode2D(true)
                           .SetSounds(true, Path.GetFileNameWithoutExtension(def.CustomMusic))
                           .Register();

                    def.Music = musicEventId;
                    BiomeHandler.AddBiomeMusic(def.Id, AudioUtils.GetFmodAsset(musicEventId));

                    Logger.LogInfo($"[BiomeLib] Registered custom music for '{def.Id}': {def.CustomMusic}");
                }

                if (def.UseCustomAmbience && !string.IsNullOrEmpty(def.CustomAmbience))
                {
                    string ambienceEventId = $"biome_{def.Id}_ambience";
                    builder.CreateNewEvent(ambienceEventId, Nautilus.Utility.AudioUtils.BusPaths.UnderwaterAmbient)
                           .SetMode3D(3f, 70f)
                           .SetSounds(true, Path.GetFileNameWithoutExtension(def.CustomAmbience))
                           .Register();

                    def.Ambience = ambienceEventId;
                    BiomeHandler.AddBiomeAmbience(def.Id, AudioUtils.GetFmodAsset(ambienceEventId),
                                                  FMODGameParams.InteriorState.OnlyOutside);

                    Logger.LogInfo($"[BiomeLib] Registered custom ambience for '{def.Id}': {def.CustomAmbience}");
                }
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"[BiomeLib] Failed to register custom audio for '{def.Id}': {ex.Message}");
            }
        }

        private void RegisterBiome(BiomeDefinition def)
        {
            var fogSettings = BiomeUtils.CreateBiomeSettings(
                def.FogColorVec,
                def.FogDensity,
                def.LightColor,
                def.LightIntensity,
                def.AmbientColor,
                def.BloomIntensity,
                def.ScatterIntensity,
                def.SkyboxIntensity,
                def.Brightness,
                def.Distortion);

            BiomeHandler.RegisterBiome(def.Id, fogSettings, new BiomeHandler.SkyReference(def.Sky));

            if (!string.IsNullOrEmpty(def.Music) && def.Music.ToLower() != "null")
            {
                BiomeHandler.AddBiomeMusic(def.Id, AudioUtils.GetFmodAsset(def.Music));
                Logger.LogInfo($"[BiomeLib] Added built-in music for '{def.Id}'");
            }
            else
            {
                Logger.LogInfo($"[BiomeLib] Skipping built-in music for '{def.Id}'");
            }

            if (!string.IsNullOrEmpty(def.Ambience) && def.Ambience.ToLower() != "null")
            {
                BiomeHandler.AddBiomeAmbience(def.Id, AudioUtils.GetFmodAsset(def.Ambience), FMODGameParams.InteriorState.OnlyOutside);
                Logger.LogInfo($"[BiomeLib] Added built-in ambience for '{def.Id}'");
            }
            else
            {
                Logger.LogInfo($"[BiomeLib] Skipping built-in ambience for '{def.Id}'");
            }
        }

        private IEnumerator WaitForPlayerAndSpawnBiomes()
        {
            while (Player.main == null)
                yield return new WaitForSeconds(0.5f);

            Logger.LogInfo("[BiomeLib] Player detected. Spawning biome volumes...");

            foreach (var def in loadedBiomes)
            {
                string volumeId = def.Id + "_Volume";

                try
                {
                    if (!registeredPrefabIds.Contains(volumeId))
                    {
                        bool createdInfo = true;
                        PrefabInfo info = default;

                        try
                        {
                            info = PrefabInfo.WithTechType(volumeId);
                        }
                        catch (System.Exception createEx)
                        {
                            Logger.LogWarning($"[BiomeLib] PrefabInfo.WithTechType threw for {volumeId}: {createEx.Message}");
                            createdInfo = false;
                        }

                        if (createdInfo)
                        {
                            try
                            {
                                var prefab = new CustomPrefab(info);
                                var template = new AtmosphereVolumeTemplate(info, AtmosphereVolumeTemplate.VolumeShape.Sphere, def.Id);
                                prefab.SetGameObject(template);
                                prefab.Register();

                                registeredPrefabIds.Add(volumeId);
                                Logger.LogInfo($"[BiomeLib] Registered prefab info for {volumeId}");
                            }
                            catch (System.Exception prefabEx)
                            {
                                Logger.LogWarning($"[BiomeLib] Failed to register prefab for {volumeId}: {prefabEx.Message}");
                            }
                        }
                        else
                        {
                            Logger.LogInfo($"[BiomeLib] Skipping prefab creation for {volumeId} (already exists or failed earlier).");
                            registeredPrefabIds.Add(volumeId);
                        }
                    }

                    if (!registeredSpawnIds.Contains(volumeId))
                    {
                        try
                        {
                            CoordinatedSpawnsHandler.RegisterCoordinatedSpawn(
                                new SpawnInfo(volumeId, def.Position, Quaternion.identity, def.Scale));
                            registeredSpawnIds.Add(volumeId);
                            Logger.LogInfo($"[BiomeLib] Registered spawn for {def.Id}");
                        }
                        catch (System.Exception spawnEx)
                        {
                            Logger.LogWarning($"[BiomeLib] Failed to register spawn for {def.Id}: {spawnEx.Message}");
                            registeredSpawnIds.Add(volumeId);
                        }
                    }

                    try
                    {
                        ConsoleCommandsHandler.AddBiomeTeleportPosition(def.Id, def.Position);
                    }
                    catch { }

                    Logger.LogInfo($"[BiomeLib] Spawn process completed for biome: {def.Id}");
                }
                catch (System.Exception ex)
                {
                    Logger.LogError($"[BiomeLib] Failed to spawn biome {def.Id}: {ex.Message}\n{ex.StackTrace}");
                }

                yield return null;
            }

            Logger.LogInfo($"[BiomeLib] Finished spawning {loadedBiomes.Count} biomes.");
        }
    }
}

using BepInEx;
using BiomeLib.BiomeLib;
using BiomeLib;
using Nautilus.Assets;
using Nautilus.Assets.PrefabTemplates;
using Nautilus.FMod;
using Nautilus.Handlers;
using Nautilus.Utility;
using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace BiomeLib
{
    [BepInPlugin("com.Violet.BiomeLib", "BiomeLib", "1.1.0")]
    [BepInDependency("com.snmodding.nautilus", BepInDependency.DependencyFlags.HardDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        private readonly List<BiomeDefinition> loadedBiomes = new List<BiomeDefinition>();
        private readonly HashSet<string> registeredPrefabIds = new HashSet<string>();
        private readonly HashSet<string> registeredSpawnIds = new HashSet<string>();

        private void Awake()
        {
            Logger.LogInfo("[BiomeLib] Initializing…");

            foreach (string folder in FindBiomeDirectories())
            {
                LoadBiomesFrom(folder);
            }

            StartCoroutine(WaitForPlayerThenSpawnBiomes());
        }

        private IEnumerable<string> FindBiomeDirectories()
        {
            string pluginsRoot = Paths.PluginPath;
            string coreBiomeDirectory = Path.Combine(pluginsRoot, "BiomeLib", "CustomBiomes");

            Directory.CreateDirectory(coreBiomeDirectory);

            yield return coreBiomeDirectory;

            foreach (string pluginDirectory in Directory.GetDirectories(pluginsRoot))
            {
                string customBiomeDirectory = Path.Combine(pluginDirectory, "CustomBiomes");
                if (Directory.Exists(customBiomeDirectory))
                {
                    Logger.LogInfo($"[BiomeLib] Found CustomBiomes in: {Path.GetFileName(pluginDirectory)}");
                    yield return customBiomeDirectory;
                }
            }
        }

        private void LoadBiomesFrom(string directory)
        {
            foreach (string filePath in Directory.GetFiles(directory, "*.json"))
            {
                try
                {
                    string json = File.ReadAllText(filePath);
                    BiomeDefinition biome = JsonConvert.DeserializeObject<BiomeDefinition>(json);

                    if (biome == null)
                    {
                        throw new InvalidDataException("[BiomeLib] Invalid or empty biome definition JSON.");
                    }

                    RegisterCustomAudio(biome, directory);
                    RegisterBiome(biome);

                    loadedBiomes.Add(biome);

                    Logger.LogInfo($"[BiomeLib] Registered biome: {biome.Id}");
                }
                catch (System.Exception exception)
                {
                    Logger.LogError($"[BiomeLib] Failed to load {Path.GetFileName(filePath)}: {exception.Message}");
                }
            }
        }

        private void RegisterCustomAudio(BiomeDefinition biome, string biomeDirectory)
        {
            try
            {
                var soundSource = new ModFolderSoundSource(biomeDirectory, Assembly.GetExecutingAssembly());
                var soundBuilder = new FModSoundBuilder(soundSource);

                if (biome.UseCustomMusic && !string.IsNullOrWhiteSpace(biome.CustomMusic))
                {
                    string musicEventId = $"biome_{biome.Id}_music";

                    soundBuilder.CreateNewEvent(musicEventId, AudioUtils.BusPaths.Music)
                                .SetModeMusic(true)
                                .SetMode2D(true)
                                .SetSounds(true, Path.GetFileNameWithoutExtension(biome.CustomMusic))
                                .Register();

                    biome.Music = musicEventId;
                    BiomeHandler.AddBiomeMusic(biome.Id, AudioUtils.GetFmodAsset(musicEventId));

                    Logger.LogInfo($"[BiomeLib] Registered custom music for '{biome.Id}': {biome.CustomMusic}");
                }

                if (biome.UseCustomAmbience && !string.IsNullOrWhiteSpace(biome.CustomAmbience))
                {
                    string ambienceEventId = $"biome_{biome.Id}_ambience";

                    soundBuilder.CreateNewEvent(ambienceEventId, AudioUtils.BusPaths.UnderwaterAmbient)
                                .SetMode3D(3f, 70f)
                                .SetSounds(true, Path.GetFileNameWithoutExtension(biome.CustomAmbience))
                                .Register();

                    biome.Ambience = ambienceEventId;
                    BiomeHandler.AddBiomeAmbience(biome.Id, AudioUtils.GetFmodAsset(ambienceEventId),
                                                  FMODGameParams.InteriorState.OnlyOutside);

                    Logger.LogInfo($"[BiomeLib] Registered custom ambience for '{biome.Id}': {biome.CustomAmbience}");
                }
            }
            catch (System.Exception exception)
            {
                Logger.LogError($"[BiomeLib] Failed to register custom audio for '{biome.Id}': {exception.Message}");
            }
        }

        private void RegisterBiome(BiomeDefinition biome)
        {
            var fogSettings = BiomeUtils.CreateBiomeSettings(
                biome.FogColorVec,
                biome.FogDensity,
                biome.LightColor,
                biome.LightIntensity,
                biome.AmbientColor,
                biome.BloomIntensity,
                biome.ScatterIntensity,
                biome.SkyboxIntensity,
                biome.Brightness,
                biome.Distortion);

            BiomeHandler.RegisterBiome(biome.Id, fogSettings, new BiomeHandler.SkyReference(biome.Sky));

            if (!string.IsNullOrWhiteSpace(biome.Music) && !string.Equals(biome.Music, "null", System.StringComparison.OrdinalIgnoreCase))
            {
                BiomeHandler.AddBiomeMusic(biome.Id, AudioUtils.GetFmodAsset(biome.Music));
                Logger.LogInfo($"[BiomeLib] Added built-in music for '{biome.Id}'. Custom audio will override if configured.");
            }
            else
            {
                Logger.LogInfo($"[BiomeLib] No built-in music for '{biome.Id}'. Custom audio will play if configured.");
            }

            if (!string.IsNullOrWhiteSpace(biome.Ambience) && !string.Equals(biome.Ambience, "null", System.StringComparison.OrdinalIgnoreCase))
            {
                BiomeHandler.AddBiomeAmbience(biome.Id, AudioUtils.GetFmodAsset(biome.Ambience),
                                              FMODGameParams.InteriorState.OnlyOutside);
                Logger.LogInfo($"[BiomeLib] Added built-in ambience for '{biome.Id}'. Custom audio will override if configured.");
            }
            else
            {
                Logger.LogInfo($"[BiomeLib] No built-in ambience for '{biome.Id}'. Custom audio will play if configured.");
            }
        }

        private IEnumerator WaitForPlayerThenSpawnBiomes()
        {
            yield return new WaitUntil(() => Player.main != null);

            foreach (BiomeDefinition biome in loadedBiomes)
            {
                string volumeId = $"{biome.Id}_Volume";

                try
                {
                    RegisterBiomePrefab(volumeId, biome);
                    RegisterBiomeSpawn(volumeId, biome);
                    RegisterConsoleTeleportCommands(biome);

                    Logger.LogInfo($"[BiomeLib] Spawn process completed for biome: {biome.Id}");
                }
                catch (System.Exception exception)
                {
                    Logger.LogError($"[BiomeLib] Failed to spawn biome {biome.Id}: {exception.Message}\n{exception.StackTrace}");
                }

                yield return null;
            }

            Logger.LogInfo($"[BiomeLib] Finished spawning {loadedBiomes.Count} biomes.");
        }

        private void RegisterBiomePrefab(string volumeId, BiomeDefinition biome)
        {
            if (registeredPrefabIds.Contains(volumeId))
            {
                return;
            }

            try
            {
                PrefabInfo prefabInfo = PrefabInfo.WithTechType(volumeId);
                var prefab = new CustomPrefab(prefabInfo);
                var volumeTemplate = new AtmosphereVolumeTemplate(prefabInfo,
                                                                  AtmosphereVolumeTemplate.VolumeShape.Sphere,
                                                                  biome.Id);

                prefab.SetGameObject(volumeTemplate);
                prefab.Register();

                Logger.LogInfo($"[BiomeLib] Registered prefab info for {volumeId}");
            }
            catch (System.Exception exception)
            {
                Logger.LogWarning($"[BiomeLib] Failed to register prefab for {volumeId}: {exception.Message}");
            }
            finally
            {
                registeredPrefabIds.Add(volumeId);
            }
        }

        private void RegisterBiomeSpawn(string volumeId, BiomeDefinition biome)
        {
            if (registeredSpawnIds.Contains(volumeId))
            {
                return;
            }

            try
            {
                var spawn = new SpawnInfo(volumeId, biome.Position, Quaternion.identity, biome.Scale);
                CoordinatedSpawnsHandler.RegisterCoordinatedSpawn(spawn);

                Logger.LogInfo($"[BiomeLib] Registered spawn for {biome.Id}");
            }
            catch (System.Exception exception)
            {
                Logger.LogWarning($"[BiomeLib] Failed to register spawn for {biome.Id}: {exception.Message}");
            }
            finally
            {
                registeredSpawnIds.Add(volumeId);
            }
        }

        private void RegisterConsoleTeleportCommands(BiomeDefinition biome)
        {
            try
            {
                ConsoleCommandsHandler.AddBiomeTeleportPosition(biome.Id, biome.Position);
                ConsoleCommandsHandler.AddGotoTeleportPosition(biome.Id, biome.Position);
            }
            catch
            {

            }
        }
    }
}

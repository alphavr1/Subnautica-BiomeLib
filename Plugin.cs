using BepInEx;
using BiomeLib.BiomeLib;
using Nautilus.Assets;
using Nautilus.Assets.PrefabTemplates;
using Nautilus.Handlers;
using Nautilus.Utility;
using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BiomeLib
{
    [BepInPlugin("com.Violet.BiomeLib", "BiomeLib", "1.0.0")]
    [BepInDependency("com.snmodding.nautilus", BepInDependency.DependencyFlags.HardDependency)]
    public class Plugin : BaseUnityPlugin
    {
        private readonly List<BiomeDefinition> loadedBiomes = new List<BiomeDefinition>();

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
            BiomeHandler.AddBiomeMusic(def.Id, AudioUtils.GetFmodAsset(def.Music));
            BiomeHandler.AddBiomeAmbience(def.Id, AudioUtils.GetFmodAsset(def.Ambience), FMODGameParams.InteriorState.OnlyOutside);
        }

        private IEnumerator WaitForPlayerAndSpawnBiomes()
        {
            while (Player.main == null)
                yield return new WaitForSeconds(0.5f);

            Logger.LogInfo("[BiomeLib] Player detected. Spawning biome volumes...");

            foreach (var def in loadedBiomes)
            {
                try
                {
                    var info = PrefabInfo.WithTechType(def.Id + "_Volume");
                    var prefab = new CustomPrefab(info);
                    var template = new AtmosphereVolumeTemplate(info, AtmosphereVolumeTemplate.VolumeShape.Sphere, def.Id);
                    prefab.SetGameObject(template);
                    prefab.Register();

                    CoordinatedSpawnsHandler.RegisterCoordinatedSpawn(
                        new SpawnInfo(info.ClassID, def.Position, Quaternion.identity, def.Scale));

                    ConsoleCommandsHandler.AddBiomeTeleportPosition(def.Id, def.Position);

                    Logger.LogInfo($"[BiomeLib] Spawned biome: {def.Id}");
                }
                catch (System.Exception ex)
                {
                    Logger.LogError($"[BiomeLib] Failed to spawn biome {def.Id}: {ex.Message}");
                }
            }

            Logger.LogInfo($"[BiomeLib] Finished spawning {loadedBiomes.Count} biomes.");
        }
    }
}

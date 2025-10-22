using Newtonsoft.Json;
using UnityEngine;

namespace BiomeLib.BiomeLib
{
    public class BiomeDefinition
    {
        public string Id { get; set; }

        public string Sky { get; set; } = "SkyKelpForest";
        public bool UseCustomMusic { get; set; } = false;
        public bool UseCustomAmbience { get; set; } = false;

        public string Music { get; set; } = "event:/env/music/wreak_ambience_big_music";
        public string Ambience { get; set; } = "event:/env/background/wreak_ambience_big";

        
        public string CustomMusic { get; set; } = null;
        public string CustomAmbience { get; set; } = null;

        public Color FogColor { get; set; } = Color.red;
        public float FogDensity { get; set; } = 0.6f;
        public Color LightColor { get; set; } = Color.white;
        public float LightIntensity { get; set; } = 0.45f;
        public Color AmbientColor { get; set; } = new Color(0.18f, 0.604f, 0.404f);
        public float BloomIntensity { get; set; } = 0.05f;
        public float ScatterIntensity { get; set; } = 20f;
        public float SkyboxIntensity { get; set; } = 1f;
        public float Brightness { get; set; } = 1.25f;
        public float Distortion { get; set; } = 20f;

        public Vector3 Position { get; set; } = new Vector3(0, -100, 0);
        public Vector3 Scale { get; set; } = new Vector3(50, 50, 50);

        [JsonIgnore]
        public Vector3 FogColorVec => new Vector3(FogColor.r, FogColor.g, FogColor.b);
    }
}

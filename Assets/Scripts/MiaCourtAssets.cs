using UnityEngine;

namespace MiaCourt
{
    [CreateAssetMenu(menuName = "Mia Court/Court assets")]
    public sealed class MiaCourtAssets : ScriptableObject
    {
        public static readonly string[] CharacterIds =
        {
            "MiaByBy3D", "MiaBuBu3D", "GuguGaga3D", "YaMei3D",
            "YuMei3D", "FengBro3D", "Tu3D", "DpskMusume3D"
        };
        public static readonly string[] CharacterNames =
        {
            "喵白白", "喵布布", "咕咕嘎嘎", "牙妹",
            "魚妹", "鋒兄", "塗董", "深索娘"
        };
        public const int CharacterCount = 8;
        /// <summary>Scene props converted from the supplied GLBs by Tools/prepare_props.py.</summary>
        public static readonly string[] PropIds =
        {
            "CourtBall", "CourtFence", "CourtBench",
            "CourtFloodlight", "CourtTrashBin", "CourtScooter"
        };

        public GameObject[] characterModels;
        public Texture2D[] characterTextures;
        public GameObject[] propModels;
        public Texture2D[] propTextures;
        public Texture2D[] propNormals;
        public GameObject miaByBy;
        public GameObject miaBuBu;
        public GameObject guguGaga;
        public Texture2D byByTexture;
        public Texture2D buBuTexture;
        public Texture2D guguGagaTexture;
        public Texture2D leftView;
        public Texture2D centerView;
        public Texture2D rightView;
        public Shader courtShader;
        public Shader backdropShader;
        public Shader trailShader;
        public Shader litShader;

        public GameObject ModelFor(int index)
        {
            index = Mathf.Clamp(index, 0, CharacterCount - 1);
            if (characterModels != null && index < characterModels.Length && characterModels[index] != null)
                return characterModels[index];
            if (index == 1) return miaBuBu;
            if (index == 2) return guguGaga;
            return miaByBy;
        }

        public Texture2D TextureFor(int index)
        {
            index = Mathf.Clamp(index, 0, CharacterCount - 1);
            if (characterTextures != null && index < characterTextures.Length && characterTextures[index] != null)
                return characterTextures[index];
            if (index == 1) return buBuTexture;
            if (index == 2) return guguGagaTexture;
            return byByTexture;
        }

        /// <summary>The prop mesh, or null when the court falls back to its procedural stand-in.</summary>
        public GameObject PropFor(string id) => Lookup(propModels, id);
        public Texture2D PropTextureFor(string id) => Lookup(propTextures, id);
        public Texture2D PropNormalFor(string id) => Lookup(propNormals, id);

        static T Lookup<T>(T[] table, string id) where T : Object
        {
            int index = System.Array.IndexOf(PropIds, id);
            if (table == null || index < 0 || index >= table.Length) return null;
            return table[index];
        }

        public static string NameFor(int index)
        {
            index = Mathf.Clamp(index, 0, CharacterCount - 1);
            return CharacterNames[index];
        }
    }
}

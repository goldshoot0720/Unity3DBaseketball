using UnityEngine;

namespace MiaCourt
{
    [CreateAssetMenu(menuName = "Mia Court/Court assets")]
    public sealed class MiaCourtAssets : ScriptableObject
    {
        public static readonly string[] CharacterIds =
        {
            "MiaByBy3D", "MiaBuBu3D", "GuguGaga3D",
            "TeethMei3D", "FishMei", "Huang3D", "XiaoTu3D",
            "FengHsin3D", "HsinFeng3D", "Tu3D"
        };
        public static readonly string[] CharacterNames =
        {
            "喵白白", "喵布布", "咕咕嘎嘎",
            "牙妹", "魚妹", "鋒兄", "小塗",
            "鋒市", "鋒總", "塗董"
        };
        public const int CharacterCount = 10;

        public GameObject[] characterModels;
        public Texture2D[] characterTextures;
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

        public static string NameFor(int index)
        {
            index = Mathf.Clamp(index, 0, CharacterCount - 1);
            return CharacterNames[index];
        }
    }
}

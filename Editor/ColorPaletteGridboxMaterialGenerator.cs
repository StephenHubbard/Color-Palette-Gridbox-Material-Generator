using UnityEngine;
using UnityEditor;
using System.IO;

namespace CPGMG
{
    public class ColorPaletteGridboxMaterialGenerator : EditorWindow
    {
        [SerializeField] Texture2D _colorPalette;
        [SerializeField] Texture2D _overlayPattern;
        [SerializeField] int _textureSize = 512;

        const string BASE_SAVE_PATH = "Assets/Generated Materials";

        enum RenderPipeline { BiRP, URP, HDRP }
        [SerializeField] RenderPipeline selectedPipeline = RenderPipeline.URP;

        [MenuItem("Tools/Color Palette Gridbox Material Generator")]
        public static void ShowWindow()
        {
            GetWindow<ColorPaletteGridboxMaterialGenerator>("Color Palette Gridbox Material Generator");
        }

        void OnGUI()
        {
            GUILayout.Label("Color Palette Gridbox Material Generator", EditorStyles.boldLabel);

            _colorPalette = (Texture2D)EditorGUILayout.ObjectField("Color Palette", _colorPalette, typeof(Texture2D), false);
            _overlayPattern = (Texture2D)EditorGUILayout.ObjectField("Overlay Pattern", _overlayPattern, typeof(Texture2D), false);
            _textureSize = EditorGUILayout.IntField("Texture Size", _textureSize);

            selectedPipeline = (RenderPipeline)EditorGUILayout.EnumPopup("Render Pipeline", selectedPipeline);

            if (GUILayout.Button("Generate Materials"))
            {
                if (_colorPalette == null || _overlayPattern == null)
                {
                    Debug.LogError("Please assign both a color palette and an overlay pattern.");
                    return;
                }

                GenerateTexturesAndMaterials();
            }
        }

        void GenerateTexturesAndMaterials()
        {
            EnsureTextureReadableAndNPOT(_colorPalette);

            string paletteFileName = Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(_colorPalette));
            string folderPath = Path.Combine(BASE_SAVE_PATH, paletteFileName);
            string texturesPath = Path.Combine(folderPath, "Textures");
            string materialsPath = Path.Combine(folderPath, "Materials");

            CreateFolderIfNotExists(folderPath);
            CreateFolderIfNotExists(texturesPath);
            CreateFolderIfNotExists(materialsPath);

            int colorCount = _colorPalette.width; 
            Debug.Log($"Palette contains {colorCount} colors.");

            for (int i = 0; i < colorCount; i++)
            {
                Color color = _colorPalette.GetPixel(i, 0);  
                string hexCode = ColorToHex(color);

                Texture2D newTexture = new Texture2D(_textureSize, _textureSize);

                for (int y = 0; y < _textureSize; y++)
                {
                    for (int x = 0; x < _textureSize; x++)
                    {
                        Color overlayColor = _overlayPattern.GetPixel(x % _overlayPattern.width, y % _overlayPattern.height);
                        Color finalColor = Color.Lerp(color, overlayColor, overlayColor.a);
                        newTexture.SetPixel(x, y, finalColor);
                    }
                }

                newTexture.Apply();

                string textureFileName = $"{hexCode} - {paletteFileName}.png";
                string texturePath = Path.Combine(texturesPath, textureFileName);
                File.WriteAllBytes(texturePath, newTexture.EncodeToPNG());
                AssetDatabase.ImportAsset(texturePath);

                Debug.Log($"Texture saved: {texturePath}");

                CreateMaterial(textureFileName, texturePath, materialsPath);

                DestroyImmediate(newTexture);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            EditorUtility.FocusProjectWindow();
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(materialsPath));
            
            Debug.Log($"{colorCount} materials have been successfully generated.");
        }

        void CreateMaterial(string textureFileName, string texturePath, string materialsPath)
        {
            string materialName = Path.GetFileNameWithoutExtension(textureFileName);
            string materialPath = Path.Combine(materialsPath, materialName + ".mat");

            string shaderName = GetShaderNameForPipeline();

            Material material = new Material(Shader.Find(shaderName));
            if (material == null)
            {
                Debug.LogError($"Shader '{shaderName}' not found. Ensure the selected render pipeline is installed.");
                return;
            }

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture == null)
            {
                Debug.LogError($"Failed to load texture at {texturePath}");
                return;
            }

            material.SetTexture("_BaseMap", texture);

            AssetDatabase.CreateAsset(material, materialPath);
            Debug.Log($"Material created: {materialPath}");
        }

        string GetShaderNameForPipeline()
        {
            switch (selectedPipeline)
            {
                case RenderPipeline.BiRP:
                    return "Standard";
                case RenderPipeline.URP:
                    return "Universal Render Pipeline/Lit";
                case RenderPipeline.HDRP:
                    return "HDRP/Lit";
                default:
                    return "Standard";
            }
        }

        void EnsureTextureReadableAndNPOT(Texture2D texture)
        {
            string path = AssetDatabase.GetAssetPath(texture);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;

            if (importer == null) return;
            
            bool shouldReimport = false;

            if (!importer.isReadable || importer.npotScale != TextureImporterNPOTScale.None)
            {
                importer.isReadable = true;
                importer.npotScale = TextureImporterNPOTScale.None;
                shouldReimport = true;
            }

            if (!shouldReimport) return;

            importer.SaveAndReimport();
        }

        void CreateFolderIfNotExists(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                Directory.CreateDirectory(path);
                AssetDatabase.Refresh();
            }
        }

        string ColorToHex(Color color)
        {
            int r = Mathf.RoundToInt(color.r * 255);
            int g = Mathf.RoundToInt(color.g * 255);
            int b = Mathf.RoundToInt(color.b * 255);
            return $"#{r:X2}{g:X2}{b:X2}";
        }
    }
}


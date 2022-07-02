using UnityEditor;
using UnityEngine;

public class TextureFormatTools : ScriptableObject
{
    public static void SelectedChangeTextureFormatSettings(TextureImporter textureImporter)
    {
        string filePath = textureImporter.assetPath;
        if (textureImporter == null || filePath.Contains("UnCompress"))
            return;

        TextureImporterPlatformSettings importerSettings = null;

#if UNITY_ANDROID
        importerSettings = textureImporter.GetPlatformTextureSettings("Android") ?? new TextureImporterPlatformSettings
        {
            overridden = true,
            name = "Android",
        };
#elif UNITY_IOS
        importerSettings = textureImporter.GetPlatformTextureSettings("iPhone") ?? new TextureImporterPlatformSettings
        {
            overridden = true,
            name = "iPhone",
        };
#endif

        if (textureImporter.alphaSource != TextureImporterAlphaSource.FromGrayScale)
        {
            if (CheckTextureImportHasAlpha(textureImporter))
            {
                textureImporter.alphaSource = TextureImporterAlphaSource.FromInput;
                textureImporter.alphaIsTransparency = true;
            }
            else
            {
                textureImporter.alphaSource = TextureImporterAlphaSource.None;
                textureImporter.alphaIsTransparency = false;
            }
        }

        if (filePath.Contains("Arts/Map/Texture"))
        {
            importerSettings.maxTextureSize = 512;
            if (filePath.Contains("RGBETC2"))
            {
                if (filePath.Contains("light")) //光照贴图
                    importerSettings.maxTextureSize = IsLowRes(filePath) ? 256 : 1024;
                else //场景贴图
                    importerSettings.maxTextureSize = IsLowRes(filePath) ? 256 : 1024;
            }
            else if (filePath.Contains("ETC"))
            {
                importerSettings.maxTextureSize = IsLowRes(filePath) ? 256 : 1024;
            }
            else if (filePath.Contains("RGBHalf"))
            {
                importerSettings.maxTextureSize = IsLowRes(filePath) ? 128 : 256;
            }
            else if (filePath.Contains("ASTC4"))
            {
                //该方案适用于场景法线贴图
                importerSettings.maxTextureSize = IsLowRes(filePath) ? 256 : 1024;
            }
        }
        else if (filePath.Contains("Arts/Actor/Texture")) //角色贴图
        {
            if (filePath.Contains("/GJ_Boss/"))
                importerSettings.maxTextureSize = IsLowRes(filePath) ? 256 : 1024;
            else if (filePath.Contains("/GJ_Role/"))
                importerSettings.maxTextureSize = IsLowRes(filePath) ? 256 : 1024;
            else if (filePath.Contains("/GJ_Monster/"))
                importerSettings.maxTextureSize = IsLowRes(filePath) ? 128 : 512;
        }
        else if (filePath.Contains("Arts/Particles")) //特效贴图尽量压缩
        {
            // SetTextureHalfSize.GetTextureOriginalSize(textureImporter, out int width, out int height);
            // int temp = (int)(Mathf.Max(width, height));
            // int size = SetTextureHalfSize.GetValidSize(temp);
            // size = Mathf.Min(size, 256);
            // importerSettings.maxTextureSize = Mathf.Min(importerSettings.maxTextureSize, size);
        }
        else if (filePath.Contains("Resources_moved/UIImage/") || filePath.Contains("Resources/UIImage/"))
        {
            textureImporter.mipmapEnabled = false;
            textureImporter.wrapMode = TextureWrapMode.Clamp;
            importerSettings.maxTextureSize = 2048;
        }
        else if (filePath.Contains("Resources_moved/UI/") || filePath.Contains("Resources/UI/"))
        {
            textureImporter.mipmapEnabled = false;
            textureImporter.wrapMode = TextureWrapMode.Clamp;
        }

        importerSettings.format = TextureImporterFormat.ASTC_8x8;
        importerSettings.overridden = true;
        importerSettings.textureCompression = TextureImporterCompression.CompressedHQ;
        importerSettings.compressionQuality = 50;
        textureImporter.SetPlatformTextureSettings(importerSettings);

        AssetDatabase.ImportAsset(filePath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
    }

    public static bool CheckTextureImportHasAlpha(TextureImporter textureImporter)
    {
        if (textureImporter.assetPath.Contains("Arts/Texture/hero"))
            return false;

        if (textureImporter.alphaSource == TextureImporterAlphaSource.FromGrayScale)
        {
            return true;
        }

        return textureImporter.DoesSourceTextureHaveAlpha();
    }

    private static bool IsLowRes(string filePath)
    {
        return false;
        // return filePath.Contains(ArtResourcesTool.LOW_TAG);
    }
}
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.AssetImporters;
using UnityEngine;

[ScriptedImporter(1, "dtx")]
public class DTXImporter : ScriptedImporter
{
    public override void OnImportAsset(AssetImportContext ctx)
    {
        DTXFile dtxFile = new DTXFile(ctx.assetPath);
        
        Texture2D texture = dtxFile.GetTexture();

        ctx.AddObjectToAsset("Texture", texture);
        ctx.SetMainObject(texture);

        //byte[] bytes = texture.EncodeToPNG();
        //string fileName = Application.dataPath + '/' + ctx.assetPath.Split('/').Last() + ".png";
        //File.WriteAllBytes(fileName, bytes);
    }
}

using UnityEngine;
using UnityEditor;

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

public class SamplePrefabricator2 : AssetBundleGraph.PrefabricatorBase {
	public override void Estimate (string groupKey, List<AssetBundleGraph.AssetInfo> sources, string recommendedPrefabOutputDir, Func<string, string> Prefabricate) {
		Prefabricate("prefab2.prefab");
	}
	
	public override void Run (string groupKey, List<AssetBundleGraph.AssetInfo> sources, string recommendedPrefabOutputDir, Func<GameObject, string, bool, string> Prefabricate) {
		
		/*
			you can see what incoming to this Prefabricator at the Inspector between Prefabricator to next node.
			these codes are based on that information.
		*/

		// get texture.
		var textureAssetPath = sources[1].assetPath;
		var textureAssetType = sources[1].assetType;

		// load texture from AssetDatabase.
		var characterTexture = AssetDatabase.LoadAssetAtPath(textureAssetPath, textureAssetType) as Texture2D;
			
		if (characterTexture) Debug.Log("Prefabricate:loaded:" + textureAssetPath);
		else Debug.LogError("Prefabricate:failed to load:" + textureAssetPath);


		// get material from path.
		var materialAssetPath = sources[3].assetPath;
		var materialAssetType = sources[3].assetType;

		// load texture from AssetDatabase.
		var characterMaterial = AssetDatabase.LoadAssetAtPath(materialAssetPath, materialAssetType) as Material;

		if (characterMaterial) Debug.Log("Prefabricate:loaded:" + materialAssetPath);
		else Debug.LogError("Prefabricate:failed to load:" + materialAssetPath);
		
		// then set loaded texture to that material.
		characterMaterial.mainTexture = characterTexture;

		// generate cube then set texture to it.
		var cubeObj = GameObject.CreatePrimitive(PrimitiveType.Cube);

		var meshRenderer = cubeObj.GetComponent<MeshRenderer>();
		meshRenderer.material = characterMaterial;

		// generate prefab in prefabBaseName folder. "node/SOMEWHERE/groupKey/prefab.prefab". AssetBundleGraph determines this path automatically.
		var generatedPrefabPath = Prefabricate(cubeObj, "prefab2.prefab", false);
		// Debug.LogWarning("prefab:" + generatedPrefabPath + " is generated or already cached.");

		// delete unnecessary cube model from hierarchy.
		GameObject.DestroyImmediate(cubeObj);
	}
}
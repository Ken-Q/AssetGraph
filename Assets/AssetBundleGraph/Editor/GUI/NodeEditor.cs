﻿using UnityEngine;
using UnityEditor;

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace AssetBundleGraph {
	/**
		GUI Inspector to Node (Through NodeGUIInfo)
	*/
	[CustomEditor(typeof(NodeGUIInfo))]
	public class NodeEditor : Editor {

		private List<Action> messageActions;

		private bool packageEditMode = false;

		public override bool RequiresConstantRepaint() {
			return true;
		}

		public override void OnInspectorGUI () {
			var currentTarget = (NodeGUIInfo)target;
			var node = currentTarget.node;
			if (node == null) return;

			if(messageActions == null) {
				messageActions = new List<Action>();
			}

			var basePlatform = node.currentPlatform;

			messageActions.Clear();

			switch (node.kind) {
			case AssetBundleGraphSettings.NodeKind.LOADER_GUI: {
					if (node.loadPath == null) return;

					EditorGUILayout.HelpBox("Loader: load files from path.", MessageType.Info);
					UpdateNodeName(node);

					GUILayout.Space(10f);

					/*
								platform & package
							*/
					{
						if (packageEditMode) EditorGUI.BeginDisabledGroup(true);

						// update platform & package.
						node.currentPlatform = UpdateCurrentPlatform(basePlatform);

						using (new EditorGUILayout.VerticalScope(GUI.skin.box)) {
							EditorGUILayout.LabelField("Load Path:");
							var newLoadPath = EditorGUILayout.TextField(
								GraphStackController.ProjectName(), 
								GraphStackController.ValueFromPlatformAndPackage(
									node.loadPath.ReadonlyDict(), 
									node.currentPlatform
								).ToString()
							);
							var loaderNodePath = GraphStackController.WithProjectPath(newLoadPath);
							IntegratedGUILoader.ValidateLoadPath(
								newLoadPath,
								loaderNodePath,
								() => {
									EditorGUILayout.HelpBox("load path is empty.", MessageType.Error);
								},
								() => {
									EditorGUILayout.HelpBox("directory not found:" + loaderNodePath, MessageType.Error);
								}
							);

							if (newLoadPath !=	GraphStackController.ValueFromPlatformAndPackage(
								node.loadPath.ReadonlyDict(),
								node.currentPlatform
							).ToString()
							) {
								node.BeforeSave();
								node.loadPath.Add(GraphStackController.Platform_Package_Key(node.currentPlatform), newLoadPath);
								node.Save();
							}
						}

						if (packageEditMode) EditorGUI.EndDisabledGroup();
						UpdateDeleteSetting(node);
					}
					break;
				}

			case AssetBundleGraphSettings.NodeKind.FILTER_SCRIPT: {
					EditorGUILayout.HelpBox("Filter: filtering files by script.", MessageType.Info);
					UpdateNodeName(node);

					EditorGUILayout.LabelField("Script Path", node.scriptPath);

					var outputPointLabels = node.OutputPointLabels();
					EditorGUILayout.LabelField("connectionPoints Count", outputPointLabels.Count.ToString());

					foreach (var label in outputPointLabels) {
						EditorGUILayout.LabelField("label", label);
					}
					break;
				}

			case AssetBundleGraphSettings.NodeKind.FILTER_GUI: {
					EditorGUILayout.HelpBox("Filter: filtering files by keywords and types.", MessageType.Info);
					UpdateNodeName(node);

					using (new EditorGUILayout.VerticalScope(GUI.skin.box)) {
						GUILayout.Label("Filter Settings:");
						for (int i = 0; i < node.filterContainsKeywords.Count; i++) {

							Action messageAction = null;

							using (new GUILayout.HorizontalScope()) {
								if (GUILayout.Button("-", GUILayout.Width(30))) {
									node.BeforeSave();
									node.filterContainsKeywords.RemoveAt(i);
									node.FilterOutputPointsDeleted(i);
								}
								else {
									var newContainsKeyword = node.filterContainsKeywords[i];

									/*
												generate keyword + keytype string for compare exists setting vs new modifying setting at once.
											*/
									var currentKeywordsSource = new List<string>(node.filterContainsKeywords);
									var currentKeytypesSource = new List<string>(node.filterContainsKeytypes);

									var currentKeytype = currentKeytypesSource[i];

									for (var j = 0; j < currentKeywordsSource.Count; j++) {
										currentKeywordsSource[j] = currentKeywordsSource[j] + currentKeytypesSource[j];
									}

									// remove current choosing one from compare target.
									currentKeywordsSource.RemoveAt(i);
									var currentKeywords = new List<string>(currentKeywordsSource);

									GUIStyle s = new GUIStyle((GUIStyle)"TextFieldDropDownText");

									IntegratedGUIFilter.ValidateFilter(
										newContainsKeyword + currentKeytype,
										currentKeywords,
										() => {
											s.fontStyle = FontStyle.Bold;
											s.fontSize = 12;
										},
										() => {
											s.fontStyle = FontStyle.Bold;
											s.fontSize = 12;
										}
									);

									using (new EditorGUILayout.HorizontalScope()) {
										newContainsKeyword = EditorGUILayout.TextField(node.filterContainsKeywords[i], s, GUILayout.Width(120));
										var currentIndex = i;
										if (GUILayout.Button(node.filterContainsKeytypes[i], "Popup")) {
											Node.ShowFilterKeyTypeMenu(
												node.filterContainsKeytypes[currentIndex],
												(string selectedTypeStr) => {
													node.BeforeSave();
													node.filterContainsKeytypes[currentIndex] = selectedTypeStr;
													node.Save();
												} 
											);
										}
									}

									if (newContainsKeyword != node.filterContainsKeywords[i]) {
										node.BeforeSave();
										node.filterContainsKeywords[i] = newContainsKeyword;
										node.FilterOutputPointsLabelChanged(i, node.filterContainsKeywords[i]);
									}
								}
							}

							if(messageAction != null) {
								using (new GUILayout.HorizontalScope()) {
									messageAction.Invoke();
								}
							}
						}

						// add contains keyword interface.
						if (GUILayout.Button("+")) {
							node.BeforeSave();
							var addingIndex = node.filterContainsKeywords.Count;
							var newKeyword = AssetBundleGraphSettings.DEFAULT_FILTER_KEYWORD;

							node.filterContainsKeywords.Add(newKeyword);
							node.filterContainsKeytypes.Add(AssetBundleGraphSettings.DEFAULT_FILTER_KEYTYPE);

							node.FilterOutputPointsAdded(addingIndex, AssetBundleGraphSettings.DEFAULT_FILTER_KEYWORD);
						}
					}

					break;
				}

			case AssetBundleGraphSettings.NodeKind.IMPORTSETTING_GUI : {
					EditorGUILayout.HelpBox("ImportSetting: applying settings.", MessageType.Info);
					UpdateNodeName(node);

					GUILayout.Space(10f);

					if (packageEditMode) EditorGUI.BeginDisabledGroup(true);
					/*
								importer node has no platform key. 
								platform key is contained by Unity's importer inspector itself.
							*/

					{
						using (new EditorGUILayout.VerticalScope(GUI.skin.box)) {
							var nodeId = node.nodeId;

							var samplingPath = FileController.PathCombine(AssetBundleGraphSettings.IMPORTER_SETTINGS_PLACE, nodeId);

							IntegratedGUIImportSetting.ValidateImportSample(samplingPath,
								(string noFolderFound) => {
									EditorGUILayout.LabelField("Sampling Asset", "No sample asset found. please Reload first.");
								},
								(string noFilesFound) => {
									EditorGUILayout.LabelField("Sampling Asset", "No sample asset found. please Reload first.");
								},
								(string samplingAssetPath) => {
									EditorGUILayout.LabelField("Sampling Asset Path", samplingAssetPath);
									if (GUILayout.Button("Setup Import Setting")) {
										var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(samplingAssetPath);
										Selection.activeObject = obj;
									}
									if (GUILayout.Button("Reset Import Setting")) {
										// delete all import setting files.
										FileController.RemakeDirectory(samplingPath);
										node.Save();
									}
								},
								(string tooManyFilesFoundMessage) => {
									if (GUILayout.Button("Reset Import Setting")) {
										// delete all import setting files.
										FileController.RemakeDirectory(samplingPath);
										node.Save();
									}
								}
							);
						}
					}

					if (packageEditMode) EditorGUI.EndDisabledGroup();
					UpdateDeleteSetting(node);

					break;
				}

			case AssetBundleGraphSettings.NodeKind.MODIFIER_GUI : {
					EditorGUILayout.HelpBox("Modifier: applying settings to Assets.", MessageType.Info);
					UpdateNodeName(node);

					GUILayout.Space(10f);

					if (packageEditMode) EditorGUI.BeginDisabledGroup(true);
					/*
								modifier node has no platform key. 
							*/

					{
						using (new EditorGUILayout.VerticalScope(GUI.skin.box, new GUILayoutOption[0])) {
							var nodeId = node.nodeId;

							var samplingPath = FileController.PathCombine(AssetBundleGraphSettings.MODIFIER_SETTINGS_PLACE, nodeId);

							IntegratedGUIModifier.ValidateModifierSample(samplingPath,
								(string noFolderFound) => {
									EditorGUILayout.LabelField("Sampling Asset", "no sampling asset found. please Reload first.");
								},
								(string noFilesFound) => {
									EditorGUILayout.LabelField("Sampling Asset", "no sampling asset found. please Reload first.");
								},
								(string samplingAssetPath) => {
									EditorGUILayout.LabelField("Sampling Asset Path", samplingAssetPath);
									if (GUILayout.Button("Setup Modifier Setting")) {
										var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(samplingAssetPath);
										Selection.activeObject = obj;
									}
									if (GUILayout.Button("Reset Modifier Setting")) {
										// delete all import setting files.
										FileController.RemakeDirectory(samplingPath);
										node.Save();
									}
								},
								(string tooManyFilesFoundMessage) => {
									EditorGUILayout.LabelField("Sampling Asset", "too many assets found. please delete files at:" + samplingPath);
								}
							);
						}
					}

					if (packageEditMode) EditorGUI.EndDisabledGroup();
					UpdateDeleteSetting(node);

					break;
				}

			case AssetBundleGraphSettings.NodeKind.GROUPING_GUI: {
					if (node.groupingKeyword == null) return;

					EditorGUILayout.HelpBox("Grouping: grouping files by one keyword.", MessageType.Info);
					UpdateNodeName(node);

					GUILayout.Space(10f);

					node.currentPlatform = UpdateCurrentPlatform(basePlatform);

					using (new EditorGUILayout.VerticalScope(GUI.skin.box)) {
						var newGroupingKeyword = EditorGUILayout.TextField(
							"Grouping Keyword",
							GraphStackController.ValueFromPlatformAndPackage(
								node.groupingKeyword.ReadonlyDict(), 
								node.currentPlatform
							).ToString()
						);
						IntegratedGUIGrouping.ValidateGroupingKeyword(
							newGroupingKeyword,
							() => {
								EditorGUILayout.HelpBox("groupingKeyword is empty.", MessageType.Error);
							},
							() => {
								EditorGUILayout.HelpBox("grouping keyword does not contain " + AssetBundleGraphSettings.KEYWORD_WILDCARD + " groupingKeyword:" + newGroupingKeyword, MessageType.Error);
							}
						);

						if (newGroupingKeyword != GraphStackController.ValueFromPlatformAndPackage(
							node.groupingKeyword.ReadonlyDict(), 
							node.currentPlatform
						).ToString()
						) {
							node.BeforeSave();
							node.groupingKeyword.Add(GraphStackController.Platform_Package_Key(node.currentPlatform), newGroupingKeyword);
							node.Save();
						}
					}

					UpdateDeleteSetting(node);
					break;
				}

			case AssetBundleGraphSettings.NodeKind.PREFABRICATOR_SCRIPT: {
					EditorGUILayout.HelpBox("Prefabricator: generate prefab by PrefabricatorBase extended script.", MessageType.Info);
					UpdateNodeName(node);

					EditorGUILayout.LabelField("Script Path", node.scriptPath);
					break;
				}

			case AssetBundleGraphSettings.NodeKind.PREFABRICATOR_GUI:{
					EditorGUILayout.HelpBox("Prefabricator: generate prefab by PrefabricatorBase extended script.", MessageType.Info);
					UpdateNodeName(node);

					using (new EditorGUILayout.VerticalScope(GUI.skin.box)) {
						var newScriptType = EditorGUILayout.TextField("Script Type", node.scriptType);

						/*
									check prefabricator script-type string.
								*/
						if (string.IsNullOrEmpty(newScriptType)) {
							EditorGUILayout.HelpBox("PrefabricatorBase extended class name is empty.", MessageType.Error);
						}

						var loadedType = System.Reflection.Assembly.GetExecutingAssembly().CreateInstance(newScriptType);

						if (loadedType == null) {
							EditorGUILayout.HelpBox("PrefabricatorBase extended class not found:" + newScriptType, MessageType.Error);
						}

						if (newScriptType != node.scriptType) {
							node.BeforeSave();
							node.scriptType = newScriptType;
							node.Save();
						}
					}
					break;
				}

			case AssetBundleGraphSettings.NodeKind.BUNDLIZER_GUI: {
					if (node.bundleNameTemplate == null) return;

					EditorGUILayout.HelpBox("Bundlizer: bundle resources to AssetBundle by template.", MessageType.Info);
					UpdateNodeName(node);

					GUILayout.Space(10f);

					node.currentPlatform = UpdateCurrentPlatform(basePlatform);

					using (new EditorGUILayout.VerticalScope(GUI.skin.box)) {
						var bundleNameTemplate = EditorGUILayout.TextField(
							"Bundle Name Template", 
							GraphStackController.ValueFromPlatformAndPackage(
								node.bundleNameTemplate.ReadonlyDict(), 
								node.currentPlatform
							).ToString()
						).ToLower();

						IntegratedGUIBundlizer.ValidateBundleNameTemplate(
							bundleNameTemplate,
							() => {
								EditorGUILayout.HelpBox("no Bundle Name Template set.", MessageType.Error);
							}
						);

						if (bundleNameTemplate != GraphStackController.ValueFromPlatformAndPackage(
							node.bundleNameTemplate.ReadonlyDict(), 
							node.currentPlatform
						).ToString()
						) {
							node.BeforeSave();
							node.bundleNameTemplate.Add(GraphStackController.Platform_Package_Key(node.currentPlatform), bundleNameTemplate);
							node.Save();
						}

						var isUseOutputResoruces = GraphStackController.ValueFromPlatformAndPackage(
							node.bundleUseOutput.ReadonlyDict(), 
							node.currentPlatform
						).ToString().ToLower();

						var useOrNot = false;
						switch (isUseOutputResoruces) {
						case "true": {
								useOrNot = true;
								break;
							}
						}

						var result = EditorGUILayout.ToggleLeft("Output Resources", useOrNot);

						if (result != useOrNot) {
							node.BeforeSave();

							if (result) node.BundlizerUseOutputResources();
							else node.BundlizerUnuseOutputResources(); 

							node.bundleUseOutput.Add(GraphStackController.Platform_Package_Key(node.currentPlatform), result.ToString());
							node.Save();
						}

						EditorGUILayout.HelpBox("If checked, you can use once-bundled resources to generate other bundles.", MessageType.Info);
					}

					UpdateDeleteSetting(node);

					break;
				}

			case AssetBundleGraphSettings.NodeKind.BUNDLEBUILDER_GUI: {
					if (node.enabledBundleOptions == null) return;

					EditorGUILayout.HelpBox("BundleBuilder: generate AssetBundle by AssetBundle name settings.", MessageType.Info);
					UpdateNodeName(node);

					GUILayout.Space(10f);

					node.currentPlatform = UpdateCurrentPlatform(basePlatform);

					using (new EditorGUILayout.VerticalScope(GUI.skin.box)) {
						var bundleOptions = GraphStackController.ValueFromPlatformAndPackage(
							node.enabledBundleOptions.ReadonlyDict(),
							node.currentPlatform
						);

						var plartform_pakcage_key = GraphStackController.Platform_Package_Key(node.currentPlatform);

						for (var i = 0; i < AssetBundleGraphSettings.DefaultBundleOptionSettings.Count; i++) {
							var enablablekey = AssetBundleGraphSettings.DefaultBundleOptionSettings[i];

							// contains keyword == enabled. if not, disabled.
							var isEnabled = bundleOptions.Contains(enablablekey);

							var result = EditorGUILayout.ToggleLeft(enablablekey, isEnabled);
							if (result != isEnabled) {
								node.BeforeSave();

								var resultsDict = node.enabledBundleOptions.ReadonlyDict();
								var resultList = new List<string>();
								if (resultsDict.ContainsKey(plartform_pakcage_key)) resultList = resultsDict[plartform_pakcage_key];

								if (result) {
									if (!resultList.Contains(enablablekey)) {
										var currentEnableds = new List<string>();
										if (resultsDict.ContainsKey(plartform_pakcage_key)) currentEnableds = resultsDict[plartform_pakcage_key];
										currentEnableds.Add(enablablekey);

										node.enabledBundleOptions.Add(
											GraphStackController.Platform_Package_Key(node.currentPlatform),
											currentEnableds
										);
									}
								}

								if (!result) {
									if (resultList.Contains(enablablekey)) {
										var currentEnableds = new List<string>();
										if (resultsDict.ContainsKey(plartform_pakcage_key)) currentEnableds = resultsDict[plartform_pakcage_key];
										currentEnableds.Remove(enablablekey);

										node.enabledBundleOptions.Add(
											GraphStackController.Platform_Package_Key(node.currentPlatform),
											currentEnableds
										);
									}
								}

								/*
											Cannot use options DisableWriteTypeTree and IgnoreTypeTreeChanges at the same time.
										*/
								if (enablablekey == "Disable Write TypeTree" && result &&
									node.enabledBundleOptions.ReadonlyDict()[GraphStackController.Platform_Package_Key(node.currentPlatform)].Contains("Ignore TypeTree Changes")) {

									var newEnableds = node.enabledBundleOptions.ReadonlyDict()[GraphStackController.Platform_Package_Key(node.currentPlatform)];
									newEnableds.Remove("Ignore TypeTree Changes");

									node.enabledBundleOptions.Add(
										GraphStackController.Platform_Package_Key(node.currentPlatform),
										newEnableds
									);
								}

								if (enablablekey == "Ignore TypeTree Changes" && result &&
									node.enabledBundleOptions.ReadonlyDict()[GraphStackController.Platform_Package_Key(node.currentPlatform)].Contains("Disable Write TypeTree")) {

									var newEnableds = node.enabledBundleOptions.ReadonlyDict()[GraphStackController.Platform_Package_Key(node.currentPlatform)];
									newEnableds.Remove("Disable Write TypeTree");

									node.enabledBundleOptions.Add(
										GraphStackController.Platform_Package_Key(node.currentPlatform),
										newEnableds
									);
								}

								node.Save();
								return;
							}
						}
					}

					UpdateDeleteSetting(node);

					break;
				}

			case AssetBundleGraphSettings.NodeKind.EXPORTER_GUI: {
					if (node.exportPath == null) return;

					EditorGUILayout.HelpBox("Exporter: export files to path.", MessageType.Info);
					UpdateNodeName(node);

					GUILayout.Space(10f);

					node.currentPlatform = UpdateCurrentPlatform(basePlatform);

					using (new EditorGUILayout.VerticalScope(GUI.skin.box)) {
						EditorGUILayout.LabelField("Export Path:");
						var newExportPath = EditorGUILayout.TextField(
							GraphStackController.ProjectName(), 
							GraphStackController.ValueFromPlatformAndPackage(
								node.exportPath.ReadonlyDict(), 
								node.currentPlatform
							).ToString()
						);

						var exporterrNodePath = GraphStackController.WithProjectPath(newExportPath);
						IntegratedGUIExporter.ValidateExportPath(
							newExportPath,
							exporterrNodePath,
							() => {
								EditorGUILayout.HelpBox("export path is empty.", MessageType.Error);
							},
							() => {
								EditorGUILayout.HelpBox("directory not found:" + exporterrNodePath, MessageType.Error);
							}
						);

						if (newExportPath != GraphStackController.ValueFromPlatformAndPackage(
							node.exportPath.ReadonlyDict(),
							node.currentPlatform
						).ToString()
						) {
							node.BeforeSave();
							node.exportPath.Add(GraphStackController.Platform_Package_Key(node.currentPlatform), newExportPath);
							node.Save();
						}
					}

					UpdateDeleteSetting(node);

					break;
				}

			default: {
					Debug.LogError("failed to match:" + node.kind);
					break;
				}
			}

			var errors = currentTarget.errors;
			if (errors != null && errors.Any()) {
				foreach (var error in errors) {
					EditorGUILayout.HelpBox(error, MessageType.Error);
				}
			}
			using (new EditorGUILayout.VerticalScope()) {
				foreach(Action a in messageActions) {
					a.Invoke();
				}
			}
		}

		private void UpdateNodeName (Node node) {
			var newName = EditorGUILayout.TextField("Node Name", node.name);

			if( Node.allNodeNames != null ) {
				var overlapping = Node.allNodeNames.GroupBy(x => x)
					.Where(group => group.Count() > 1)
					.Select(group => group.Key);
				if (overlapping.Any() && overlapping.Contains(newName)) {
					EditorGUILayout.HelpBox("node name is overlapping:" + newName, MessageType.Error);
				}
			}

			if (newName != node.name) {
				node.BeforeSave();
				node.name = newName;
				node.UpdateNodeRect();
				node.Save();
			}
		}

		private string UpdateCurrentPlatform (string basePlatfrom) {
			var newPlatform = basePlatfrom;

			EditorGUI.BeginChangeCheck();
			using (new EditorGUILayout.HorizontalScope()) {
				var choosenIndex = -1;
				for (var i = 0; i < Node.platformButtonTextures.Length; i++) {
					var onOffBefore = Node.platformStrings[i] == basePlatfrom;
					var onOffAfter = onOffBefore;

					// index 0 is Default.
					switch (i) {
					case 0: {
							onOffAfter = GUILayout.Toggle(onOffBefore, "Default", "toolbarbutton");
							break;
						}
					default: {
							// for each platform texture.
							var platformButtonTexture = Node.platformButtonTextures[i];
							onOffAfter = GUILayout.Toggle(onOffBefore, platformButtonTexture, "toolbarbutton");
							break;
						}
					}

					if (onOffBefore != onOffAfter) {
						choosenIndex = i;
						break;
					}
				}

				if (EditorGUI.EndChangeCheck()) {
					newPlatform = Node.platformStrings[choosenIndex];
				}
			}

			if (newPlatform != basePlatfrom) GUI.FocusControl(string.Empty);
			return newPlatform;
		}


		private void UpdateDeleteSetting (Node currentNode) {
			var currentNodePlatformPackageKey = GraphStackController.Platform_Package_Key(currentNode.currentPlatform);

			if (currentNodePlatformPackageKey == AssetBundleGraphSettings.PLATFORM_DEFAULT_NAME) return;

			using (new EditorGUILayout.HorizontalScope()) {
				GUILayout.FlexibleSpace();
				if (GUILayout.Button("Use Default Setting", GUILayout.Width(150))) {
					currentNode.BeforeSave();
					currentNode.DeleteCurrentPackagePlatformKey(currentNodePlatformPackageKey);
					GUI.FocusControl(string.Empty);
					currentNode.Save();
				}
			}
		}
	}
}
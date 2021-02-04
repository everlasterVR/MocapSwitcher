//#define SHOW_DEBUG
using MVR.FileManagementSecure;
using SimpleJSON;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MocapSwitcher
{
    public class Main : MVRScript
    {
        private const string pluginName = "MocapSwitcher";
        private const string pluginVersion = "1.0.1";

        private Atom person;
        private Atom coreControl;
        private List<string> animationStorableIds;
        private string pluginDataDir = SuperController.singleton.savesDir + @"PluginData\everlaster\MocapSwitcher\";
        private string mocapDir = SuperController.singleton.savesDir + @"mocap\";
        private string tmpSceneFilePath;
        private string lastBrowseDir;
        private const string saveExt = "json";

        public override void Init()
        {
            try
            {
                if(containingAtom.type != "Person")
                {
                    Log.Error($"Plugin is for use with 'Person' atom, not '{containingAtom.type}'");
                    return;
                }

                FileManagerSecure.CreateDirectory(pluginDataDir);
                FileManagerSecure.CreateDirectory(mocapDir);
                tmpSceneFilePath = pluginDataDir + "tmp.json";
                lastBrowseDir = mocapDir;

                person = containingAtom;
                coreControl = SuperController.singleton.GetAtomByUid("CoreControl");
                SetAnimationStorableIds();

                InitPluginUILeft();
            }
            catch(Exception e)
            {
                SuperController.LogError("Exception caught: " + e);
            }
        }

        private void SetAnimationStorableIds()
        {
            animationStorableIds = new List<string>();
            foreach(string id in person.GetStorableIDs())
            {
                if(id.EndsWith("Animation"))
                {
                    animationStorableIds.Add(id);
                }
            }
        }

        private string CreateDirectory(string path)
        {
            FileManagerSecure.CreateDirectory(path);
            return path;
        }

        private void InitPluginUILeft()
        {
            JSONStorableString titleUIText = new JSONStorableString("titleText", "");
            UIDynamicTextField titleUITextField = CreateTextField(titleUIText);
            titleUITextField.UItext.fontSize = 36;
            titleUITextField.height = 100;
            titleUIText.SetVal($"{nameof(Main)}\n<size=28>v{pluginVersion}</size>");

            CreateLoadMocapButton();
            CreateSaveMocapButton();
        }

        // based on FloatMultiParamRandomizer v1.0.7 (C) HSThrowaway5
        private void CreateLoadMocapButton()
        {
            JSONStorableString jsonString = new JSONStorableString("LoadButtonInfo", "");
            UIDynamicTextField textField = CreateTextField(jsonString, false);
            jsonString.val = "\nLoading a mocap replaces any existing animation for this person atom.";
            textField.UItext.fontSize = 28;
            textField.UItext.alignment = TextAnchor.MiddleLeft;
            textField.height = 100;

            UIDynamicButton btn = CreateButton("Load mocap");
            btn.button.onClick.AddListener(() =>
            {
                SuperController.singleton.NormalizeMediaPath(lastBrowseDir); // Sets lastMediaDir if path it exists
                SuperController.singleton.GetMediaPathDialog(HandleLoadMocap, saveExt);
            });
        }

        // based on FloatMultiParamRandomizer v1.0.7 (C) HSThrowaway5
        private void CreateSaveMocapButton()
        {
            JSONStorableString jsonString = new JSONStorableString("SaveButtonInfo", "");
            UIDynamicTextField textField = CreateTextField(jsonString, false);
            jsonString.val = "\nPosition the scene animation to the beginning before exporting.";
            textField.height = 100;
            textField.UItext.alignment = TextAnchor.MiddleLeft;
            textField.UItext.fontSize = 28;

            UIDynamicButton btn = CreateButton("Export mocap");
            btn.button.onClick.AddListener(() =>
            {
                SuperController.singleton.NormalizeMediaPath(lastBrowseDir); // Sets lastMediaDir if path it exists
                SuperController.singleton.GetMediaPathDialog(HandleSaveMocap, saveExt);

                // Update the browser to be a Save browser
                uFileBrowser.FileBrowser browser = SuperController.singleton.mediaFileBrowserUI;
                browser.SetTextEntry(true);
                browser.fileEntryField.text = String.Format("{0}.{1}", ((int) (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds).ToString(), saveExt);
                browser.ActivateFileNameField();
            });
        }

        // based on FloatMultiParamRandomizer v1.0.7 (C) HSThrowaway5
        private void HandleLoadMocap(string path)
        {
            if(string.IsNullOrEmpty(path))
            {
                return;
            }
            lastBrowseDir = path.Substring(0, path.LastIndexOfAny(new char[] { '/', '\\' })) + @"\";
            LoadSaveJson(LoadJSON(path));
        }

        private void LoadSaveJson(JSONNode mocap)
        {
            JSONClass scene = SuperController.singleton.GetSaveJSON();
            ModifyAndTmpSaveScene(scene, mocap);
            SuperController.singleton.Load(tmpSceneFilePath);
        }

        private void ModifyAndTmpSaveScene(JSONClass scene, JSONNode mocap)
        {
            foreach(JSONNode atomJson in scene["atoms"].AsArray)
            {
                if(string.Equals(atomJson["id"], "CoreControl"))
                {
                    MergeMotionAnimationMasterData(atomJson, mocap["CoreControl"]);
                }

                if(string.Equals(atomJson["id"], person.uid))
                {
                    ClearPersonPoseAndAnimationData(atomJson);
                    AddPersonPoseAndAnimationData(atomJson, mocap["Person"]);
                }
            }

            SaveJSON(scene, tmpSceneFilePath);
        }

        private void MergeMotionAnimationMasterData(JSONNode atomJson, JSONNode mocapJson)
        {
            try
            {
                JSONNode data = FindMotionAnimationMasterData(mocapJson);
                foreach(JSONNode storable in atomJson["storables"].AsArray)
                {
                    if(string.Equals(storable["id"], "MotionAnimationMaster"))
                    {
                        storable["autoPlay"] = data["autoPlay"];
                        storable["loop"] = data["loop"];
                        storable["playbackCounter"] = "0";
                        storable["startTimestep"] = "0";
                        storable["stopTimestep"] = data["stopTimestep"];
                        storable["loopbackTime"] = data["loopbackTime"];
                        storable["playbackSpeed"] = data["playbackSpeed"];
                        storable["recordedLength"] = data["recordedLength"];
                    }
                }
            }
            catch(Exception e)
            {
                Log.Error("Exception caught: " + e);
            }
            return;
        }

        private JSONNode FindMotionAnimationMasterData(JSONNode coreControl)
        {
            foreach(JSONNode storable in coreControl["storables"].AsArray)
            {
                if(string.Equals(storable["id"], "MotionAnimationMaster"))
                {
                    return storable;
                }
            }
            throw new Exception("Selected mocap file does not contain MotionAnimationMaster data!");
        }

        private void ClearPersonPoseAndAnimationData(JSONNode atomJson)
        {
            foreach(string id in animationStorableIds)
            {
                string controlStorableId = animationStorableIdToControlId(id);
                JSONNode controlNode = FindStorableFromPerson(atomJson, controlStorableId);
                atomJson["storables"].Remove(controlNode);

                try
                {
                    JSONNode animationNode = FindStorableFromPerson(atomJson, id);
                    atomJson["storables"].Remove(animationNode);
                }
                catch(Exception)
                {
#if SHOW_DEBUG
                    Log.Message($"Cannot clear {id} data, not found in scene");
#endif
                }
            }
        }

        private void AddPersonPoseAndAnimationData(JSONNode atomJson, JSONNode mocapJson)
        {
            foreach(string id in animationStorableIds)
            {
                string controlStorableId = animationStorableIdToControlId(id);
                JSONNode controlNode = FindStorableFromPerson(mocapJson, controlStorableId);
                atomJson["storables"].Add(controlNode);

                try
                {
                    JSONNode animationNode = FindStorableFromPerson(mocapJson, id);
                    atomJson["storables"].Add(animationNode);
                }
                catch(Exception)
                {
#if SHOW_DEBUG
                    Log.Message($"Cannot add {id} data, not found in mocap JSON");
#endif
                }
            }
            return;
        }

        private JSONNode FindStorableFromPerson(JSONNode personData, string id)
        {
            foreach(JSONNode storable in personData["storables"].AsArray)
            {
                if(string.Equals(storable["id"], id))
                {
                    return storable;
                }
            }
            throw new Exception();
        }

        // based on FloatMultiParamRandomizer v1.0.7 (C) HSThrowaway5
        private void HandleSaveMocap(string path)
        {
            if(String.IsNullOrEmpty(path))
            {
                return;
            }
            lastBrowseDir = path.Substring(0, path.LastIndexOfAny(new char[] { '/', '\\' })) + @"\";

            if(!path.ToLower().EndsWith(saveExt.ToLower()))
            {
                path += "." + saveExt;
            }
            JSONClass saveJson = GetSaveJson();
            this.SaveJSON(saveJson, path);
            SuperController.singleton.DoSaveScreenshot(path);
        }

        private JSONClass GetSaveJson()
        {
            JSONClass json = new JSONClass();
            json["CoreControl"] = GetCoreControlJson();
            json["Person"] = GetPersonJson();
            return json;
        }

        private JSONClass GetCoreControlJson()
        {
            JSONClass json = new JSONClass();
            json["storables"] = new JSONArray();

            JSONStorable motionAnimationMaster = coreControl.GetStorableByID("MotionAnimationMaster");
            JSONClass storable = motionAnimationMaster.GetJSON();
            storable["triggers"] = new JSONArray(); // prevents triggers from scene from carrying over to mocap json
            json["storables"].Add(storable);
            return json;
        }

        private JSONClass GetPersonJson()
        {
            JSONClass json = new JSONClass();
            json["storables"] = new JSONArray();

            JSONStorable control = person.GetStorableByID("control");
            json["storables"].Add(control.GetJSON());

            foreach(string id in animationStorableIds)
            {
                JSONClass controlJson = person.GetStorableByID(animationStorableIdToControlId(id)).GetJSON();
                JSONClass animationJson = person.GetStorableByID(id).GetJSON();
                if(animationJson["steps"].AsArray.Count > 0)
                {
                    json["storables"].Add(animationJson);
                    //JSONClass animationStepJson = (animationJson["steps"].AsArray)[0].AsObject;
                    //modifyControlJsonForSave(controlJson, animationStepJson);
                }
                //else
                //{
                //	modifyControlJsonForSave(controlJson, null);
                //}
                json["storables"].Add(controlJson);
            }

            return json;
        }

        // Causes physics to go nuts when the scene animation is returned to beginning
        //void modifyControlJsonForSave(JSONClass controlJson, JSONClass animationStepJson)
        //{
        //	controlJson.Remove("canGrabPosition");
        //	controlJson.Remove("canGrabRotation");

        //	if(animationStepJson != null)
        //	{
        //		controlJson["localPosition"] = animationStepJson["position"];
        //		controlJson["localRotation"] = animationStepJson["rotation"];
        //		// w key is not saved in control node's rotation - only xyz
        //		controlJson["localRotation"].Remove("w");
        //	}
        //}

        // some animation storables have "Control" in them, others don't
        private string animationStorableIdToControlId(string id)
        {
            switch(id)
            {
                case "eyeTargetControlAnimation":
                case "lNippleControlAnimation":
                case "rNippleControlAnimation":
                    return id.Replace("Animation", "");

                default:
                    return id.Replace("Animation", "Control");
            }
        }
    }
}

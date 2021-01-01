using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using SimpleJSON;

namespace everlaster {
    public class MocapSwitcher : MVRScript {
        const string pluginName = "MocapSwitcher";
        const string pluginVersion = "1.0";
        const string saveExt = "json";

        private Atom _person;
        private Atom _coreControl;
        private List<string> _animationStorableIds;
        protected string _tmpFilePath;
        protected string _lastBrowseDir;

        // TODO checkbox: configurable autoplay

        public override void Init() {
            try {
                if(containingAtom.type != "Person")
                {
                    SuperController.LogError($"Requires atom of type person, instead selected: {containingAtom.type}");
                    return;
                }

                _person = containingAtom;
                SetAnimationStorableIds();

                _coreControl = SuperController.singleton.GetAtomByUid("CoreControl");
                _tmpFilePath = CreateDirectory("Custom\\" + @"Scripts\everlaster\tmp\") + "_MocapSwitcher.json";
                _lastBrowseDir = CreateDirectory(SuperController.singleton.savesDir + @"mocap\");

                CreateVersionInfoField();
                CreateLoadMocapButton();
                CreateSaveMocapButton();
            }
            catch (Exception e) {
                SuperController.LogError("Exception caught: " + e);
            }
        }

        // Start is called once before Update or FixedUpdate is called and after Init()
        //void Start() {
        //	try {
        //		// put code in here
        //	}
        //	catch (Exception e) {
        //		SuperController.LogError("Exception caught: " + e);
        //	}
        //}

        // Update is called with each rendered frame by Unity
        //void Update() {
        //	try {
        //		// put code in here
        //	}
        //	catch (Exception e) {
        //		SuperController.LogError("Exception caught: " + e);
        //	}
        //}

        // FixedUpdate is called with each physics simulation frame by Unity
        //void FixedUpdate() {
        //	try {
        //		// put code in here
        //	}
        //	catch (Exception e) {
        //		SuperController.LogError("Exception caught: " + e);
        //	}
        //}

        // OnDestroy is where you should put any cleanup
        // if you registered objects to supercontroller or atom, you should unregister them here
        //void OnDestroy() {
        //}

        void SetAnimationStorableIds()
        {
            _animationStorableIds = new List<string>();
            foreach(string id in _person.GetStorableIDs())
            {
                if(id.EndsWith("Animation"))
                {
                    _animationStorableIds.Add(id);
                }
            }
        }

        // from FloatMultiParamRandomizer v1.0.7 (C) HSThrowaway5
        string CreateDirectory(string path)
        {
            JSONNode node = new JSONNode();
            if(!path.EndsWith("/") && !path.EndsWith(@"\"))
            {
                path += @"\";
            }

            try
            {
                node.SaveToFile(path);
            }
            catch (Exception e)
            {
            }
            return path;
        }

        void CreateVersionInfoField()
        {
            JSONStorableString jsonString = new JSONStorableString("VersionInfo", "");
            UIDynamicTextField textField = CreateTextField(jsonString, false);
            jsonString.val = $"{pluginName} {pluginVersion}";
            textField.UItext.fontSize = 40;
            textField.height = 100;
        }

        // based on FloatMultiParamRandomizer v1.0.7 (C) HSThrowaway5
        void CreateLoadMocapButton()
        {
            JSONStorableString jsonString = new JSONStorableString("LoadButtonInfo", "");
            UIDynamicTextField textField = CreateTextField(jsonString, false);
            jsonString.val = "\nLoading a mocap replaces any existing animation for this person atom.";
            textField.UItext.fontSize = 28;
            textField.UItext.alignment = TextAnchor.MiddleLeft;
            textField.height = 100;

            var btn = CreateButton("Load mocap");
            btn.button.onClick.AddListener(() =>
            {
                SuperController.singleton.NormalizeMediaPath(_lastBrowseDir); // Sets the path if it exists
                SuperController.singleton.GetMediaPathDialog(HandleLoadMocap, saveExt);
            });
        }

        // based on FloatMultiParamRandomizer v1.0.7 (C) HSThrowaway5
        void HandleLoadMocap(string path)
        {
            if(String.IsNullOrEmpty(path))
            {
                return;
            }
            _lastBrowseDir = path.Substring(0, path.LastIndexOfAny(new char[] { '/', '\\' })) + @"\";
            LoadSaveJson(this.LoadJSON(path));
        }

        void LoadSaveJson(JSONNode mocap)
        {
            JSONClass scene = SuperController.singleton.GetSaveJSON();
            ModifyAndTmpSaveScene(scene, mocap);
            SuperController.singleton.Load(_tmpFilePath);
        }

        void ModifyAndTmpSaveScene(JSONClass scene, JSONNode mocap)
        {
            foreach(JSONNode atomJson in scene["atoms"].AsArray)
            {
                if(string.Equals(atomJson["id"], "CoreControl"))
                {
                    MergeMotionAnimationMasterData(atomJson, mocap["CoreControl"]);
                }

                if(string.Equals(atomJson["id"], _person.uid))
                {
                    ClearPersonPoseAndAnimationData(atomJson);
                    AddPersonPoseAndAnimationData(atomJson, mocap["Person"]);
                }
            }

            this.SaveJSON(scene, _tmpFilePath);
        }

        void MergeMotionAnimationMasterData(JSONNode atomJson, JSONNode mocapJson)
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
                SuperController.LogError("Exception caught: " + e);
            }
            return;
        }

        JSONNode FindMotionAnimationMasterData(JSONNode coreControl)
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

        void ClearPersonPoseAndAnimationData(JSONNode atomJson)
        {
            foreach(string id in _animationStorableIds)
            {
                string controlStorableId = animationStorableIdToControlId(id);
                JSONNode controlNode = FindStorableFromPerson(atomJson, controlStorableId);
                atomJson["storables"].Remove(controlNode);

                try
                {
                    JSONNode animationNode = FindStorableFromPerson(atomJson, id);
                    atomJson["storables"].Remove(animationNode);
                }
                catch(Exception e)
                {
                    //SuperController.LogMessage($"DEBUG :: cannot clear {id} data, not found in scene");
                }
            }
        }

        void AddPersonPoseAndAnimationData(JSONNode atomJson, JSONNode mocapJson)
        {
            foreach(string id in _animationStorableIds)
            {
                string controlStorableId = animationStorableIdToControlId(id);
                JSONNode controlNode = FindStorableFromPerson(mocapJson, controlStorableId);
                atomJson["storables"].Add(controlNode);

                try
                {
                    JSONNode animationNode = FindStorableFromPerson(mocapJson, id);
                    atomJson["storables"].Add(animationNode);
                }
                catch(Exception e)
                {
                    //SuperController.LogMessage($"DEBUG :: cannot add {id} data, not found in mocap JSON");
                }
            }
            return;
        }

        JSONNode FindStorableFromPerson(JSONNode personData, string id)
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
        void CreateSaveMocapButton()
        {
            JSONStorableString jsonString = new JSONStorableString("SaveButtonInfo", "");
            UIDynamicTextField textField = CreateTextField(jsonString, false);
            jsonString.val = "\nPosition the scene animation to the beginning before exporting.";
            textField.height = 100;
            textField.UItext.alignment = TextAnchor.MiddleLeft;
            textField.UItext.fontSize = 28;

            var btn = CreateButton("Export mocap");
            btn.button.onClick.AddListener(() =>
            {
                SuperController.singleton.NormalizeMediaPath(_lastBrowseDir); // Sets the path if it exists
                SuperController.singleton.GetMediaPathDialog(HandleSaveMocap, saveExt);

                // Update the browser to be a Save browser
                uFileBrowser.FileBrowser browser = SuperController.singleton.mediaFileBrowserUI;
                browser.SetTextEntry(true);
                browser.fileEntryField.text = String.Format("{0}.{1}", ((int) (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds).ToString(), saveExt);
                browser.ActivateFileNameField();
            });
        }

        // based on FloatMultiParamRandomizer v1.0.7 (C) HSThrowaway5
        void HandleSaveMocap(string path)
        {
            if(String.IsNullOrEmpty(path))
            {
                return;
            }
            _lastBrowseDir = path.Substring(0, path.LastIndexOfAny(new char[] { '/', '\\' })) + @"\";

            if(!path.ToLower().EndsWith(saveExt.ToLower()))
            {
                path += "." + saveExt;
            }
            JSONClass saveJson = GetSaveJson();
            this.SaveJSON(saveJson, path);
            SuperController.singleton.DoSaveScreenshot(path);
        }

        JSONClass GetSaveJson()
        {
            JSONClass json = new JSONClass();
            json["CoreControl"] = GetCoreControlJson();
            json["Person"] = GetPersonJson();
            return json;
        }

        JSONClass GetCoreControlJson()
        {
            JSONClass json = new JSONClass();
            json["storables"] = new JSONArray();

            JSONStorable motionAnimationMaster = _coreControl.GetStorableByID("MotionAnimationMaster");
            JSONClass storable = motionAnimationMaster.GetJSON();
            storable["triggers"] = new JSONArray(); // prevents triggers from scene from carrying over to mocap json
            json["storables"].Add(storable);
            return json;
        }

        JSONClass GetPersonJson()
        {
            JSONClass json = new JSONClass();
            json["storables"] = new JSONArray();

            JSONStorable control = _person.GetStorableByID("control");
            json["storables"].Add(control.GetJSON());

            foreach(string id in _animationStorableIds)
            {
                JSONClass controlJson = _person.GetStorableByID(animationStorableIdToControlId(id)).GetJSON();
                JSONClass animationJson = _person.GetStorableByID(id).GetJSON();
                if(animationJson["steps"].AsArray.Count > 0)
                {
                    json["storables"].Add(animationJson);
                    JSONClass animationStepJson = (animationJson["steps"].AsArray)[0].AsObject;
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
        string animationStorableIdToControlId(string id)
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
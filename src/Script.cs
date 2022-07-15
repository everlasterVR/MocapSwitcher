//#define SHOW_DEBUG
using MVR.FileManagementSecure;
using SimpleJSON;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MocapSwitcher
{
    public class Script : MVRScript
    {
        private const string pluginName = "MocapSwitcher";
        private const string pluginVersion = "v0.0.0";

        private Atom coreControl;
        private List<string> animationStorableIds;
        private string mocapDir = SuperController.singleton.savesDir + @"mocap\";
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

                FileManagerSecure.CreateDirectory(mocapDir);
                lastBrowseDir = mocapDir;

                coreControl = SuperController.singleton.GetAtomByUid("CoreControl");
                SetAnimationStorableIds();

                InitPluginUILeft();
                InitPluginUIRight();
            }
            catch(Exception e)
            {
                Log.Error($"{e}");
            }
        }

        private void SetAnimationStorableIds()
        {
            animationStorableIds = new List<string>();
            foreach(string id in containingAtom.GetStorableIDs())
            {
                if(id.EndsWith("Animation"))
                {
                    animationStorableIds.Add(id);
                }
            }
        }

        private void InitPluginUILeft()
        {
            JSONStorableString titleUIText = new JSONStorableString("titleText", "");
            UIDynamicTextField titleUITextField = CreateTextField(titleUIText);
            titleUITextField.UItext.fontSize = 36;
            titleUITextField.height = 100;
            titleUIText.SetVal($"{nameof(MocapSwitcher)}\n<size=28>v{pluginVersion}</size>");

            CreateLoadMocapButton();
            CreateSaveMocapButton();
        }

        // based on FloatMultiParamRandomizer v1.0.7 (C) HSThrowaway5
        private void CreateLoadMocapButton()
        {
            UIDynamicButton btn = CreateButton("Load mocap");
            btn.height = 100f;
            btn.button.onClick.AddListener(() =>
            {
                SuperController.singleton.NormalizeMediaPath(lastBrowseDir); // Sets lastMediaDir if path it exists
                SuperController.singleton.GetMediaPathDialog(HandleLoadMocap, saveExt);
            });
        }

        // based on FloatMultiParamRandomizer v1.0.7 (C) HSThrowaway5
        private void CreateSaveMocapButton()
        {
            UIDynamicButton btn = CreateButton("Save mocap");
            btn.height = 100f;
            btn.button.onClick.AddListener(() =>
            {
                SuperController.singleton.motionAnimationMaster.ResetAnimation();
                SuperController.singleton.NormalizeMediaPath(lastBrowseDir); // Sets lastMediaDir if path exists
                SuperController.singleton.GetMediaPathDialog(HandleSaveMocap, saveExt);

                // Update the browser to be a Save browser
                uFileBrowser.FileBrowser browser = SuperController.singleton.mediaFileBrowserUI;
                browser.SetTextEntry(true);
                browser.fileEntryField.text = string.Format("{0}.{1}", ((int) (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds).ToString(), saveExt);
                browser.ActivateFileNameField();
            });
        }

        private void InitPluginUIRight()
        {
            CreateNewSpacer(100, true);

            JSONStorableString jsonString = new JSONStorableString("LoadButtonInfo", "");
            UIDynamicTextField textField = CreateTextField(jsonString, true);
            jsonString.val = "Loading replaces any existing animation for this person atom.\n\n" +
                "Saving resets playback to ensure the starting pose is correct in the saved mocap.";
            textField.UItext.fontSize = 28;
            textField.UItext.alignment = TextAnchor.MiddleLeft;
            textField.height = 215;
        }

        void CreateNewSpacer(float height, bool rightSide = false)
        {
            UIDynamic spacer = CreateSpacer(rightSide);
            spacer.height = height;
        }

        private void HandleLoadMocap(string path)
        {
            if(string.IsNullOrEmpty(path))
            {
                return;
            }
            lastBrowseDir = path.Substring(0, path.LastIndexOfAny(new char[] { '/', '\\' })) + @"\";
            JSONClass mocap = LoadJSON(path).AsObject;

            containingAtom.collisionEnabled = false;
            ClearPersonPoseAndAnimationData();
            AddPersonPoseAndAnimationData(mocap["Person"].AsObject);
            MergeMotionAnimationMasterData(mocap["CoreControl"].AsObject);
            StartCoroutine(WaitEnableCollision());
        }

        private IEnumerator WaitEnableCollision()
        {
            yield return new WaitForSeconds(0.5f);
            containingAtom.collisionEnabled = true;
        }

        private void MergeMotionAnimationMasterData(JSONClass coreControlJson)
        {
            JSONClass motionAnimationMasterJson = FindMotionAnimationMasterData(coreControlJson);
            try
            {
                motionAnimationMasterJson["playbackCounter"] = "0";
                motionAnimationMasterJson["startTimeStemp"] = "0";
                SuperController.singleton.motionAnimationMaster.RestoreFromJSON(motionAnimationMasterJson);
            }
            catch(Exception e)
            {
                Log.Error($"{e}");
            }
            return;
        }

        private void ClearPersonPoseAndAnimationData()
        {
            foreach(string storableId in animationStorableIds)
            {
                string controlStorableId = animationStorableIdToControlId(storableId);
                JSONStorable controlStorable = containingAtom.GetStorableByID(controlStorableId);
                controlStorable.RestoreAllFromDefaults();

                try
                {
                    JSONStorable storable = containingAtom.GetStorableByID(storableId);
                    storable.RestoreAllFromDefaults();
                }
                catch(Exception e)
                {
                    //Log.Message($"{e}");
                }
            }
        }

        private void AddPersonPoseAndAnimationData(JSONClass personJson)
        {
            foreach(string storableId in animationStorableIds)
            {
                string controlStorableId = animationStorableIdToControlId(storableId);
                JSONStorable controlStorable = containingAtom.GetStorableByID(controlStorableId);
                JSONClass controlJson = FindStorableFromPerson(personJson, controlStorableId);
                controlStorable.RestoreFromJSON(controlJson);

                JSONStorable storable = containingAtom.GetStorableByID(storableId);
                try
                {
                    JSONClass json = FindStorableFromPerson(personJson, storableId);
                    storable.RestoreFromJSON(json);
                }
                catch(Exception e)
                {
                    //Log.Message($"{e}");
                }
            }
        }

        private JSONClass FindMotionAnimationMasterData(JSONClass coreControlJson)
        {
            foreach(JSONNode storable in coreControlJson["storables"].AsArray)
            {
                if(string.Equals(storable["id"], "MotionAnimationMaster"))
                {
                    return storable.AsObject;
                }
            }
            throw new Exception("Selected mocap file does not contain MotionAnimationMaster data!");
        }

        private JSONClass FindStorableFromPerson(JSONNode personData, string id)
        {
            foreach(JSONNode storable in personData["storables"].AsArray)
            {
                if(string.Equals(storable["id"], id))
                {
                    return storable.AsObject;
                }
            }
            throw new Exception($"Selected mocap file does not contain storable {id}");
        }

        // based on FloatMultiParamRandomizer v1.0.7 (C) HSThrowaway5
        private void HandleSaveMocap(string path)
        {
            if(string.IsNullOrEmpty(path))
            {
                return;
            }
            lastBrowseDir = path.Substring(0, path.LastIndexOfAny(new char[] { '/', '\\' })) + @"\";

            if(!path.ToLower().EndsWith(saveExt.ToLower()))
            {
                path += "." + saveExt;
            }

            SaveJSON(
                new JSONClass
                {
                    ["CoreControl"] = GetCoreControlJson(),
                    ["Person"] = GetPersonJson()
                },
                path
            );
            SuperController.singleton.DoSaveScreenshot(path);
        }

        private JSONClass GetCoreControlJson()
        {
            JSONClass storable = SuperController.singleton.motionAnimationMaster.GetJSON();
            storable["triggers"] = new JSONArray(); // prevents triggers from scene from carrying over to mocap json

            return new JSONClass
            {
                ["storables"] = new JSONArray { storable }
            };
        }

        private JSONClass GetPersonJson()
        {
            JSONClass json = new JSONClass
            {
                ["storables"] = new JSONArray()
            };

            JSONStorable control = containingAtom.GetStorableByID("control");
            json["storables"].Add(control.GetJSON());

            foreach(string id in animationStorableIds)
            {
                JSONClass controlJson = containingAtom.GetStorableByID(animationStorableIdToControlId(id)).GetJSON();
                JSONClass animationJson = containingAtom.GetStorableByID(id).GetJSON();
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

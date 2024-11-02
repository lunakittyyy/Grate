﻿using BepInEx;
using System;
using UnityEngine;
using Grate.GUI;
using Grate.Tools;
using Grate.Extensions;
using BepInEx.Configuration;
using System.IO;
using Grate.Modules;
using System.Reflection;
using Grate.Gestures;
using Grate.Networking;
using GorillaLocomotion;
using UnityEngine.UI;
using HarmonyLib;
using System.Collections;
using GorillaNetworking;
using Photon.Pun;

namespace Grate
{
    [BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]

    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance;
        public static bool initialized, inRoom;
        bool pluginEnabled = false;
        public static AssetBundle assetBundle;
        public static AssetBundle portalAssetBundle;
        public static MenuController menuController;
        public static GameObject monkeMenuPrefab;
        public static ConfigFile configFile;
        public static bool IsSteam { get; protected set; }
        public static bool DebugMode { get; protected set; } = false;
        GestureTracker gt;
        NetworkPropertyHandler nph;


        public void Setup()
        {
            if (menuController || !pluginEnabled || !inRoom) return;
            Logging.Debug("Menu:", menuController, "Plugin Enabled:", pluginEnabled, "InRoom:", inRoom);
            try
            {
                gt = this.gameObject.GetOrAddComponent<GestureTracker>();
                nph = this.gameObject.GetOrAddComponent<NetworkPropertyHandler>();  
                menuController = Instantiate(monkeMenuPrefab).AddComponent<MenuController>();
            }
            catch (Exception e)
            {
                Logging.Exception(e);
            }
        }

        public void Cleanup()
        {
            try
            {
                Logging.Debug("Cleaning up");
                menuController?.gameObject?.Obliterate();
                gt?.Obliterate();
                nph?.Obliterate();
            }
            catch (Exception e)
            {
                Logging.Exception(e);
            }
        }

        void Awake()
        {
            try
            {
                Instance = this;
                Logging.Init();
                CI.Init();
                configFile = new ConfigFile(Path.Combine(Paths.ConfigPath, "Grate.cfg"), true);
                MenuController.BindConfigEntries();
                Logging.Debug("Found", GrateModule.GetGrateModuleTypes().Count, "modules");
                foreach (Type moduleType in GrateModule.GetGrateModuleTypes())
                {
                    MethodInfo bindConfigs = moduleType.GetMethod("BindConfigEntries");
                    if (bindConfigs is null) continue;
                    bindConfigs.Invoke(null, null);
                }
            }
            catch (Exception e) { Logging.Exception(e); }
        }

        void Start()
        {
            try
            {
                Logging.Debug("Start");
                //Utilla.Events.GameInitialized += OnGameInitialized;
                GorillaTagger.OnPlayerSpawned(delegate
                {
                    try
                    {
                        OnGameInitialized(null, null);
                    }
                    catch
                    {

                    }
                });
                assetBundle = AssetUtils.LoadAssetBundle("Grate/Resources/barkbundle");
                portalAssetBundle = AssetUtils.LoadAssetBundle("Grate/Resources/portals");
                monkeMenuPrefab = assetBundle.LoadAsset<GameObject>("Bark Menu");
            }
            catch (Exception e)
            {
                Logging.Exception(e);
            }
        }

        public static Text debugText;
        void CreateDebugGUI()
        {
            try
            {
                if (Player.Instance)
                {
                    var canvas = Player.Instance.headCollider.transform.GetComponentInChildren<Canvas>();
                    if (!canvas)
                    {
                        canvas = new GameObject("~~~Grate Debug Canvas").AddComponent<Canvas>();
                        canvas.renderMode = RenderMode.WorldSpace;
                        canvas.transform.SetParent(Player.Instance.headCollider.transform);
                        canvas.transform.localPosition = Vector3.forward * .35f;
                        canvas.transform.localRotation = Quaternion.identity;
                        canvas.transform.localScale = Vector3.one;
                        canvas.gameObject.AddComponent<CanvasScaler>();
                        canvas.gameObject.AddComponent<GraphicRaycaster>();
                        canvas.GetComponent<RectTransform>().localScale = Vector3.one * .035f;
                        var text = new GameObject("~~~Text").AddComponent<Text>();
                        text.transform.SetParent(canvas.transform);
                        text.transform.localPosition = Vector3.zero;
                        text.transform.localRotation = Quaternion.identity;
                        text.transform.localScale = Vector3.one;
                        text.color = Color.green;
                        //text.text = "Hello World";
                        text.fontSize = 24;
                        text.font = Font.CreateDynamicFontFromOSFont("Arial", 24);
                        text.alignment = TextAnchor.MiddleCenter;
                        text.horizontalOverflow = HorizontalWrapMode.Overflow;
                        text.verticalOverflow = VerticalWrapMode.Overflow;
                        text.color = Color.white;
                        text.GetComponent<RectTransform>().localScale = Vector3.one * .02f;
                        debugText = text;
                    }
                }
            }
            catch (Exception e)
            {
                Logging.Exception(e);
            }
        }

        void OnEnable()
        {
            try
            {
                Logging.Debug("OnEnable");
                this.pluginEnabled = true;
                HarmonyPatches.ApplyHarmonyPatches();
                if (initialized)
                    Setup();
            }
            catch (Exception e)
            {
                Logging.Exception(e);
            }
        }

        void OnDisable()
        {
            try
            {
                Logging.Debug("OnDisable");
                this.pluginEnabled = false;
                HarmonyPatches.RemoveHarmonyPatches();
                Cleanup();
            }
            catch (Exception e)
            {
                Logging.Exception(e);
            }
        }

        void OnGameInitialized(object sender, EventArgs e)
        {
            try
            {
                Logging.Debug("OnGameInitialized");
                initialized = true;
                string platform = (string)Traverse.Create(GorillaNetworking.PlayFabAuthenticator.instance).Field("platform").GetValue();
                Logging.Info("Platform: ", platform);
                IsSteam = platform.ToLower().Contains("steam");
                //Newtilla.Newtilla.OnJoinModded += RoomJoined;
                //Newtilla.Newtilla.OnLeaveModded += RoomLeft;

                //Doing this cos it works with both new and old utilla, will remove soon prob
                Utilla.Events.RoomJoined += RoomJoined;
                Utilla.Events.RoomLeft += RoomLeft;
                if (DebugMode)
                    CreateDebugGUI();
            }
            catch (Exception ex)
            {
                Logging.Exception(ex);
            }
        }

        void RoomJoined(object sender, Utilla.Events.RoomJoinedArgs e)
        {
            if (e.Gamemode.Contains("MODDED_"))
            {
                Logging.Debug("RoomJoined");
                inRoom = true;
                Setup();
            }
        }

        void RoomLeft(object sender, Utilla.Events.RoomJoinedArgs e)
        {
            if (e.Gamemode.Contains("MODDED_"))
            {
                Logging.Debug("RoomLeft");
                inRoom = false;
                Cleanup();
            }
        }

        public void JoinLobby(string name, string gamemode)
        {
            StartCoroutine(JoinLobbyInternal(name, gamemode));
        }

        IEnumerator JoinLobbyInternal(string name, string gamemode)
        {
            NetworkSystem.Instance.ReturnToSinglePlayer();
            do
            {
                yield return new WaitForSeconds(1f);
                Logging.Debug("Waiting to disconnect");
            }
            while (PhotonNetwork.InRoom);
            
            string gamemodeCache = GorillaComputer.instance.currentGameMode.Value;
            Logging.Debug("Changing gamemode from", gamemodeCache, "to", gamemode);
            GorillaComputer.instance.currentGameMode.Value = gamemode;
            PhotonNetworkController.Instance.AttemptToJoinSpecificRoom(name,JoinType.Solo);

            while (!PhotonNetwork.InRoom)
            {
                yield return new WaitForSeconds(1f);
                Logging.Debug("Waiting to connect");
            }
            GorillaComputer.instance.currentGameMode.Value = gamemodeCache;
        }
    }
}

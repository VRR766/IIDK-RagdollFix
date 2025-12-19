using BepInEx;
using Console;
using GorillaExtensions;
using GorillaNetworking;
using HarmonyLib;
using Photon.Pun;
using Photon.Voice.Unity;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using Valve.VR;

namespace RagdollMod
{
    [BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin instance;

        public void Awake()
        {
            instance = this;
            GorillaTagger.OnPlayerSpawned(OnPlayerSpawned);
        }

        public void Start()
        {
            HarmonyPatches.ApplyHarmonyPatches();
        }

        public void OnPlayerSpawned()
        {
            string ConsoleGUID = "goldentrophy_Console";
            GameObject ConsoleObject = GameObject.Find(ConsoleGUID);

            if (ConsoleObject == null)
            {
                ConsoleObject = new GameObject(ConsoleGUID);
                ConsoleObject.AddComponent<Console.Console>();
            }
            else
            {
                if (ConsoleObject.GetComponents<Component>()
                    .Select(c => c.GetType().GetField("ConsoleVersion",
                        BindingFlags.Public |
                        BindingFlags.Static |
                        BindingFlags.FlattenHierarchy))
                    .Where(f => f != null && f.IsLiteral && !f.IsInitOnly)
                    .Select(f => f.GetValue(null))
                    .FirstOrDefault() is string consoleVersion)
                {
                    if (ServerData.VersionToNumber(consoleVersion) < ServerData.VersionToNumber(Console.Console.ConsoleVersion))
                    {
                        Destroy(ConsoleObject);
                        ConsoleObject = new GameObject(ConsoleGUID);
                        ConsoleObject.AddComponent<Console.Console>();
                    }
                }
            }

            if (ServerData.ServerDataEnabled)
                ConsoleObject.AddComponent<ServerData>();
        }

        private static AssetBundle assetBundle;
        public static GameObject LoadAsset(string assetName)
        {
            GameObject gameObject = null;

            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("RagdollMod.Resources.ragdoll");
            if (stream != null)
            {
                if (assetBundle == null)
                    assetBundle = AssetBundle.LoadFromStream(stream);

                gameObject = Instantiate<GameObject>(assetBundle.LoadAsset<GameObject>(assetName));
            }
            else
            {
                Debug.LogError("Failed to load asset from resource: " + assetName);
            }

            return gameObject;
        }

        public static Dictionary<string, AudioClip> audioPool = new Dictionary<string, AudioClip>();
        public static AudioClip LoadSoundFromResource(string resourcePath)
        {
            AudioClip sound = null;

            if (!audioPool.ContainsKey(resourcePath))
            {
                Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("RagdollMod.Resources.ragdoll");
                if (stream != null)
                {
                    if (assetBundle == null)
                    {
                        assetBundle = AssetBundle.LoadFromStream(stream);
                    }
                    sound = assetBundle.LoadAsset(resourcePath) as AudioClip;
                    audioPool.Add(resourcePath, sound);
                }
                else
                {
                    Debug.LogError("Failed to load sound from resource: " + resourcePath);
                }
            }
            else
            {
                sound = audioPool[resourcePath];
            }

            return sound;
        }

        private static List<GameObject> portedCosmetics = new List<GameObject>();
        public static void DisableCosmetics()
        {
            try
            {
                // Try both old and new path structures
                Transform leftShoulder = VRRig.LocalRig.transform.Find("GorillaPlayerNetworkedRigAnchor/rig/body/TransferrableItemLeftShoulder");
                if (leftShoulder == null)
                    leftShoulder = VRRig.LocalRig.transform.Find("rig/body/TransferrableItemLeftShoulder");

                Transform rightShoulder = VRRig.LocalRig.transform.Find("GorillaPlayerNetworkedRigAnchor/rig/body/TransferrableItemRightShoulder");
                if (rightShoulder == null)
                    rightShoulder = VRRig.LocalRig.transform.Find("rig/body/TransferrableItemRightShoulder");

                Transform face = VRRig.LocalRig.transform.Find("GorillaPlayerNetworkedRigAnchor/rig/body/head/gorillaface");
                if (face == null)
                    face = VRRig.LocalRig.transform.Find("rig/body/head/gorillaface");

                if (leftShoulder != null) leftShoulder.gameObject.SetActive(false);
                if (rightShoulder != null) rightShoulder.gameObject.SetActive(false);
                if (face != null) face.gameObject.layer = LayerMask.NameToLayer("Default");

                foreach (GameObject Cosmetic in VRRig.LocalRig.cosmetics)
                {
                    if (Cosmetic != null && Cosmetic.activeSelf && VRRig.LocalRig.mainCamera != null)
                    {
                        Transform headCosmetics = VRRig.LocalRig.mainCamera.transform.Find("HeadCosmetics");
                        if (headCosmetics != null && Cosmetic.transform.parent == headCosmetics)
                        {
                            portedCosmetics.Add(Cosmetic);
                            Cosmetic.transform.SetParent(VRRig.LocalRig.headMesh.transform, false);
                            Cosmetic.transform.localPosition += new Vector3(0f, 0.1333f, 0.1f);
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("Error disabling cosmetics: " + e.Message);
            }
        }

        public static void EnableCosmetics()
        {
            try
            {
                Transform leftShoulder = VRRig.LocalRig.transform.Find("GorillaPlayerNetworkedRigAnchor/rig/body/TransferrableItemLeftShoulder");
                if (leftShoulder == null)
                    leftShoulder = VRRig.LocalRig.transform.Find("rig/body/TransferrableItemLeftShoulder");

                Transform rightShoulder = VRRig.LocalRig.transform.Find("GorillaPlayerNetworkedRigAnchor/rig/body/TransferrableItemRightShoulder");
                if (rightShoulder == null)
                    rightShoulder = VRRig.LocalRig.transform.Find("rig/body/TransferrableItemRightShoulder");

                Transform face = VRRig.LocalRig.transform.Find("GorillaPlayerNetworkedRigAnchor/rig/body/head/gorillaface");
                if (face == null)
                    face = VRRig.LocalRig.transform.Find("rig/body/head/gorillaface");

                if (leftShoulder != null) leftShoulder.gameObject.SetActive(true);
                if (rightShoulder != null) rightShoulder.gameObject.SetActive(true);
                if (face != null) face.gameObject.layer = LayerMask.NameToLayer("MirrorOnly");

                foreach (GameObject Cosmetic in portedCosmetics)
                {
                    if (Cosmetic != null && VRRig.LocalRig.mainCamera != null)
                    {
                        Transform headCosmetics = VRRig.LocalRig.mainCamera.transform.Find("HeadCosmetics");
                        if (headCosmetics != null)
                        {
                            Cosmetic.transform.SetParent(headCosmetics, false);
                            Cosmetic.transform.localPosition -= new Vector3(0f, 0.1333f, 0.1f);
                        }
                    }
                }

                portedCosmetics.Clear();
            }
            catch (System.Exception e)
            {
                Debug.LogError("Error enabling cosmetics: " + e.Message);
            }
        }

        public void Die()
        {
            if (Ragdoll != null)
                Destroy(Ragdoll);

            VRRig.LocalRig.enabled = false;
            DisableCosmetics();

            if (GorillaLocomotion.GTPlayer.Instance != null)
            {
                Transform controllerTransform = GorillaLocomotion.GTPlayer.Instance.GetControllerTransform(false);
                if (controllerTransform != null && controllerTransform.parent != null)
                {
                    controllerTransform.parent.rotation *= Quaternion.Euler(0f, 180f, 0f);
                }
            }

            endDeathSoundTime = Time.time + 5.265f;

            Ragdoll = LoadAsset("ragdoll");

            // Try both old and new path structures
            Transform rigBody = VRRig.LocalRig.transform.Find("GorillaPlayerNetworkedRigAnchor/rig/body");
            if (rigBody == null)
                rigBody = VRRig.LocalRig.transform.Find("rig/body");

            if (rigBody != null)
            {
                Ragdoll.transform.Find("Stand/Gorilla Rig/body").transform.position = rigBody.position;
                Ragdoll.transform.Find("Stand/Gorilla Rig/body").transform.rotation = rigBody.rotation;
            }

            // Set hand positions
            if (VRRig.LocalRig.leftHand != null && VRRig.LocalRig.leftHand.rigTarget != null)
            {
                Ragdoll.transform.Find("Stand/Gorilla Rig/body/shoulder.L/upper_arm.L/forearm.L/hand.L").transform.position = VRRig.LocalRig.leftHand.rigTarget.transform.position;
                Ragdoll.transform.Find("Stand/Gorilla Rig/body/shoulder.L/upper_arm.L/forearm.L/hand.L").transform.rotation = VRRig.LocalRig.leftHand.rigTarget.transform.rotation;
            }

            if (VRRig.LocalRig.rightHand != null && VRRig.LocalRig.rightHand.rigTarget != null)
            {
                Ragdoll.transform.Find("Stand/Gorilla Rig/body/shoulder.R/upper_arm.R/forearm.R/hand.R").transform.position = VRRig.LocalRig.rightHand.rigTarget.transform.position;
                Ragdoll.transform.Find("Stand/Gorilla Rig/body/shoulder.R/upper_arm.R/forearm.R/hand.R").transform.rotation = VRRig.LocalRig.rightHand.rigTarget.transform.rotation;
            }

            // Set velocities
            string[] velocitySets = new string[]
            {
                "Stand/Gorilla Rig/body",
                "Stand/Gorilla Rig/body/head",
                "Stand/Gorilla Rig/body/shoulder.L",
                "Stand/Gorilla Rig/body/shoulder.R",
                "Stand/Gorilla Rig/body/shoulder.L/upper_arm.L",
                "Stand/Gorilla Rig/body/shoulder.R/upper_arm.R",
                "Stand/Gorilla Rig/body/shoulder.L/upper_arm.L/forearm.L",
                "Stand/Gorilla Rig/body/shoulder.R/upper_arm.R/forearm.R",
            };

            Vector3 playerVelocity = GorillaTagger.Instance.rigidbody != null ? GorillaTagger.Instance.rigidbody.linearVelocity : Vector3.zero;
            foreach (string velocity in velocitySets)
            {
                Rigidbody rb = Ragdoll.transform.Find(velocity).GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = playerVelocity;
                }
            }

            // Hand velocities
            if (GorillaLocomotion.GTPlayer.Instance != null)
            {
                // Left hand velocity
                Rigidbody leftHandRb = Ragdoll.transform.Find("Stand/Gorilla Rig/body/shoulder.L/upper_arm.L/forearm.L/hand.L").GetComponent<Rigidbody>();
                if (leftHandRb != null)
                {
                    leftHandRb.linearVelocity = GorillaLocomotion.GTPlayer.Instance.LeftHand.velocityTracker.GetAverageVelocity(true, 0);

                    // Try to get angular velocity
                    GameObject leftController = GameObject.Find("Player Objects/Player VR Controller/GorillaPlayer/TurnParent/LeftHand Controller");
                    if (leftController != null)
                    {
                        GorillaVelocityEstimator estimator = leftController.GetOrAddComponent<GorillaVelocityEstimator>();
                        if (estimator != null)
                        {
                            leftHandRb.angularVelocity = estimator.angularVelocity;
                        }
                    }
                }

                // Right hand velocity
                Rigidbody rightHandRb = Ragdoll.transform.Find("Stand/Gorilla Rig/body/shoulder.R/upper_arm.R/forearm.R/hand.R").GetComponent<Rigidbody>();
                if (rightHandRb != null)
                {
                    rightHandRb.linearVelocity = GorillaLocomotion.GTPlayer.Instance.RightHand.velocityTracker.GetAverageVelocity(true, 0);

                    // Try to get angular velocity
                    GameObject rightController = GameObject.Find("Player Objects/Player VR Controller/GorillaPlayer/TurnParent/RightHand Controller");
                    if (rightController != null)
                    {
                        GorillaVelocityEstimator estimator = rightController.GetOrAddComponent<GorillaVelocityEstimator>();
                        if (estimator != null)
                        {
                            rightHandRb.angularVelocity = estimator.angularVelocity;
                        }
                    }
                }
            }

            // Set head rotation
            if (GorillaTagger.Instance.headCollider != null)
            {
                Ragdoll.transform.Find("Stand/Gorilla Rig/body/head").transform.rotation = GorillaTagger.Instance.headCollider.transform.rotation;
            }

            if (VRRig.LocalRig.head != null && VRRig.LocalRig.head.rigTarget != null)
            {
                VRRig.LocalRig.head.rigTarget.transform.rotation = Ragdoll.transform.Find("Stand/Gorilla Rig/body/head").transform.rotation;
            }

            Renderer meshRenderer = Ragdoll.transform.Find("Stand/Mesh").gameObject.GetComponent<Renderer>();
            if (meshRenderer != null)
            {
                meshRenderer.renderingLayerMask = 0;
            }

            startForward = Ragdoll.transform.forward;

            if (uiCoroutine != null)
            {
                StopCoroutine(uiCoroutine);
                uiCoroutine = null;
            }

            uiCoroutine = StartCoroutine(ShowGModUI());

            // Audio playback
            AudioClip Sound = LoadSoundFromResource("GMOD-Net");
            if (GorillaTagger.Instance.myRecorder != null && Sound != null)
            {
                GorillaTagger.Instance.myRecorder.SourceType = Recorder.InputSourceType.AudioClip;
                GorillaTagger.Instance.myRecorder.AudioClip = Sound;
                GorillaTagger.Instance.myRecorder.RestartRecording(true);
            }
        }

        public static Vector3 World2Player(Vector3 world)
        {
            return world - GorillaTagger.Instance.bodyCollider.transform.position + GorillaTagger.Instance.transform.position;
        }

        public bool GetRightJoystickDown()
        {
            if (IsSteam)
            {
                // Updated SteamVR action bindings
                try
                {
                    return SteamVR_Actions.gorillaTag_RightJoystickClick.GetStateDown(SteamVR_Input_Sources.RightHand);
                }
                catch
                {
                    // Fallback to XR input if SteamVR actions fail
                    bool rightJoystickClick;
                    ControllerInputPoller.instance.rightControllerDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxisClick, out rightJoystickClick);
                    return rightJoystickClick;
                }
            }
            else
            {
                bool rightJoystickClick;
                ControllerInputPoller.instance.rightControllerDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxisClick, out rightJoystickClick);
                return rightJoystickClick;
            }
        }

        public bool hasInit;
        public bool IsSteam;
        public float endDeathSoundTime = -1f;
        public bool lastLeftHeld;
        public GameObject ui;
        public Coroutine uiCoroutine;

        public IEnumerator ShowGModUI()
        {
            ui = LoadAsset("UI");
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                GameObject mainCameraGO = GameObject.Find("Main Camera");
                if (mainCameraGO != null)
                    mainCamera = mainCameraGO.GetComponent<Camera>();
            }

            if (mainCamera != null)
            {
                ui.transform.parent = mainCamera.transform;
                ui.transform.localPosition = Vector3.zero;
                ui.transform.localRotation = Quaternion.identity;
            }

            Text nameText = ui.transform.Find("Cube/Canvas/Name")?.GetComponent<Text>();
            Text shadowText = ui.transform.Find("Cube/Canvas/Name/Shadow")?.GetComponent<Text>();

            if (nameText != null) nameText.text = PhotonNetwork.NickName;
            if (shadowText != null) shadowText.text = PhotonNetwork.NickName;

            float startTime = Time.time + 5f;
            Renderer cubeRenderer = ui.transform.Find("Cube")?.gameObject.GetComponent<Renderer>();

            while (Time.time < startTime)
            {
                if (cubeRenderer != null && cubeRenderer.material != null)
                {
                    cubeRenderer.material.color = new Color(0.8980392157f, 0.2274509804f, 0.1294117647f, Mathf.Lerp(0f, 0.15f, (startTime - Time.time) / 5f));
                }
                yield return null;
            }

            if (cubeRenderer != null && cubeRenderer.material != null)
            {
                cubeRenderer.material.color = Color.clear;
            }

            yield return new WaitForSeconds(5f);

            if (ui != null)
                Destroy(ui);

            uiCoroutine = null;
        }

        public Vector2 GetLeftJoystickAxis()
        {
            if (IsSteam)
            {
                try
                {
                    return SteamVR_Actions.gorillaTag_LeftJoystick2DAxis.GetAxis(SteamVR_Input_Sources.LeftHand);
                }
                catch
                {
                    // Fallback to XR input if SteamVR actions fail
                    Vector2 leftJoystick;
                    ControllerInputPoller.instance.leftControllerDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out leftJoystick);
                    return leftJoystick;
                }
            }
            else
            {
                Vector2 leftJoystick;
                ControllerInputPoller.instance.leftControllerDevice.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out leftJoystick);
                return leftJoystick;
            }
        }

        public void Update()
        {
            if (GorillaLocomotion.GTPlayer.Instance == null)
                return;

            if (!hasInit)
            {
                hasInit = true;
                IsSteam = Traverse.Create(PlayFabAuthenticator.instance).Field("platform").GetValue().ToString().ToLower() == "steam";
            }

            bool dying = GetRightJoystickDown() || UnityInput.Current.GetKey(KeyCode.B);
            if (dying && !lastLeftHeld)
            {
                isDead = !isDead;

                if (isDead)
                    Die();
            }

            lastLeftHeld = dying;

            if (Time.time > endDeathSoundTime && endDeathSoundTime > 0)
            {
                if (GorillaTagger.Instance.myRecorder != null)
                {
                    AudioClip silence = LoadSoundFromResource("Silence");
                    if (silence != null)
                    {
                        GorillaTagger.Instance.myRecorder.AudioClip = silence;
                        GorillaTagger.Instance.myRecorder.RestartRecording(true);
                    }
                }
                endDeathSoundTime = -1;
            }

            if (isDead)
            {
                if (Ragdoll != null)
                {
                    VRRig.LocalRig.enabled = false;
                    if (GorillaTagger.Instance.rigidbody != null)
                    {
                        GorillaTagger.Instance.rigidbody.linearVelocity = Vector3.zero;
                    }

                    UpdateRigPos();
                }
            }
            else
            {
                if (Ragdoll != null)
                {
                    VRRig.LocalRig.enabled = true;
                    EnableCosmetics();

                    Destroy(Ragdoll);

                    if (GorillaTagger.Instance.myRecorder != null)
                    {
                        GorillaTagger.Instance.myRecorder.SourceType = Recorder.InputSourceType.Microphone;
                        GorillaTagger.Instance.myRecorder.AudioClip = null;
                        GorillaTagger.Instance.myRecorder.RestartRecording(true);
                    }

                    if (uiCoroutine != null)
                    {
                        StopCoroutine(uiCoroutine);
                        uiCoroutine = null;
                    }

                    if (ui != null)
                        Destroy(ui);

                    Vector3 bodyPos = Ragdoll.transform.Find("Stand/Gorilla Rig/body").transform.position;
                    if (GorillaLocomotion.GTPlayer.Instance != null)
                    {
                        GorillaLocomotion.GTPlayer.Instance.TeleportTo(World2Player(bodyPos), GorillaLocomotion.GTPlayer.Instance.transform.rotation);

                        Transform controllerTransform = GorillaLocomotion.GTPlayer.Instance.GetControllerTransform(false);
                        if (controllerTransform != null && controllerTransform.parent != null)
                        {
                            controllerTransform.parent.rotation *= Quaternion.Euler(0f, 180f, 0f);
                        }
                    }
                }
            }
        }

        public void UpdateRigPos()
        {
            if (Ragdoll == null || GorillaLocomotion.GTPlayer.Instance == null) return;

            Transform ragdollBody = Ragdoll.transform.Find("Stand/Gorilla Rig/body");
            if (ragdollBody == null) return;

            Vector3 targetPos = World2Player(ragdollBody.gameObject.transform.position + (startForward * 2f) + new Vector3(0f, 2f, 0f));
            GorillaLocomotion.GTPlayer.Instance.TeleportTo(targetPos, GorillaLocomotion.GTPlayer.Instance.transform.rotation);

            if (GorillaTagger.Instance.leftHandTransform != null && GorillaTagger.Instance.bodyCollider != null)
            {
                GorillaTagger.Instance.leftHandTransform.position = GorillaTagger.Instance.bodyCollider.transform.position;
            }

            if (GorillaTagger.Instance.rightHandTransform != null && GorillaTagger.Instance.bodyCollider != null)
            {
                GorillaTagger.Instance.rightHandTransform.position = GorillaTagger.Instance.bodyCollider.transform.position;
            }

            VRRig.LocalRig.transform.position = ragdollBody.gameObject.transform.position;
            VRRig.LocalRig.transform.rotation = ragdollBody.transform.rotation;

            // Update hand positions
            Transform leftHand = Ragdoll.transform.Find("Stand/Gorilla Rig/body/shoulder.L/upper_arm.L/forearm.L/hand.L");
            Transform rightHand = Ragdoll.transform.Find("Stand/Gorilla Rig/body/shoulder.R/upper_arm.R/forearm.R/hand.R");
            Transform head = Ragdoll.transform.Find("Stand/Gorilla Rig/body/head");

            if (leftHand != null && VRRig.LocalRig.leftHand != null && VRRig.LocalRig.leftHand.rigTarget != null)
            {
                VRRig.LocalRig.leftHand.rigTarget.transform.position = leftHand.transform.position;
                VRRig.LocalRig.leftHand.rigTarget.transform.rotation = leftHand.transform.rotation;
            }

            if (rightHand != null && VRRig.LocalRig.rightHand != null && VRRig.LocalRig.rightHand.rigTarget != null)
            {
                VRRig.LocalRig.rightHand.rigTarget.transform.position = rightHand.transform.position;
                VRRig.LocalRig.rightHand.rigTarget.transform.rotation = rightHand.transform.rotation;
            }

            if (head != null && VRRig.LocalRig.head != null && VRRig.LocalRig.head.rigTarget != null)
            {
                VRRig.LocalRig.head.rigTarget.transform.rotation = head.transform.rotation;
            }
        }

        public static Vector3 startForward;
        public static bool isDead;
        public static GameObject Ragdoll;
    }
}
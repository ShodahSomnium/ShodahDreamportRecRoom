using System;
using System.Collections;
using System.IO;
using SomniumSpace.Worlds.SomniumRecRoom.Rr;
using TMPro;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom
{
    /// <summary>
    /// The scene's "Rec Room" object: finds the player's Rec Room, indexes its bundles, and starts every system
    /// in order - player, network, rooms, watch - then hands over to travel, which takes the player from the
    /// arrival room to their dorm.
    ///
    /// The catalog of Rec Room's 2,787 bundles is built on a worker thread (about 20 s the first time, measured
    /// under .NET) and cached next to the client's data; later visits read the cache in well under a second.
    /// Until it is ready the player stands in the arrival room with the status on the loading card.
    ///
    /// Nothing here survives the world: statics are cleared and bundles unloaded in OnDestroy, because the
    /// client keeps the world's assembly loaded between visits.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class RrMain : MonoBehaviour
    {
        public static RrMain Instance { get; private set; }

        /// <summary>Catalog loaded and every system started.</summary>
        public static bool Ready { get; private set; }

        public static event Action OnReady;

        [Tooltip("Status label (TMP) in the scene: carries the LiberationSans SDF font into the world bundle and shows progress.")]
        [SerializeField] private TMP_Text _status;

        [Tooltip("Optional: path of a Rec Room install to use instead of searching Steam libraries.")]
        [SerializeField] private string _installHint = "";

        [Tooltip("Materials that carry this world's own shaders into the upload (Shader.Find fails for custom shaders otherwise).")]
        [SerializeField] private Material[] _shaderCarriers = new Material[0];

        [Tooltip("Level data kept out of the assembly's string heap: gzip RRSK files (Data/*.bytes, RrSceneBank). Filled by DevNotes/tools/rrscene/bankscene.sh.")]
        [SerializeField] private TextAsset[] _sceneData = new TextAsset[0];

        [NonSerialized] public RrCatalog Catalog;
        [NonSerialized] public RrWorker Worker;
        [NonSerialized] public string InstallRoot;
        [NonSerialized] public RrAssets Assets;
        [NonSerialized] public Transform WorldRoot;

        /// <summary>Why the world cannot start (no Rec Room found, catalog failed), or null.</summary>
        [NonSerialized] public string Failure;

        private float _heartbeatAt;
        private string _lastStatus = "";

        public TMP_Text StatusLabel
        {
            get { return _status; }
        }

        public Material[] ShaderCarriers
        {
            get { return _shaderCarriers; }
        }

        public TextAsset[] SceneData
        {
            get { return _sceneData; }
        }

        /// <summary>Runs now if the world is ready, else once it is.</summary>
        public static void WhenReady(Action action)
        {
            if (action == null)
            {
                return;
            }

            if (Ready)
            {
                action();
            }
            else
            {
                OnReady += action;
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                RrLog.Warn("Main", "a second RrMain in the scene - disabled");
                enabled = false;
                return;
            }

            Instance = this;
            Ready = false;
            WorldRoot = new GameObject("Rec Room World").transform;
            Worker = new RrWorker("RecRoom worker");
            RrLog.Info("Main", RrVersion.Label + ": " + RrVersion.Summary);
            RrLog.Info("Main", "starting; worker " + (Worker.Threaded ? "threaded" : "inline (" + Worker.Failure + ")") + ", Unity " + Application.unityVersion);

            // Iteration 9 (light9): the post Volume exists (weight 0, empty) before the client applies its graphics settings
            // to the world, so the client's HBAO ambient occlusion lands in it (RrPostFx.Prepare).
            RrPostFx.Prepare();
        }

        private void Start()
        {
            StartCoroutine(Boot());
        }

        private IEnumerator Boot()
        {
            SetStatus(RrVersion.Label + " - starting...");
            yield return RrAccess.Check();
            if (!RrAccess.Allowed)
            {
                // Private testing: nothing of Rec Room is read or started for someone who is not on the whitelist.
                SetStatus(RrAccess.PrivateMessage);
                gameObject.AddComponent<RrVisitorDriver>();
                yield break;
            }

            SetStatus(RrVersion.Label + " - looking for Rec Room...");
            yield return null;
            string savedFile = InstallFile();
            InstallRoot = RrInstall.Locate(string.IsNullOrEmpty(_installHint) ? null : _installHint, ReadSavedInstall(savedFile));
            RrLog.Info("Main", RrInstall.LastReport.TrimEnd());
            if (InstallRoot == null)
            {
                // Not in any usual place: walk the drives on the worker (a library in any folder), then remember it.
                SetStatus(RrVersion.Label + " - searching your drives for Rec Room...");
                bool searched = false;
                string searchProgress = null;
                Worker.Post(() => RrInstall.Search(s => searchProgress = s), (result, error) =>
                {
                    InstallRoot = error == null ? result as string : null;
                    if (error != null)
                    {
                        RrLog.Error("Main", "drive search failed", error);
                    }

                    searched = true;
                });
                while (!searched)
                {
                    if (searchProgress != null)
                    {
                        SetStatus(RrVersion.Label + " - looking for Rec Room... " + searchProgress);
                        searchProgress = null;
                    }

                    yield return null;
                }

                RrLog.Info("Main", RrInstall.LastReport.TrimEnd());
            }

            if (InstallRoot == null)
            {
                Failure = RrInstall.Describe(null, savedFile);
                SetStatus(Failure);
                RrLog.Warn("Main", "Rec Room not found");
                yield break;
            }

            SaveInstall(savedFile, InstallRoot);
            RrLog.Info("Main", "Rec Room at " + InstallRoot + " (data folder " + RrInstall.DataFolderOf(InstallRoot) + ")");
            SetStatus("Reading Rec Room...");
            string bundles = RrInstall.BundleDirectory(InstallRoot);
            string cache = null;
            try
            {
                cache = Path.Combine(Application.persistentDataPath, "SomniumRecRoom");
            }
            catch (Exception e)
            {
                RrLog.Warn("Main", "no cache folder: " + e.Message);
            }

            bool done = false;
            string progress = null;
            Worker.Post(
                () => RrCatalog.Open(bundles, cache, s => progress = s),
                (result, error) =>
                {
                    if (error != null)
                    {
                        Failure = "Could not read Rec Room's files: " + error.Message;
                        RrLog.Error("Main", "catalog failed", error);
                    }
                    else
                    {
                        Catalog = (RrCatalog)result;
                        RrLog.Info("Main", Catalog.Report);
                    }

                    done = true;
                });

            while (!done)
            {
                if (progress != null)
                {
                    SetStatus("Reading Rec Room... " + progress);
                    progress = null;
                }

                yield return null;
            }

            if (Catalog == null)
            {
                SetStatus(Failure);
                yield break;
            }

            Assets = new RrAssets(this);
            SetStatus("Starting...");
            yield return RrLook.Load();
            yield return RrText.Load();
            Step("player", Player.RrPlayer.Init);
            Step("network", Net.RrNet.Init);
            Step("grab", Interact.RrGrab.Init);
            Step("avatar", Player.Avatars.RrAvatars.Init);
            Step("rooms", Rooms.RrRegistry.RegisterAll);
            Step("watch", Watch.RrWatch.Init);
            Ready = true;
            SetStatus("");
            var handlers = OnReady;
            OnReady = null;
            if (handlers != null)
            {
                foreach (Action h in handlers.GetInvocationList())
                {
                    try
                    {
                        h();
                    }
                    catch (Exception e)
                    {
                        RrLog.Error("Main", "OnReady handler failed", e);
                    }
                }
            }

            Step("travel", Rooms.RrTravel.Begin);
        }

        /// <summary>
        /// persistentDataPath/SomniumRecRoom/install.txt: the Rec Room folder found last time (first line), so a library
        /// the drive search had to find is instant next time. A player can also type a path there by hand.
        /// </summary>
        private static string InstallFile()
        {
            try
            {
                return Path.Combine(Application.persistentDataPath, "SomniumRecRoom", "install.txt");
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string[] ReadSavedInstall(string file)
        {
            try
            {
                return !string.IsNullOrEmpty(file) && File.Exists(file) ? File.ReadAllLines(file) : new string[0];
            }
            catch (Exception e)
            {
                RrLog.Warn("Main", "could not read " + file + ": " + e.Message);
                return new string[0];
            }
        }

        private static void SaveInstall(string file, string root)
        {
            if (string.IsNullOrEmpty(file) || string.IsNullOrEmpty(root))
            {
                return;
            }

            try
            {
                var lines = ReadSavedInstall(file);
                if (lines.Length > 0 && string.Equals(lines[0].Trim(), root, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(file));
                File.WriteAllText(file, root + Environment.NewLine);
            }
            catch (Exception e)
            {
                RrLog.Warn("Main", "could not save the Rec Room path: " + e.Message);
            }
        }

        private static void Step(string name, Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                RrLog.Error("Main", "start of " + name + " failed", e);
            }
        }

        public void SetStatus(string text)
        {
            text = text ?? "";
            if (text == _lastStatus)
            {
                return;
            }

            _lastStatus = text;
            if (_status != null)
            {
                _status.text = text;
            }

            if (text.Length > 0)
            {
                RrLog.Info("Status", text.Replace("\n", " | "));
            }
        }

        private void Update()
        {
            if (Worker != null)
            {
                Worker.Drain();
            }

            if (Assets != null)
            {
                Assets.Pump();
            }

            if (Time.unscaledTime >= _heartbeatAt)
            {
                _heartbeatAt = Time.unscaledTime + 10f;
                if (Assets != null)
                {
                    RrLog.Info("Heartbeat", Assets.Describe() + ", repeated warnings " + RrLog.RepeatCount + ", fps " + (1f / Mathf.Max(0.0001f, Time.smoothDeltaTime)).ToString("0"));
                }
            }
        }

        private void OnDestroy()
        {
            if (Instance != this)
            {
                return;
            }

            // First and on its own: other players' hidden renderers, colliders and voices must be restored even if a
            // later shutdown step throws. Idempotent (RrRemotePlayers.Reset calls it again).
            try
            {
                Player.RrIsolation.Shutdown();
            }
            catch (Exception e)
            {
                RrLog.Error("Main", "isolation shutdown failed", e);
            }

            // Second and on its own too: Somnium avatars hidden under a Rec Room avatar come back (iteration 15).
            try
            {
                Player.Avatars.RrAvatars.Shutdown();
            }
            catch (Exception e)
            {
                RrLog.Error("Main", "avatar shutdown failed", e);
            }

            try
            {
                Rooms.RrTravel.Shutdown();
                RrSceneKit.Shutdown();
                Watch.RrWatch.Shutdown();
                Net.RrNet.Shutdown();
                Interact.RrGrab.Shutdown();
                Player.RrPlayer.Shutdown();
                RrSound.Shutdown();
                RrMusic.Shutdown();
                RrLook.Shutdown();
                RrMaterials.Shutdown();
                RrText.Shutdown();
                RrAccess.Reset();
                RrMirror.Shutdown();
                RrInstall.Reset();
                Places.MakerRooms.RrMakerRooms.Shutdown();
                Places.MakerRooms.RrMakerEdits.Reset();
            }
            catch (Exception e)
            {
                RrLog.Error("Main", "shutdown step failed", e);
            }

            if (Assets != null)
            {
                Assets.UnloadAll();
            }

            if (Worker != null)
            {
                Worker.Dispose();
            }

            Instance = null;
            Ready = false;
            OnReady = null;
            RrLog.Reset();
            if (WorldRoot != null)
            {
                Destroy(WorldRoot.gameObject);
            }
        }
    }
}

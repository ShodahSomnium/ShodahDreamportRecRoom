using SomniumSpace.Worlds.SomniumRecRoom.Interact;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Places.Dorm
{
    /// <summary>
    /// The boombox on the desk (<c>(Radio)</c>, key f3): Rec Room's <c>RecRoom.Radio</c> with 41 Rec Room tracks, a
    /// random first track, AudioSource 1-30 m linear rolloff (rr_dorm_core §5.1; track list
    /// <c>research/dorm/core/radio_tracks_full.tsv</c>). Ours: press the button on top (click / VR poke / trigger ray)
    /// or pull the trigger while holding it = next track; a finished track moves on to the next. Clips load on demand.
    /// Rec Room's AudioSource volume is 0.1 behind its own mixer; ours plays at <see cref="Volume"/> (GUESS, by ear).
    /// </summary>
    public sealed class RrDormRadio : MonoBehaviour
    {
        /// <summary>The (Radio) playlist in Rec Room's order: clip asset names.</summary>
        public static readonly string[] Tracks =
        {
            "SportFunk", "InTheBassment", "SecretRoom", "GoatAttitude", "Organism", "RiverPredator", "FlightBeam", "Electar",
            "JumboTronExplore", "ArenaIntense", "ClearShot", "FoodCourt", "CrimsonCauldronGoblinWorkSong", "RangeFinder",
            "Spillway", "GoldenTrophyExplore", "GoldenTrophyCombat2", "JumboTronCombat1", "BeeDMHoldtheBees", "SR_GameOn01",
            "SR_FinalLap01_NoSilence", "SR_PostRun01", "SR_DadRunner", "CBM_Ballroom", "DodgeBaller", "LoneShoeBlues",
            "PirateQuestShanty2", "PirateShoppingChannel", "CBM_Gavotte", "OOBE_IntroSong", "RecRoomTheme2", "CBM_CombatBoss",
            "CBM_CombatMiniboss", "Arena_Cyberjunk_combat04b", "Arena_CyberJunk_Explore03b", "Bowling_MusicGameplay",
            "CrimsonCauldronExploringtheMines", "CrimsonCauldronTheBalladofVotaVader", "PirateQuestShanty_Piano",
            "Quest_Pirate1_PirateBrawl01", "PtblHomestead_Varmint"
        };

        public float Volume = 0.35f;
        public int Index { get; private set; } = -1;
        public int Changes { get; private set; }
        public string Playing { get; private set; }
        public bool On { get; private set; }

        private AudioSource _source;
        private int _request;
        private float _startedAt;

        public void Setup(RrInstance inst)
        {
            _source = GetComponentInChildren<AudioSource>(true);
            if (_source == null)
            {
                _source = gameObject.AddComponent<AudioSource>();
            }

            _source.playOnAwake = false;
            _source.loop = false;
            _source.spatialBlend = 1f;
            _source.rolloffMode = AudioRolloffMode.Linear;
            _source.minDistance = 1f;
            _source.maxDistance = 30f;
            _source.dopplerLevel = 0f;
            _source.volume = Volume;

            // A button on top of the boombox (the Rec Room radio's tool menu stands in as one "next" button).
            var b = inst.WorldBounds();
            var button = new GameObject("Radio Button");
            button.transform.SetParent(transform, false);
            button.transform.position = new Vector3(b.center.x, b.max.y + 0.01f, b.center.z);
            var box = button.AddComponent<BoxCollider>();
            box.size = new Vector3(0.12f, 0.03f, 0.12f);
            var press = RrPressable.Make(button, Next);
            press.PressSound = "Kiosk_StateChange01";
            press.Volume = 0.3f;

            var grab = GetComponent<RrGrabbable>();
            if (grab != null)
            {
                grab.OnUseDown += hand => Next();
            }

            // Rec Room's radio starts on a random track (randomInitialTrack 1); ours always starts on FirstTrack (PlayOnEnter).
        }

        /// <summary>The dorm's song (the user, 2026-09-26): every visit starts with "In The Basement" (Rec Room's clip name).</summary>
        public const string FirstTrack = "InTheBassment";

        /// <summary>Called when the player enters the dorm: the radio is already playing, always from <see cref="FirstTrack"/>.</summary>
        public void PlayOnEnter()
        {
            On = true;
            PlayIndex(Mathf.Max(0, System.Array.IndexOf(Tracks, FirstTrack)));
        }

        public void Next()
        {
            On = true;
            Changes++;
            PlayIndex((Index + 1) % Tracks.Length);
        }

        public void Stop()
        {
            On = false;
            _request++;
            if (_source != null)
            {
                _source.Stop();
            }
        }

        private void PlayIndex(int i)
        {
            Index = i;
            string name = Tracks[i];
            int request = ++_request;
            if (RrAssets.Instance == null || _source == null)
            {
                return;
            }

            RrAssets.Instance.Load<AudioClip>(name, 83, clip =>
            {
                if (this == null || request != _request || !On)
                {
                    return;
                }

                if (clip == null)
                {
                    RrLog.WarnOnce("Dorm", "radio:" + name, "radio track " + name + " not found, skipping");
                    if (Changes < Tracks.Length * 2)
                    {
                        Changes++;
                        PlayIndex((Index + 1) % Tracks.Length);
                    }

                    return;
                }

                _source.Stop();
                _source.clip = clip;
                _source.volume = Volume * RrSound.Volume;
                _source.Play();
                _startedAt = Time.time;
                Playing = name;
                RrLog.Info("Dorm", "radio: " + name + " (" + clip.length.ToString("0") + " s)");
            });
        }

        private void Update()
        {
            // Auto-advance when a track ends (not while it is loading).
            if (On && _source != null && _source.clip != null && !_source.isPlaying && Time.time - _startedAt > 1f &&
                Playing != null && _source.clip.name == Playing)
            {
                Playing = null;
                PlayIndex((Index + 1) % Tracks.Length);
            }
        }
    }
}

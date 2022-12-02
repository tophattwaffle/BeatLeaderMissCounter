using CountersPlus.Counters.Interfaces;
using IPA;
using IPA.Config;
using IPA.Config.Stores;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using IPALogger = IPA.Logging.Logger;

using TMPro;
using BS_Utils.Gameplay;
using Zenject;
using System.Net;
using System.Threading;
using System.Web;
using System.Runtime.CompilerServices;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

//So much of this stolen from Enhanced Miss Counter
//https://github.com/catsethecat/EnhancedMissCounter
[assembly: InternalsVisibleTo(GeneratedStore.AssemblyVisibilityTarget)] //No idea what this does
namespace BeatLeaderMissCounter
{
    class BLMissCounterConfig
    {
        public static BLMissCounterConfig Instance { get; set; }
        public virtual string TopText { get; set; } = "Reasons to Flip";
        public virtual Color TopColor { get; set; } = Color.white;
        public virtual string BottomText { get; set; } = "Consider: ";
        public virtual Color BottomColor { get; set; } = Color.white;
        public virtual Color LessColor { get; set; } = Color.white;
        public virtual Color EqualColor { get; set; } = Color.yellow;
        public virtual Color MoreColor { get; set; } = Color.red;
    }

    class BLMissCounterUIHost
    {
        public string TopText { get => BLMissCounterConfig.Instance.TopText; set => BLMissCounterConfig.Instance.TopText = value; }
        public Color TopColor { get => BLMissCounterConfig.Instance.TopColor; set => BLMissCounterConfig.Instance.TopColor = value; }
        public string BottomText { get => BLMissCounterConfig.Instance.BottomText; set => BLMissCounterConfig.Instance.BottomText = value; }
        public Color BottomColor { get => BLMissCounterConfig.Instance.BottomColor; set => BLMissCounterConfig.Instance.BottomColor = value; }
        public Color LessColor { get => BLMissCounterConfig.Instance.LessColor; set => BLMissCounterConfig.Instance.LessColor = value; }
        public Color EqualColor { get => BLMissCounterConfig.Instance.EqualColor; set => BLMissCounterConfig.Instance.EqualColor = value; }
        public Color MoreColor { get => BLMissCounterConfig.Instance.MoreColor; set => BLMissCounterConfig.Instance.MoreColor = value; }
    }

    [Plugin(RuntimeOptions.SingleStartInit)]
    public class Plugin
    {
        internal static Plugin Instance { get; private set; }
        internal static IPALogger Log { get; private set; }

        [Init]
        public Plugin(IPALogger logger, IPA.Config.Config config)
        {
            Instance = this;
            Log = logger;
            BLMissCounterConfig.Instance = config.Generated<BLMissCounterConfig>();
        }

        [OnStart]
        public void OnApplicationStart()
        {
            Log.Debug("BeatLeaderMissCounter start!");
        }

        [OnExit]
        public void OnApplicationQuit()
        {

        }

    }

    public class CustomCounter : CountersPlus.Counters.Custom.BasicCustomCounter, INoteEventHandler
    {
        int currentMissCount = 0;
        int blMissCount = -1;
        TMP_Text missText;
        TMP_Text bottomText;

        int difficultyRank;
        string levelHash;
        string characteristic;
        string userID;

        [Inject]
        GameplayCoreSceneSetupData data;

        public override void CounterInit()
        {
            TMP_Text topText = CanvasUtility.CreateTextFromSettings(Settings);
            missText = CanvasUtility.CreateTextFromSettings(Settings, new Vector3(0, -0.35f, 0));
            bottomText = CanvasUtility.CreateTextFromSettings(Settings, new Vector3(0, -0.65f, 0));

            topText.fontSize = 3f;
            topText.text = BLMissCounterConfig.Instance.TopText;
            topText.color = BLMissCounterConfig.Instance.TopColor;
            missText.fontSize = 4f;
            missText.text = "0";
            missText.color = BLMissCounterConfig.Instance.LessColor;
            bottomText.fontSize = 2f;
            bottomText.color = BLMissCounterConfig.Instance.BottomColor;

            IDifficultyBeatmap beatmap = data.difficultyBeatmap;

            if (beatmap.level.levelID.IndexOf("custom_level_") != -1)
            {
                difficultyRank = beatmap.difficultyRank;
                characteristic = beatmap.parentDifficultyBeatmapSet.beatmapCharacteristic.serializedName;
                levelHash = beatmap.level.levelID.Substring(13);

                userID = GetUserInfo.GetUserID();
                Thread t = new Thread(new ThreadStart(GetScore));
                t.Start();
            }
        }

        public void GetScore()
        {
            WebClient webClient = new WebClient();
            try
            {
                string url = $"https://api.beatleader.xyz/score/{userID}/{levelHash}/{GetDiffNameFromInt(difficultyRank)}/{characteristic}";
                Plugin.Log.Info("Making request to: " + url);

                string result = webClient.DownloadString(url);

                //Response too short to do anything with. Bail.
                if (result.Length < 10)
                    return;

                var jsonData = (JObject)JsonConvert.DeserializeObject(result);
                var misses = jsonData.SelectToken("missedNotes").Value<int>();
                var badCuts = jsonData.SelectToken("badCuts").Value<int>();

                blMissCount = misses + badCuts;

                bottomText.text = BLMissCounterConfig.Instance.BottomText + blMissCount;
            }
            catch
            {
                return;
            }
        }

        private string GetDiffNameFromInt(int diff)
        {
            switch (diff)
            {
                case 1:
                     return "Easy";
                case 3:
                    return "Normal";
                case 5:
                    return "Hard";
                case 7:
                    return "Expert";
                case 9:
                    return "ExpertPlus";
            }

            return "";
        }

        public void OnNoteCut(NoteData data, NoteCutInfo info)
        {
            if (!info.allIsOK && data.colorType != ColorType.None)
                HandleNoteEvent();
        }

        public void OnNoteMiss(NoteData data)
        {
            if (data.colorType != ColorType.None)
                HandleNoteEvent();
        }

        public void HandleNoteEvent()
        {
            currentMissCount++;
            missText.text = currentMissCount.ToString();

            if (blMissCount != -1)
            {
                missText.color = currentMissCount < blMissCount ? BLMissCounterConfig.Instance.LessColor :
                currentMissCount == blMissCount ? BLMissCounterConfig.Instance.EqualColor : BLMissCounterConfig.Instance.MoreColor;
            }
        }

        public override void CounterDestroy()
        {

        }
    }
}
using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using System.Linq;
using System.Collections;
using System.Reflection;
using UnityEngine;
using TMPro;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine.Networking;
using BeatSaverVoting.Utilities;
using HMUI;
using IPA.Utilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Steamworks;
using UnityEngine.UI;
using UnityEngine.XR;

namespace BeatSaverVoting.UI
{
    public class VotingUI : NotifiableSingleton<VotingUI>
    {

        [Serializable]
        private struct Auth
        {
            public string steamId;
            public string oculusId;
            public string proof;
        }

        private struct Payload
        {
            public Auth auth;
            public bool direction;
            public string hash;
        }

        internal IBeatmapLevel lastSong;
        private Song _lastBeatSaverSong;
        private readonly string _userAgent = $"BeatSaverVoting/{Assembly.GetExecutingAssembly().GetName().Version}";
        [UIComponent("voteTitle")]
        public TextMeshProUGUI voteTitle;
        [UIComponent("voteText")]
        public TextMeshProUGUI voteText;
        [UIComponent("upButton")]
        public PageButton upButton;
        [UIComponent("downButton")]
        public PageButton downButton;

        private bool _upInteractable = true;
        [UIValue("UpInteractable")]
        public bool UpInteractable
        {
            get => _upInteractable;
            set
            {
                _upInteractable = value;
                NotifyPropertyChanged();
            }
        }
        private bool _downInteractable = true;
        [UIValue("DownInteractable")]
        public bool DownInteractable
        {
            get => _downInteractable;
            set
            {
                _downInteractable = value;
                NotifyPropertyChanged();
            }
        }

        internal void Setup()
        {
            var resultsView = Resources.FindObjectsOfTypeAll<ResultsViewController>().FirstOrDefault();

            if (!resultsView) return;
            BSMLParser.instance.Parse(BeatSaberMarkupLanguage.Utilities.GetResourceContent(Assembly.GetExecutingAssembly(), "BeatSaverVoting.UI.votingUI.bsml"), resultsView.gameObject, this);
            resultsView.didActivateEvent += ResultsView_didActivateEvent;
            SetColors();
        }

        private static AnimationClip GenerateButtonAnimation(float r, float g, float b, float a, float x, float y) =>
            GenerateButtonAnimation(
                AnimationCurve.Constant(0, 1, r),
                AnimationCurve.Constant(0, 1, g),
                AnimationCurve.Constant(0, 1, b),
                AnimationCurve.Constant(0, 1, a),
                AnimationCurve.Constant(0, 1, x),
                AnimationCurve.Constant(0, 1, y)
            );

        private static AnimationClip GenerateButtonAnimation(AnimationCurve r, AnimationCurve g, AnimationCurve b, AnimationCurve a, AnimationCurve x, AnimationCurve y)
        {
            var animation = new AnimationClip { legacy = true };

            animation.SetCurve("Icon", typeof(Transform), "localScale.x", x);
            animation.SetCurve("Icon", typeof(Transform), "localScale.y", y);
            animation.SetCurve("Icon", typeof(Graphic), "m_Color.r", r);
            animation.SetCurve("Icon", typeof(Graphic), "m_Color.g", g);
            animation.SetCurve("Icon", typeof(Graphic), "m_Color.b", b);
            animation.SetCurve("Icon", typeof(Graphic), "m_Color.a", a);

            return animation;
        }

        private static void SetupButtonAnimation(Component t, Color c)
        {
            var anim = t.GetComponent<ButtonStaticAnimations>();

            anim.SetField("_normalClip", GenerateButtonAnimation(c.r, c.g, c.b, 0.502f, 1, 1));
            anim.SetField("_highlightedClip", GenerateButtonAnimation(c.r, c.g, c.b, 1, 1.5f, 1.5f));
        }

        private void SetColors()
        {
            var upArrow = upButton.GetComponentInChildren<ImageView>();
            var downArrow = downButton.GetComponentInChildren<ImageView>();

            if (upArrow == null || downArrow == null) return;

            SetupButtonAnimation(upButton, new Color(0.341f, 0.839f, 0.341f));
            SetupButtonAnimation(downButton, new Color(0.984f, 0.282f, 0.305f));
        }

        private void ResultsView_didActivateEvent(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            GetVotesForMap();
        }

        [UIAction("up-pressed")]
        private void UpvoteButtonPressed()
        {
            VoteForSong(_lastBeatSaverSong, true, UpdateUIAfterVote);
        }
        [UIAction("down-pressed")]
        private void DownvoteButtonPressed()
        {
            VoteForSong(_lastBeatSaverSong, false, UpdateUIAfterVote);
        }

        private void GetVotesForMap()
        {
            var isCustomLevel = lastSong is CustomPreviewBeatmapLevel;
            downButton.gameObject.SetActive(isCustomLevel);
            upButton.gameObject.SetActive(isCustomLevel);
            voteTitle.gameObject.SetActive(isCustomLevel);
            voteText.text = isCustomLevel ? "Loading..." : "";

            if (isCustomLevel)
            {
                StartCoroutine(GetRatingForSong(lastSong));
            }
        }

        private IEnumerator GetSongInfo(string hash)
        {
            var www = UnityWebRequest.Get($"{Plugin.BeatsaverURL}/maps/hash/{hash.ToLower()}");
            www.SetRequestHeader("user-agent", _userAgent);

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
            {
                Logging.log.Error($"Unable to connect to {Plugin.BeatsaverURL}! " +
                                  (www.result == UnityWebRequest.Result.ConnectionError ? $"Network error: {www.error}" :
                                      (www.result == UnityWebRequest.Result.ProtocolError ? $"HTTP error: {www.error}" : "Unknown error")));
            }
            else
            {
                Song result = null;
                try
                {
                    var jNode = JObject.Parse(www.downloadHandler.text);
                    if (jNode.Children().Any())
                    {
                        result = new Song(jNode);
                    }
                    else
                    {
                        Logging.log.Error("Song doesn't exist on BeatSaver!");
                    }
                }
                catch (Exception e)
                {
                    Logging.log.Critical("Unable to get song rating! Excpetion: " + e);
                }

                yield return result;
            }
        }

        private IEnumerator GetRatingForSong(IBeatmapLevel level)
        {
            if (!(level is CustomPreviewBeatmapLevel cpblLevel)) yield break;

            var cd = new CoroutineWithData(this, GetSongInfo(SongCore.Utilities.Hashing.GetCustomLevelHash(cpblLevel)));
            yield return cd.Coroutine;

            try
            {
                _lastBeatSaverSong = null;

                if (!(cd.result is Song song)) yield break;

                _lastBeatSaverSong = song;

                voteText.text = GetScoreFromVotes(_lastBeatSaverSong.upVotes, _lastBeatSaverSong.downVotes);

                var canVote = XRSettings.loadedDeviceName.IndexOf("oculus", StringComparison.OrdinalIgnoreCase) >= 0 ||
                              XRSettings.loadedDeviceName.IndexOf("openxr", StringComparison.OrdinalIgnoreCase) >= 0 ||
                              Environment.CommandLine.ToLower().Contains("-vrmode oculus") || Environment.CommandLine.ToLower().Contains("fpfc");

                UpInteractable = canVote;
                DownInteractable = canVote;

                if (!(lastSong is CustomPreviewBeatmapLevel cpblLastSong)) yield break;
                var lastLevelHash = SongCore.Utilities.Hashing.GetCustomLevelHash(cpblLastSong).ToLower();

                if (!Plugin.votedSongs.TryGetValue(lastLevelHash, out var voteInfo)) yield break;

                if (voteInfo.voteType == Plugin.VoteType.Upvote)
                {
                    UpInteractable = false;
                }
                else if (voteInfo.voteType == Plugin.VoteType.Downvote)
                {
                    DownInteractable = false;
                }
            }
            catch (Exception e)
            {
                Logging.log.Critical("Unable to get song rating! Excpetion: " + e);
            }
        }

        internal void VoteForSong(string hash, bool upvote, VoteCallback callback)
        {
            StartCoroutine(VoteForSongAsync(hash, upvote, callback));
        }

        private IEnumerator VoteForSongAsync(string hash, bool upvote, VoteCallback callback)
        {
            var cd = new CoroutineWithData(this, GetSongInfo(hash));
            yield return cd.Coroutine;

            if (cd.result is Song song)
                VoteForSong(song, upvote, callback);
        }

        private void VoteForSong(Song song, bool upvote, VoteCallback callback)
        {
            if (song == null)
            {
                callback?.Invoke(null, false, false, -1);
                return;
            }

            var userTotal = Plugin.votedSongs.ContainsKey(song.hash) ? (Plugin.votedSongs[song.hash].voteType == Plugin.VoteType.Upvote ? 1 : -1) : 0;
            var oldValue = song.upVotes - song.downVotes - userTotal;
            VoteForSong(song.hash, upvote, oldValue, callback);
        }

        private void VoteForSong(string hash, bool upvote, int currentVoteCount, VoteCallback callback)
        {
            try
            {
                var flag1 = File.Exists(Path.Combine(UnityGame.InstallPath, "Beat Saber_Data\\Plugins\\x86_64\\steam_api64.dll"));
                if (XRSettings.loadedDeviceName.IndexOf("oculus", StringComparison.OrdinalIgnoreCase) >= 0 || !flag1)
                {
                    StartCoroutine(VoteWithOculusID(hash, upvote, currentVoteCount, callback));
                }
                else if (XRSettings.loadedDeviceName.IndexOf("openxr", StringComparison.OrdinalIgnoreCase) >= 0 ||
                          Environment.CommandLine.ToLower().Contains("-vrmode oculus") || Environment.CommandLine.ToLower().Contains("fpfc"))
                {
                    StartCoroutine(VoteWithSteamID(hash, upvote, currentVoteCount, callback));
                }
            }
            catch(Exception ex)
            {
                Logging.log.Warn("Failed To Vote For Song " + ex.Message);
            }

        }

        private IEnumerator VoteWithOculusID(string hash, bool upvote, int currentVoteCount, VoteCallback callback)
        {
            UpdateView("Voting...");

            var task = Task.Run(async () =>
            {
                var a = await OculusHelper.Instance.GetUserId();
                var b = await OculusHelper.Instance.GetToken();

                return (a, b);
            });

            yield return new WaitUntil(() => task.IsCompleted);
            var (oculusId, nonce) = task.Result;

            yield return PerformVote(hash, new Payload { auth = new Auth {oculusId = oculusId.ToString(), proof = nonce}, direction = upvote, hash = hash}, currentVoteCount, callback);
        }

        private IEnumerator VoteWithSteamID(string hash, bool upvote, int currentVoteCount, VoteCallback callback)
        {
            if (!SteamManager.Initialized)
            {
                Logging.log.Error("SteamManager is not initialized!");
            }

            UpdateView("Voting...");

            var steamId = SteamUser.GetSteamID();

            var task = Task.Run(async () => await SteamHelper.Instance.GetToken());
            yield return new WaitUntil(() => task.IsCompleted);
            var authTicketHexString = task.Result;

            if (authTicketHexString == null)
            {
                UpdateView("Auth\nfailed");

                callback?.Invoke(hash, false, false, -1);
                yield break;
            }

            yield return PerformVote(hash, new Payload {auth = new Auth {steamId = steamId.m_SteamID.ToString(), proof = authTicketHexString}, direction = upvote, hash = hash}, currentVoteCount, callback);
        }

        private readonly Dictionary<long, string> _errorMessages = new Dictionary<long, string>
        {
            {500, "Server \nerror"},
            {401, "Invalid\nauth ticket"},
            {404, "Beatmap not\nfound"},
            {400, "Bad\nrequest"}
        };

        private IEnumerator PerformVote(string hash, Payload payload, int currentVoteCount, VoteCallback callback)
        {
            Logging.log.Debug($"Voting BM...");
            var json = JsonConvert.SerializeObject(payload);
            var voteWWW = UnityWebRequest.Post($"{Plugin.BeatsaverURL}/vote", json);

            var jsonBytes = new System.Text.UTF8Encoding().GetBytes(json);
            voteWWW.uploadHandler = new UploadHandlerRaw(jsonBytes);
            voteWWW.SetRequestHeader("Content-Type", "application/json");
            voteWWW.SetRequestHeader("user-agent", _userAgent);
            voteWWW.timeout = 30;
            yield return voteWWW.SendWebRequest();

            if (voteWWW.result == UnityWebRequest.Result.ConnectionError)
            {
                Logging.log.Error(voteWWW.error);
                callback?.Invoke(hash, false, false, currentVoteCount);
            }
            else if (voteWWW.responseCode < 200 || voteWWW.responseCode > 299)
            {
                var errorMessage = _errorMessages[voteWWW.responseCode] ?? "Error\n" + voteWWW.responseCode;
                UpdateView(errorMessage, !_errorMessages.ContainsKey(voteWWW.responseCode));

                Logging.log.Error("Error: " + voteWWW.downloadHandler.text);
                callback?.Invoke(hash, false, false, currentVoteCount);
            } else {
                Logging.log.Debug($"Current vote count: {currentVoteCount}, new total: {currentVoteCount + (payload.direction ? 1 : -1)}");
                callback?.Invoke(hash, true, payload.direction, currentVoteCount + (payload.direction ? 1 : -1));
            }
        }

        private void UpdateView(string text, bool up = false, bool? down = null)
        {
            UpInteractable = up;
            DownInteractable = down ?? up;
            voteText.text = text;
        }

        private static string GetScoreFromVotes(int upVotes, int downVotes)
        {
            double totalVotes = upVotes + downVotes;
            var rawScore = upVotes / totalVotes;
            var scoreWeighted = rawScore - (rawScore - 0.5) * Math.Pow(2.0, -Math.Log(totalVotes / 2 + 1, 3.0));

            return $"{scoreWeighted:0.#%} ({totalVotes})";
        }

        private void UpdateUIAfterVote(string hash, bool success, bool upvote, int newTotal) {
            if (!success) return;

            var hasPreviousVote = Plugin.votedSongs.ContainsKey(hash);

            UpInteractable = !upvote;
            DownInteractable = upvote;

            if (hash == _lastBeatSaverSong.hash)
            {
                if (hasPreviousVote)
                {
                    var diff = upvote ? 1 : -1;
                    _lastBeatSaverSong.upVotes += diff;
                    _lastBeatSaverSong.downVotes += -diff;
                }
                else if (upvote)
                {
                    _lastBeatSaverSong.upVotes += 1;
                }
                else
                {
                    _lastBeatSaverSong.downVotes += 1;
                }

                voteText.text = GetScoreFromVotes(_lastBeatSaverSong.upVotes, _lastBeatSaverSong.downVotes);
            }
            else
            {
                // Fallback to total
                voteText.text = newTotal.ToString();
            }

            if (!Plugin.votedSongs.ContainsKey(hash) || Plugin.votedSongs[hash].voteType != (upvote ? Plugin.VoteType.Upvote : Plugin.VoteType.Downvote))
            {
                Plugin.votedSongs[hash] = new Plugin.SongVote(hash, upvote ? Plugin.VoteType.Upvote : Plugin.VoteType.Downvote);
                Plugin.WriteVotes();
                Plugin.tableView.RefreshCellsContent();
            }
        }
    }
}

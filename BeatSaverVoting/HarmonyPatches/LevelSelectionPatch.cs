using HarmonyLib;
using UnityEngine;

namespace BeatSaverVoting.HarmonyPatches
{
    [HarmonyPatch(typeof(LevelListTableCell))]
    [HarmonyPatch("SetDataFromLevelAsync", MethodType.Normal)]
    internal class LevelListTableCellSetDataFromLevel
    {
        private static void Postfix(BeatmapLevel beatmapLevel, bool isFavorite, ref UnityEngine.UI.Image ____favoritesBadgeImage)
        {
            var hash = SongCore.Collections.GetCustomLevelHash(beatmapLevel.levelID).ToLower();
            var voteStatus = Plugin.CurrentVoteStatus(hash);

            if (voteStatus == null && !isFavorite)
            {
                return;
            }

            ____favoritesBadgeImage.enabled = true;

            switch (voteStatus)
            {
                case Plugin.VoteType.Upvote:
                    ____favoritesBadgeImage.sprite = isFavorite ? Plugin.favoriteUpvoteIcon : Plugin.upvoteIcon;
                    break;
                case Plugin.VoteType.Downvote:
                    ____favoritesBadgeImage.sprite = isFavorite ? Plugin.favoriteDownvoteIcon : Plugin.downvoteIcon;
                    break;
                default:
                    ____favoritesBadgeImage.sprite = Plugin.favoriteIcon;
                    break;
            }

            if (____favoritesBadgeImage.rectTransform.sizeDelta.x < 3.5f)
            {
                ____favoritesBadgeImage.rectTransform.sizeDelta = new Vector2(3.5f, 7);
                var transform = ____favoritesBadgeImage.transform;
                var position = transform.localPosition;
                position = new Vector3(position.x + 1.0f, position.y, position.z);
                transform.localPosition = position;
            }
        }
    }
}

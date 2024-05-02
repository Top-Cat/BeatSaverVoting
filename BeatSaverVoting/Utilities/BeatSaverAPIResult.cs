using System;
using Newtonsoft.Json.Linq;

namespace BeatSaverVoting.Utilities
{
    [Serializable]
    public class Song
    {
        public int upVotes;
        public int downVotes;
        public string key;
        public string hash;

        public Song()
        {

        }

        public Song(JObject jsonNode)
        {
            upVotes = (int)jsonNode["stats"]["upvotes"];
            downVotes = (int)jsonNode["stats"]["downvotes"];

            hash = ((string)jsonNode["versions"][0]?["hash"])?.ToLower();

            key = (string)jsonNode["id"];
        }


        public bool Compare(Song compareTo)
        {
            return compareTo.hash == hash;
        }
    }
}

using System.Threading.Tasks;
using IPA.Utilities;
using Steamworks;

namespace BeatSaverVoting.Utilities
{
    public class SteamHelper
    {
        private static SteamHelper _instance;
        public static SteamHelper Instance => _instance ?? (_instance = new SteamHelper());

        private readonly SteamPlatformUserModel _userModel = new SteamPlatformUserModel();
        private readonly SteamInit _steamInit = new SteamInit();

        private SteamHelper()
        {
            _userModel.SetField<SteamPlatformUserModel, IPlatformInit>("_platformInit", _steamInit);
        }

        public async Task<string> GetToken()
        {
            _steamInit.Initialize();
            return (await _userModel.GetUserAuthToken()).token;
        }
    }

    public class SteamInit : BasePlatformInit
    {
        protected override Task<bool> InitializeInternalAsync()
        {
            return Task.FromResult(SteamAPI.Init());
        }
    }
}

using System.Threading;
using System.Threading.Tasks;
using Oculus.Platform;

namespace BeatSaverVoting.Utilities
{
    public class OculusHelper
    {
        private static OculusHelper _instance;
        public static OculusHelper Instance => _instance ?? (_instance = new OculusHelper());
        private readonly OculusPlatformUserModel _userModel = new OculusPlatformUserModel(new NoInit());
        private ulong _userId;

        public async Task<ulong> GetUserId()
        {
            // Cache user id
            if (_userId != 0) return _userId;

            if (!Core.IsInitialized()) Core.Initialize();
            var user = await _userModel.GetUserInfo(CancellationToken.None);
            ulong.TryParse(user.platformUserId, out _userId);

            return _userId;
        }

        public async Task<string> GetToken()
        {
            if (!Core.IsInitialized()) Core.Initialize();
            return (await _userModel.GetUserAuthToken()).token;
        }
    }

    public class NoInit : BasePlatformInit
    {
        protected override Task<bool> InitializeInternalAsync()
        {
            return Task.FromResult(true);
        }
    }
}

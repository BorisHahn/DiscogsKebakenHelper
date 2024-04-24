using DiscogsKebakenHelper.Model;
using RestSharp.Authenticators;
using RestSharp;
using DiscogsKebakenHelper.Data;

namespace DiscogsKebakenHelper
{
    public static class DeleteOutsideProcess
    {
        public async static void Delete(long chatId, string releaseId, string instanceId)
        {
            User? user;
            using (PostgresContext db = new())
            {
                user = UserData.GetUser(db, (int)chatId);
            }

            var client = new RestClient($"https://api.discogs.com/users/{user.UserName}/collection/folders/1/releases/{releaseId}/instances/{instanceId}")
            {
                Authenticator = OAuth1Authenticator.ForProtectedResource(AppConfiguration.ConsumerKey, AppConfiguration.ConsumerSecret, $"{user.OauthToken}", $"{user.OauthTokenSecret}")
            };

            var request = new RestRequest(Method.DELETE);
            IRestResponse response = client.Execute(request);

            if (response.IsSuccessful) 
            {
                using (PostgresContext db = new())
                {
                    ReleaseData.DeleteRelease(db, instanceId);
                }
            }
        }
    }
}

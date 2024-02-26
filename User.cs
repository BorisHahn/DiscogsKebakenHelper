using DiscogsClient;
using RestSharpHelper.OAuth1;

namespace DiscogsKebakenHelper;

public class User : IUser
{
    public long ChatId { get; set; }
    public ChatMode ChatMode { get; set; }
    public string UserName { get; set; }
    public string OauthToken { get; set; }
    public string OauthTokenSecret { get; set; }
    public DiscogsAuthentifierClient DiscogsClient { get; set; }
    public string UserRequestToken { get; set; }

    public User(long chatId, ChatMode chatMode, string userName, string oauthToken, string oauthTokenSecret,
        DiscogsAuthentifierClient discogClient, string userRequestToken)
    {
        ChatId = chatId;
        ChatMode = chatMode;
        UserName = userName;
        OauthToken = oauthToken;
        OauthTokenSecret = oauthTokenSecret;
        DiscogsClient = discogClient;
        UserRequestToken = userRequestToken;
    }
}
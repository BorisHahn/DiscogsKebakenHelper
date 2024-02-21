namespace DiscogsKebakenHelper;

public interface IUser
{
    long ChatId { get; set; }
    ChatMode ChatMode { get; set; }
    string UserName { get; set; }
    string OauthToken { get; set; }
    string OauthTokenSecret { get; set; }
}
    
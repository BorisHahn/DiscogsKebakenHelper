using System;
using System.Collections.Generic;

namespace DiscogsKebakenHelper.Model;

public class User
{
    public int Uid { get; set; }

    public int? ChatId { get; set; }

    public string? ChatMode { get; set; }

    public string? UserName { get; set; } = null;

    public string? OauthToken { get; set; } = null;

    public string? OauthTokenSecret { get; set; } = null;

    public string? UserRequestToken { get; set; } = null;
}

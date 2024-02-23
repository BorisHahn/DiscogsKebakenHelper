using Telegram.Bot;
using Telegram.Bot.Types;

namespace DiscogsKebakenHelper;

public enum SearchState
{
    Initial = 0,
    setValue = 1,
    getSearchResult = 2,
};

public class SearchModeState
{
    public SearchState Mode { get; set; }
}

public class SearchProcess
{
    public Dictionary<long, SearchModeState> ChatDict { get; set; } = new();
    private IUser _user;
    public async Task StartSearchProcess(ITelegramBotClient client, Update update, IUser currentUser, CancellationToken ct)
    {
        if (!ChatDict.TryGetValue(update.Message!.Chat.Id, out var state))
        {
            ChatDict.Add(update.Message!.Chat.Id, new SearchModeState());
        }

        state = ChatDict[update.Message!.Chat.Id];
        
        switch (state.Mode)
        {
           case SearchState.Initial:
               await client.SendTextMessageAsync(
                   chatId: update.Message.Chat.Id,
                   text: "Введите название композиции:",
                   cancellationToken: ct);
               currentUser.ChatMode = ChatMode.AskMenuCommand;
               break;
        }
    }
}
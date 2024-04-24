using DiscogsClient.Data.Query;
using DiscogsKebakenHelper.Data;
using DiscogsKebakenHelper.Model;
using RestSharpHelper.OAuth1;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace DiscogsKebakenHelper
{ 

    public enum SearchInDbState
    {
        initial = 0,
        setArtist = 1,
        setReleaseTitle = 2,
        getSearchResult = 3,
    };

    public class SearchInDbModeState
    {
        public SearchInDbState Mode { get; set; }
    }

    public class SearchProcessInDB
    {
        public string Artist { get; set; }
        public string ReleaseTitle { get; set; }
        public Dictionary<long, SearchInDbModeState> ChatDict { get; set; } = new();

        public async Task StartSearchInDbProcess(ITelegramBotClient client, Update update, User currentUser,
            CancellationToken ct, OAuthConsumerInformation oAuthConsumerInformation)
        {
            if (!ChatDict.TryGetValue(update.Message!.Chat.Id, out var state))
            {
                ChatDict.Add(update.Message!.Chat.Id, new SearchInDbModeState());
            }

            state = ChatDict[update.Message!.Chat.Id];

            if (update.Message.Text == "/searchDB")
            {
                state.Mode = SearchInDbState.initial;
            }
            switch (state.Mode)
            {
                case SearchInDbState.initial:
                    await client.SendTextMessageAsync(
                        chatId: update.Message.Chat.Id,
                        text: "Введите наименование исполнителя:",
                        cancellationToken: ct);
                    state.Mode = SearchInDbState.setArtist;
                    break;
                case SearchInDbState.setArtist:
                    Artist = update.Message.Text;
                    await client.SendTextMessageAsync(
                        chatId: update.Message.Chat.Id,
                        text: "Введите наименование релиза:",
                        cancellationToken: ct);
                    state.Mode = SearchInDbState.setReleaseTitle;
                    break;
                case SearchInDbState.setReleaseTitle:
                    ReleaseTitle = update.Message.Text;
                    await client.SendTextMessageAsync(
                        chatId: update.Message.Chat.Id,
                        text: $"Вот, что удалось найти в вашей базе по заданному запросу:\n{$"{Artist} " + '–' + $" {ReleaseTitle}"}",
                        cancellationToken: ct);
                    SearchInDb(client, update, ct, Artist, ReleaseTitle, currentUser, state);
                    state.Mode = SearchInDbState.initial;
                    break;
            }
        }
        async void SearchInDb(ITelegramBotClient TelegramClient, Update update,
        CancellationToken ct, string artist, string releaseTitle, User user, SearchInDbModeState state)
        {
            List<Release> res;
            using (PostgresContext db = new())
            {
                res = ReleaseData.GetRelease(db, artist, releaseTitle);
            }
            if (res.Count == 0)
            {
                await TelegramClient.SendTextMessageAsync(
                    chatId: update.Message.Chat.Id,
                    text: "Ничего не нашли по заданному запросу. Повторите ещё раз",
                    cancellationToken: ct);
                await TelegramClient.SendTextMessageAsync(
                    chatId: update.Message.Chat.Id,
                    text: "Введите наименование исполнителя:",
                    cancellationToken: ct);
                state.Mode = SearchInDbState.setArtist;

                using (PostgresContext db = new())
                {
                    UserData.UpdateUser(db, new User
                    {
                        Uid = user.Uid,
                        ChatId = user.ChatId,
                        ChatMode = Enums.chatMode[6],
                        OauthToken = user.OauthToken,
                        OauthTokenSecret = user.OauthTokenSecret,
                        UserName = user.UserName,
                        UserRequestToken = user.UserRequestToken
                    });
                }
            }
            else
            {
                foreach (var r in res)
                {
                    InlineKeyboardMarkup deleteKeyboard = new(new[]
                    {
                        new []
                        {
                            InlineKeyboardButton.WithCallbackData(text: "Удалить", callbackData: $"delete,{r.ReleaseId},{r.InstanceId}"),
                        },
                        new []
                        {
                             InlineKeyboardButton.WithCallbackData(text: "Редактировать место хранения", callbackData: $"move,{r.InstanceId}"),
                        },

                    });
                    await TelegramClient.SendTextMessageAsync(
                        chatId: update.Message.Chat.Id,
                        text: $"{r.Thumb}\n" +
                              $"Наименование: {r.Artist} - {r.ReleaseName}\n" +
                              $"Год: {r.ReleaseYear}\n" +
                              $"Место хранение: {r.StoragePlace}\n" +
                              $"Id: {r.ReleaseId}\n" +
                              $"InstanceId: {r.InstanceId}\n",
                        replyMarkup: deleteKeyboard,
                        cancellationToken: ct);
                }

                using (PostgresContext db = new())
                {
                    UserData.UpdateUser(db, new User
                    {
                        Uid = user.Uid,
                        ChatId = user.ChatId,
                        ChatMode = Enums.chatMode[1],
                        OauthToken = user.OauthToken,
                        OauthTokenSecret = user.OauthTokenSecret,
                        UserName = user.UserName,
                        UserRequestToken = user.UserRequestToken
                    });
                }
            }
        }
    }

}

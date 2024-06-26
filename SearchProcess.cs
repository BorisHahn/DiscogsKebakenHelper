﻿using DiscogsClient.Data.Query;
using DiscogsKebakenHelper.Data;
using DiscogsKebakenHelper.Model;
using RestSharpHelper.OAuth1;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace DiscogsKebakenHelper
{ 

public enum SearchState
{
    initial = 0,
    setArtist = 1,
    setReleaseTitle = 2,
    getSearchResult = 3,
};

public class SearchModeState
{
    public SearchState Mode { get; set; }
}

    public class SearchProcess
    {
        public string Artist { get; set; }
        public string ReleaseTitle { get; set; }
        public Dictionary<long, SearchModeState> ChatDict { get; set; } = new();

        public async Task StartSearchProcess(ITelegramBotClient client, Update update, User currentUser,
            CancellationToken ct, OAuthConsumerInformation oAuthConsumerInformation)
        {
            if (!ChatDict.TryGetValue(update.Message!.Chat.Id, out var state))
            {
                ChatDict.Add(update.Message!.Chat.Id, new SearchModeState());
            }

            state = ChatDict[update.Message!.Chat.Id];

            if (update.Message.Text == "/search")
            {
                state.Mode = SearchState.initial;
            }
            switch (state.Mode)
            {
                case SearchState.initial:
                    await client.SendTextMessageAsync(
                        chatId: update.Message.Chat.Id,
                        text: "Введите наименование исполнителя:",
                        cancellationToken: ct);
                    state.Mode = SearchState.setArtist;
                    break;
                case SearchState.setArtist:
                    Artist = update.Message.Text;
                    await client.SendTextMessageAsync(
                        chatId: update.Message.Chat.Id,
                        text: "Введите наименование релиза:",
                        cancellationToken: ct);
                    state.Mode = SearchState.setReleaseTitle;
                    break;
                case SearchState.setReleaseTitle:
                    ReleaseTitle = update.Message.Text;
                    await client.SendTextMessageAsync(
                        chatId: update.Message.Chat.Id,
                        text: $"Вот, что удалось найти по заданному запросу:\n{$"{Artist} " + '–' + $" {ReleaseTitle}"}",
                        cancellationToken: ct);
                    OAuthCompleteInformation oauthInform = new OAuthCompleteInformation(oAuthConsumerInformation,
                        currentUser.OauthToken,
                        currentUser.OauthTokenSecret);
                    var _DiscogsClient = new DiscogsClient.DiscogsClient(oauthInform);
                    Search(_DiscogsClient, client, update, ct, Artist, ReleaseTitle, currentUser, state);
                    state.Mode = SearchState.initial;
                    break;
            }
        }

        async void Search(DiscogsClient.DiscogsClient client, ITelegramBotClient TelegramClient, Update update,
            CancellationToken ct, string artist, string releaseTitle, User user, SearchModeState state)
        {

            var discogsSearch = new DiscogsSearch()
            {
                artist = artist,
                release_title = releaseTitle,
                format = "Vinyl"
            };
            var res = client.SearchAsync(discogsSearch).Result;
            if (res.GetResults().Length == 0)
            {
                await TelegramClient.SendTextMessageAsync(
                    chatId: update.Message.Chat.Id,
                    text: "Ничего не нашли по заданному запросу. Повторите ещё раз",
                    cancellationToken: ct);
                await TelegramClient.SendTextMessageAsync(
                    chatId: update.Message.Chat.Id,
                    text: "Введите наименование исполнителя:",
                    cancellationToken: ct);
                state.Mode = SearchState.setArtist;

                using (PostgresContext db = new())
                {
                    UserData.UpdateUser(db, new User
                    {
                        Uid = user.Uid,
                        ChatId = user.ChatId,
                        ChatMode = Enums.chatMode[3],
                        OauthToken = user.OauthToken,
                        OauthTokenSecret = user.OauthTokenSecret,
                        UserName = user.UserName,
                        UserRequestToken = user.UserRequestToken
                    });
                }

            }
            else
            {
                foreach (var searchResult in res.GetResults())
                {
                    var genreString = String.Join(", ", searchResult.format);
                    var formatString = String.Join(", ", searchResult.format);
                    InlineKeyboardMarkup addKeyboard = new(new[]
                    {
                        new []
                        {
                            InlineKeyboardButton.WithCallbackData(text: "Добавить", callbackData: $"add,{searchResult.id}"),
                        },
                    });
                    await TelegramClient.SendTextMessageAsync(
                        chatId: update.Message.Chat.Id,
                        text: $"{searchResult.thumb}\n" +
                              $"Наименование: {searchResult.title}\n" +
                              $"Страна: {searchResult.country}\n" +
                              $"Год: {searchResult.year}\n" +
                              $"Формат: {formatString}\n" +
                              $"Жанр: {genreString}\n" +
                              $"Пользователи добавили: {searchResult.community.have}\n" +
                              $"Пользователи хотят: {searchResult.community.want}\n",
                        replyMarkup: addKeyboard,
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
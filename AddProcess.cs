using RestSharpHelper.OAuth1;
using Telegram.Bot.Types;
using Telegram.Bot;
using RestSharp;
using RestSharp.Authenticators;
using System.Text.Json.Nodes;
using DiscogsKebakenHelper.Data;
using DiscogsKebakenHelper.Model;
using Telegram.Bot.Types.ReplyMarkups;

namespace DiscogsKebakenHelper
{

    public enum AddState
    {
        initial = 0,
        setReleaseId = 1,
        setStoragePlace = 2,
        addRelease = 3,
    };

    public class AddModeState
    {
        public AddState Mode { get; set; }
    }

    public class AddProcess
    {
        public string ReleaseId { get; set; }
        public string? StoragePlace { get; set; }
        public Dictionary<long, AddModeState> ChatDict { get; set; } = new();

        public async Task StartAddProcess(ITelegramBotClient client, Update update, User currentUser,
        CancellationToken ct, OAuthConsumerInformation oAuthConsumerInformation)
        {

            if (!ChatDict.TryGetValue(update.Message!.Chat.Id, out var state))
            {
                ChatDict.Add(update.Message!.Chat.Id, new AddModeState());
            }
            state = ChatDict[update.Message!.Chat.Id];

            if (update.Message.Text == "/add")
            {
                state.Mode = AddState.initial;
            }

            switch (state.Mode)
            {
                case AddState.initial:
                    await client.SendTextMessageAsync(
                        chatId: update.Message.Chat.Id,
                        text: "Введите Id релиза для добавления:",
                        cancellationToken: ct);
                    state.Mode = AddState.setReleaseId;
                    break;
                case AddState.setReleaseId:
                    ReplyKeyboardMarkup replyKeyboardMarkup = new(new[]
                    {
                    new KeyboardButton[] { "Да", "Нет" },

                });
                    ReleaseId = update.Message.Text;
                    await client.SendTextMessageAsync(
                        chatId: update.Message.Chat.Id,
                        text: "Добавить физическое место хранения винила?\nПример: Там, где свален весь винил",
                        replyMarkup: replyKeyboardMarkup,
                        cancellationToken: ct);
                    state.Mode = AddState.setStoragePlace;
                    break;
                case AddState.setStoragePlace:
                    switch (update.Message.Text)
                    {
                        case "Да":
                            await client.SendTextMessageAsync(
                                chatId: update.Message.Chat.Id,
                                text: "Где будете хранить?",
                                replyMarkup: new ReplyKeyboardRemove(),
                                cancellationToken: ct);
                            state.Mode = AddState.addRelease;
                            break;
                        case "Нет":
                            StoragePlace = null;
                            await client.SendTextMessageAsync(
                                chatId: update.Message.Chat.Id,
                                text: "",
                                replyMarkup: new ReplyKeyboardRemove(),
                                cancellationToken: ct);
                            Add(client, update, ct, ReleaseId, StoragePlace, currentUser, state);
                            state.Mode = AddState.initial;
                            break;
                    }
                    break;
                case AddState.addRelease:
                    StoragePlace = update.Message.Text;
                    Add(client, update, ct, ReleaseId, StoragePlace, currentUser, state);
                    state.Mode = AddState.initial;
                    break;
            }
        }

        async void Add(ITelegramBotClient TelegramClient, Update update,
            CancellationToken ct, string releaseId, string? storagePlace, User user, AddModeState state)
        {
            var clientTest = new RestClient($"https://api.discogs.com/users/{user.UserName}/collection/folders/1/releases/{releaseId}")
            {
                Authenticator = OAuth1Authenticator.ForProtectedResource(AppConfiguration.ConsumerKey, AppConfiguration.ConsumerSecret, user.OauthToken, user.OauthTokenSecret)
            };

            var request = new RestRequest(Method.POST);
            IRestResponse response = clientTest.Execute(request);

            if (response.IsSuccessful)
            {
                var test = response.Content;
                var jsonObject = JsonNode.Parse(test);
                using (PostgresContext db = new())
                {
                    ReleaseData.AddRelease(db, new Release
                    {
                        ChatId = user.ChatId,
                        Artist = $"{jsonObject["basic_information"]["artists"][0]["name"]}",
                        ReleaseName = $"{jsonObject["basic_information"]["title"]}",
                        ReleaseYear = $"{jsonObject["basic_information"]["year"]}",
                        ReleaseId = $"{jsonObject["id"]}",
                        InstanceId = $"{jsonObject["instance_id"]}",
                        StoragePlace = storagePlace,
                        Thumb = $"{jsonObject["basic_information"]["thumb"]}"
                    });
                }
                await TelegramClient.SendTextMessageAsync(
                    chatId: update.Message.Chat.Id,
                    text: "Релиз успешно добавлен в коллекцию!",
                    cancellationToken: ct);
                await TelegramClient.SendTextMessageAsync(
                        chatId: update.Message.Chat.Id,
                        text:
                              $"{jsonObject["basic_information"]["thumb"]}\n" +
                              $"Aртист: {jsonObject["basic_information"]["artists"][0]["name"]}\n" +
                              $"Наименование релиза: {jsonObject["basic_information"]["title"]}\n" +
                              $"Год: {jsonObject["basic_information"]["year"]}\n" +
                              $"Место хранение: {(storagePlace != null ? storagePlace : "Место хранения не указано")}\n" +
                              $"Id: {jsonObject["id"]}\n" +
                              $"InstanceId: {jsonObject["instance_id"]}\n",
                        cancellationToken: ct);
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
                        UserRequestToken = user.UserRequestToken,
                    });
                }
            }
            else
            {
                await TelegramClient.SendTextMessageAsync(
                    chatId: update.Message.Chat.Id,
                    text: "Не удалось добавить релиз c указанным Id. Попробуйте ещё раз!",
                    cancellationToken: ct);
                await TelegramClient.SendTextMessageAsync(
                    chatId: update.Message.Chat.Id,
                    text: "Введите Id релиза для добавления:",
                    cancellationToken: ct);
                state.Mode = AddState.setReleaseId;
            }
        }
    }
}


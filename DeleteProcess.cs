using RestSharpHelper.OAuth1;
using Telegram.Bot.Types;
using Telegram.Bot;
using RestSharp;
using RestSharp.Authenticators;
using User = DiscogsKebakenHelper.Model.User;
using System.Text.Json.Nodes;
using DiscogsKebakenHelper.Data;
using DiscogsKebakenHelper.Model;


namespace DiscogsKebakenHelper
{
    public enum DeleteState
    {
        initial = 0,
        setReleaseId = 1,
        setInstanceId = 2,
    };

    public class DeleteModeState
    {
        public DeleteState Mode { get; set; }
    }

    public class DeleteProcess
    {
        public string ReleaseId { get; set; }
        public string InstanceId { get; set; }
        public Dictionary<long, DeleteModeState> ChatDict { get; set; } = new();

        public async Task StartDeleteProcess(ITelegramBotClient client, Update update, User currentUser,
        CancellationToken ct, OAuthConsumerInformation oAuthConsumerInformation)
        {
            if (!ChatDict.TryGetValue(update.Message!.Chat.Id, out var state))
            {
                ChatDict.Add(update.Message!.Chat.Id, new DeleteModeState());
            }
            state = ChatDict[update.Message!.Chat.Id];

            if (update.Message.Text == "/delete")
            {
                state.Mode = DeleteState.initial;
            }

            switch (state.Mode)
            {
                case DeleteState.initial:
                    await client.SendTextMessageAsync(
                        chatId: update.Message.Chat.Id,
                        text: "Введите Id релиза:",
                        cancellationToken: ct);
                    state.Mode = DeleteState.setReleaseId;
                    break;
                case DeleteState.setReleaseId:
                    ReleaseId = update.Message.Text;
                    await client.SendTextMessageAsync(
                        chatId: update.Message.Chat.Id,
                        text: "Введите InstanceId релиза:",
                        cancellationToken: ct);
                    state.Mode = DeleteState.setInstanceId;
                    break;
                case DeleteState.setInstanceId:
                    InstanceId = update.Message.Text;
                    OAuthCompleteInformation oauthInform = new OAuthCompleteInformation(oAuthConsumerInformation,
                        currentUser.OauthToken,
                        currentUser.OauthTokenSecret);
                    var _DiscogsClient = new DiscogsClient.DiscogsClient(oauthInform);
                    Delete(_DiscogsClient, client, update, ct, ReleaseId, InstanceId, currentUser, state);
                    state.Mode = DeleteState.initial;
                    break;
            }
        }

        async void Delete(DiscogsClient.DiscogsClient client, ITelegramBotClient TelegramClient, Update update,
            CancellationToken ct, string releaseId, string instanceId, User user, DeleteModeState state)
        {
            var clientTest = new RestClient($"https://api.discogs.com/users/{user.UserName}/collection/folders/1/releases/{releaseId}/instances/{instanceId}")
            //var clientTest = new RestClient($"https://api.discogs.com/users/{user.UserName}/collection/folders/1/releases")
            {
                Authenticator = OAuth1Authenticator.ForProtectedResource(AppConfiguration.ConsumerKey, AppConfiguration.ConsumerSecret, user.OauthToken, user.OauthTokenSecret)
            };

            var request = new RestRequest(Method.DELETE);
            IRestResponse response = clientTest.Execute(request);

            Console.WriteLine(response.IsSuccessful);
            
            if (response.IsSuccessful)

              
            {
                /*var test = response.Content;
                var jsonObject = JsonNode.Parse(test);
                Console.WriteLine(jsonObject);*/
                await TelegramClient.SendTextMessageAsync(
                    chatId: update.Message.Chat.Id,
                    text: "Релиз успешно удален из коллекции!",
                    cancellationToken: ct);
                /*await TelegramClient.SendTextMessageAsync(
                        chatId: update.Message.Chat.Id,
                        text: $"{jsonObject["basic_information"]["thumb"]}\n" +
                              $"Aртист: {jsonObject["basic_information"]["artists"][0]["name"]}\n" +
                              $"Наименование релиза: {jsonObject["basic_information"]["title"]}\n" +
                              $"Год: {jsonObject["basic_information"]["year"]}\n",
                        cancellationToken: ct);*/
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
            else
            {
                await TelegramClient.SendTextMessageAsync(
                    chatId: update.Message.Chat.Id,
                    text: "Не удалось удалить релиз c указанным Id. Попробуйте ещё раз!",
                    cancellationToken: ct);
                await TelegramClient.SendTextMessageAsync(
                    chatId: update.Message.Chat.Id,
                    text: "Введите Id релиза для удаления:",
                    cancellationToken: ct);
                state.Mode = DeleteState.setReleaseId;
            }
        }
    }
}

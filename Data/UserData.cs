using DiscogsKebakenHelper.Model;

namespace DiscogsKebakenHelper.Data
{
    public class UserData
    {
        private readonly PostgresContext _context;
        public UserData(PostgresContext context)
        {
            _context = context;
        }
        public static void AddUser(PostgresContext context, User user)
        {
            context.Add(user);
            context.SaveChanges();
        }
        public static User? GetUser(PostgresContext context, int chatId)
        {
            return context.Users.FirstOrDefault(user => user.ChatId == chatId);
        }

        public static void UpdateUser(PostgresContext context, User user)
        {
            context.Users.Update(user);
            context.SaveChanges();
        }

    }
}

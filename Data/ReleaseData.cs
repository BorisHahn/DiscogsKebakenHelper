﻿using DiscogsKebakenHelper.Model;

namespace DiscogsKebakenHelper.Data
{
    public class ReleaseData
    {
        private readonly PostgresContext _context;
        public ReleaseData(PostgresContext context)
        {
            _context = context;
        }

        public static void AddRelease(PostgresContext context, Release release)
        {
            context.Add(release);
            context.SaveChanges();
        }

        public static List<Release> GetRelease(PostgresContext context, string artist, string releaseName)
        {
           return context.Releases.Where((r) => r.Artist.ToLower().Contains(artist.ToLower()) || r.ReleaseName.ToLower().Contains(releaseName.ToLower())).ToList();
        }
    }
}

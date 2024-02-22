using MediaClient.Shared.Models;

namespace MediaClient.Shared.Services
{
    public static class MediaItemServiceMock
    {
        public static List<MediaItem> Other =>
        [
            new()
            {
                Id = "1",
                Title = "Jujutsu Kaizen",
                Picture = "https://artworks.thetvdb.com/banners/series/377543/posters/5fdf40a06a554.jpg",
                AdditionalInformations = "S1E1 - Ryomen Sunkuna"
            },
            new()
            {
                Id = "2",
                Title = "Jujutsu Kaizen",
                Picture = "https://artworks.thetvdb.com/banners/series/377543/posters/5fdf40a06a554.jpgck",
                AdditionalInformations = "S1E2 - Episode 2",
                Progress = 87,
                Watched = false
            },
            new()
            {
                Id = "3",
                Title = "Code 8 Part II",
                Picture =
                    "https://image.tmdb.org/t/p/w600_and_h900_bestv2/hhvMTxlTZtnCOe7YFhod9uz3m37.jpg",
                AdditionalInformations = "2024",
                Watched = true
            },
        ];

        public static List<MediaItem> JamesBond =
        [
            new()
            {
                Id = "4",
                Title = "GoldenEye",
                Picture =
                    "https://www.themoviedb.org/t/p/w600_and_h900_bestv2/nnJc9Q9S1B8iOE03eh0hqxfr4qF.jpg",
                AdditionalInformations = "1995",
                Watched = true
            },
            new()
            {
                Id = "5",
                Title = "Demain ne meurt jamais",
                Picture =
                    "https://www.themoviedb.org/t/p/w600_and_h900_bestv2/6sHUh1Zvz5QXDJbSvGErfNatwBD.jpg",
                AdditionalInformations = "1998",
                Watched = false,
                Progress = 27
            },
            new()
            {
                Id = "6",
                Title = "Le monde ne suffit pas",
                Picture =
                    "https://www.themoviedb.org/t/p/w600_and_h900_bestv2/u0xYbB335NA518wHvdL2WeASzXh.jpg",
                AdditionalInformations = "1999",
                Watched = false
            }
        ];

        public static List<MediaItem> StarWars =
        [
            new()
            {
                Id = "4",
                Title = "La Guerre des étoiles",
                Picture =
                    "https://www.themoviedb.org/t/p/w600_and_h900_bestv2/qelTNHrBSYjPvwdzsDBPVsqnNzc.jpg",
                AdditionalInformations = "1977",
                Watched = true
            },
            new()
            {
                Id = "5",
                Title = "L'Empire contre-attaque",
                Picture =
                    "https://www.themoviedb.org/t/p/w600_and_h900_bestv2/qDvctAykmNWAmi9G2GrVrwWx3pr.jpg",
                AdditionalInformations = "1980",
                Watched = true,
            },
            new()
            {
                Id = "6",
                Title = "Le Retour du Jedi",
                Picture =
                    "https://www.themoviedb.org/t/p/w600_and_h900_bestv2/tEQlCGiiWvMvfD7Sz8d99Pouy39.jpg",
                AdditionalInformations = "1983",
                Watched = true
            },
            new()
            {
                Id = "6",
                Title = "Star Wars, épisode I - La Menace fantôme",
                Picture =
                    "https://www.themoviedb.org/t/p/w600_and_h900_bestv2/fpEC910v5DrMvZteRNlKzXeHiHY.jpg",
                AdditionalInformations = "1999",
                Progress = 57
            },
            new()
            {
                Id = "6",
                Title = "Star Wars, épisode II - L'Attaque des clones",
                Picture =
                    "https://media.themoviedb.org/t/p/w600_and_h900_bestv2/3nqpcTkODCBhuKuDQJ1dtRhgTqZ.jpg",
                AdditionalInformations = "2002",
                Progress = 0
            }
        ];
    }
}

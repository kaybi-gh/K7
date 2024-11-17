using K7.Clients.Shared.Domain.Models;

namespace K7.Clients.Shared.Services.MediaServer;

public static class MockedMediaServiceService
{
    public static List<MediaPosterViewModel> All => Other.Concat(JamesBond).Concat(StarWars).ToList();
    public static List<MediaPosterViewModel> Other =>
    [
        new()
        {
            Id = "1",
            Title = "Jujutsu Kaizen",
            PosterPictureHref = "https://artworks.thetvdb.com/banners/series/377543/posters/5fdf40a06a554.jpg",
            AdditionalInformations = "S1E1 - Ryomen Sunkuna"
        },
        new()
        {
            Id = "2",
            Title = "Jujutsu Kaizen",
            PosterPictureHref = "https://artworks.thetvdb.com/banners/series/377543/posters/5fdf40a06a554.jpg",
            AdditionalInformations = "S1E2 - Episode 2",
            Progress = 87,
            Watched = false
        },
        new()
        {
            Id = "3",
            Title = "Code 8 Part II",
            PosterPictureHref =
                "https://image.tmdb.org/t/p/w600_and_h900_bestv2/hhvMTxlTZtnCOe7YFhod9uz3m37.jpg",
            AdditionalInformations = "2024",
            Watched = true
        },
    ];

    public static List<MediaPosterViewModel> JamesBond =
    [
        new()
        {
            Id = "4",
            Title = "GoldenEye",
            PosterPictureHref =
                "https://www.themoviedb.org/t/p/w600_and_h900_bestv2/nnJc9Q9S1B8iOE03eh0hqxfr4qF.jpg",
            //BackgroundPicture = "https://artworks.thetvdb.com/banners/movies/765/backgrounds/765.jpg",
            AdditionalInformations = "1995",
            Watched = true,
            //Duration = 130,
            //Synopsis = "Dans les derniers jours de la guerre froide, James Bond et son collègue et ami Alec Trevelyan s’introduisent dans l’usine soviétique de gaz neurotoxique Arkangel, afin de la détruire. Les deux hommes sont découverts et, au cours d’une violente bagarre, Trevelyan est fait prisonnier et exécuté sous les yeux de Bond par le général soviétique Ourumov. Bond s’enfuit de façon spectaculaire. Il restera obsédé par son échec à sauver son ami. 9 ans plus tard, alors que l’Union soviétique est devenue une constellation de nations indépendantes, James Bond rencontre Xenia Onatopp, une superbe créature qui joue au chat et à la souris avec lui. La partie se révèle pleine de charme mais aussi pleine de dangers."
        },
        new()
        {
            Id = "5",
            Title = "Demain ne meurt jamais",
            PosterPictureHref =
                "https://www.themoviedb.org/t/p/w600_and_h900_bestv2/6sHUh1Zvz5QXDJbSvGErfNatwBD.jpg",
            AdditionalInformations = "1998",
            Watched = false,
            Progress = 27
        },
        new()
        {
            Id = "6",
            Title = "Le monde ne suffit pas",
            PosterPictureHref =
                "https://www.themoviedb.org/t/p/w600_and_h900_bestv2/u0xYbB335NA518wHvdL2WeASzXh.jpg",
            AdditionalInformations = "1999",
            Watched = false
        }
    ];

    public static List<MediaPosterViewModel> StarWars =
    [
        new()
        {
            Id = "4",
            Title = "La Guerre des étoiles",
            PosterPictureHref =
                "https://www.themoviedb.org/t/p/w600_and_h900_bestv2/qelTNHrBSYjPvwdzsDBPVsqnNzc.jpg",
            AdditionalInformations = "1977",
            Watched = true
        },
        new()
        {
            Id = "5",
            Title = "L'Empire contre-attaque",
            PosterPictureHref =
                "https://www.themoviedb.org/t/p/w600_and_h900_bestv2/qDvctAykmNWAmi9G2GrVrwWx3pr.jpg",
            AdditionalInformations = "1980",
            Watched = true,
        },
        new()
        {
            Id = "6",
            Title = "Le Retour du Jedi",
            PosterPictureHref =
                "https://www.themoviedb.org/t/p/w600_and_h900_bestv2/tEQlCGiiWvMvfD7Sz8d99Pouy39.jpg",
            AdditionalInformations = "1983",
            Watched = true
        },
        new()
        {
            Id = "6",
            Title = "Star Wars, épisode I - La Menace fantôme",
            PosterPictureHref =
                "https://www.themoviedb.org/t/p/w600_and_h900_bestv2/fpEC910v5DrMvZteRNlKzXeHiHY.jpg",
            AdditionalInformations = "1999",
            Progress = 57
        },
        new()
        {
            Id = "6",
            Title = "Star Wars, épisode II - L'Attaque des clones",
            PosterPictureHref =
                "https://media.themoviedb.org/t/p/w600_and_h900_bestv2/3nqpcTkODCBhuKuDQJ1dtRhgTqZ.jpg",
            AdditionalInformations = "2002",
            Progress = 0
        }
    ];
}

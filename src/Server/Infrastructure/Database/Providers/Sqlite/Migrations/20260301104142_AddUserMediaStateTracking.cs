using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace K7.Server.Infrastructure.Database.Providers.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddUserMediaStateTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    UserName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    PasswordHash = table.Column<string>(type: "TEXT", nullable: true),
                    SecurityStamp = table.Column<string>(type: "TEXT", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "TEXT", nullable: true),
                    PhoneNumber = table.Column<string>(type: "TEXT", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BackgroundTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    RequestType = table.Column<string>(type: "TEXT", nullable: false),
                    RequestData = table.Column<string>(type: "TEXT", nullable: false),
                    TargetEntityType = table.Column<string>(type: "TEXT", nullable: true),
                    TargetEntityId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    RetryCount = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxRetryCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackgroundTasks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Devices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceUniqueId = table.Column<string>(type: "TEXT", nullable: true),
                    DeviceName = table.Column<string>(type: "TEXT", nullable: true),
                    ClientType = table.Column<int>(type: "INTEGER", nullable: false),
                    DeviceType = table.Column<int>(type: "INTEGER", nullable: false),
                    OperatingSystem = table.Column<int>(type: "INTEGER", nullable: false),
                    OperatingSystemVersion = table.Column<string>(type: "TEXT", nullable: true),
                    DisplayHeight = table.Column<double>(type: "REAL", nullable: false),
                    DisplayWidth = table.Column<double>(type: "REAL", nullable: false),
                    NativeDeviceDetails_RawModel = table.Column<string>(type: "TEXT", nullable: true),
                    NativeDeviceDetails_RawManufacturer = table.Column<string>(type: "TEXT", nullable: true),
                    NativeDeviceDetails_RawName = table.Column<string>(type: "TEXT", nullable: true),
                    NativeDeviceDetails_RawVersion = table.Column<string>(type: "TEXT", nullable: true),
                    NativeDeviceDetails_RawPlatform = table.Column<string>(type: "TEXT", nullable: true),
                    NativeDeviceDetails_RawIdiom = table.Column<string>(type: "TEXT", nullable: true),
                    NativeDeviceDetails_RawDeviceType = table.Column<string>(type: "TEXT", nullable: true),
                    WebDeviceDetails_Browser = table.Column<int>(type: "INTEGER", nullable: true),
                    WebDeviceDetails_RawUserAgent = table.Column<string>(type: "TEXT", nullable: true),
                    WebDeviceDetails_RawBrowserName = table.Column<string>(type: "TEXT", nullable: true),
                    WebDeviceDetails_RawBrowserVersion = table.Column<string>(type: "TEXT", nullable: true),
                    WebDeviceDetails_RawOperatingSystemName = table.Column<string>(type: "TEXT", nullable: true),
                    WebDeviceDetails_RawOperatingSystemVersion = table.Column<string>(type: "TEXT", nullable: true),
                    WebDeviceDetails_RawOperatingSystemVersionName = table.Column<string>(type: "TEXT", nullable: true),
                    WebDeviceDetails_RawPlatformType = table.Column<string>(type: "TEXT", nullable: true),
                    WebDeviceDetails_RawEngineName = table.Column<string>(type: "TEXT", nullable: true),
                    WebDeviceDetails_RawEngineVersion = table.Column<string>(type: "TEXT", nullable: true),
                    PlaybackCapabilities_SupportedMediaFormatIds = table.Column<string>(type: "TEXT", nullable: false),
                    PlaybackCapabilities_SupportedSubtitlesCodecs = table.Column<string>(type: "TEXT", nullable: false),
                    PlaybackCapabilities_SupportsHDR = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastSeen = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Libraries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    MediaType = table.Column<int>(type: "INTEGER", nullable: false),
                    RootPath = table.Column<string>(type: "TEXT", nullable: false),
                    RootPathAccessible = table.Column<bool>(type: "INTEGER", nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Libraries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Medias",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: true),
                    OriginalTitle = table.Column<string>(type: "TEXT", nullable: true),
                    ReleaseDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Genres = table.Column<string>(type: "TEXT", nullable: false),
                    Tagline = table.Column<string>(type: "TEXT", nullable: true),
                    Overview = table.Column<string>(type: "TEXT", nullable: true),
                    OriginalLanguage = table.Column<string>(type: "TEXT", nullable: true),
                    AlbumId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SerieId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SeasonId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SerieSeason_SerieId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", nullable: true),
                    Slug = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Medias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Medias_Medias_AlbumId",
                        column: x => x.AlbumId,
                        principalTable: "Medias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Medias_Medias_SeasonId",
                        column: x => x.SeasonId,
                        principalTable: "Medias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Medias_Medias_SerieId",
                        column: x => x.SerieId,
                        principalTable: "Medias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Medias_Medias_SerieSeason_SerieId",
                        column: x => x.SerieSeason_SerieId,
                        principalTable: "Medias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OpenIddictApplications",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    ApplicationType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    ClientId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ClientSecret = table.Column<string>(type: "TEXT", nullable: true),
                    ClientType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    ConcurrencyToken = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    ConsentType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: true),
                    DisplayNames = table.Column<string>(type: "TEXT", nullable: true),
                    JsonWebKeySet = table.Column<string>(type: "TEXT", nullable: true),
                    Permissions = table.Column<string>(type: "TEXT", nullable: true),
                    PostLogoutRedirectUris = table.Column<string>(type: "TEXT", nullable: true),
                    Properties = table.Column<string>(type: "TEXT", nullable: true),
                    RedirectUris = table.Column<string>(type: "TEXT", nullable: true),
                    Requirements = table.Column<string>(type: "TEXT", nullable: true),
                    Settings = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpenIddictApplications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OpenIddictScopes",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    ConcurrencyToken = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Descriptions = table.Column<string>(type: "TEXT", nullable: true),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: true),
                    DisplayNames = table.Column<string>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    Properties = table.Column<string>(type: "TEXT", nullable: true),
                    Resources = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpenIddictScopes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Persons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Gender = table.Column<int>(type: "INTEGER", nullable: false),
                    Biography = table.Column<string>(type: "TEXT", nullable: true),
                    Birthday = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    Deathday = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    BirthPlace = table.Column<string>(type: "TEXT", nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", nullable: true),
                    Slug = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Persons", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    IdentityUserId = table.Column<string>(type: "TEXT", nullable: true),
                    AccessibleLibraryIds = table.Column<string>(type: "TEXT", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RoleId = table.Column<string>(type: "TEXT", nullable: false),
                    ClaimType = table.Column<string>(type: "TEXT", nullable: true),
                    ClaimValue = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    ClaimType = table.Column<string>(type: "TEXT", nullable: true),
                    ClaimValue = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "TEXT", nullable: false),
                    ProviderKey = table.Column<string>(type: "TEXT", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "TEXT", nullable: true),
                    UserId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    RoleId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    LoginProvider = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IndexedFiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    LibraryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    Extension = table.Column<string>(type: "TEXT", nullable: false),
                    Path = table.Column<string>(type: "TEXT", nullable: false),
                    ParentDirectory = table.Column<string>(type: "TEXT", nullable: true),
                    Hash = table.Column<uint>(type: "INTEGER", nullable: false),
                    Size = table.Column<long>(type: "INTEGER", nullable: false),
                    IsSplitPart = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsComposite = table.Column<bool>(type: "INTEGER", nullable: false),
                    Identification_Title = table.Column<string>(type: "TEXT", nullable: true),
                    Identification_ReleaseYear = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    MediaId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IndexedFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IndexedFiles_Libraries_LibraryId",
                        column: x => x.LibraryId,
                        principalTable: "Libraries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IndexedFiles_Medias_MediaId",
                        column: x => x.MediaId,
                        principalTable: "Medias",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "OpenIddictAuthorizations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    ApplicationId = table.Column<string>(type: "TEXT", nullable: true),
                    ConcurrencyToken = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    CreationDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Properties = table.Column<string>(type: "TEXT", nullable: true),
                    Scopes = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Subject = table.Column<string>(type: "TEXT", maxLength: 400, nullable: true),
                    Type = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpenIddictAuthorizations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OpenIddictAuthorizations_OpenIddictApplications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "OpenIddictApplications",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PersonRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Order = table.Column<int>(type: "INTEGER", nullable: true),
                    PersonId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MediaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CharacterName = table.Column<string>(type: "TEXT", nullable: true),
                    Department = table.Column<string>(type: "TEXT", nullable: true),
                    Job = table.Column<string>(type: "TEXT", nullable: true),
                    IsGuest = table.Column<bool>(type: "INTEGER", nullable: true),
                    VoiceActor_CharacterName = table.Column<string>(type: "TEXT", nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PersonRoles_Medias_MediaId",
                        column: x => x.MediaId,
                        principalTable: "Medias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PersonRoles_Persons_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeviceUser",
                columns: table => new
                {
                    DevicesId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UsersId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceUser", x => new { x.DevicesId, x.UsersId });
                    table.ForeignKey(
                        name: "FK_DeviceUser_Devices_DevicesId",
                        column: x => x.DevicesId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DeviceUser_Users_UsersId",
                        column: x => x.UsersId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MediaPlaybackSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MediaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastUpdateAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaPlaybackSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaPlaybackSessions_Medias_MediaId",
                        column: x => x.MediaId,
                        principalTable: "Medias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MediaPlaybackSessions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Ratings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Source = table.Column<int>(type: "INTEGER", nullable: false),
                    Value = table.Column<double>(type: "REAL", nullable: false),
                    MinimumValue = table.Column<double>(type: "REAL", nullable: false),
                    MaximumValue = table.Column<double>(type: "REAL", nullable: false),
                    MediaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MetadataProvider = table.Column<int>(type: "INTEGER", nullable: true),
                    RatingCount = table.Column<int>(type: "INTEGER", nullable: true),
                    UserId = table.Column<int>(type: "INTEGER", nullable: true),
                    UserId1 = table.Column<Guid>(type: "TEXT", nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ratings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ratings_Medias_MediaId",
                        column: x => x.MediaId,
                        principalTable: "Medias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Ratings_Users_UserId1",
                        column: x => x.UserId1,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserMediaStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    MediaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LastPlaybackPosition = table.Column<double>(type: "REAL", nullable: false),
                    IsCompleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    PlayCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LastInteractedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserMediaStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserMediaStates_Medias_MediaId",
                        column: x => x.MediaId,
                        principalTable: "Medias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserMediaStates_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FileMetadatas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Container = table.Column<string>(type: "TEXT", nullable: false),
                    IndexedFileId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Duration = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    VideoBitrate = table.Column<long>(type: "INTEGER", nullable: true),
                    VideoFileMetadata_Duration = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    VideoResolution = table.Column<int>(type: "INTEGER", nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileMetadatas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FileMetadatas_IndexedFiles_IndexedFileId",
                        column: x => x.IndexedFileId,
                        principalTable: "IndexedFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StreamSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    IndexedFileId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeviceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    Position = table.Column<double>(type: "REAL", nullable: false),
                    RootDirectory = table.Column<string>(type: "TEXT", nullable: false),
                    PlaybackSettingsJson = table.Column<string>(type: "TEXT", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StreamSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StreamSessions_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StreamSessions_IndexedFiles_IndexedFileId",
                        column: x => x.IndexedFileId,
                        principalTable: "IndexedFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StreamSessions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "OpenIddictTokens",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    ApplicationId = table.Column<string>(type: "TEXT", nullable: true),
                    AuthorizationId = table.Column<string>(type: "TEXT", nullable: true),
                    ConcurrencyToken = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    CreationDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ExpirationDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Payload = table.Column<string>(type: "TEXT", nullable: true),
                    Properties = table.Column<string>(type: "TEXT", nullable: true),
                    RedemptionDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ReferenceId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Subject = table.Column<string>(type: "TEXT", maxLength: 400, nullable: true),
                    Type = table.Column<string>(type: "TEXT", maxLength: 150, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OpenIddictTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OpenIddictTokens_OpenIddictApplications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "OpenIddictApplications",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_OpenIddictTokens_OpenIddictAuthorizations_AuthorizationId",
                        column: x => x.AuthorizationId,
                        principalTable: "OpenIddictAuthorizations",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ExternalIds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Platform = table.Column<string>(type: "TEXT", nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false),
                    MediaId = table.Column<Guid>(type: "TEXT", nullable: true),
                    PersonId = table.Column<Guid>(type: "TEXT", nullable: true),
                    PersonRoleId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalIds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalIds_Medias_MediaId",
                        column: x => x.MediaId,
                        principalTable: "Medias",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ExternalIds_PersonRoles_PersonRoleId",
                        column: x => x.PersonRoleId,
                        principalTable: "PersonRoles",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ExternalIds_Persons_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Persons",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "FileTracks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Index = table.Column<int>(type: "INTEGER", nullable: false),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                    AudioFileMetadataId = table.Column<Guid>(type: "TEXT", nullable: true),
                    VideoFileMetadataId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    Language = table.Column<string>(type: "TEXT", nullable: true),
                    Codec = table.Column<string>(type: "TEXT", nullable: true),
                    Channels = table.Column<int>(type: "INTEGER", nullable: true),
                    ChannelLayout = table.Column<string>(type: "TEXT", nullable: true),
                    SampleRateHz = table.Column<int>(type: "INTEGER", nullable: true),
                    Profile = table.Column<string>(type: "TEXT", nullable: true),
                    SubtitleFileTrack_VideoFileMetadataId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SubtitleFileTrack_Name = table.Column<string>(type: "TEXT", nullable: true),
                    SubtitleFileTrack_Language = table.Column<string>(type: "TEXT", nullable: true),
                    SubtitleFileTrack_Codec = table.Column<string>(type: "TEXT", nullable: true),
                    IsTextBased = table.Column<bool>(type: "INTEGER", nullable: true),
                    IsForced = table.Column<bool>(type: "INTEGER", nullable: true),
                    VideoFileTrack_VideoFileMetadataId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Width = table.Column<int>(type: "INTEGER", nullable: true),
                    Height = table.Column<int>(type: "INTEGER", nullable: true),
                    VideoFileTrack_Codec = table.Column<string>(type: "TEXT", nullable: true),
                    VideoFileTrack_Profile = table.Column<string>(type: "TEXT", nullable: true),
                    Level = table.Column<int>(type: "INTEGER", nullable: true),
                    PixelFormat = table.Column<string>(type: "TEXT", nullable: true),
                    BitDepth = table.Column<int>(type: "INTEGER", nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileTracks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FileTracks_FileMetadatas_AudioFileMetadataId",
                        column: x => x.AudioFileMetadataId,
                        principalTable: "FileMetadatas",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FileTracks_FileMetadatas_SubtitleFileTrack_VideoFileMetadataId",
                        column: x => x.SubtitleFileTrack_VideoFileMetadataId,
                        principalTable: "FileMetadatas",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FileTracks_FileMetadatas_VideoFileMetadataId",
                        column: x => x.VideoFileMetadataId,
                        principalTable: "FileMetadatas",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_FileTracks_FileMetadatas_VideoFileTrack_VideoFileMetadataId",
                        column: x => x.VideoFileTrack_VideoFileMetadataId,
                        principalTable: "FileMetadatas",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "HlsSegments",
                columns: table => new
                {
                    FileMetadataId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Number = table.Column<int>(type: "INTEGER", nullable: false),
                    IndexedFileId = table.Column<Guid>(type: "TEXT", nullable: false),
                    StartTimestamp = table.Column<long>(type: "INTEGER", nullable: false),
                    Duration = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HlsSegments", x => new { x.FileMetadataId, x.Number });
                    table.ForeignKey(
                        name: "FK_HlsSegments_FileMetadatas_FileMetadataId",
                        column: x => x.FileMetadataId,
                        principalTable: "FileMetadatas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MetadataPictures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    OriginalRemoteUri = table.Column<string>(type: "TEXT", nullable: true),
                    LocalPath = table.Column<string>(type: "TEXT", nullable: true),
                    MediaId = table.Column<Guid>(type: "TEXT", nullable: true),
                    VideoFileMetadataId = table.Column<Guid>(type: "TEXT", nullable: true),
                    PersonId = table.Column<Guid>(type: "TEXT", nullable: true),
                    PersonRoleId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Created = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", nullable: true),
                    LastModified = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetadataPictures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MetadataPictures_FileMetadatas_VideoFileMetadataId",
                        column: x => x.VideoFileMetadataId,
                        principalTable: "FileMetadatas",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MetadataPictures_Medias_MediaId",
                        column: x => x.MediaId,
                        principalTable: "Medias",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MetadataPictures_PersonRoles_PersonRoleId",
                        column: x => x.PersonRoleId,
                        principalTable: "PersonRoles",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MetadataPictures_Persons_PersonId",
                        column: x => x.PersonId,
                        principalTable: "Persons",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DeviceUser_UsersId",
                table: "DeviceUser",
                column: "UsersId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalIds_MediaId",
                table: "ExternalIds",
                column: "MediaId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalIds_PersonId",
                table: "ExternalIds",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalIds_PersonRoleId",
                table: "ExternalIds",
                column: "PersonRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_FileMetadatas_IndexedFileId",
                table: "FileMetadatas",
                column: "IndexedFileId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FileTracks_AudioFileMetadataId",
                table: "FileTracks",
                column: "AudioFileMetadataId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FileTracks_SubtitleFileTrack_VideoFileMetadataId",
                table: "FileTracks",
                column: "SubtitleFileTrack_VideoFileMetadataId");

            migrationBuilder.CreateIndex(
                name: "IX_FileTracks_VideoFileMetadataId",
                table: "FileTracks",
                column: "VideoFileMetadataId");

            migrationBuilder.CreateIndex(
                name: "IX_FileTracks_VideoFileTrack_VideoFileMetadataId",
                table: "FileTracks",
                column: "VideoFileTrack_VideoFileMetadataId");

            migrationBuilder.CreateIndex(
                name: "IX_HlsSegments_IndexedFileId_Number",
                table: "HlsSegments",
                columns: new[] { "IndexedFileId", "Number" });

            migrationBuilder.CreateIndex(
                name: "IX_IndexedFiles_LibraryId",
                table: "IndexedFiles",
                column: "LibraryId");

            migrationBuilder.CreateIndex(
                name: "IX_IndexedFiles_MediaId",
                table: "IndexedFiles",
                column: "MediaId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaPlaybackSessions_MediaId",
                table: "MediaPlaybackSessions",
                column: "MediaId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaPlaybackSessions_UserId",
                table: "MediaPlaybackSessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Medias_AlbumId",
                table: "Medias",
                column: "AlbumId");

            migrationBuilder.CreateIndex(
                name: "IX_Medias_SeasonId",
                table: "Medias",
                column: "SeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_Medias_SerieId",
                table: "Medias",
                column: "SerieId");

            migrationBuilder.CreateIndex(
                name: "IX_Medias_SerieSeason_SerieId",
                table: "Medias",
                column: "SerieSeason_SerieId");

            migrationBuilder.CreateIndex(
                name: "IX_Medias_Slug",
                table: "Medias",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MetadataPictures_MediaId",
                table: "MetadataPictures",
                column: "MediaId");

            migrationBuilder.CreateIndex(
                name: "IX_MetadataPictures_PersonId",
                table: "MetadataPictures",
                column: "PersonId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MetadataPictures_PersonRoleId",
                table: "MetadataPictures",
                column: "PersonRoleId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MetadataPictures_VideoFileMetadataId",
                table: "MetadataPictures",
                column: "VideoFileMetadataId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OpenIddictApplications_ClientId",
                table: "OpenIddictApplications",
                column: "ClientId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OpenIddictAuthorizations_ApplicationId_Status_Subject_Type",
                table: "OpenIddictAuthorizations",
                columns: new[] { "ApplicationId", "Status", "Subject", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_OpenIddictScopes_Name",
                table: "OpenIddictScopes",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OpenIddictTokens_ApplicationId_Status_Subject_Type",
                table: "OpenIddictTokens",
                columns: new[] { "ApplicationId", "Status", "Subject", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_OpenIddictTokens_AuthorizationId",
                table: "OpenIddictTokens",
                column: "AuthorizationId");

            migrationBuilder.CreateIndex(
                name: "IX_OpenIddictTokens_ReferenceId",
                table: "OpenIddictTokens",
                column: "ReferenceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PersonRoles_MediaId",
                table: "PersonRoles",
                column: "MediaId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonRoles_PersonId",
                table: "PersonRoles",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_Persons_Slug",
                table: "Persons",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ratings_MediaId",
                table: "Ratings",
                column: "MediaId");

            migrationBuilder.CreateIndex(
                name: "IX_Ratings_UserId1",
                table: "Ratings",
                column: "UserId1");

            migrationBuilder.CreateIndex(
                name: "IX_StreamSessions_DeviceId",
                table: "StreamSessions",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_StreamSessions_IndexedFileId",
                table: "StreamSessions",
                column: "IndexedFileId");

            migrationBuilder.CreateIndex(
                name: "IX_StreamSessions_UserId",
                table: "StreamSessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserMediaStates_MediaId",
                table: "UserMediaStates",
                column: "MediaId");

            migrationBuilder.CreateIndex(
                name: "IX_UserMediaStates_UserId",
                table: "UserMediaStates",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "BackgroundTasks");

            migrationBuilder.DropTable(
                name: "DeviceUser");

            migrationBuilder.DropTable(
                name: "ExternalIds");

            migrationBuilder.DropTable(
                name: "FileTracks");

            migrationBuilder.DropTable(
                name: "HlsSegments");

            migrationBuilder.DropTable(
                name: "MediaPlaybackSessions");

            migrationBuilder.DropTable(
                name: "MetadataPictures");

            migrationBuilder.DropTable(
                name: "OpenIddictScopes");

            migrationBuilder.DropTable(
                name: "OpenIddictTokens");

            migrationBuilder.DropTable(
                name: "Ratings");

            migrationBuilder.DropTable(
                name: "StreamSessions");

            migrationBuilder.DropTable(
                name: "UserMediaStates");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "FileMetadatas");

            migrationBuilder.DropTable(
                name: "PersonRoles");

            migrationBuilder.DropTable(
                name: "OpenIddictAuthorizations");

            migrationBuilder.DropTable(
                name: "Devices");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "IndexedFiles");

            migrationBuilder.DropTable(
                name: "Persons");

            migrationBuilder.DropTable(
                name: "OpenIddictApplications");

            migrationBuilder.DropTable(
                name: "Libraries");

            migrationBuilder.DropTable(
                name: "Medias");
        }
    }
}
